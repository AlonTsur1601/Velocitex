using System.Text;
using System.Text.Json;
using Velocitex.Core.Save;

namespace Velocitex.Core.Profile;

public static class ProfileStore
{
    public const string DefaultPath = "user://profile.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static PlayerProfile CreateDefault()
    {
        PlayerProfile profile = new();
        foreach (string cosmeticId in CosmeticCatalog.CreateBaseUnlockSet())
        {
            profile.UnlockedCosmeticIds.Add(cosmeticId);
        }

        return profile;
    }

    public static PlayerProfile Load(out string? warning, string path = DefaultPath)
    {
        warning = null;
        string absolutePath = ResolvePath(path);
        if (!File.Exists(absolutePath))
        {
            return CreateDefault();
        }

        if (!TryRead(absolutePath, out PlayerProfile? profile, out warning))
        {
            string backupPath = absolutePath + ".bak";
            if (!TryRead(backupPath, out profile, out string? backupWarning))
            {
                warning = $"Profile and backup were invalid. {warning} {backupWarning}".Trim();
                return CreateDefault();
            }

            File.Copy(backupPath, absolutePath, overwrite: true);
            warning = "The active profile was invalid and was restored from backup.";
        }

        return Normalize(profile!);
    }

    public static bool Save(PlayerProfile profile, out string? error, string path = DefaultPath)
    {
        error = null;
        try
        {
            Normalize(profile);
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            string absolutePath = ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            AtomicWrite(absolutePath, JsonSerializer.Serialize(profile, JsonOptions));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            error = exception.Message;
            return false;
        }
    }

    public static PlayerProfile Normalize(PlayerProfile profile)
    {
        foreach (string cosmeticId in CosmeticCatalog.CreateBaseUnlockSet())
        {
            profile.UnlockedCosmeticIds.Add(cosmeticId);
        }

        profile.PrimaryColorId = NormalizeSelection(profile, CosmeticKind.Color, profile.PrimaryColorId, "cherry");
        profile.SecondaryColorId = NormalizeSelection(profile, CosmeticKind.Color, profile.SecondaryColorId, "vanilla");
        profile.PatternId = NormalizeSelection(profile, CosmeticKind.Pattern, profile.PatternId, "none");
        profile.TrailId = NormalizeSelection(profile, CosmeticKind.Trail, profile.TrailId, "off");
        profile.CleanRoomStreak = Math.Max(0, profile.CleanRoomStreak);
        return profile;
    }

    public static void DeleteTestFiles(string path)
    {
        string absolutePath = ResolvePath(path);
        foreach (string candidate in new[] { absolutePath, absolutePath + ".tmp", absolutePath + ".bak" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    private static string NormalizeSelection(
        PlayerProfile profile,
        CosmeticKind kind,
        string selectedId,
        string fallbackId)
    {
        return CosmeticCatalog.Find(kind, selectedId) is not null &&
            profile.UnlockedCosmeticIds.Contains(selectedId)
            ? selectedId
            : fallbackId;
    }

    private static bool TryRead(
        string path,
        out PlayerProfile? profile,
        out string? error)
    {
        profile = null;
        error = null;
        if (!File.Exists(path))
        {
            error = "File does not exist.";
            return false;
        }

        try
        {
            profile = JsonSerializer.Deserialize<PlayerProfile>(File.ReadAllText(path), JsonOptions);
            if (profile is null || profile.Version != PlayerProfile.CurrentVersion)
            {
                error = "Profile version is missing or unsupported.";
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void AtomicWrite(string finalPath, string contents)
    {
        string temporaryPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";
        byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
        using (FileStream stream = new(
            temporaryPath,
            FileMode.Create,
            System.IO.FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }

        if (File.Exists(finalPath))
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Replace(temporaryPath, finalPath, backupPath, true);
        }
        else
        {
            File.Move(temporaryPath, finalPath);
        }
    }

    private static string ResolvePath(string path)
    {
        return path.StartsWith("user://", StringComparison.Ordinal)
            ? Godot.ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
    }
}
