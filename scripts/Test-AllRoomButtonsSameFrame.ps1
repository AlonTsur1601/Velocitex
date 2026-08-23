$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

$allOutput = @()
foreach ($room in 1..30)
{
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/AllRoomButtonSameFrameSmokeTest.tscn" --quit-after 1200 -- "--button-room=$room" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    $joinedRoomOutput = $output -join "`n"
    if ($exitCode -ne 0 -or
        $joinedRoomOutput -notmatch "ROOM_BUTTON_SAME_FRAME_ROOM_PASS: Room $($room.ToString('00'))" -or
        $joinedRoomOutput -match 'ERROR:|ObjectDB instances were leaked|resources still in use')
    {
        throw "Same-frame button smoke test failed for Room $($room.ToString('00'))."
    }
    $allOutput += $output
}

$joinedOutput = $allOutput -join "`n"

$roomCounts = [regex]::Matches($joinedOutput, 'ROOM_BUTTON_SAME_FRAME_COUNT: Room (?<room>\d{2}): (?<count>\d+) buttons\.')
if ($roomCounts.Count -ne 30)
{
    throw "All-room same-frame button smoke test did not report exactly one count for every Room 01-30 (found $($roomCounts.Count))."
}

$reportedRooms = $roomCounts | ForEach-Object { [int]$_.Groups['room'].Value }
if ((Compare-Object -ReferenceObject (1..30) -DifferenceObject $reportedRooms).Count -ne 0)
{
    throw "All-room same-frame button smoke test room-count report is incomplete or duplicated."
}

$reportedTotal = ($roomCounts | ForEach-Object { [int]$_.Groups['count'].Value } | Measure-Object -Sum).Sum
$itemPasses = [regex]::Matches($joinedOutput, 'ROOM_BUTTON_SAME_FRAME_ITEM_PASS:').Count
if ($itemPasses -ne $reportedTotal)
{
    throw "All-room same-frame button smoke test reported $itemPasses button passes but per-room counts total $reportedTotal."
}

$cannonCounts = [regex]::Matches($joinedOutput, 'ROOM_CANNON_HITBOX_COUNT: Room (?<room>\d{2}): player=(?<player>\d+), interference=(?<interference>\d+)\.')
if ($cannonCounts.Count -ne 30)
{
    throw "Cannon hitbox audit did not report exactly one count for every Room 01-30."
}
$playerCannonTotal = ($cannonCounts | ForEach-Object { [int]$_.Groups['player'].Value } | Measure-Object -Sum).Sum
$interferenceCannonTotal = ($cannonCounts | ForEach-Object { [int]$_.Groups['interference'].Value } | Measure-Object -Sum).Sum

"ALL_ROOM_BUTTON_SAME_FRAME_PASS: verified $itemPasses buttons across Rooms 01-30 through real floor contact."
"ALL_CANNON_HITBOX_PASS: verified complete solid hitboxes on $playerCannonTotal player cannons and $interferenceCannonTotal interference cannons across Rooms 01-30."
