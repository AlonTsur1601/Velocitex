$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$passedRooms = 0

for ($room = 1; $room -le 26; $room++) {
    $scriptName = "Test-Room{0:D2}Solution.ps1" -f $room
    $scriptPath = Join-Path $PSScriptRoot $scriptName
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "Missing solution test for Room $room at $scriptPath"
    }

    & $scriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "Room $room solution regression failed with exit code $LASTEXITCODE"
    }
    $passedRooms++
}

& (Join-Path $PSScriptRoot "Test-CoreRoomsSolutions.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Core Rooms 27-28 solution regression failed with exit code $LASTEXITCODE"
}
$passedRooms += 2

Write-Output "ALL_ROOM_SOLUTIONS_PASS: $passedRooms rooms completed 10 deterministic SolutionTrace runs each (280 completions)."
