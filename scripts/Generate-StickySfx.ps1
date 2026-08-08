param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root "assets\audio\source\slime_foley_reference_cc0.wav"
$outputPath = Join-Path $root "assets\audio\sfx\surface_sticky_contact.wav"
$bytes = [IO.File]::ReadAllBytes($sourcePath)
$sourceChannels = [BitConverter]::ToInt16($bytes, 22)
$sourceRate = [BitConverter]::ToInt32($bytes, 24)
$bits = [BitConverter]::ToInt16($bytes, 34)
$chunkOffset = 12
$dataOffset = 0
while ($chunkOffset + 8 -le $bytes.Length) {
    $chunkId = [Text.Encoding]::ASCII.GetString($bytes, $chunkOffset, 4)
    $chunkLength = [BitConverter]::ToInt32($bytes, $chunkOffset + 4)
    if ($chunkId -eq "data") { $dataOffset = $chunkOffset + 8; break }
    $chunkOffset += 8 + $chunkLength + ($chunkLength % 2)
}
if ($sourceChannels -ne 1 -or $bits -ne 16 -or $dataOffset -eq 0) {
    throw "The CC0 slime Foley source must be mono 16-bit PCM with a standard WAV header."
}
$source = [double[]]::new(($bytes.Length - $dataOffset) / 2)
for ($index = 0; $index -lt $source.Length; $index++) { $source[$index] = [BitConverter]::ToInt16($bytes, $dataOffset + ($index * 2)) / 32768.0 }

$outputRate = 44100
$durationSeconds = 1.08
$frameCount = [int]($outputRate * $durationSeconds)
$left = [double[]]::new($frameCount)
$right = [double[]]::new($frameCount)
$events = @(
    @{ Source=10.72; At=0.00; Length=0.82; Speed=0.86; Gain=1.00; Reverse=$false },
    @{ Source=14.66; At=0.20; Length=0.70; Speed=1.14; Gain=0.58; Reverse=$true },
    @{ Source=9.42; At=0.50; Length=0.48; Speed=0.94; Gain=0.44; Reverse=$false }
)
foreach ($event in $events) {
    $eventFrames = [int]($event.Length * $outputRate)
    for ($localFrame = 0; $localFrame -lt $eventFrames; $localFrame++) {
        $outputFrame = [int]($event.At * $outputRate) + $localFrame
        if ($outputFrame -ge $frameCount) { break }
        $progress = $localFrame / [double]([Math]::Max(1, $eventFrames - 1))
        $attack = [Math]::Min($progress / 0.025, 1.0)
        $release = [Math]::Pow([Math]::Max(1.0 - $progress, 0.0), 0.72)
        $envelope = $attack * $release
        $sourceProgress = if ($event.Reverse) { 1.0 - $progress } else { $progress }
        $sourcePosition = ($event.Source + ($sourceProgress * $event.Length * $event.Speed)) * $sourceRate
        $sourceIndex = [int][Math]::Floor($sourcePosition)
        if ($sourceIndex -lt 0 -or $sourceIndex + 2 -ge $source.Length) { continue }
        $fraction = $sourcePosition - $sourceIndex
        $sample = ($source[$sourceIndex] * (1.0 - $fraction)) + ($source[$sourceIndex + 1] * $fraction)
        $rightIndex = [Math]::Min($source.Length - 1, $sourceIndex + 97)
        $rightSample = ($sample * 0.62) + ($source[$rightIndex] * 0.38)
        $left[$outputFrame] += $sample * $envelope * $event.Gain
        $right[$outputFrame] += $rightSample * $envelope * $event.Gain
    }
}

$filterAlpha = 1.0 - [Math]::Exp((-2.0 * [Math]::PI * 1800.0) / $outputRate)
$leftStageOne = 0.0; $leftStageTwo = 0.0; $rightStageOne = 0.0; $rightStageTwo = 0.0
for ($index = 0; $index -lt $frameCount; $index++) {
    $leftStageOne += $filterAlpha * ($left[$index] - $leftStageOne)
    $leftStageTwo += $filterAlpha * ($leftStageOne - $leftStageTwo)
    $rightStageOne += $filterAlpha * ($right[$index] - $rightStageOne)
    $rightStageTwo += $filterAlpha * ($rightStageOne - $rightStageTwo)
    $left[$index] = $leftStageTwo
    $right[$index] = $rightStageTwo
}

$peak = 0.0
for ($index = 0; $index -lt $frameCount; $index++) {
    $peak = [Math]::Max($peak, [Math]::Abs($left[$index])); $peak = [Math]::Max($peak, [Math]::Abs($right[$index]))
}
$gain = if ($peak -gt 0.0) { 0.72 / $peak } else { 1.0 }
$samples = [int16[]]::new($frameCount * 2)
for ($index = 0; $index -lt $frameCount; $index++) {
    $samples[$index * 2] = [int16]([Math]::Round([Math]::Tanh($left[$index] * $gain) * 29400.0))
    $samples[$index * 2 + 1] = [int16]([Math]::Round([Math]::Tanh($right[$index] * $gain) * 29400.0))
}

$stream = [IO.File]::Open($outputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $dataLength = $frameCount * 4
    $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([int](36 + $dataLength)); $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVEfmt ")); $writer.Write([int]16)
    $writer.Write([int16]1); $writer.Write([int16]2); $writer.Write([int]$outputRate); $writer.Write([int]($outputRate * 4)); $writer.Write([int16]4); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes("data")); $writer.Write([int]$dataLength); foreach ($sample in $samples) { $writer.Write($sample) }
}
finally { $writer.Dispose(); $stream.Dispose() }

Write-Output "STICKY_SFX_GENERATION_PASS: three transformed Foley squeezes form an original sticky contact -> $outputPath"
