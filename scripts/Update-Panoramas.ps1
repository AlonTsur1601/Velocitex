$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" |
    Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$captures = @(
    @{ Scene = "res://scenes/MovementTestRoom.tscn"; Id = "room01_a" },
    @{ Scene = "res://scenes/MovementTestRoom.tscn"; Id = "room01_b" },
    @{ Scene = "res://scenes/Room02.tscn"; Id = "room02_a" },
    @{ Scene = "res://scenes/Room02.tscn"; Id = "room02_b" },
    @{ Scene = "res://scenes/Room03.tscn"; Id = "room03_a" },
    @{ Scene = "res://scenes/Room03.tscn"; Id = "room03_b" },
    @{ Scene = "res://scenes/Room04.tscn"; Id = "room04_a" },
    @{ Scene = "res://scenes/Room04.tscn"; Id = "room04_b" },
    @{ Scene = "res://scenes/Room05.tscn"; Id = "room05_a" },
    @{ Scene = "res://scenes/Room05.tscn"; Id = "room05_b" },
    @{ Scene = "res://scenes/Room06.tscn"; Id = "room06_a" },
    @{ Scene = "res://scenes/Room06.tscn"; Id = "room06_b" },
    @{ Scene = "res://scenes/Room07.tscn"; Id = "room07_a" },
    @{ Scene = "res://scenes/Room07.tscn"; Id = "room07_b" },
    @{ Scene = "res://scenes/Room08.tscn"; Id = "room08_a" },
    @{ Scene = "res://scenes/Room08.tscn"; Id = "room08_b" },
    @{ Scene = "res://scenes/Room09.tscn"; Id = "room09_a" },
    @{ Scene = "res://scenes/Room09.tscn"; Id = "room09_b" },
    @{ Scene = "res://scenes/Room09.tscn"; Id = "room09_c" },
    @{ Scene = "res://scenes/Room10.tscn"; Id = "room10_a" },
    @{ Scene = "res://scenes/Room10.tscn"; Id = "room10_b" },
    @{ Scene = "res://scenes/Room11.tscn"; Id = "room11_a" },
    @{ Scene = "res://scenes/Room11.tscn"; Id = "room11_b" },
    @{ Scene = "res://scenes/Room12.tscn"; Id = "room12_a" },
    @{ Scene = "res://scenes/Room12.tscn"; Id = "room12_b" },
    @{ Scene = "res://scenes/Room13.tscn"; Id = "room13_a" },
    @{ Scene = "res://scenes/Room13.tscn"; Id = "room13_b" },
    @{ Scene = "res://scenes/Room14.tscn"; Id = "room14_a" },
    @{ Scene = "res://scenes/Room14.tscn"; Id = "room14_b" },
    @{ Scene = "res://scenes/Room15.tscn"; Id = "room15_a" },
    @{ Scene = "res://scenes/Room15.tscn"; Id = "room15_b" },
    @{ Scene = "res://scenes/Room16.tscn"; Id = "room16_a" },
    @{ Scene = "res://scenes/Room16.tscn"; Id = "room16_b" },
    @{ Scene = "res://scenes/Room17.tscn"; Id = "room17_a" },
    @{ Scene = "res://scenes/Room17.tscn"; Id = "room17_b" },
    @{ Scene = "res://scenes/Room18.tscn"; Id = "room18_a" },
    @{ Scene = "res://scenes/Room18.tscn"; Id = "room18_b" },
    @{ Scene = "res://scenes/Room19.tscn"; Id = "room19_a" },
    @{ Scene = "res://scenes/Room19.tscn"; Id = "room19_b" },
    @{ Scene = "res://scenes/Room20.tscn"; Id = "room20_a" },
    @{ Scene = "res://scenes/Room20.tscn"; Id = "room20_b" },
    @{ Scene = "res://scenes/Room21.tscn"; Id = "room21_a" },
    @{ Scene = "res://scenes/Room21.tscn"; Id = "room21_b" },
    @{ Scene = "res://scenes/Room22.tscn"; Id = "room22_a" },
    @{ Scene = "res://scenes/Room22.tscn"; Id = "room22_b" },
    @{ Scene = "res://scenes/Room23.tscn"; Id = "room23_a" },
    @{ Scene = "res://scenes/Room23.tscn"; Id = "room23_b" },
    @{ Scene = "res://scenes/Room24.tscn"; Id = "room24_a" },
    @{ Scene = "res://scenes/Room24.tscn"; Id = "room24_b" },
    @{ Scene = "res://scenes/Room25.tscn"; Id = "room25_a" },
    @{ Scene = "res://scenes/Room25.tscn"; Id = "room25_b" },
    @{ Scene = "res://scenes/Room26.tscn"; Id = "room26_a" },
    @{ Scene = "res://scenes/Room26.tscn"; Id = "room26_b" },
    @{ Scene = "res://scenes/Room27.tscn"; Id = "room27_a" },
    @{ Scene = "res://scenes/Room27.tscn"; Id = "room27_b" },
    @{ Scene = "res://scenes/Room28.tscn"; Id = "room28_a" },
    @{ Scene = "res://scenes/Room28.tscn"; Id = "room28_b" }
)

foreach ($capture in $captures) {
    $captured = $false
    for ($attempt = 1; $attempt -le 3 -and -not $captured; $attempt++) {
        & $godot.FullName --path $root --resolution 2560x720 $capture.Scene -- "--panorama-capture=$($capture.Id)"
        if ($LASTEXITCODE -eq 0) {
            $captured = $true
        } elseif ($attempt -lt 3) {
            Start-Sleep -Milliseconds 350
        }
    }
    if (-not $captured) {
        throw "Panorama $($capture.Id) failed after three attempts."
    }
}

Write-Output "PANORAMA_UPDATE_PASS: generated $($captures.Count) current room panoramas."
