using Godot;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;

namespace Velocitex.Tests;

public partial class ProfileSmokeTest : Node
{
    private const string TestPath = "user://profile-smoke.json";

    public override void _Ready()
    {
        ProfileStore.DeleteTestFiles(TestPath);
        PlayerProfile profile = ProfileStore.CreateDefault();
        if (profile.UnlockedCosmeticIds.Count != 18)
        {
            Fail($"Expected 18 base cosmetics, found {profile.UnlockedCosmeticIds.Count}.");
            return;
        }

        profile.PrimaryColorId = "blueberry";
        profile.SecondaryColorId = "vanilla";
        profile.PatternId = "spiral";
        profile.TrailId = "trail-gold";
        profile.UnlockedAdvancementIds.Add("clean-wrapper");
        if (!ProfileStore.Save(profile, out string? saveError, TestPath))
        {
            Fail($"Profile save failed: {saveError}");
            return;
        }

        PlayerProfile loaded = ProfileStore.Load(out string? loadWarning, TestPath);
        if (loadWarning is not null || loaded.PrimaryColorId != "blueberry" ||
            loaded.PatternId != "spiral" || loaded.TrailId != "trail-gold" ||
            !loaded.UnlockedAdvancementIds.Contains("clean-wrapper"))
        {
            Fail($"Profile round-trip failed: {loadWarning}");
            return;
        }

        loaded.PrimaryColorId = "missing-color";
        loaded.PatternId = "missing-pattern";
        loaded.TrailId = "missing-trail";
        ProfileStore.Normalize(loaded);
        if (loaded.PrimaryColorId != "cherry" || loaded.PatternId != "none" || loaded.TrailId != "off")
        {
            Fail("Invalid cosmetic selections were not normalized.");
            return;
        }

        loaded.PrimaryColorId = "lime";
        if (!ProfileStore.Save(loaded, out saveError, TestPath))
        {
            Fail($"Profile overwrite failed: {saveError}");
            return;
        }

        string absolutePath = ProjectSettings.GlobalizePath(TestPath);
        if (!File.Exists(absolutePath + ".bak"))
        {
            Fail("Profile overwrite did not retain a backup.");
            return;
        }

        File.WriteAllText(absolutePath, "{ broken profile");
        PlayerProfile recovered = ProfileStore.Load(out loadWarning, TestPath);
        if (loadWarning is null || recovered.PrimaryColorId != "blueberry")
        {
            Fail("Corrupted profile was not restored from the prior backup.");
            return;
        }

        ProfileStore.DeleteTestFiles(TestPath);
        GD.Print("PROFILE_SMOKE_PASS: base catalog, round-trip, normalization and backup recovery work.");
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        ProfileStore.DeleteTestFiles(TestPath);
        GD.PushError($"PROFILE_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }
}
