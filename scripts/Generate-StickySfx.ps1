param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root "assets\audio\sfx"
$outputPath = Join-Path $outputDirectory "surface_sticky_contact.wav"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$sampleRate = 44100
$durationSeconds = 0.42
$frameCount = [int]($sampleRate * $durationSeconds)
$channelCount = 2
$samples = [int16[]]::new($frameCount * $channelCount)
$random = [System.Random]::new(73129)
$filteredNoiseLeft = 0.0
$filteredNoiseRight = 0.0

for ($index = 0; $index -lt $frameCount; $index++) {
    $time = $index / [double]$sampleRate
    $normalized = $time / $durationSeconds
    $attack = [Math]::Min($time / 0.004, 1.0)
    $decay = [Math]::Pow([Math]::Max(1.0 - $normalized, 0.0), 1.45)
    $envelope = $attack * $decay

    $whiteNoiseLeft = ($random.NextDouble() * 2.0) - 1.0
    $whiteNoiseRight = ($random.NextDouble() * 2.0) - 1.0
    $filteredNoiseLeft += ($whiteNoiseLeft - $filteredNoiseLeft) * 0.075
    $filteredNoiseRight += ($whiteNoiseRight - $filteredNoiseRight) * 0.075
    $textureLeft = ($whiteNoiseLeft - $filteredNoiseLeft) * 0.58
    $textureRight = ($whiteNoiseRight - $filteredNoiseRight) * 0.58

    $snapAEnvelope = [Math]::Exp(-[Math]::Abs($time - 0.105) * 112.0)
    $snapBEnvelope = [Math]::Exp(-[Math]::Abs($time - 0.238) * 138.0)
    $snapA = [Math]::Sin(2.0 * [Math]::PI * 520.0 * $time) * $snapAEnvelope * 0.22
    $snapB = [Math]::Sin(2.0 * [Math]::PI * 760.0 * $time) * $snapBEnvelope * 0.16
    $tack = [Math]::Sin(2.0 * [Math]::PI * 410.0 * $time) * [Math]::Exp(-11.0 * $time) * 0.18

    $leftValue = ($envelope * (($textureLeft * 0.72) + $tack)) + ($snapA * 0.95) + ($snapB * 0.58)
    $rightValue = ($envelope * (($textureRight * 0.72) + ($tack * 0.94))) + ($snapA * 0.58) + ($snapB * 0.95)
    $leftClipped = [Math]::Tanh($leftValue * 1.34) * 0.76
    $rightClipped = [Math]::Tanh($rightValue * 1.34) * 0.76
    $leftClamped = [Math]::Max(-1.0, [Math]::Min(1.0, $leftClipped))
    $rightClamped = [Math]::Max(-1.0, [Math]::Min(1.0, $rightClipped))
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

Write-Output "STICKY_SFX_GENERATION_PASS: $outputPath ($frameCount stereo frames at ${sampleRate}Hz)."
