namespace Monthwise.Domain.DataTransfer;

/// <summary>
/// The largest export file an import accepts and the most its content may unpack to (backup and restore
/// Appendix 3). Both are far above the data of a household; widen them here if that ever changes.
/// </summary>
public static class DataTransferLimits
{
    public const int MaxFileMegabytes = 10;
    public const long MaxFileBytes = MaxFileMegabytes * 1024L * 1024;
    public const long MaxContentBytes = 50L * 1024 * 1024;
}
