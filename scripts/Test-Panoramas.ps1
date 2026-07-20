$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$panoramaRoot = Join-Path $root "assets\panoramas"
$required = @("room01_a.png", "room01_b.png", "room02_a.png", "room02_b.png", "room03_a.png", "room03_b.png", "room04_a.png", "room04_b.png", "room05_a.png", "room05_b.png", "room06_a.png", "room06_b.png", "room07_a.png", "room07_b.png", "room08_a.png", "room08_b.png", "room09_a.png", "room09_b.png", "room09_c.png", "room10_a.png", "room10_b.png", "room11_a.png", "room11_b.png", "room12_a.png", "room12_b.png", "room13_a.png", "room13_b.png", "room14_a.png", "room14_b.png", "room15_a.png", "room15_b.png", "room16_a.png", "room16_b.png", "room17_a.png", "room17_b.png", "room18_a.png", "room18_b.png", "room19_a.png", "room19_b.png", "room20_a.png", "room20_b.png", "room21_a.png", "room21_b.png", "room22_a.png", "room22_b.png", "room23_a.png", "room23_b.png", "room24_a.png", "room24_b.png", "room25_a.png", "room25_b.png", "room26_a.png", "room26_b.png", "room27_a.png", "room27_b.png", "room28_a.png", "room28_b.png")
$sources = @(
    "scenes\MovementTestRoom.tscn",
    "scenes\Room02.tscn",
    "scenes\Room03.tscn",
    "scenes\Room04.tscn",
    "scenes\Room05.tscn",
    "scenes\Room06.tscn",
    "scenes\Room07.tscn",
    "scenes\Room08.tscn",
    "scenes\Room09.tscn",
    "scenes\Room10.tscn",
    "scenes\Room11.tscn",
    "scenes\Room12.tscn",
    "scenes\Room13.tscn",
    "scenes\Room14.tscn",
    "scenes\Room15.tscn",
    "scenes\Room16.tscn",
    "scenes\Room17.tscn",
    "scenes\Room18.tscn",
    "scenes\Room19.tscn",
    "scenes\Room20.tscn",
    "scenes\Room21.tscn",
    "scenes\Room22.tscn",
    "scenes\Room23.tscn",
    "scenes\Room24.tscn",
    "scenes\Room25.tscn",
    "scenes\Room26.tscn",
    "scenes\Room27.tscn",
    "scenes\Room28.tscn",
    "src\Gameplay\TestRoom\MovementTestRoom.cs",
    "src\Gameplay\Rooms\Room02Runtime.cs",
    "src\Gameplay\Rooms\Room03Runtime.cs",
    "src\Gameplay\Rooms\Room04Runtime.cs",
    "src\Gameplay\Rooms\Room05Runtime.cs",
    "src\Gameplay\Rooms\Room06Runtime.cs",
    "src\Gameplay\Rooms\Room07Runtime.cs",
    "src\Gameplay\Rooms\Room08Runtime.cs",
    "src\Gameplay\Rooms\Room09Runtime.cs",
    "src\Gameplay\Rooms\Room10Runtime.cs",
    "src\Gameplay\Rooms\Room11Runtime.cs",
    "src\Gameplay\Rooms\Room12Runtime.cs",
    "src\Gameplay\Rooms\Room13Runtime.cs",
    "src\Gameplay\Rooms\Room14Runtime.cs",
    "src\Gameplay\Rooms\Room15Runtime.cs",
    "src\Gameplay\Rooms\Room16Runtime.cs",
    "src\Gameplay\Interaction\PlayerCannon3D.cs",
    "src\Gameplay\Rooms\Room17Runtime.cs",
    "src\Gameplay\Physics\InterferenceCannon3D.cs",
    "src\Gameplay\Rooms\Room18Runtime.cs",
    "src\Gameplay\Physics\MovingPlatform3D.cs",
    "src\Gameplay\Rooms\Room19Runtime.cs",
    "src\Gameplay\Physics\MomentumPiston3D.cs",
    "src\Gameplay\Rooms\Room20Runtime.cs",
    "src\Gameplay\Rooms\Room21Runtime.cs",
    "src\Gameplay\Rooms\Room22Runtime.cs",
    "src\Gameplay\Rooms\Room23Runtime.cs",
    "src\Gameplay\Physics\MomentumBank3D.cs",
    "src\Gameplay\Rooms\Room24Runtime.cs",
    "src\Gameplay\Physics\BrittleBarrier3D.cs",
    "src\Gameplay\Rooms\Room25Runtime.cs",
    "src\Gameplay\Rooms\Room26Runtime.cs",
    "src\Gameplay\Rooms\CoreRoomsRuntime.cs",
    "src\Gameplay\Physics\MomentumRail3D.cs",
    "src\Gameplay\Physics\ForceVolume3D.cs",
    "src\Core\Physics\ForceVolumeProfile.cs",
    "resources\force_volumes\low_gravity.tres",
    "resources\force_volumes\strong_gravity.tres",
    "resources\force_volumes\crosswind.tres",
    "src\Gameplay\Physics\ProfiledSurfaceBody.cs",
    "src\Gameplay\Player\PlayerBall.cs",
    "src\Core\Physics\SurfaceProfile.cs",
    "src\Gameplay\Interaction\MechanicalLever.cs",
    "src\Gameplay\Rooms\RoomGeometry.cs",
    "src\Gameplay\Visual\SurfaceMeshFactory.cs",
    "src\Gameplay\Visual\SurfaceDetail.cs",
    "resources\shaders\contained_overlay.gdshader",
    "resources\shaders\sticky_caramel.gdshader",
    "resources\materials\sticky_caramel.tres",
    "resources\surfaces\sticky.tres",
    "resources\shaders\accelerator_belt.gdshader",
    "resources\materials\accelerator_belt.tres",
    "resources\surfaces\accelerator.tres",
    "resources\shaders\super_elastic_membrane.gdshader",
    "resources\materials\super_elastic_membrane.tres",
    "resources\surfaces\super_elastic.tres",
    "src\UI\Visual\PanoramaCaptureController.cs",
    "assets\textures\brushed_metal.png",
    "assets\textures\diamond_plate.png",
    "assets\textures\industrial_concrete.png",
    "assets\textures\caramel_plates.svg",
    "assets\textures\copper_rivets.svg",
    "assets\textures\hazard_grate.svg",
    "assets\textures\frictionless_glass.svg",
    "assets\textures\absorbing_foam.svg",
    "assets\textures\one_way_teeth.svg",
    "assets\textures\brittle_sugar_glass.svg",
    "assets\textures\gelatin_cells.svg",
    "assets\textures\rubber_chevrons.svg",
    "assets\textures\overlays\cracks.svg",
    "assets\textures\overlays\drips.svg",
    "assets\textures\overlays\edge_scuffs.svg",
    "assets\textures\overlays\grime.svg",
    "assets\textures\overlays\grime_02.svg",
    "assets\textures\overlays\grime_03.svg",
    "assets\textures\overlays\grime_04.svg",
    "assets\textures\overlays\grime_05.svg",
    "assets\textures\overlays\micro_grain.svg",
    "assets\textures\overlays\micro_concrete.png",
    "assets\textures\overlays\micro_metal_wear.png",
    "assets\textures\overlays\oil_rings.svg",
    "assets\textures\overlays\patina.svg",
    "assets\textures\overlays\scratches.svg",
    "assets\textures\overlays\sugar_dust.svg",
    "scripts\Generate-MicroTextures.ps1"
)
$latestSource = $sources |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $root $_) } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

Add-Type -AssemblyName System.Drawing

foreach ($file in $required) {
    $path = Join-Path $panoramaRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing panorama: $file"
    }
    $panorama = Get-Item -LiteralPath $path
    if ($panorama.LastWriteTimeUtc -lt $latestSource.LastWriteTimeUtc) {
        throw "$file is stale. Run scripts/Update-Panoramas.ps1 after room or texture changes."
    }
    $image = [System.Drawing.Image]::FromFile($path)
    try {
        if ($image.Width -lt 2560 -or $image.Height -lt 1440) {
            throw "$file is only $($image.Width)x$($image.Height); expected at least 2560x1440."
        }
    } finally {
        $image.Dispose()
    }
}

Write-Output "PANORAMA_SMOKE_PASS: all panoramas are current and at least 2560x1440."
