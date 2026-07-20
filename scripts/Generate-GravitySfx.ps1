param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root "assets\audio\sfx"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

function Write-StereoWave {
    param(
        [string]$Path,
        [double]$Duration,
        [bool]$Heavy
    )

    $sampleRate = 44100
    $frameCount = [int]($sampleRate * $Duration)
    $samples = [int16[]]::new($frameCount * 2)
    $random = [System.Random]::new($(if ($Heavy) { 12012 } else { 11011 }))

    for ($index = 0; $index -lt $frameCount; $index++) {
        $time = $index / [double]$sampleRate
        $normalized = $time / $Duration
        $attack = [Math]::Min($time / $(if ($Heavy) { 0.004 } else { 0.035 }), 1.0)
        $release = [Math]::Pow([Math]::Max(1.0 - $normalized, 0.0), $(if ($Heavy) { 1.35 } else { 0.8 }))
        $envelope = $attack * $release
        $frequency = if ($Heavy) { 190.0 - (28.0 * $normalized) } else { 260.0 + (330.0 * $normalized) }
        $tone = [Math]::Sin(2.0 * [Math]::PI * $frequency * $time)
        $overtone = [Math]::Sin(2.0 * [Math]::PI * ($frequency * 2.08) * $time) * 0.3
        $metal = [Math]::Sin(2.0 * [Math]::PI * $(if ($Heavy) { 760.0 } else { 1120.0 }) * $time) * [Math]::Exp(-8.5 * $time) * 0.2
        $clampPulse = if ($Heavy) { [Math]::Exp(-[Math]::Abs($time - 0.09) * 95.0) + (0.72 * [Math]::Exp(-[Math]::Abs($time - 0.19) * 105.0)) } else { 0.0 }
        $noiseLeft = (($random.NextDouble() * 2.0) - 1.0) * $(if ($Heavy) { 0.13 } else { 0.06 }) * $envelope
        $noiseRight = (($random.NextDouble() * 2.0) - 1.0) * $(if ($Heavy) { 0.13 } else { 0.06 }) * $envelope
        $body = $envelope * (($tone * $(if ($Heavy) { 0.36 } else { 0.48 })) + $overtone + $metal) + ($clampPulse * 0.12)
        $left = [Math]::Tanh(($body + $noiseLeft) * 1.3) * 0.72
        $right = [Math]::Tanh((($body * 0.97) + $noiseRight) * 1.3) * 0.72
        $samples[$index * 2] = [int16]([Math]::Round($left * 32767.0))
        $samples[($index * 2) + 1] = [int16]([Math]::Round($right * 32767.0))
    }

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $dataLength = $frameCount * 4
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF"))
        $writer.Write([int](36 + $dataLength))
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("WAVE"))
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("fmt "))
        $writer.Write([int]16)
        $writer.Write([int16]1)
        $writer.Write([int16]2)
        $writer.Write([int]$sampleRate)
        $writer.Write([int]($sampleRate * 4))
        $writer.Write([int16]4)
        $writer.Write([int16]16)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes("data"))
        $writer.Write([int]$dataLength)
        foreach ($sample in $samples) { $writer.Write($sample) }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }

    return $frameCount
}

$lowPath = Join-Path $outputDirectory "force_low_gravity_enter.wav"
$strongPath = Join-Path $outputDirectory "force_strong_gravity_enter.wav"
$lowFrames = Write-StereoWave -Path $lowPath -Duration 0.72 -Heavy $false
$strongFrames = Write-StereoWave -Path $strongPath -Duration 0.48 -Heavy $true
Write-Output "GRAVITY_SFX_GENERATION_PASS: low=$lowFrames frames, strong=$strongFrames frames at 44100Hz stereo."
