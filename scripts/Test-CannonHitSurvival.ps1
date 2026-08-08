$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) { throw "Portable Godot console executable was not found." }

$cases = @(
    @{ Scene = "res://scenes/Room17.tscn"; Argument = "--room17-achievement-negative-smoke"; Marker = "ROOM17_ACHIEVEMENT_NEGATIVE_PASS" },
    @{ Scene = "res://scenes/Room20.tscn"; Argument = "--room20-achievement-negative-smoke"; Marker = "ROOM20_ACHIEVEMENT_NEGATIVE_PASS" },
    @{ Scene = "res://scenes/Room30.tscn"; Argument = "--room30-hit-survival-smoke"; Marker = "ROOM30_HIT_SURVIVAL_PASS" }
)

foreach ($case in $cases) {
    $ErrorActionPreference = "Continue"
    $output = & $godot.FullName --headless --fixed-fps 60 --path $root $case.Scene --quit-after 1200 -- $case.Argument 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $output | Write-Output
    if ($exitCode -ne 0 -or ($output -join "`n") -notmatch $case.Marker) {
        throw "$($case.Scene) did not prove that a surviving cannon hit still permits ordinary completion."
    }
}

Write-Output "CANNON_HIT_SURVIVAL_PASS: Rooms 17, 20 and 30 remain completable after a projectile hit; only their clean-run achievements are denied."
