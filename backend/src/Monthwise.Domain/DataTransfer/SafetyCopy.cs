namespace Monthwise.Domain.DataTransfer;

/// <summary>
/// The household data as it was right before the newest import (backup and restore FR-7), kept on
/// the installation so a wrong import can be undone. Only the newest one is kept.
/// </summary>
public sealed class SafetyCopy
{
    public const int SingletonId = 1;

    private SafetyCopy()
    {
        Release = string.Empty;
        Content = string.Empty;
    }

    public int Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string Release { get; private set; }

    /// <summary>The exported data, protected with the installation's encryption key.</summary>
    public string Content { get; private set; }

    public static SafetyCopy Create(DateTimeOffset now, string release, string content) =>
        new() { Id = SingletonId, CreatedAt = now, Release = release, Content = content };

    public void Replace(DateTimeOffset now, string release, string content)
    {
        CreatedAt = now;
        Release = release;
        Content = content;
    }
}
