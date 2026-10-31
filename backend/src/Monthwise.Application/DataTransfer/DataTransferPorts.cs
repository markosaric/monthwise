using Monthwise.Domain.Common;
using Monthwise.Domain.DataTransfer;

namespace Monthwise.Application.DataTransfer;

/// <summary>Reads and replaces all household data at once (backup and restore FR-2, FR-6, FR-7).</summary>
public interface IHouseholdDataStore
{
    /// <summary>All household data from one moment, while normal use goes on.</summary>
    Task<HouseholdData> ReadAllAsync(DateTimeOffset now, string release, CancellationToken cancellationToken);

    Task<DataSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);

    /// <summary>Whether this release can import the data: a known release and every table complete. Changes nothing.</summary>
    Task<Result> CheckAsync(HouseholdData data, CancellationToken cancellationToken);

    /// <summary>Replaces all household data with <paramref name="data"/>, brought up to this release; on any failure nothing changes.</summary>
    Task<Result> ReplaceAllAsync(HouseholdData data, CancellationToken cancellationToken);
}

/// <summary>Writes and reads export files and safety copies (FR-3, FR-4, FR-7).</summary>
public interface IExportFileCodec
{
    /// <summary>The export file, protected with <paramref name="password"/>.</summary>
    byte[] Write(HouseholdData data, string password);

    Result<HouseholdData> Read(byte[] file, string password);

    /// <summary>A safety copy, protected with the installation's encryption key instead of a password.</summary>
    string Protect(HouseholdData data);

    HouseholdData Unprotect(string content);
}

/// <summary>The history, the reminder setting and the safety copy; none of them are household data.</summary>
public interface IDataTransferRepository
{
    void Add(DataTransferRecord record);

    Task<DataTransferRecord?> GetLastSuccessfulAsync(IReadOnlyCollection<DataTransferKind> kinds, CancellationToken cancellationToken);

    Task<(IReadOnlyList<DataTransferRecord> Items, int TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<BackupReminder> GetReminderAsync(CancellationToken cancellationToken);

    Task<SafetyCopy?> GetSafetyCopyAsync(CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetSafetyCopyCreatedAtAsync(CancellationToken cancellationToken);

    void Add(SafetyCopy safetyCopy);
}
