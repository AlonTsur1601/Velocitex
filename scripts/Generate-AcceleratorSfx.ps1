param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root "assets\audio\sfx"
$outputPath = Join-Path $outputDirectory "surface_accelerator_contact.wav"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$sampleRate = 44100
$durationSeconds = 0.62
$frameCount = [int]($sampleRate * $durationSeconds)
$channelCount = 2
$samples = [int16[]]::new($frameCount * $channelCount)
$random = [System.Random]::new(84017)
$leftNoise = 0.0
$rightNoise = 0.0
$phase = 0.0

for ($index = 0; $index -lt $frameCount; $index++) {
    $time = $index / [double]$sampleRate
    $normalized = $time / $durationSeconds
    $attack = [Math]::Min($time / 0.025, 1.0)
    $release = [Math]::Pow([Math]::Max(1.0 - $normalized, 0.0), 0.72)
    $envelope = $attack * $release

    $frequency = 240.0 + (360.0 * [Math]::Pow($normalized, 0.82))
    $phase += 2.0 * [Math]::PI * $frequency / $sampleRate
    $motor = ([Math]::Sin($phase) * 0.46) + ([Math]::Sin($phase * 2.03) * 0.16)

    $whiteLeft = ($random.NextDouble() * 2.0) - 1.0
    $whiteRight = ($random.NextDouble() * 2.0) - 1.0
    $leftNoise += ($whiteLeft - $leftNoise) * (0.04 + ($normalized * 0.1))
    $rightNoise += ($whiteRight - $rightNoise) * (0.04 + ($normalized * 0.1))
    $airLeft = ($leftNoise * 0.62) + ($whiteLeft * 0.08)
    $airRight = ($rightNoise * 0.62) + ($whiteRight * 0.08)

    $pulse = [Math]::Pow([Math]::Max([Math]::Sin(2.0 * [Math]::PI * 7.5 * $time), 0.0), 7.0) * 0.12
    $leftValue = $envelope * (($motor * 0.55) + ($airLeft * 0.42) + ($pulse * 0.8))
    $rightValue = $envelope * (($motor * 0.55) + ($airRight * 0.42) + ($pulse * 1.0))
    $leftClamped = [Math]::Max(-1.0, [Math]::Min(1.0, [Math]::Tanh($leftValue * 1.42) * 0.72))
    $rightClamped = [Math]::Max(-1.0, [Math]::Min(1.0, [Math]::Tanh($rightValue * 1.42) * 0.72))
    $samples[$index * 2] = [int16]([Math]::Round($leftClamped * 32767.0))
    $samples[($index * 2) + 1] = [int16]([Math]::Round($rightClamped * 32767.0))
}

$stream = [System.IO.File]::Open($outputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $dataLength = $frameCount * $channelCount * 2
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF"))
    $writer.Write([int](36 + $dataLength))
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("WAVE"))
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("fmt "))
    $writer.Write([int]16)
    $writer.Write([int16]1)
    $writer.Write([int16]$channelCount)
    $writer.Write([int]$sampleRate)
    $writer.Write([int]($sampleRate * $channelCount * 2))
    $writer.Write([int16]($channelCount * 2))
    $writer.Write([int16]16)
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("data"))
    $writer.Write([int]$dataLength)
    foreach ($sample in $samples) {
        $writer.Write($sample)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Output "ACCELERATOR_SFX_GENERATION_PASS: $outputPath ($frameCount stereo frames at ${sampleRate}Hz)."
