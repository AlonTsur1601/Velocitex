using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Velocitex.Core.Save;

public static class CampaignSaveService
{
    public const int MaximumRoomCount = 28;
    public const int MaximumSnapshotCount = 5;
    public const string DefaultRoot = "user://campaign";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static bool Save(
        CampaignSnapshot snapshot,
        Image? thumbnail,
        out string? error,
        string root = DefaultRoot)
    {
        error = Validate(snapshot);
        if (error is not null)
        {
            return false;
        }

        try
        {
            string absoluteRoot = ResolveRoot(root);
            Directory.CreateDirectory(absoluteRoot);
            string stem = GetStem(snapshot);

            if (thumbnail is not null)
            {
                string thumbnailPath = Path.Combine(absoluteRoot, $"{stem}.jpg");
                thumbnail.Resize(256, 144, Image.Interpolation.Lanczos);
                Error imageError = thumbnail.SaveJpg(thumbnailPath, 0.78f);
                snapshot.ThumbnailPath = imageError == Error.Ok ? thumbnailPath : null;
            }

            string finalPath = Path.Combine(absoluteRoot, $"{stem}.json");
            AtomicWrite(finalPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            PruneOldestSnapshots(absoluteRoot);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            error = exception.Message;
            return false;
        }
    }

    public static IReadOnlyList<CampaignSnapshot> LoadAll(
        out IReadOnlyList<string> errors,
        string root = DefaultRoot)
    {
        List<CampaignSnapshot> snapshots = new(MaximumSnapshotCount);
        List<string> loadErrors = new();
        string absoluteRoot = ResolveRoot(root);
        if (!Directory.Exists(absoluteRoot))
        {
            errors = loadErrors;
            return snapshots;
        }

        foreach (string path in Directory.EnumerateFiles(absoluteRoot, "*.json"))
        {
            if (TryLoadPath(path, out CampaignSnapshot? snapshot, out string? error) && snapshot is not null)
            {
                if (snapshot.Kind == SnapshotKind.RoomComplete) snapshots.Add(snapshot);
            }
            else
            {
                loadErrors.Add($"{Path.GetFileName(path)}: {error}");
            }
        }

        errors = loadErrors;
        return snapshots
            .OrderByDescending(snapshot => snapshot.SavedAtUtc)
            .ThenByDescending(snapshot => snapshot.RoomNumber)
            .ToArray();
    }

    public static CampaignSnapshot? LoadLatest(string root = DefaultRoot)
    {
        return LoadAll(out _, root).FirstOrDefault();
    }

    public static IReadOnlySet<int> GetCompletedRoomNumbers(string root = DefaultRoot)
    {
        return LoadAll(out _, root)
            .Where(snapshot => snapshot.Kind == SnapshotKind.RoomComplete)
            .Select(snapshot => snapshot.RoomNumber)
            .ToHashSet();
    }

    public static bool DeleteAll(out string? error, string root = DefaultRoot)
    {
        error = null;
        try
        {
            string absoluteRoot = ResolveRoot(root);
            if (!Directory.Exists(absoluteRoot))
            {
                return true;
            }

            foreach (string path in Directory.EnumerateFiles(absoluteRoot))
            {
                string fileName = Path.GetFileName(path);
                if (IsCampaignFile(fileName))
                {
                    File.Delete(path);
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryLoadPath(
        string finalPath,
        out CampaignSnapshot? snapshot,
        out string? error)
    {
        if (TryDeserialize(finalPath, out snapshot, out error))
        {
            return true;
        }

        string backupPath = finalPath + ".bak";
        string? backupError = null;
        if (!File.Exists(backupPath) || !TryDeserialize(backupPath, out snapshot, out backupError))
        {
            error = backupError is null ? error : $"{error}; backup: {backupError}";
            return false;
        }

        try
        {
            File.Copy(backupPath, finalPath, overwrite: true);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Backup loaded but could not be restored: {exception.Message}";
            return true;
        }
    }

    private static bool TryDeserialize(
        string path,
        out CampaignSnapshot? snapshot,
        out string? error)
    {
        snapshot = null;
        error = null;
        try
        {
            snapshot = JsonSerializer.Deserialize<CampaignSnapshot>(File.ReadAllText(path), JsonOptions);
            if (snapshot is null)
            {
                error = "File contained no snapshot.";
                return false;
            }

            error = Validate(snapshot);
            return error is null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string? Validate(CampaignSnapshot snapshot)
    {
        if (snapshot.Version != CampaignSnapshot.CurrentVersion)
        {
            return $"Unsupported snapshot version {snapshot.Version}.";
        }

        if (snapshot.RoomNumber is < 1 or > MaximumRoomCount)
        {
            return $"Room number {snapshot.RoomNumber} is out of range.";
        }

        if (snapshot.Kind != SnapshotKind.RoomComplete)
        {
            return "Only completed-room snapshots are supported.";
        }

        if (string.IsNullOrWhiteSpace(snapshot.RoomId) || string.IsNullOrWhiteSpace(snapshot.RoomName))
        {
            return "Room identity is missing.";
        }

        if (snapshot.CampaignElapsedSeconds < 0.0)
        {
            return "Campaign elapsed time cannot be negative.";
        }

        return null;
    }

    private static void AtomicWrite(string finalPath, string contents)
    {
        string temporaryPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(contents);
        using (FileStream stream = new(
            temporaryPath,
            FileMode.Create,
            System.IO.FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(finalPath))
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Replace(temporaryPath, finalPath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, finalPath);
        }
    }

    private static bool IsCampaignFile(string fileName)
    {
        if (!fileName.StartsWith("room-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".json.tmp", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStem(CampaignSnapshot snapshot)
    {
        return $"complete-{snapshot.SavedAtUtc.UtcTicks:D19}-{snapshot.RoomNumber:D2}";
    }

    private static void PruneOldestSnapshots(string root)
    {
        CampaignSnapshot[] old = LoadAll(out _, root).Skip(MaximumSnapshotCount).ToArray();
        foreach (CampaignSnapshot snapshot in old)
        {
            string stem = Path.Combine(root, GetStem(snapshot));
            foreach (string suffix in new[] { ".json", ".json.bak", ".jpg" }) if (File.Exists(stem + suffix)) File.Delete(stem + suffix);
        }
    }

    private static string ResolveRoot(string root)
    {
        return root.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(root)
            : Path.GetFullPath(root);
    }
}
