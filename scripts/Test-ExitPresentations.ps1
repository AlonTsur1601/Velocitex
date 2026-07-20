$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

for ($room = 1; $room -le 30; $room++) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/ExitPresentationSmokeTest.tscn" --quit-after 1200 -- "--exit-presentation-room=$room" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0) {
        throw "Exit presentation smoke for Room $room exited with code $exitCode."
    }
    if (($output -join "`n") -notmatch "EXIT_PRESENTATION_ROOM_PASS: Room $($room.ToString('00'))") {
        throw "Exit presentation smoke for Room $room did not report success."
    }
}

Write-Output "EXIT_PRESENTATION_PASS: Rooms 01-28 use wall-proud threshold-free frames, level lever bases, sealed corridors and requirement-locked doors."
