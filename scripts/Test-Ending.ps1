$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1

if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$output = & $godot.FullName --headless --fixed-fps 60 --path $root --quit-after 600 -- --ending-smoke 2>&1
$exitCode = $LASTEXITCODE
$output | Write-Output
if ($exitCode -ne 0) {
    throw "Ending smoke test exited with code $exitCode"
}

$text = $output -join "`n"
if ($text -notmatch "ENDING_FREEZE_FRAME") {
    throw "Ending smoke test did not reach the mouth freeze frame."
}

if ($text -notmatch "ENDING_SMOKE_PASS") {
    throw "Ending smoke test did not complete the credits/loading/menu sequence."
}

if ($text -notmatch "ENDING_CREDITS_COMPLETE: blackout and large credits completed; coverage=(0\.[7-9]|1\.0)[0-9]*x(0\.[6-9]|1\.0)[0-9]*") {
    throw "Ending smoke test did not prove that the credits occupy most of the screen."
}

Write-Output "ENDING_TEST_PASS: mouth blackout, large fading credits, startup loading and menu return verified."
