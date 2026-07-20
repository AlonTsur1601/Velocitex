$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$rooms = if ($args.Count -gt 0) { @($args | ForEach-Object { [int]$_ }) } else { @(1..28) }
foreach ($room in $rooms) {
    foreach ($mode in @("direct-goal-bypass", "forward-bypass", "steering-bypass")) {
        $quitAfter = if ($mode -eq "steering-bypass") { 20000 } else { 3200 }
        $ErrorActionPreference = "Continue"
        $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/RoomBypassSmokeTest.tscn" --quit-after $quitAfter -- "--$mode-room=$room" 2>&1
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = "Stop"
        $output | Write-Output
        if ($exitCode -ne 0) {
            throw "Room $room $mode smoke failed with exit code $exitCode."
        }
        $requiredMarker = if ($mode -eq "direct-goal-bypass") { "DIRECT_GOAL_BYPASS_PASS" } elseif ($mode -eq "forward-bypass") { "FORWARD_BYPASS_PASS" } else { "STEERING_BYPASS_PASS" }
        if (($output -join "`n") -notmatch $requiredMarker) {
            throw "Room $room $mode smoke exited without its pass marker."
        }
    }
}

Write-Output "ROOM_BYPASS_SUITE_PASS: direct-goal, forward-only and six sustained steering routes were rejected in $($rooms.Count) rooms."
