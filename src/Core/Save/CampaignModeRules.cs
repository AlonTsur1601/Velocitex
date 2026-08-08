namespace Velocitex.Core.Save;

/// <summary>Single source of truth for campaign-mode persistence and recovery.</summary>
public static class CampaignModeRules
{
    public static bool AllowsManualLoad(CampaignMode mode) => mode != CampaignMode.Extreme;
    public static bool AllowsCheckpointSelection(CampaignMode mode) => mode == CampaignMode.Hard;
    public static bool SavesRoomStart(CampaignMode mode, int roomNumber) =>
        mode == CampaignMode.Normal || CampaignModes.IsCheckpoint(roomNumber);

    public static int RestartRoomAfterFailure(CampaignMode mode, int currentRoom) => mode switch
    {
        CampaignMode.Hard => CampaignModes.CheckpointFor(currentRoom),
        CampaignMode.Extreme => 1,
        _ => currentRoom,
    };

    public static IReadOnlyList<int> Checkpoints => new[] { 1, 6, 11, 16, 21, 26 };
}
