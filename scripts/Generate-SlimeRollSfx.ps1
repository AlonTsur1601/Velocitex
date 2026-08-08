param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root "assets\audio\source\slime_foley_reference_cc0.wav"
$outputPath = Join-Path $root "assets\audio\sfx\player_roll_slime_loop.wav"
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
for ($index = 0; $index -lt $source.Length; $index++) {
    $source[$index] = [BitConverter]::ToInt16($bytes, $dataOffset + ($index * 2)) / 32768.0
}

$outputRate = 44100
$durationSeconds = 2.8
$frameCount = [int]($outputRate * $durationSeconds)
$left = [double[]]::new($frameCount)
$right = [double[]]::new($frameCount)
$events = @(
    @{ Source=7.82; At=-0.12; Length=0.78; Speed=0.91; Gain=0.86; Reverse=$false },
    @{ Source=8.54; At=0.25; Length=0.82; Speed=1.08; Gain=0.72; Reverse=$true },
    @{ Source=9.36; At=0.62; Length=0.86; Speed=0.84; Gain=0.92; Reverse=$false },
    @{ Source=10.78; At=1.02; Length=0.80; Speed=1.13; Gain=0.76; Reverse=$true },
    @{ Source=12.18; At=1.39; Length=0.88; Speed=0.88; Gain=0.88; Reverse=$false },
    @{ Source=14.62; At=1.80; Length=0.82; Speed=1.05; Gain=0.78; Reverse=$true },
    @{ Source=15.28; At=2.18; Length=0.86; Speed=0.94; Gain=0.90; Reverse=$false },
    @{ Source=8.18; At=2.55; Length=0.78; Speed=1.10; Gain=0.70; Reverse=$true }
)

foreach ($event in $events) {
    $eventFrames = [int]($event.Length * $outputRate)
    for ($localFrame = 0; $localFrame -lt $eventFrames; $localFrame++) {
        $outputFrame = [int]($event.At * $outputRate) + $localFrame
        if ($outputFrame -lt 0 -or $outputFrame -ge $frameCount) { continue }
        $progress = $localFrame / [double]([Math]::Max(1, $eventFrames - 1))
        $window = [Math]::Pow([Math]::Sin([Math]::PI * $progress), 0.62)
        $sourceProgress = if ($event.Reverse) { 1.0 - $progress } else { $progress }
        $sourcePosition = (($event.Source + ($sourceProgress * $event.Length * $event.Speed)) * $sourceRate)
        $sourceIndex = [int][Math]::Floor($sourcePosition)
        if ($sourceIndex -lt 0 -or $sourceIndex + 2 -ge $source.Length) { continue }
        $fraction = $sourcePosition - $sourceIndex
        $sample = ($source[$sourceIndex] * (1.0 - $fraction)) + ($source[$sourceIndex + 1] * $fraction)
        $rightIndex = [Math]::Min($source.Length - 1, $sourceIndex + 73)
        $rightSample = ($sample * 0.68) + ($source[$rightIndex] * 0.32)
        $left[$outputFrame] += $sample * $window * $event.Gain
        $right[$outputFrame] += $rightSample * $window * $event.Gain
    }
}

# Remove the isolated high-frequency scrape in the source Foley while keeping
# the wet compression and suction body intact.
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

# Fold the overlapping end back into the beginning for a seamless, irregular roll loop.
$crossfadeFrames = [int](0.24 * $outputRate)
for ($index = 0; $index -lt $crossfadeFrames; $index++) {
    $blend = $index / [double]($crossfadeFrames - 1)
    $tail = $frameCount - $crossfadeFrames + $index
    $left[$tail] = ($left[$tail] * (1.0 - $blend)) + ($left[$index] * $blend)
    $right[$tail] = ($right[$tail] * (1.0 - $blend)) + ($right[$index] * $blend)
}

$peak = 0.0
for ($index = 0; $index -lt $frameCount; $index++) {
    $peak = [Math]::Max($peak, [Math]::Abs($left[$index])); $peak = [Math]::Max($peak, [Math]::Abs($right[$index]))
}
$gain = if ($peak -gt 0.0) { 0.70 / $peak } else { 1.0 }
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

Write-Output "SLIME_ROLL_SFX_GENERATION_PASS: eight transformed CC0 Foley squishes form an original rolling loop -> $outputPath"
