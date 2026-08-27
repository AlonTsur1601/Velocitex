using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Velocitex.Core.Updates;

public enum UpdateCheckStatus { UpToDate, UpdateAvailable, Failed }

public sealed record ReleaseUpdateInfo(Version Version, Uri DownloadUri);
public sealed record UpdateCheckResult(UpdateCheckStatus Status, string Message, ReleaseUpdateInfo? Update = null);

public static class ReleaseUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/AlonTsur1601/Velocitex/releases/latest";
    private const string ReleaseAssetName = "Velocitex.zip";
    private static readonly HttpClient Client = CreateClient();

    private const string VersionLabelFileName = "version.txt";

    public static Version CurrentVersion
    {
        get
        {
            foreach (string directory in VersionLabelDirectories())
            {
                string labelPath = Path.Combine(directory, VersionLabelFileName);
                if (File.Exists(labelPath) && Version.TryParse(File.ReadAllText(labelPath).Trim(), out Version? labeledVersion) && labeledVersion is not null)
                    return labeledVersion;
            }

            Version assemblyVersion = typeof(ReleaseUpdateService).Assembly.GetName().Version ?? new Version(1, 4, 0);
            Version normalizedAssemblyVersion = new(assemblyVersion.Major, assemblyVersion.Minor, Math.Max(0, assemblyVersion.Build));
            return normalizedAssemblyVersion;
        }
    }

    private static IEnumerable<string> VersionLabelDirectories()
    {
        // The real executable's own folder (where the updater actually installs files) takes priority;
        // AppContext.BaseDirectory can point at the Godot Mono "data_..." subfolder instead.
        string? processDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrWhiteSpace(processDirectory)) yield return processDirectory;
        yield return AppContext.BaseDirectory;
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await Client.GetAsync(LatestReleaseUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(UpdateCheckStatus.Failed, "GitHub could not provide the latest release.");
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            JsonElement root = json.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
                return new(UpdateCheckStatus.Failed, "The latest GitHub release is not a stable release.");
            if (!TryParseVersion(root.GetProperty("tag_name").GetString(), out Version? latest))
                return new(UpdateCheckStatus.Failed, "The latest GitHub release has an invalid version tag.");
            if (latest <= CurrentVersion) return new(UpdateCheckStatus.UpToDate, $"Velocitex v{CurrentVersion} is up to date.");
            JsonElement asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(item =>
                string.Equals(item.GetProperty("name").GetString(), ReleaseAssetName, StringComparison.Ordinal));
            if (asset.ValueKind == JsonValueKind.Undefined || !Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out Uri? download) ||
                download.Scheme != Uri.UriSchemeHttps || !string.Equals(download.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                return new(UpdateCheckStatus.Failed, "The latest release does not contain a valid Velocitex.zip download.");
            return new(UpdateCheckStatus.UpdateAvailable, $"Velocitex v{latest} is ready to install.", new(latest!, download));
        }
        catch (OperationCanceledException) { return new(UpdateCheckStatus.Failed, "The update check timed out."); }
        catch (HttpRequestException) { return new(UpdateCheckStatus.Failed, "Could not connect to GitHub to check for updates."); }
        catch (JsonException) { return new(UpdateCheckStatus.Failed, "GitHub returned an invalid update response."); }
    }

    public static async Task<string?> DownloadAndStartUpdaterAsync(ReleaseUpdateInfo update, Action<long, long?>? progress = null, CancellationToken cancellationToken = default)
    {
        string installDirectory = ResolveInstallDirectory();
        string? updaterPath = FindInstalledUpdaterPath();
        if (updaterPath is null) return "The installed updater is missing. Reinstall Velocitex from GitHub Releases.";
        string stagingDirectory = Path.Combine(Path.GetTempPath(), "VelocitexUpdate", Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(stagingDirectory, ReleaseAssetName);
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            using (HttpRequestMessage request = new(HttpMethod.Get, update.DownloadUri))
            using (HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;
                // Attempt to create the destination file with retries to tolerate transient locks (antivirus, leftover handles)
                const int writeMaxAttempts = 5;
                FileStream? destinationStream = null;
                try
                {
                    for (int attempt = 1; attempt <= writeMaxAttempts; attempt++)
                    {
                        try
                        {
                            destinationStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                            break;
                        }
                        catch (IOException) when (attempt < writeMaxAttempts)
                        {
                            await Task.Delay(300 * attempt, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (destinationStream is null) throw new IOException("Could not create package file for writing.");

                    await using (destinationStream)
                    await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken))
                    {
                        byte[] buffer = new byte[81920];
                        long downloadedBytes = 0;
                        long lastProgressTimestamp = Stopwatch.GetTimestamp();
                        int read;
                        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            downloadedBytes += read;
                            long now = Stopwatch.GetTimestamp();
                            bool intervalElapsed = Stopwatch.GetElapsedTime(lastProgressTimestamp).TotalMilliseconds >= 100;
                            if (intervalElapsed || totalBytes is > 0 && downloadedBytes >= totalBytes.Value)
                            {
                                progress?.Invoke(downloadedBytes, totalBytes);
                                lastProgressTimestamp = now;
                            }
                        }
                        await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // ensure stream is disposed if it was created but not wrapped by await using above
                    if (destinationStream is not null)
                    {
                        await destinationStream.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            string extractedDirectory = Path.Combine(stagingDirectory, "extracted");
            await ExtractPackageWithRetryAsync(packagePath, extractedDirectory, cancellationToken).ConfigureAwait(false);
            ProcessStartInfo start = new(updaterPath) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = installDirectory };
            start.ArgumentList.Add("--parent-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add("--install-dir"); start.ArgumentList.Add(installDirectory);
            start.ArgumentList.Add("--extracted-dir"); start.ArgumentList.Add(extractedDirectory);
            start.ArgumentList.Add("--launch"); start.ArgumentList.Add(Path.Combine(installDirectory, "Velocitex.exe"));
            start.ArgumentList.Add("--version"); start.ArgumentList.Add(update.Version.ToString());
            if (Process.Start(start) is null) return "Could not start the updater.";
            return null;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return $"The update could not be prepared: {error.Message}";
        }
    }

    public static bool TryParseVersion(string? tag, out Version? version) => Version.TryParse(tag?.Trim().TrimStart('v', 'V'), out version);

    public static string? FindInstalledUpdaterPath()
    {
        foreach (string directory in CandidateInstallDirectories())
        {
            string candidate = Path.Combine(directory, "VelocitexUpdater.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static void ExtractPackage(string packagePath, string extractedDirectory)
    {
        Directory.CreateDirectory(extractedDirectory);
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        bool executable = false, pck = false;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string relative = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is "." or "..")) throw new InvalidDataException("The update archive contains an unsafe path.");
            string target = Path.GetFullPath(Path.Combine(extractedDirectory, relative));
            if (!target.StartsWith(extractedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update archive contains an unsafe path.");
            executable |= string.Equals(relative, "Velocitex.exe", StringComparison.OrdinalIgnoreCase);
            pck |= string.Equals(relative, "Velocitex.pck", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
        if (!executable || !pck) throw new InvalidDataException("The update archive is missing Velocitex.exe or Velocitex.pck.");
    }

    private static async Task ExtractPackageWithRetryAsync(string packagePath, string extractedDirectory, CancellationToken cancellationToken)
    {
        const int maxAttempts = 8;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                ExtractPackage(packagePath, extractedDirectory);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // The freshly downloaded file may briefly be locked (antivirus scan, indexing); back off and retry.
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Velocitex", "1.4.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static string ResolveInstallDirectory()
    {
        string? updaterPath = FindInstalledUpdaterPath();
        if (updaterPath is not null)
        {
            return Path.GetDirectoryName(updaterPath)!;
        }

        string? processDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        return string.IsNullOrWhiteSpace(processDirectory)
            ? AppContext.BaseDirectory
            : processDirectory;
    }

    private static IEnumerable<string> CandidateInstallDirectories()
    {
        HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);
        AddCandidate(Path.GetDirectoryName(Environment.ProcessPath));
        AddCandidate(AppContext.BaseDirectory);
        AddCandidate(Directory.GetParent(AppContext.BaseDirectory)?.FullName);
        return candidates;

        void AddCandidate(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                candidates.Add(Path.GetFullPath(directory));
            }
        }
    }
}
