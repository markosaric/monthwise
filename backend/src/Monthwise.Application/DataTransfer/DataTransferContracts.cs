using System.Text.Json.Nodes;
using Monthwise.Domain.DataTransfer;

namespace Monthwise.Application.DataTransfer;

/// <summary>How much household data there is, shown before an import (backup and restore FR-2, FR-4).</summary>
public sealed record DataSummaryResponse(int Cards, int Loans, int Inputs, int CardPurchases, int Bills, int Expenses);

/// <summary>One line of the data and backups history (FR-9).</summary>
public sealed record DataTransferResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    DataTransferKind Kind,
    string? UserName,
    bool Succeeded,
    string? FileName,
    string Release,
    DateTimeOffset? FileCreatedAt,
    string? Message);

/// <summary>The export reminder setting and whether the Dashboard shows it now (FR-10, FR-11).</summary>
public sealed record BackupReminderResponse(bool Enabled, int IntervalDays, IReadOnlyList<int> IntervalChoices, bool IsDue);

/// <summary>
/// The top of the Data and backups section (FR-1, FR-9): the last successful export and import, the
/// safety copy, the reminder, and whether this user may export and import.
/// </summary>
public sealed record DataOverviewResponse(
    DataTransferResponse? LastExport,
    DataTransferResponse? LastImport,
    DateTimeOffset? SafetyCopyCreatedAt,
    BackupReminderResponse Reminder,
    bool CanManage);

/// <summary>A page of the history, newest first.</summary>
public sealed record DataHistoryResponse(IReadOnlyList<DataTransferResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>What is in an export file, next to what is in Monthwise now (FR-4, FR-5).</summary>
public sealed record ImportCheckResponse(
    DateTimeOffset FileCreatedAt,
    string FileRelease,
    DataSummaryResponse File,
    DataSummaryResponse Current);

/// <summary>What was imported (FR-8).</summary>
public sealed record ImportResponse(DateTimeOffset FileCreatedAt, DataSummaryResponse Summary);

public sealed record ExportRequest(string? Password);

public sealed record BackupReminderRequest(bool Enabled, int IntervalDays);

public sealed record SafetyCopyRestoreRequest(string? Confirmation);

/// <summary>An export file ready to download.</summary>
public sealed record ExportFile(string FileName, byte[] Content);

/// <summary>
/// All household data at one moment, as it travels in an export file or a safety copy: every
/// exported table as rows of column name to value, with card numbers and IBANs readable.
/// </summary>
public sealed record HouseholdData(
    DateTimeOffset CreatedAt,
    string Release,
    string Migration,
    DataSummaryResponse Summary,
    JsonObject Tables);
