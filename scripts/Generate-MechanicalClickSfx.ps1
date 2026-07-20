$ErrorActionPreference = "Stop"

$rate = 44100
$duration = 0.16
$frameCount = [int]($rate * $duration)
$random = [Random]::new(7319)
$samples = [Collections.Generic.List[int16]]::new($frameCount * 2)

for ($frame = 0; $frame -lt $frameCount; $frame++) {
    $time = $frame / $rate
    $attack = [Math]::Min(1.0, $time / 0.002)
    $decay = [Math]::Exp(-$time * 35.0)
    $metal = ([Math]::Sin(2.0 * [Math]::PI * 1850.0 * $time) * 0.48) +
        ([Math]::Sin(2.0 * [Math]::PI * 2730.0 * $time) * 0.24)
    $body = [Math]::Sin(2.0 * [Math]::PI * 390.0 * $time) * [Math]::Exp(-$time * 24.0) * 0.22
    $noise = (($random.NextDouble() * 2.0) - 1.0) * [Math]::Exp(-$time * 70.0) * 0.12
    $sample = [Math]::Max(-1.0, [Math]::Min(1.0, ($metal * $attack * $decay) + $body + $noise))
    $left = [int16]($sample * 25000.0)
    $right = [int16]($sample * 24250.0)
    $samples.Add($left)
    $samples.Add($right)
}

$path = Join-Path $PSScriptRoot "..\assets\audio\sfx\device_mechanical_click.wav"
$stream = [IO.File]::Create($path)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $dataLength = $samples.Count * 2
    $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"))
    $writer.Write([int](36 + $dataLength))
    $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVEfmt "))
    $writer.Write([int]16)
    $writer.Write([int16]1)
    $writer.Write([int16]2)
    $writer.Write([int]$rate)
    $writer.Write([int]($rate * 4))
    $writer.Write([int16]4)
    $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes("data"))
    $writer.Write([int]$dataLength)
    foreach ($sample in $samples) {
        $writer.Write($sample)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Output "MECHANICAL_CLICK_SFX_GENERATED: $path"
