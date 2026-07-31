using System.Diagnostics;
using System.Threading;
using System.IO;

static string? ValueOrNull(string[] args, string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
static string Value(string[] args, string name) { var v = ValueOrNull(args, name); return v ?? throw new ArgumentException($"Missing {name}"); }

try
{
    // Log invocation for diagnostics
    try
    {
        string invLog = Path.Combine(Path.GetTempPath(), "VelocitexUpdaterInvocation.txt");
        File.AppendAllText(invLog, $"[{DateTime.UtcNow:O}] Args: {string.Join(' ', args)}\nCwd: {Environment.CurrentDirectory}\n\n");
    }
    catch { /* best-effort logging */ }

    string? parentPidStr = ValueOrNull(args, "--parent-pid");
    int parentPid = -1; if (parentPidStr is not null && int.TryParse(parentPidStr, out int p)) parentPid = p;
    string install = Path.GetFullPath(Value(args, "--install-dir"));
    string stage = Path.GetFullPath(Value(args, "--extracted-dir"));
    string launch = Path.GetFullPath(Value(args, "--launch"));
    string version = Value(args, "--version");

    if (!Version.TryParse(version, out Version? parsedVersion) || parsedVersion is null) throw new InvalidDataException("Invalid update version.");
    if (!launch.StartsWith(install + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe launch path.");

    if (parentPid > 0)
    {
        try { Process.GetProcessById(parentPid).WaitForExit(30000); } catch (ArgumentException) { }
    }

    if (!File.Exists(Path.Combine(stage, "Velocitex.exe")) || !File.Exists(Path.Combine(stage, "Velocitex.pck"))) throw new InvalidDataException("Incomplete update package.");
    const int maxAttempts = 8;
    foreach (string file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(stage, file); if (relative.Equals("VelocitexUpdater.exe", StringComparison.OrdinalIgnoreCase)) continue;
        string target = Path.Combine(install, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try { File.Copy(file, target, true); break; }
            catch (IOException) when (attempt < maxAttempts) { Thread.Sleep(300 * attempt); }
        }
    }
    File.WriteAllText(Path.Combine(install, "version.txt"), version);
    Process.Start(new ProcessStartInfo(launch) { UseShellExecute = true, WorkingDirectory = install });
}
catch (Exception error) { 
    try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "VelocitexUpdateError.txt"), error.ToString()); File.AppendAllText(Path.Combine(Path.GetTempPath(), "VelocitexUpdaterInvocation.txt"), "ERROR:\n" + error.ToString() + "\n\n"); } catch {};
    Environment.ExitCode = 1; 
}
