$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $root "assets\audio\sfx\force_wind_enter.wav"
$sampleRate = 44100
$duration = 0.82
$frameCount = [int]($sampleRate * $duration)
$random = [System.Random]::new(1313)
$samples = [int16[]]::new($frameCount * 2)
$previous = 0.0

for ($i = 0; $i -lt $frameCount; $i++) {
    $t = $i / [double]$sampleRate
    $attack = [Math]::Min(1.0, $t / 0.08)
    $release = [Math]::Min(1.0, ($duration - $t) / 0.22)
    $envelope = $attack * $release
    $noise = ($random.NextDouble() * 2.0) - 1.0
    $previous = ($previous * 0.87) + ($noise * 0.13)
    $flutter = 0.72 + (0.28 * [Math]::Sin(2.0 * [Math]::PI * 7.5 * $t))
    $air = $previous * $envelope * $flutter
    $tone = [Math]::Sin(2.0 * [Math]::PI * (165.0 + 110.0 * $t) * $t) * $envelope * 0.12
    $left = [Math]::Max(-1.0, [Math]::Min(1.0, ($air * 0.48) + $tone))
    $rightDelay = [Math]::Sin(2.0 * [Math]::PI * 4.0 * $t) * 0.06
    $right = [Math]::Max(-1.0, [Math]::Min(1.0, ($air * 0.43) + $tone + $rightDelay))
    $samples[$i * 2] = [int16]($left * 32767)
    $samples[($i * 2) + 1] = [int16]($right * 32767)
}

$directory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$stream = [System.IO.File]::Create($outputPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $dataLength = $samples.Length * 2
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF"))
    $writer.Write([int](36 + $dataLength))
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("WAVEfmt "))
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

Write-Output "WIND_SFX_GENERATION_PASS: $frameCount stereo frames at ${sampleRate}Hz."
