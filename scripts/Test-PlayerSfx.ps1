$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem -LiteralPath (Join-Path $root ".tools\Godot") -Recurse -Filter "Godot*_mono_win64_console.exe" | Select-Object -First 1
if (-not $godot) {
    throw "Portable Godot console executable was not found."
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:NUGET_PACKAGES = Join-Path $root ".packages\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$rollFiles = @(
    "player_roll_metal_loop.wav",
    "player_roll_glass_loop.wav",
    "player_roll_soft_loop.wav",
    "player_roll_rubber_loop.wav"
)
foreach ($rollFile in $rollFiles) {
    $rollPath = Join-Path $root "assets\audio\sfx\$rollFile"
    $bytes = [IO.File]::ReadAllBytes($rollPath)
    if ($bytes.Length -le 44) {
        throw "Rolling source audio is missing PCM data: $rollFile"
    }

    $sumSquares = 0.0
    $sampleCount = 0
    for ($offset = 44; $offset + 1 -lt $bytes.Length; $offset += 2) {
        $sample = [BitConverter]::ToInt16($bytes, $offset) / 32768.0
        $sumSquares += $sample * $sample
        $sampleCount++
    }
    $rms = [Math]::Sqrt($sumSquares / [Math]::Max($sampleCount, 1))
    if ($rms -lt 0.015) {
        throw "Rolling source audio is silent or effectively inaudible: $rollFile (RMS=$($rms.ToString('F4')))."
    }

    $crossings = 0
    $previous = [BitConverter]::ToInt16($bytes, 44)
    for ($offset = 48; $offset + 1 -lt $bytes.Length; $offset += 4) {
        $current = [BitConverter]::ToInt16($bytes, $offset)
        if (($previous -lt 0 -and $current -ge 0) -or ($previous -ge 0 -and $current -lt 0)) {
            $crossings++
        }
        $previous = $current
    }
    $durationSeconds = ($bytes.Length - 44) / (44100.0 * 4.0)
    if (($crossings / $durationSeconds) -lt 150.0) {
        throw "Rolling source lacks laptop-audible texture: $rollFile ($([Math]::Round($crossings / $durationSeconds)) zero crossings/s)."
    }
}
Write-Output "ROLL_SOURCE_AUDIO_PASS: all four rolling loops contain audible PCM data and laptop-audible texture."

$impactFiles = @(
    "player_impact_metal_tap.wav",
    "player_impact_metal_light.wav",
    "player_impact_metal_medium.wav",
    "player_impact_metal_heavy.wav",
    "player_impact_metal_crash.wav"
)
foreach ($impactFile in $impactFiles) {
    $impactPath = Join-Path $root "assets\audio\sfx\$impactFile"
    $bytes = [IO.File]::ReadAllBytes($impactPath)
    $sumSquares = 0.0
    $sampleCount = 0
    for ($offset = 44; $offset + 1 -lt $bytes.Length; $offset += 2) {
        $sample = [BitConverter]::ToInt16($bytes, $offset) / 32768.0
        $sumSquares += $sample * $sample
        $sampleCount++
    }
    $rms = [Math]::Sqrt($sumSquares / [Math]::Max($sampleCount, 1))
    if ($sampleCount -eq 0 -or $rms -lt 0.01) {
        throw "Metal impact source is missing or silent: $impactFile."
    }
}
Write-Output "METAL_IMPACT_SOURCE_PASS: five non-silent metal impact tiers are present."

& $godot.FullName --headless --path $root "res://scenes/PlayerSfxSmokeTest.tscn"
if ($LASTEXITCODE -ne 0) {
    throw "Player SFX smoke test exited with code $LASTEXITCODE."
}

$gameplayMusic = Get-ChildItem -LiteralPath (Join-Path $root "src\Gameplay") -Recurse -Filter "*.cs" |
    Select-String -Pattern 'Bus\s*=\s*"Music"|Bus\s*=\s*&"Music"'
if ($gameplayMusic) {
    throw "Gameplay code routes audio to the Music bus: $($gameplayMusic.Path):$($gameplayMusic.LineNumber)"
}

Write-Output "GAMEPLAY_MUSIC_SCOPE_PASS: gameplay code contains no Music-bus playback."
