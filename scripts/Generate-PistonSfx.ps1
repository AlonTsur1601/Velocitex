$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "assets\audio\sfx\device_piston_fire.wav"
$rate = 44100; $duration = 0.62; $frames = [int]($rate * $duration); $samples = [int16[]]::new($frames * 2); $random = [Random]::new(1919)
$previousNoise = 0.0
for ($i = 0; $i -lt $frames; $i++) {
    $t = $i / [double]$rate
    $body = [Math]::Sin(2 * [Math]::PI * 190 * $t) * [Math]::Exp(-8.5 * $t)
    $clack = (([Math]::Sin(2 * [Math]::PI * 540 * $t) * .34) + ([Math]::Sin(2 * [Math]::PI * 980 * $t) * .2)) * [Math]::Exp(-12 * $t)
    $noise = ($random.NextDouble() * 2) - 1
    $air = ($noise - $previousNoise) * [Math]::Exp(-15 * $t)
    $previousNoise = $noise
    $l = [Math]::Tanh(($body * .38 + $clack + $air * .15) * 1.35) * .78
    $r = [Math]::Tanh(($body * .35 + $clack * 1.08 - $air * .13) * 1.35) * .78
    $samples[$i * 2] = [int16]($l * 32767); $samples[$i * 2 + 1] = [int16]($r * 32767)
}
$stream = [IO.File]::Create($path); $writer = [IO.BinaryWriter]::new($stream); try { $length = $samples.Length * 2; $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([int](36 + $length)); $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVEfmt ")); $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]2); $writer.Write([int]$rate); $writer.Write([int]($rate * 4)); $writer.Write([int16]4); $writer.Write([int16]16); $writer.Write([Text.Encoding]::ASCII.GetBytes("data")); $writer.Write([int]$length); foreach ($sample in $samples) { $writer.Write($sample) } } finally { $writer.Dispose(); $stream.Dispose() }
Write-Output "PISTON_SFX_GENERATION_PASS: $frames stereo frames at ${rate}Hz."
