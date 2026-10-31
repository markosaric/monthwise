using Monthwise.Application.Common;
using Monthwise.Domain.Common;
using Monthwise.Domain.DataTransfer;

namespace Monthwise.Application.DataTransfer;

/// <summary>The top of the Data and backups section and the Dashboard reminder (backup and restore FR-1, FR-9, FR-10).</summary>
public sealed class DataOverviewHandler(IDataTransferRepository repository, TimeProvider timeProvider)
{
    private static readonly DataTransferKind[] ExportKinds = [DataTransferKind.Export];
    private static readonly DataTransferKind[] ImportKinds = [DataTransferKind.Import, DataTransferKind.SafetyCopyRestore];

    public async Task<DataOverviewResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var lastExport = await repository.GetLastSuccessfulAsync(ExportKinds, cancellationToken);
        var lastImport = await repository.GetLastSuccessfulAsync(ImportKinds, cancellationToken);
        var safetyCopyCreatedAt = await repository.GetSafetyCopyCreatedAtAsync(cancellationToken);
        var reminder = await repository.GetReminderAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        return new DataOverviewResponse(
            lastExport?.ToResponse(),
            lastImport?.ToResponse(),
            safetyCopyCreatedAt,
            reminder.ToResponse(now, lastExport?.OccurredAt),
            // Login comes in a later phase; until then whoever uses Monthwise may export and import.
            CanManage: true);
    }
}

/// <summary>The data and backups history, newest first (FR-9).</summary>
public sealed class DataHistoryHandler(IDataTransferRepository repository)
{
    public const int PageSize = 50;

    public async Task<DataHistoryResponse> HandleAsync(int page, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        var (items, totalCount) = await repository.GetPageAsync(page, PageSize, cancellationToken);
        return new DataHistoryResponse(items.Select(r => r.ToResponse()).ToList(), page, PageSize, totalCount);
    }
}

/// <summary>Exports all household data to a password-protected file (FR-2, FR-3).</summary>
public sealed class ExportDataHandler(
    IHouseholdDataStore store,
    IExportFileCodec codec,
    IDataTransferRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<ExportFile>> HandleAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Password is null || request.Password.Length < DataTransferErrors.PasswordMinLength)
        {
            return Result.Failure<ExportFile>(DataTransferErrors.PasswordTooShort);
        }

        var now = timeProvider.GetUtcNow();
        var fileName = $"monthwise-export-{timeProvider.GetLocalNow():yyyy-MM-dd-HHmm}.monthwise";
        byte[] content;
        try
        {
            var data = await store.ReadAllAsync(now, ReleaseVersion.Current, cancellationToken);
            content = codec.Write(data, request.Password);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            repository.Add(DataTransferRecord.Create(
                DataTransferKind.Export, now, succeeded: false, ReleaseVersion.Current, fileName, message: DataTransferErrors.ExportFailed.Message));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        repository.Add(DataTransferRecord.Create(DataTransferKind.Export, now, succeeded: true, ReleaseVersion.Current, fileName));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new ExportFile(fileName, content));
    }
}

