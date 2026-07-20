$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "assets\audio\sfx\device_moving_platform.wav"
$rate = 44100; $duration = 1.45; $frames = [int]($rate * $duration); $samples = [int16[]]::new($frames * 2); $random = [Random]::new(1818)
for ($i = 0; $i -lt $frames; $i++) {
    $t = $i / [double]$rate
    $attack = [Math]::Min(1, $t * 8); $release = [Math]::Min(1, ($duration - $t) * 5); $env = $attack * $release
    $motor = [Math]::Sin(2 * [Math]::PI * (146 + 5 * [Math]::Sin(2 * [Math]::PI * .7 * $t)) * $t)
    $gear = ([Math]::Sin(2 * [Math]::PI * 292 * $t) * .22) + ([Math]::Sin(2 * [Math]::PI * 584 * $t) * .1)
    $grit = (($random.NextDouble() * 2) - 1) * .07
    $l = [Math]::Tanh(($motor * .25 + $gear + $grit) * $env) * .72
    $r = [Math]::Tanh(($motor * .23 + $gear * .92 - $grit) * $env) * .72
    $samples[$i * 2] = [int16]($l * 32767); $samples[$i * 2 + 1] = [int16]($r * 32767)
}
$stream = [IO.File]::Create($path); $writer = [IO.BinaryWriter]::new($stream); try { $length = $samples.Length * 2; $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([int](36 + $length)); $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVEfmt ")); $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]2); $writer.Write([int]$rate); $writer.Write([int]($rate * 4)); $writer.Write([int16]4); $writer.Write([int16]16); $writer.Write([Text.Encoding]::ASCII.GetBytes("data")); $writer.Write([int]$length); foreach ($sample in $samples) { $writer.Write($sample) } } finally { $writer.Dispose(); $stream.Dispose() }
Write-Output "MOVING_PLATFORM_SFX_GENERATION_PASS: $frames stereo frames at ${rate}Hz."
