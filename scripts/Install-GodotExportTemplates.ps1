$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $root ".tools\Godot"
$godot = Get-ChildItem -LiteralPath $toolsRoot -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$expectedLength = 1200753503
$expectedHash = "4C02A0B99AD9C5BC243C2E79468628DB3DF89350D9DB8FC995988F69D126E069"
$downloadUrl = "https://github.com/godotengine/godot-builds/releases/download/4.7-stable/Godot_v4.7-stable_mono_export_templates.tpz"
$cacheRoot = Join-Path $root ".tools\cache"
$archivePath = Join-Path $cacheRoot "Godot_v4.7-stable_mono_export_templates.tpz"
$editorData = Join-Path $godot.Directory.FullName "editor_data"
$targetRoot = Join-Path $editorData "export_templates\4.7.stable.mono"

$resolvedRoot = [IO.Path]::GetFullPath($root)
foreach ($candidate in @($cacheRoot, $archivePath, $targetRoot)) {
    if (-not [IO.Path]::GetFullPath($candidate).StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Template path resolved outside the project: $candidate"
    }
}

New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $archivePath) -or (Get-Item -LiteralPath $archivePath).Length -ne $expectedLength) {
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    & curl.exe --fail --location --retry 4 --retry-delay 3 --output $archivePath $downloadUrl
    if ($LASTEXITCODE -ne 0) {
        throw "Godot export-template download failed with code $LASTEXITCODE."
    }
}

$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash) {
    throw "Godot export-template checksum mismatch."
}

New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $selected = $archive.Entries | Where-Object {
        $_.FullName -match '^templates/windows_(debug|release)_x86_64(_console)?\.exe$' -or
        $_.FullName -eq 'templates/version.txt'
    }
    foreach ($entry in $selected) {
        $destination = Join-Path $targetRoot ([IO.Path]::GetFileName($entry.FullName))
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
    }
}
finally {
    $archive.Dispose()
}

$required = @("windows_debug_x86_64.exe", "windows_release_x86_64.exe")
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $targetRoot $name))) {
        throw "The export archive did not contain $name."
    }
}

Write-Output "EXPORT_TEMPLATES_READY=$targetRoot"

