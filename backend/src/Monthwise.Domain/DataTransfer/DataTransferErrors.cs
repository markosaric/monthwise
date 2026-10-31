using Monthwise.Domain.Common;

namespace Monthwise.Domain.DataTransfer;

/// <summary>Business rule errors of export and import (backup and restore requirements).</summary>
public static class DataTransferErrors
{
    public const int PasswordMinLength = 6;
    public const string ConfirmationWord = "REPLACE";

    public static readonly DomainError PasswordTooShort = new(
        "data.password_too_short", $"The password must be at least {PasswordMinLength} characters long.", "password");

    public static readonly DomainError PasswordRequired = new(
        "data.password_required", "Enter the password of the file.", "password");

    public static readonly DomainError FileRequired = new(
        "data.file_required", "Choose an export file.", "file");

    public static readonly DomainError NotAnExportFile = new(
        "data.not_an_export_file", "This is not a Monthwise export file.", "file");

    public static readonly DomainError WrongPasswordOrDamaged = new(
        "data.wrong_password_or_damaged", "The password is wrong or the file is damaged.", "password");

    public static readonly DomainError FileIncomplete = new(
        "data.file_incomplete", "The file is incomplete: it does not hold all the data Monthwise expects.", "file");

    public static readonly DomainError UnknownRelease = new(
        "data.unknown_release", "The file was not made by a release of Monthwise this one knows.", "file");

    public static readonly DomainError ConfirmationRequired = new(
        "data.confirmation_required", $"Type {ConfirmationWord} to confirm that all data is replaced.", "confirmation");

    public static readonly DomainError ReminderIntervalInvalid = new(
        "data.reminder_interval_invalid", "Choose 7, 14, 30, 60 or 90 days.", "intervalDays");

    public static readonly DomainError ImportRunning = new(
        "data.import_running", "An import is already running.", Kind: ErrorKind.Conflict);

    public static readonly DomainError NoSafetyCopy = new(
        "data.no_safety_copy", "There is no safety copy.", Kind: ErrorKind.NotFound);

    public static readonly DomainError ImportFailed = new(
        "data.import_failed", "The import failed and no data was changed. See the log for details.", Kind: ErrorKind.Conflict);

    public static readonly DomainError ExportFailed = new(
        "data.export_failed", "The export failed. See the log for details.", Kind: ErrorKind.Conflict);

    /// <summary>A file made by a newer release; the message names it, never the data.</summary>
    public static DomainError NewerRelease(string release) => new(
        "data.newer_release",
        $"The file was made by Monthwise {release}, which is newer than this one. Update Monthwise first.",
        "file");
}
