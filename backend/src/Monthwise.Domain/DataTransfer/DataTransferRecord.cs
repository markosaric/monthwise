namespace Monthwise.Domain.DataTransfer;

/// <summary>What was done with the household data (backup and restore FR-9).</summary>
public enum DataTransferKind
{
    Export,
    Import,
    /// <summary>The copy Monthwise makes by itself right before an import (FR-7).</summary>
    SafetyCopy,
    /// <summary>The safety copy brought back.</summary>
    SafetyCopyRestore,
}

/// <summary>
/// One line of the data and backups history (FR-9). It belongs to the installation, not to the
/// household data: it is never exported and an import does not replace it. Records are never
/// changed or deleted.
/// </summary>
public sealed class DataTransferRecord
{
    public const int FileNameMaxLength = 200;
    public const int ReleaseMaxLength = 100;
    public const int MessageMaxLength = 500;
    public const int UserNameMaxLength = 100;

    private DataTransferRecord()
    {
        Release = string.Empty;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DataTransferKind Kind { get; private set; }

    /// <summary>Who did it; empty until login exists.</summary>
    public string? UserName { get; private set; }
    public bool Succeeded { get; private set; }
    public string? FileName { get; private set; }

    /// <summary>The release that did it.</summary>
    public string Release { get; private set; }

    /// <summary>For an import: when the imported data was exported.</summary>
    public DateTimeOffset? FileCreatedAt { get; private set; }

    /// <summary>A plain reason for a failure; never household data.</summary>
    public string? Message { get; private set; }

    public static DataTransferRecord Create(
        DataTransferKind kind,
        DateTimeOffset now,
        bool succeeded,
        string release,
        string? fileName = null,
        DateTimeOffset? fileCreatedAt = null,
        string? message = null,
        string? userName = null) => new()
        {
            Id = Guid.CreateVersion7(now),
            OccurredAt = now,
            Kind = kind,
            Succeeded = succeeded,
            Release = Truncate(release, ReleaseMaxLength)!,
            FileName = Truncate(fileName, FileNameMaxLength),
            FileCreatedAt = fileCreatedAt,
            Message = Truncate(message, MessageMaxLength),
            UserName = Truncate(userName, UserNameMaxLength),
        };

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
