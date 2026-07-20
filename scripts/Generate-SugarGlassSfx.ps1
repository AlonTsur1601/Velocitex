$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "assets\audio\sfx\device_sugar_glass_break.wav"
$rate = 44100
$duration = 0.72
$frames = [int]($rate * $duration)
$samples = [int16[]]::new($frames * 2)
$random = [Random]::new(2424)
$previousNoise = 0.0
$strikes = @(0.0, 0.055, 0.12, 0.205, 0.31)
for ($i = 0; $i -lt $frames; $i++) {
    $t = $i / [double]$rate
    $left = 0.0
    $right = 0.0
    for ($strikeIndex = 0; $strikeIndex -lt $strikes.Count; $strikeIndex++) {
        $local = $t - $strikes[$strikeIndex]
        if ($local -lt 0) { continue }
        $decay = [Math]::Exp(-14.0 * $local)
        $pitch = 2050.0 + ($strikeIndex * 430.0)
        $chime = ([Math]::Sin(2 * [Math]::PI * $pitch * $local) * 0.42) +
                 ([Math]::Sin(2 * [Math]::PI * ($pitch * 1.61) * $local) * 0.24) +
                 ([Math]::Sin(2 * [Math]::PI * ($pitch * 2.17) * $local) * 0.12)
        $pan = if (($strikeIndex % 2) -eq 0) { 0.16 } else { -0.16 }
        $left += $chime * $decay * (1.0 - $pan)
        $right += $chime * $decay * (1.0 + $pan)
    }
    $noise = ($random.NextDouble() * 2.0) - 1.0
    $spark = ($noise - $previousNoise) * [Math]::Exp(-7.5 * $t) * 0.13
    $previousNoise = $noise
    $left = [Math]::Tanh(($left + $spark) * 1.15) * 0.76
    $right = [Math]::Tanh(($right - ($spark * 0.82)) * 1.15) * 0.76
    $samples[$i * 2] = [int16]($left * 32767)
    $samples[$i * 2 + 1] = [int16]($right * 32767)
}
$stream = [IO.File]::Create($path)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $length = $samples.Length * 2
    $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([int](36 + $length)); $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVEfmt "))
    $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]2); $writer.Write([int]$rate); $writer.Write([int]($rate * 4)); $writer.Write([int16]4); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes("data")); $writer.Write([int]$length)
    foreach ($sample in $samples) { $writer.Write($sample) }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}
Write-Output "SUGAR_GLASS_SFX_GENERATION_PASS: $frames stereo frames at ${rate}Hz."
