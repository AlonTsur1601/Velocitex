param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root "assets\audio\sfx"
$outputPath = Join-Path $outputDirectory "surface_super_elastic_bounce.wav"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$sampleRate = 44100
$durationSeconds = 0.58
$frameCount = [int]($sampleRate * $durationSeconds)
$channelCount = 2
$samples = [int16[]]::new($frameCount * $channelCount)
$random = [System.Random]::new(90631)
$springPhase = 0.0

for ($index = 0; $index -lt $frameCount; $index++) {
    $time = $index / [double]$sampleRate
    $normalized = $time / $durationSeconds
    $attack = [Math]::Min($time / 0.003, 1.0)
    $release = [Math]::Pow([Math]::Max(1.0 - $normalized, 0.0), 1.35)
    $envelope = $attack * $release
    $frequency = 420.0 + (610.0 * [Math]::Pow($normalized, 0.58))
    $springPhase += 2.0 * [Math]::PI * $frequency / $sampleRate
    $spring = ([Math]::Sin($springPhase) * 0.38) + ([Math]::Sin($springPhase * 1.51) * 0.17)
    $chime = [Math]::Sin(2.0 * [Math]::PI * 1260.0 * $time) * [Math]::Exp(-9.5 * $time) * 0.24

    $whiteLeft = ($random.NextDouble() * 2.0) - 1.0
    $whiteRight = ($random.NextDouble() * 2.0) - 1.0
    $snap = [Math]::Exp(-82.0 * $time)

    $leftValue = $envelope * ($spring + $chime) + ($whiteLeft * $snap * 0.2)
    $rightValue = $envelope * (($spring * 0.96) + ($chime * 1.08)) + ($whiteRight * $snap * 0.2)
    $leftClamped = [Math]::Max(-1.0, [Math]::Min(1.0, [Math]::Tanh($leftValue * 1.52) * 0.78))
    $rightClamped = [Math]::Max(-1.0, [Math]::Min(1.0, [Math]::Tanh($rightValue * 1.52) * 0.78))
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

Write-Output "SUPER_ELASTIC_SFX_GENERATION_PASS: $outputPath ($frameCount stereo frames at ${sampleRate}Hz)."
