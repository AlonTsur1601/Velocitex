$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }
$output = & $godot.FullName --headless --path $root res://scenes/ChallengeModesSmokeTest.tscn --quit-after 300 2>&1
$output | Write-Output
if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch "CHALLENGE_MODES_SMOKE_PASS") { throw "Challenge modes smoke failed." }
