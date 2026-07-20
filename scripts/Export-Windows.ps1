$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

& (Join-Path $PSScriptRoot "Build.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Project build failed with code $LASTEXITCODE."
}

$outputRoot = Join-Path $root "builds\windows\Velocitex"
$outputExe = Join-Path $outputRoot "Velocitex.exe"
$resolvedRoot = [IO.Path]::GetFullPath($root)
if (-not [IO.Path]::GetFullPath($outputRoot).StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Windows export path resolved outside the project."
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$exportOut = Join-Path $outputRoot "export-stdout.log"
$exportErr = Join-Path $outputRoot "export-stderr.log"
$exportProcess = Start-Process -FilePath $godot.FullName `
    -ArgumentList @("--headless", "--path", $root, "--export-release", '"Windows Desktop"', $outputExe) `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput $exportOut -RedirectStandardError $exportErr
$deadline = [DateTime]::UtcNow.AddMinutes(6)
$lastSize = -1L
$lastFileCount = -1
$stableSince = $null
while (-not $exportProcess.HasExited -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 500
    $exportProcess.Refresh()
    if ((Test-Path -LiteralPath $outputExe) -and (Test-Path -LiteralPath (Join-Path $outputRoot "Velocitex.pck"))) {
        $files = @(Get-ChildItem -LiteralPath $outputRoot -Recurse -File)
        $size = ($files | Measure-Object Length -Sum).Sum
        $fileCount = $files.Count
        $runtimeReady = $files.Name -contains "hostfxr.dll" -and
            $files.Name -contains "coreclr.dll" -and
            $files.Name -contains "Velocitex.dll"
        if ($runtimeReady -and $fileCount -ge 175 -and $size -eq $lastSize -and $fileCount -eq $lastFileCount) {
            if ($null -eq $stableSince) { $stableSince = [DateTime]::UtcNow }
            if (([DateTime]::UtcNow - $stableSince).TotalSeconds -ge 15) { break }
        } else {
            $lastSize = $size
            $lastFileCount = $fileCount
            $stableSince = $null
        }
    }
}

if (-not $exportProcess.HasExited) {
    Stop-Process -Id $exportProcess.Id -Force
    $exportProcess.WaitForExit()
} else {
    $exportProcess.WaitForExit()
}

if (-not (Test-Path -LiteralPath $outputExe)) {
    $details = if (Test-Path -LiteralPath $exportErr) { Get-Content -LiteralPath $exportErr -Raw } else { "No export log." }
    throw "Velocitex.exe was not created. $details"
}

$exportedFiles = @(Get-ChildItem -LiteralPath $outputRoot -Recurse -File)
foreach ($runtimeFile in @("hostfxr.dll", "coreclr.dll", "Velocitex.dll")) {
    if ($exportedFiles.Name -notcontains $runtimeFile) {
        throw "Windows export is incomplete: $runtimeFile is missing."
    }
}

$smokeOut = Join-Path $outputRoot "smoke-stdout.log"
$smokeErr = Join-Path $outputRoot "smoke-stderr.log"
$smoke = Start-Process -FilePath $outputExe -ArgumentList @("--headless", "--quit-after", "3") `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput $smokeOut -RedirectStandardError $smokeErr
if (-not $smoke.WaitForExit(20000)) {
    Stop-Process -Id $smoke.Id -Force
    throw "Exported Velocitex.exe did not complete its startup smoke test within 20 seconds."
}
$smoke.WaitForExit()
$smoke.Refresh()
if ($null -ne $smoke.ExitCode -and $smoke.ExitCode -ne 0) {
    throw "Exported Velocitex.exe smoke test failed with code $($smoke.ExitCode)."
}
if ((Test-Path -LiteralPath $smokeErr) -and (Get-Item -LiteralPath $smokeErr).Length -gt 0) {
    throw "Exported Velocitex.exe reported runtime warnings or errors: $(Get-Content -LiteralPath $smokeErr -Raw)"
}

foreach ($log in @($exportOut, $exportErr, $smokeOut, $smokeErr)) {
    if (Test-Path -LiteralPath $log) {
        Remove-Item -LiteralPath $log -Force
    }
}

$fileCount = (Get-ChildItem -LiteralPath $outputRoot -Recurse -File).Count
$totalBytes = (Get-ChildItem -LiteralPath $outputRoot -Recurse -File | Measure-Object Length -Sum).Sum
Write-Output "WINDOWS_EXPORT_PASS: $outputExe ($fileCount files, $([Math]::Round($totalBytes / 1MB, 1)) MB)"
