using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monthwise.Application.DataTransfer;
using Monthwise.Domain.Common;
using Monthwise.Domain.DataTransfer;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Infrastructure.DataTransfer;

/// <summary>
/// The export file (backup and restore FR-3): a short header, then the gzip-compressed JSON of the
/// household data encrypted with AES-256-GCM under a key derived from the user's password
/// (PBKDF2-HMAC-SHA256). The header is bound as associated data. See docs/decisions/0007-data-export-and-import.md.
/// </summary>
internal sealed class ExportFileCodec(ISensitiveDataCipher cipher) : IExportFileCodec
{
    public const string FileFormat = "monthwise-export";
    public const int FormatVersion = 1;
    public const int DefaultIterations = 600_000;

    private const int MinIterations = 100_000;
    private const int MaxIterations = 10_000_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private static readonly byte[] Magic = "MWEXPORT"u8.ToArray();
    private static readonly int HeaderSize = Magic.Length + 1 + sizeof(int) + SaltSize + NonceSize;

    /// <summary>Lower in tests only, to keep them fast; files always carry their own count.</summary>
    internal int Iterations { get; init; } = DefaultIterations;

    public byte[] Write(HouseholdData data, string password)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(password);
        var plain = Compress(Serialize(data));

        var file = new byte[HeaderSize + plain.Length + TagSize];
        var header = file.AsSpan(0, HeaderSize);
        Magic.CopyTo(header);
        header[Magic.Length] = FormatVersion;
        BinaryPrimitives.WriteInt32BigEndian(header[(Magic.Length + 1)..], Iterations);
        var salt = header.Slice(Magic.Length + 1 + sizeof(int), SaltSize);
        var nonce = header.Slice(Magic.Length + 1 + sizeof(int) + SaltSize, NonceSize);
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        var key = DeriveKey(password, salt, Iterations);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plain, file.AsSpan(HeaderSize, plain.Length), file.AsSpan(HeaderSize + plain.Length, TagSize), header);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
        return file;
    }

    public Result<HouseholdData> Read(byte[] file, string password)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(password);
        if (file.Length < HeaderSize + TagSize || !file.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            return Result.Failure<HouseholdData>(DataTransferErrors.NotAnExportFile);
        }
        if (file[Magic.Length] != FormatVersion)
        {
            return Result.Failure<HouseholdData>(DataTransferErrors.UnknownRelease);
        }

        var header = file.AsSpan(0, HeaderSize);
        var iterations = BinaryPrimitives.ReadInt32BigEndian(header[(Magic.Length + 1)..]);
        if (iterations is < MinIterations or > MaxIterations && iterations != Iterations)
        {
            return Result.Failure<HouseholdData>(DataTransferErrors.WrongPasswordOrDamaged);
        }
        var salt = header.Slice(Magic.Length + 1 + sizeof(int), SaltSize);
        var nonce = header.Slice(Magic.Length + 1 + sizeof(int) + SaltSize, NonceSize);
        var cipherLength = file.Length - HeaderSize - TagSize;
        var plain = new byte[cipherLength];

        var key = DeriveKey(password, salt, iterations);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, file.AsSpan(HeaderSize, cipherLength), file.AsSpan(HeaderSize + cipherLength, TagSize), plain, header);
        }
        catch (AuthenticationTagMismatchException)
        {
            // AES-GCM cannot tell a wrong password from a changed file.
            return Result.Failure<HouseholdData>(DataTransferErrors.WrongPasswordOrDamaged);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        try
        {
            return Deserialize(Decompress(plain)) is { } data
                ? Result.Success(data)
                : Result.Failure<HouseholdData>(DataTransferErrors.FileIncomplete);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or FormatException or InvalidOperationException)
        {
            return Result.Failure<HouseholdData>(DataTransferErrors.FileIncomplete);
        }
    }

    public string Protect(HouseholdData data) =>
        cipher.Encrypt(Convert.ToBase64String(Compress(Serialize(data))));

    public HouseholdData Unprotect(string content) =>
        Deserialize(Decompress(Convert.FromBase64String(cipher.Decrypt(content))))
        ?? throw new InvalidDataException("The safety copy is incomplete.");

    private static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, KeySize);

    private static byte[] Serialize(HouseholdData data)
    {
        var summary = data.Summary;
        var root = new JsonObject
        {
            ["format"] = FileFormat,
            ["formatVersion"] = FormatVersion,
            ["createdAt"] = data.CreatedAt,
            ["release"] = data.Release,
            ["migration"] = data.Migration,
            ["summary"] = new JsonObject
            {
                ["cards"] = summary.Cards,
                ["loans"] = summary.Loans,
                ["inputs"] = summary.Inputs,
                ["cardPurchases"] = summary.CardPurchases,
                ["bills"] = summary.Bills,
                ["expenses"] = summary.Expenses,
            },
            ["tables"] = data.Tables.DeepClone(),
        };
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static HouseholdData? Deserialize(byte[] json)
    {
        if (JsonNode.Parse(json) is not JsonObject root
            || (string?)root["format"] != FileFormat
            || root["tables"] is not JsonObject tables
            || root["summary"] is not JsonObject summary
            || (string?)root["migration"] is not { Length: > 0 } migration)
        {
            return null;
        }

        return new HouseholdData(
            root["createdAt"]!.GetValue<DateTimeOffset>(),
            (string?)root["release"] ?? "unknown",
            migration,
            new DataSummaryResponse(
                (int?)summary["cards"] ?? 0,
                (int?)summary["loans"] ?? 0,
                (int?)summary["inputs"] ?? 0,
                (int?)summary["cardPurchases"] ?? 0,
                (int?)summary["bills"] ?? 0,
                (int?)summary["expenses"] ?? 0),
            tables);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
