param(
    [string]$GodotPath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $GodotPath = (Get-ChildItem -Path $root -Recurse -Filter "Godot_v*_mono_win64_console.exe" -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}
if ([string]::IsNullOrWhiteSpace($GodotPath) -or -not (Test-Path -LiteralPath $GodotPath)) {
    throw "Godot .NET console executable was not found."
}

$ErrorActionPreference = "Continue"
$output = & $GodotPath --headless --fixed-fps 60 --path $root "res://scenes/Room11.tscn" --quit-after 40000 -- --room11-variation-smoke 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$output | ForEach-Object { Write-Host $_ }
if ($exitCode -ne 0 -or ($output -join "`n") -notmatch "ROOM11_VARIATION_PASS") {
    throw "Room 11 varied-input smoke failed."
}
