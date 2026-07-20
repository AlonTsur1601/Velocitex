$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$rooms = if ($args.Count -gt 0) { @($args | ForEach-Object { [int]$_ }) } else { @(1..28) }
$results = [System.Collections.Generic.List[string]]::new()
foreach ($room in $rooms) {
    if ($room -lt 1 -or $room -gt 28) {
        throw "Room number $room is outside the supported 01-28 range."
    }

    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/RoomShellContainmentSmokeTest.tscn" --quit-after 1800 -- "--containment-room=$room" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $text = $output -join "`n"
    $marker = "ROOM_SHELL_CONTAINMENT_ROOM_PASS: Room $($room.ToString('00'))"
    if ($exitCode -ne 0 -or $text -notmatch [regex]::Escape($marker)) {
        $output | Write-Output
        throw "Room shell containment smoke failed for Room $($room.ToString('00')) with code $exitCode."
    }

    $line = $output | Where-Object { $_ -match "ROOM_SHELL_CONTAINMENT_ROOM_PASS" } | Select-Object -Last 1
    $results.Add([string]$line)
}

$results | Write-Output
Write-Output "ROOM_SHELL_CONTAINMENT_SUITE_PASS: all relevant meshes and collision shapes stay inside the shell in $($rooms.Count) room(s)."
