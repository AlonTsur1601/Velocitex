$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "assets\audio\sfx\device_rail_attach.wav"
$sampleRate = 44100
$duration = 0.54
$frames = [int]($sampleRate * $duration)
$samples = [int16[]]::new($frames * 2)
for ($i = 0; $i -lt $frames; $i++) {
    $t = $i / [double]$sampleRate
    $envelope = [Math]::Exp(-5.2 * $t) * [Math]::Min(1.0, $t / 0.012)
    $clack = [Math]::Sin(2.0 * [Math]::PI * 460.0 * $t) * $envelope
    $hum = [Math]::Sin(2.0 * [Math]::PI * (125.0 + 210.0 * $t) * $t) * [Math]::Exp(-2.4 * $t)
    $left = [Math]::Max(-1.0, [Math]::Min(1.0, ($clack * 0.48) + ($hum * 0.28)))
    $right = [Math]::Max(-1.0, [Math]::Min(1.0, ($clack * 0.42) + ($hum * 0.32)))
    $samples[$i * 2] = [int16]($left * 32767)
    $samples[$i * 2 + 1] = [int16]($right * 32767)
}
$stream = [System.IO.File]::Create($path)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $dataLength = $samples.Length * 2
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([int](36 + $dataLength)); $writer.Write([System.Text.Encoding]::ASCII.GetBytes("WAVEfmt "))
    $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]2); $writer.Write([int]$sampleRate); $writer.Write([int]($sampleRate * 4)); $writer.Write([int16]4); $writer.Write([int16]16)
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("data")); $writer.Write([int]$dataLength); foreach ($sample in $samples) { $writer.Write($sample) }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output "RAIL_SFX_GENERATION_PASS: $frames stereo frames at ${sampleRate}Hz."
