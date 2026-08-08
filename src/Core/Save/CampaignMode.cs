namespace Velocitex.Core.Save;

public enum CampaignMode { Normal, Hard, Extreme }

public static class CampaignModes
{
    public static string Root(CampaignMode mode) => mode switch
    {
        CampaignMode.Hard => "user://campaign-hard",
        CampaignMode.Extreme => "user://campaign-extreme",
        _ => CampaignSaveService.DefaultRoot,
    };

    public static bool IsCheckpoint(int roomNumber) => roomNumber is 1 or 6 or 11 or 16 or 21 or 26;
    public static int CheckpointFor(int roomNumber) => Math.Max(1, ((roomNumber - 1) / 5 * 5) + 1);
}