/// <summary>
/// Checks and imports an export file, and brings the safety copy back (FR-4 to FR-8). An import
/// replaces all household data or changes nothing, and makes a safety copy first.
/// </summary>
public sealed class ImportDataHandler(
    IHouseholdDataStore store,
    IExportFileCodec codec,
    IDataTransferRepository repository,
    IUnitOfWork unitOfWork,
    ImportGate gate,
    TimeProvider timeProvider)
{
    public async Task<Result<ImportCheckResponse>> CheckAsync(byte[]? file, string? password, CancellationToken cancellationToken)
    {
        var read = ReadFile(file, password);
        if (read.IsFailure)
        {
            return Result.Failure<ImportCheckResponse>(read.Errors);
        }

        var data = read.Value;
        var check = await store.CheckAsync(data, cancellationToken);
        if (check.IsFailure)
        {
            return Result.Failure<ImportCheckResponse>(check.Errors);
        }

        var current = await store.GetSummaryAsync(cancellationToken);
        return Result.Success(new ImportCheckResponse(data.CreatedAt, data.Release, data.Summary, current));
    }

    public async Task<Result<ImportResponse>> ImportAsync(
        byte[]? file, string? fileName, string? password, string? confirmation, CancellationToken cancellationToken)
    {
        if (confirmation?.Trim() != DataTransferErrors.ConfirmationWord)
        {
            return Result.Failure<ImportResponse>(DataTransferErrors.ConfirmationRequired);
        }
        if (file is null || file.Length == 0)
        {
            return Result.Failure<ImportResponse>(DataTransferErrors.FileRequired);
        }

        return await RunExclusiveAsync(
            DataTransferKind.Import,
            fileName,
            _ => Task.FromResult(ReadFile(file, password)),
            cancellationToken);
    }

    public async Task<Result<ImportResponse>> RestoreSafetyCopyAsync(SafetyCopyRestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Confirmation?.Trim() != DataTransferErrors.ConfirmationWord)
        {
            return Result.Failure<ImportResponse>(DataTransferErrors.ConfirmationRequired);
        }

        return await RunExclusiveAsync(
            DataTransferKind.SafetyCopyRestore,
            fileName: null,
            async ct => await repository.GetSafetyCopyAsync(ct) is { } copy
                ? Result.Success(codec.Unprotect(copy.Content))
                : Result.Failure<HouseholdData>(DataTransferErrors.NoSafetyCopy),
            cancellationToken);
    }

    private async Task<Result<ImportResponse>> RunExclusiveAsync(
        DataTransferKind kind,
        string? fileName,
        Func<CancellationToken, Task<Result<HouseholdData>>> load,
        CancellationToken cancellationToken)
    {
        if (!gate.TryEnter())
        {
            return Result.Failure<ImportResponse>(DataTransferErrors.ImportRunning);
        }

        try
        {
            var loaded = await load(cancellationToken);
            if (loaded.IsFailure)
            {
                if (loaded.Errors[0] != DataTransferErrors.NoSafetyCopy)
                {
                    await RecordAsync(kind, fileName, null, loaded.Errors[0].Message, cancellationToken);
                }
                return Result.Failure<ImportResponse>(loaded.Errors);
            }

            var data = loaded.Value;
            var check = await store.CheckAsync(data, cancellationToken);
            if (check.IsFailure)
            {
                await RecordAsync(kind, fileName, data.CreatedAt, check.Errors[0].Message, cancellationToken);
                return Result.Failure<ImportResponse>(check.Errors);
            }

            await MakeSafetyCopyAsync(cancellationToken);

            var replaced = await store.ReplaceAllAsync(data, cancellationToken);
            if (replaced.IsFailure)
            {
                await RecordAsync(kind, fileName, data.CreatedAt, replaced.Errors[0].Message, cancellationToken);
                return Result.Failure<ImportResponse>(replaced.Errors);
            }

            var now = timeProvider.GetUtcNow();
            repository.Add(DataTransferRecord.Create(kind, now, succeeded: true, ReleaseVersion.Current, fileName, data.CreatedAt));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            gate.LastImportedAt = now;
            return Result.Success(new ImportResponse(data.CreatedAt, data.Summary));
        }
        finally
        {
            gate.Exit();
        }
    }

    /// <summary>Keeps the current data so a wrong import can be undone; replaces the previous safety copy.</summary>
    private async Task MakeSafetyCopyAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var current = await store.ReadAllAsync(now, ReleaseVersion.Current, cancellationToken);
        var content = codec.Protect(current);
        if (await repository.GetSafetyCopyAsync(cancellationToken) is { } existing)
        {
            existing.Replace(now, ReleaseVersion.Current, content);
        }
        else
        {
            repository.Add(SafetyCopy.Create(now, ReleaseVersion.Current, content));
        }
        repository.Add(DataTransferRecord.Create(DataTransferKind.SafetyCopy, now, succeeded: true, ReleaseVersion.Current));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordAsync(
        DataTransferKind kind, string? fileName, DateTimeOffset? fileCreatedAt, string message, CancellationToken cancellationToken)
    {
        repository.Add(DataTransferRecord.Create(
            kind, timeProvider.GetUtcNow(), succeeded: false, ReleaseVersion.Current, fileName, fileCreatedAt, message));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Result<HouseholdData> ReadFile(byte[]? file, string? password)
    {
        if (file is null || file.Length == 0)
        {
            return Result.Failure<HouseholdData>(DataTransferErrors.FileRequired);
        }
        if (string.IsNullOrEmpty(password))
        {
            return Result.Failure<HouseholdData>(DataTransferErrors.PasswordRequired);
        }
        return codec.Read(file, password);
    }
}

/// <summary>Changes and dismisses the export reminder (FR-10, FR-11).</summary>
public sealed class BackupReminderHandler(
    IDataTransferRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly DataTransferKind[] ExportKinds = [DataTransferKind.Export];

    public async Task<Result<BackupReminderResponse>> UpdateAsync(BackupReminderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reminder = await repository.GetReminderAsync(cancellationToken);
        var changed = reminder.Change(request.Enabled, request.IntervalDays);
        if (changed.IsFailure)
        {
            return Result.Failure<BackupReminderResponse>(changed.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToResponseAsync(reminder, cancellationToken));
    }

    public async Task<BackupReminderResponse> DismissAsync(CancellationToken cancellationToken)
    {
        var reminder = await repository.GetReminderAsync(cancellationToken);
        reminder.Dismiss(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(reminder, cancellationToken);
    }

    private async Task<BackupReminderResponse> ToResponseAsync(BackupReminder reminder, CancellationToken cancellationToken)
    {
        var lastExport = await repository.GetLastSuccessfulAsync(ExportKinds, cancellationToken);
        return reminder.ToResponse(timeProvider.GetUtcNow(), lastExport?.OccurredAt);
    }
}

internal static class DataTransferMapping
{
    public static DataTransferResponse ToResponse(this DataTransferRecord record) => new(
        record.Id,
        record.OccurredAt,
        record.Kind,
        record.UserName,
        record.Succeeded,
        record.FileName,
        record.Release,
        record.FileCreatedAt,
        record.Message);

    public static BackupReminderResponse ToResponse(this BackupReminder reminder, DateTimeOffset now, DateTimeOffset? lastExportAt) => new(
        reminder.Enabled,
        reminder.IntervalDays,
        BackupReminder.IntervalChoices,
        reminder.IsDue(now, lastExportAt));
}
