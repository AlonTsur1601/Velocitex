$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$results = [System.Collections.Generic.List[string]]::new()
$failures = [System.Collections.Generic.List[string]]::new()
for ($room = 1; $room -le 28; $room++) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root "res://scenes/SurfaceConnectionSmokeTest.tscn" --quit-after 900 -- "--surface-room=$room" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $text = $output -join "`n"
    if ($exitCode -ne 0) {
        $failures.Add("Room $($room.ToString('00')) exited with code $exitCode.`n$text")
        continue
    }
    if ($text -notmatch "SURFACE_CONNECTION_ROOM_PASS: Room $($room.ToString('00'))") {
        $failures.Add("Room $($room.ToString('00')) did not report a pass.`n$text")
        continue
    }
    $line = $output | Where-Object { $_ -match "SURFACE_CONNECTION_ROOM_PASS" } | Select-Object -Last 1
    $results.Add([string]$line)
}

$failures | Write-Output
if ($failures.Count -gt 0) {
    throw "Surface connection smoke found problems in $($failures.Count) room(s)."
}
$results | Write-Output
Write-Output "SURFACE_CONNECTION_PASS: all adjoining platform and slope edges in Rooms 01-28 are flush and connected."
