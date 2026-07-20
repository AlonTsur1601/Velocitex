namespace Velocitex.Core.Save;

public enum SnapshotKind
{
    RoomStart,
    RoomComplete,
}

public sealed class CampaignSnapshot
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public required string RoomId { get; init; }
    public required string RoomName { get; init; }
    public int RoomNumber { get; init; }
    public SnapshotKind Kind { get; init; }
    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public double CampaignElapsedSeconds { get; init; }
    public string? ThumbnailPath { get; set; }
}
