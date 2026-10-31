using System.Data;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Monthwise.Application.DataTransfer;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.DataTransfer;
using Monthwise.Infrastructure.Persistence;
using Monthwise.Infrastructure.Persistence.Configurations;
using Monthwise.Infrastructure.Security;
using Npgsql;
using NpgsqlTypes;

namespace Monthwise.Infrastructure.DataTransfer;

/// <summary>
/// Reads and replaces all household data (backup and restore FR-2, FR-6, FR-7). Every table of the
/// database is household data except <see cref="InstallationTables"/>, so later features are
/// exported without extra code. An import loads the file into a temporary schema built by the
/// migrations up to the file's release, brings it up to date with the remaining migrations, and
/// copies it over the household tables, all in one transaction.
/// See docs/decisions/0007-data-export-and-import.md.
/// </summary>
internal sealed partial class HouseholdDataStore(
    MonthwiseDbContext dbContext,
    ISensitiveDataCipher cipher,
    ILogger<HouseholdDataStore> logger) : IHouseholdDataStore
{
    /// <summary>Tables of the installation, not of the household: never exported, never replaced by an import.</summary>
    public static readonly IReadOnlySet<string> InstallationTables = new HashSet<string>(StringComparer.Ordinal)
    {
        "__ef_migrations_history",
        "data_transfer_log",
        "data_backup_settings",
        "data_safety_copy",
    };

    private const int CommandTimeoutSeconds = 600;

    private static readonly (string Table, string Field)[] SummaryTables =
    [
        ("card", nameof(DataSummaryResponse.Cards)),
        ("loan", nameof(DataSummaryResponse.Loans)),
        ("cash_flow_input", nameof(DataSummaryResponse.Inputs)),
        ("card_purchase", nameof(DataSummaryResponse.CardPurchases)),
        ("bill", nameof(DataSummaryResponse.Bills)),
        ("expense", nameof(DataSummaryResponse.Expenses)),
    ];

    public async Task<HouseholdData> ReadAllAsync(DateTimeOffset now, string release, CancellationToken cancellationToken)
    {
        // One snapshot of all tables, while other requests go on changing data.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await ExecuteAsync("SET TRANSACTION READ ONLY", cancellationToken);
        var schema = await GetCurrentSchemaAsync(cancellationToken);
        var migration = await ScalarAsync<string>(
            $"SELECT migration_id FROM {Quote(schema)}.\"__ef_migrations_history\" ORDER BY migration_id DESC LIMIT 1", cancellationToken);

        var tables = new JsonObject();
        foreach (var table in await GetHouseholdTablesAsync(schema, cancellationToken))
        {
            var json = await ScalarAsync<string>(
                $"SELECT coalesce(jsonb_agg(to_jsonb(t)), '[]'::jsonb)::text FROM {Quote(schema)}.{Quote(table)} t", cancellationToken);
            var rows = JsonNode.Parse(json)!.AsArray();
            if (table == SensitiveColumns.Table)
            {
                DecryptSensitiveData(rows);
            }
            tables[table] = rows;
        }
        await transaction.CommitAsync(cancellationToken);

        return new HouseholdData(now, release, migration, Summarize(tables), tables);
    }

    public async Task<DataSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var schema = await GetCurrentSchemaAsync(cancellationToken);
        var existing = await GetHouseholdTablesAsync(schema, cancellationToken);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (table, field) in SummaryTables)
        {
            counts[field] = existing.Contains(table)
                ? (int)await ScalarAsync<long>($"SELECT count(*) FROM {Quote(schema)}.{Quote(table)}", cancellationToken)
                : 0;
        }
        return ToSummary(counts);
    }

    public async Task<Result> CheckAsync(HouseholdData data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var known = CheckRelease(data);
        if (known.IsFailure)
        {
            return known;
        }

        // Build the tables of the file's release and compare; the transaction is rolled back.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var importSchema = await CreateImportSchemaAsync(data.Migration, cancellationToken);
        var complete = await CheckCompleteAsync(data, importSchema, cancellationToken);
        await transaction.RollbackAsync(cancellationToken);
        return complete;
    }

    public async Task<Result> ReplaceAllAsync(HouseholdData data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var known = CheckRelease(data);
        if (known.IsFailure)
        {
            return known;
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var schema = await GetCurrentSchemaAsync(cancellationToken);
            var targetTables = await GetHouseholdTablesAsync(schema, cancellationToken);
            await ExecuteAsync(
                $"LOCK TABLE {string.Join(", ", targetTables.Select(t => $"{Quote(schema)}.{Quote(t)}"))} IN ACCESS EXCLUSIVE MODE",
                cancellationToken);

            var importSchema = await CreateImportSchemaAsync(data.Migration, cancellationToken);
            var complete = await CheckCompleteAsync(data, importSchema, cancellationToken);
            if (complete.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return complete;
            }

            await LoadAsync(data, importSchema, cancellationToken);

            var latest = dbContext.Database.GetMigrations().Last();
            if (data.Migration != latest)
            {
                // The same migrations that update a database bring the imported data up to this release.
                await ExecuteAsync(
                    dbContext.GetService<IMigrator>().GenerateScript(data.Migration, latest, MigrationsSqlGenerationOptions.NoTransactions),
                    cancellationToken);
            }

            await CopyAsync(importSchema, schema, targetTables, cancellationToken);
            await ExecuteAsync($"DROP SCHEMA {Quote(importSchema)} CASCADE", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogImportFailed(logger, ex);
            return Result.Failure(DataTransferErrors.ImportFailed);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Importing household data failed; nothing was changed")]
    private static partial void LogImportFailed(ILogger logger, Exception exception);

    private Result CheckRelease(HouseholdData data)
    {
        var migrations = dbContext.Database.GetMigrations().ToList();
        if (migrations.Contains(data.Migration))
        {
            return Result.Success();
        }
        return string.CompareOrdinal(data.Migration, migrations[^1]) > 0
            ? Result.Failure(DataTransferErrors.NewerRelease(data.Release))
            : Result.Failure(DataTransferErrors.UnknownRelease);
    }

    /// <summary>A new schema with the tables of <paramref name="migration"/>, used for the rest of the transaction.</summary>
    private async Task<string> CreateImportSchemaAsync(string migration, CancellationToken cancellationToken)
    {
        var importSchema = $"monthwise_import_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE SCHEMA {Quote(importSchema)}; SET LOCAL search_path TO {Quote(importSchema)}", cancellationToken);
        // The migrations use unqualified names, so they build their tables in the import schema.
        var script = dbContext.GetService<IMigrator>().GenerateScript(
            fromMigration: null, toMigration: migration, MigrationsSqlGenerationOptions.NoTransactions);
        await ExecuteAsync(script, cancellationToken);
        return importSchema;
    }

    /// <summary>The file has exactly the household tables of its release, and no unknown columns.</summary>
    private async Task<Result> CheckCompleteAsync(HouseholdData data, string importSchema, CancellationToken cancellationToken)
    {
        var tables = await GetHouseholdTablesAsync(importSchema, cancellationToken);
        if (!tables.SetEquals(data.Tables.Select(t => t.Key)))
        {
            return Result.Failure(DataTransferErrors.FileIncomplete);
        }

        var columns = await GetColumnsAsync(importSchema, cancellationToken);
        foreach (var (table, rows) in data.Tables)
        {
            if (rows is not JsonArray array)
            {
                return Result.Failure(DataTransferErrors.FileIncomplete);
            }
            foreach (var row in array)
            {
                if (row is not JsonObject values || values.Any(v => !columns[table].Contains(v.Key)))
                {
                    return Result.Failure(DataTransferErrors.FileIncomplete);
                }
            }
        }
        return Result.Success();
    }

    /// <summary>Puts the file's rows into the import schema in place of the rows the migrations added.</summary>
    private async Task LoadAsync(HouseholdData data, string importSchema, CancellationToken cancellationToken)
    {
        var tables = await GetHouseholdTablesAsync(importSchema, cancellationToken);
        var columns = await GetColumnsAsync(importSchema, cancellationToken);
        await ExecuteAsync($"TRUNCATE {string.Join(", ", tables.Select(t => $"{Quote(importSchema)}.{Quote(t)}"))}", cancellationToken);

        foreach (var table in await GetInsertOrderAsync(importSchema, tables, cancellationToken))
        {
            var rows = data.Tables[table]!.AsArray().DeepClone().AsArray();
            if (rows.Count == 0)
            {
                continue;
            }
            if (table == SensitiveColumns.Table)
            {
                EncryptSensitiveData(rows, columns[table]);
            }

            var names = columns[table]
                .Where(c => rows.Any(r => r!.AsObject().ContainsKey(c)))
                .Select(Quote)
                .ToList();
            var list = string.Join(", ", names);
            var target = $"{Quote(importSchema)}.{Quote(table)}";
            await using var command = await CreateCommandAsync(
                $"INSERT INTO {target} ({list}) SELECT {list} FROM jsonb_populate_recordset(NULL::{target}, @rows)", cancellationToken);
            command.Parameters.Add(new NpgsqlParameter("rows", NpgsqlDbType.Jsonb) { Value = rows.ToJsonString() });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>Replaces every household table with its imported copy.</summary>
    private async Task CopyAsync(string importSchema, string schema, HashSet<string> targetTables, CancellationToken cancellationToken)
    {
        if (!targetTables.SetEquals(await GetHouseholdTablesAsync(importSchema, cancellationToken)))
        {
            throw new InvalidOperationException("The imported tables do not match the tables of this release.");
        }

        var columns = await GetColumnsAsync(schema, cancellationToken);
        await ExecuteAsync($"TRUNCATE {string.Join(", ", targetTables.Select(t => $"{Quote(schema)}.{Quote(t)}"))}", cancellationToken);
        foreach (var table in await GetInsertOrderAsync(schema, targetTables, cancellationToken))
        {
            var list = string.Join(", ", columns[table].Select(Quote));
            await ExecuteAsync(
                $"INSERT INTO {Quote(schema)}.{Quote(table)} ({list}) SELECT {list} FROM {Quote(importSchema)}.{Quote(table)}",
                cancellationToken);
        }
    }

    private void DecryptSensitiveData(JsonArray rows)
    {
        var sensitive = SensitiveColumns.From(dbContext);
        foreach (var row in rows.Select(r => r!.AsObject()))
        {
            foreach (var column in new[] { sensitive.CardNumber, sensitive.Iban })
            {
                if (row[column] is JsonValue value && value.TryGetValue<string>(out var stored))
                {
                    row[column] = cipher.Decrypt(stored);
                }
            }
            row.Remove(sensitive.CardNumberHash);
            row.Remove(sensitive.IbanHash);
        }
    }

    /// <summary>Encrypts with this installation's key and hashes with its hash key, so a file works with other keys.</summary>
    private void EncryptSensitiveData(JsonArray rows, HashSet<string> columns)
    {
        var sensitive = SensitiveColumns.From(dbContext);
        foreach (var row in rows.Select(r => r!.AsObject()))
        {
            string? numberHash = null;
            string? ibanHash = null;
            if (row[sensitive.CardNumber] is JsonValue numberValue && numberValue.TryGetValue<string>(out var digits))
            {
                var number = CardNumber.Parse(digits);
                row[sensitive.CardNumber] = cipher.Encrypt(number.Digits);
                numberHash = MonthwiseDbContext.HashCardNumber(cipher, number);
            }
            if (row[sensitive.Iban] is JsonValue ibanValue && ibanValue.TryGetValue<string>(out var text))
            {
                var iban = Iban.Parse(text);
                row[sensitive.Iban] = cipher.Encrypt(iban.Value);
                ibanHash = MonthwiseDbContext.HashIban(cipher, iban);
            }
            if (columns.Contains(sensitive.CardNumberHash))
            {
                row[sensitive.CardNumberHash] = numberHash;
            }
            if (columns.Contains(sensitive.IbanHash))
            {
                row[sensitive.IbanHash] = ibanHash;
            }
        }
    }

    private static DataSummaryResponse Summarize(JsonObject tables) =>
        ToSummary(SummaryTables.ToDictionary(
            s => s.Field,
            s => tables[s.Table] is JsonArray rows ? rows.Count : 0,
            StringComparer.Ordinal));

    private static DataSummaryResponse ToSummary(Dictionary<string, int> counts) => new(
        counts[nameof(DataSummaryResponse.Cards)],
        counts[nameof(DataSummaryResponse.Loans)],
        counts[nameof(DataSummaryResponse.Inputs)],
        counts[nameof(DataSummaryResponse.CardPurchases)],
        counts[nameof(DataSummaryResponse.Bills)],
        counts[nameof(DataSummaryResponse.Expenses)]);

    private Task<string> GetCurrentSchemaAsync(CancellationToken cancellationToken) =>
        ScalarAsync<string>("SELECT current_schema()", cancellationToken);

    private async Task<HashSet<string>> GetHouseholdTablesAsync(string schema, CancellationToken cancellationToken)
    {
        await using var command = await CreateCommandAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema AND table_type = 'BASE TABLE'",
            cancellationToken);
        command.Parameters.AddWithValue("schema", schema);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            if (!InstallationTables.Contains(table))
            {
                tables.Add(table);
            }
        }
        return tables;
    }

    private async Task<Dictionary<string, HashSet<string>>> GetColumnsAsync(string schema, CancellationToken cancellationToken)
    {
        await using var command = await CreateCommandAsync(
            """
            SELECT table_name, column_name FROM information_schema.columns
            WHERE table_schema = @schema AND is_generated = 'NEVER'
            ORDER BY table_name, ordinal_position
            """,
            cancellationToken);
        command.Parameters.AddWithValue("schema", schema);
        var columns = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            if (!columns.TryGetValue(table, out var set))
            {
                columns[table] = set = new HashSet<string>(StringComparer.Ordinal);
            }
            set.Add(reader.GetString(1));
        }
        return columns;
    }

    /// <summary>Tables ordered so that every table comes after the tables its foreign keys point to.</summary>
    private async Task<List<string>> GetInsertOrderAsync(string schema, HashSet<string> tables, CancellationToken cancellationToken)
    {
        await using var command = await CreateCommandAsync(
            """
            SELECT child.relname, parent.relname
            FROM pg_constraint c
            JOIN pg_class child ON child.oid = c.conrelid
            JOIN pg_class parent ON parent.oid = c.confrelid
            JOIN pg_namespace n ON n.oid = child.relnamespace
            WHERE c.contype = 'f' AND n.nspname = @schema
            """,
            cancellationToken);
        command.Parameters.AddWithValue("schema", schema);
        var dependsOn = tables.ToDictionary(t => t, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var (child, parent) = (reader.GetString(0), reader.GetString(1));
                // A table pointing to itself is inserted in one statement, which PostgreSQL checks at its end.
                if (child != parent && tables.Contains(parent) && dependsOn.TryGetValue(child, out var parents))
                {
                    parents.Add(parent);
                }
            }
        }

        var order = new List<string>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (order.Count < tables.Count)
        {
            var ready = dependsOn.Keys
                .Where(t => !done.Contains(t) && dependsOn[t].IsSubsetOf(done))
                .Order(StringComparer.Ordinal)
                .ToList();
            if (ready.Count == 0)
            {
                throw new InvalidOperationException("The household tables have foreign keys in a cycle.");
            }
            order.AddRange(ready);
            done.UnionWith(ready);
        }
        return order;
    }

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var command = await CreateCommandAsync(sql, cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<T> ScalarAsync<T>(string sql, CancellationToken cancellationToken)
    {
        await using var command = await CreateCommandAsync(sql, cancellationToken);
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<NpgsqlCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
#pragma warning disable CA2100 // Identifiers come from the database catalog and are quoted; values are parameters.
        return new NpgsqlCommand(sql, connection)
        {
            Transaction = (NpgsqlTransaction?)dbContext.Database.CurrentTransaction?.GetDbTransaction(),
            CommandTimeout = CommandTimeoutSeconds,
        };
#pragma warning restore CA2100
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>The card number and IBAN columns and their hashes, found through the EF model.</summary>
    private sealed record SensitiveColumns(string CardNumber, string Iban, string CardNumberHash, string IbanHash)
    {
        public const string Table = "card";

        public static SensitiveColumns From(MonthwiseDbContext dbContext)
        {
            var entity = dbContext.Model.FindEntityType(typeof(CardSensitiveData))!;
            var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            string Column(string property) => entity.FindProperty(property)!.GetColumnName(store)!;
            return new SensitiveColumns(
                Column(nameof(CardSensitiveData.CardNumber)),
                Column(nameof(CardSensitiveData.Iban)),
                Column(CardSensitiveDataConfiguration.CardNumberHash),
                Column(CardSensitiveDataConfiguration.IbanHash));
        }
    }
}
