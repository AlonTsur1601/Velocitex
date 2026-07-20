$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root "assets\audio\music"
$outputPath = Join-Path $outputDirectory "menu_motion.wav"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$rate = 44100
$duration = 32.0
$frameCount = [int]($rate * $duration)
$samples = [int16[]]::new($frameCount * 2)
$roots = @(110.0, 130.813, 146.832, 164.814, 146.832, 130.813, 123.471, 130.813)
$arpeggio = @(1.0, 1.5, 2.0, 1.25, 1.5, 2.0, 1.25, 1.5)

for ($frame = 0; $frame -lt $frameCount; $frame++) {
    $time = $frame / [double]$rate
    $section = [int]($time / 4.0) % $roots.Count
    $sectionTime = $time % 4.0
    $attack = [Math]::Min(1.0, $sectionTime / 0.42)
    $release = [Math]::Min(1.0, (4.0 - $sectionTime) / 0.62)
    $sectionEnvelope = ($attack * $attack * (3.0 - (2.0 * $attack))) * ($release * $release * (3.0 - (2.0 * $release)))
    $rootFrequency = $roots[$section]

    $padLeft = (
        ([Math]::Sin(2.0 * [Math]::PI * $rootFrequency * $time) * 0.12) +
        ([Math]::Sin(2.0 * [Math]::PI * ($rootFrequency * 1.25) * $time + 0.42) * 0.07) +
        ([Math]::Sin(2.0 * [Math]::PI * ($rootFrequency * 1.5) * $time + 0.9) * 0.055)
    ) * $sectionEnvelope
    $padRight = (
        ([Math]::Sin(2.0 * [Math]::PI * ($rootFrequency * 1.0017) * $time + 0.12) * 0.12) +
        ([Math]::Sin(2.0 * [Math]::PI * ($rootFrequency * 1.252) * $time + 0.72) * 0.07) +
        ([Math]::Sin(2.0 * [Math]::PI * ($rootFrequency * 1.497) * $time + 1.15) * 0.055)
    ) * $sectionEnvelope

    $step = [int]($time / 0.5) % $arpeggio.Count
    $stepTime = $time % 0.5
    $pluckEnvelope = [Math]::Exp(-8.5 * $stepTime) * [Math]::Min(1.0, $stepTime / 0.012)
    $pluckFrequency = $rootFrequency * $arpeggio[$step] * 2.0
    $pluck = ([Math]::Sin(2.0 * [Math]::PI * $pluckFrequency * $time) +
        ([Math]::Sin(2.0 * [Math]::PI * ($pluckFrequency * 2.01) * $time) * 0.24)) * $pluckEnvelope * 0.055

    $pulsePhase = $time % 2.0
    $pulseEnvelope = if ($pulsePhase -lt 0.22) { [Math]::Sin([Math]::PI * $pulsePhase / 0.22) } else { 0.0 }
    $mechanicalPulse = [Math]::Sin(2.0 * [Math]::PI * 55.0 * $time) * $pulseEnvelope * 0.038
    $air = [Math]::Sin(2.0 * [Math]::PI * 0.125 * $time) * [Math]::Sin(2.0 * [Math]::PI * 220.0 * $time) * 0.008

    $pan = if (($step % 2) -eq 0) { 0.72 } else { -0.72 }
    $left = [Math]::Tanh(($padLeft + ($pluck * (1.0 - ($pan * 0.24))) + $mechanicalPulse + $air) * 1.35) * 0.72
    $right = [Math]::Tanh(($padRight + ($pluck * (1.0 + ($pan * 0.24))) + $mechanicalPulse - $air) * 1.35) * 0.72
    $samples[$frame * 2] = [int16]([Math]::Round($left * 32767.0))
    $samples[($frame * 2) + 1] = [int16]([Math]::Round($right * 32767.0))
}

$stream = [IO.File]::Create($outputPath)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $dataLength = $samples.Length * 2
    $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"))
    $writer.Write([int](36 + $dataLength))
    $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"))
    $writer.Write([Text.Encoding]::ASCII.GetBytes("fmt "))
    $writer.Write([int]16)
    $writer.Write([int16]1)
    $writer.Write([int16]2)
    $writer.Write([int]$rate)
    $writer.Write([int]($rate * 4))
    $writer.Write([int16]4)
    $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes("data"))
    $writer.Write([int]$dataLength)
    foreach ($sample in $samples) { $writer.Write($sample) }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Output "MENU_MUSIC_GENERATION_PASS: $outputPath (32-second original stereo loop)."
