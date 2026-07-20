$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "assets\audio\sfx\device_player_cannon_fire.wav"
$rate = 44100; $duration = 0.72; $frames = [int]($rate * $duration); $samples = [int16[]]::new($frames * 2); $random = [Random]::new(1616)
$previousNoise = 0.0
for ($i=0; $i -lt $frames; $i++) {
    $t=$i/[double]$rate
    $body=[Math]::Sin(2*[Math]::PI*168*$t)*[Math]::Exp(-6.8*$t)
    $latch=(([Math]::Sin(2*[Math]::PI*720*$t)*.3)+([Math]::Sin(2*[Math]::PI*1240*$t)*.17))*[Math]::Exp(-15*$t)
    $noise=($random.NextDouble()*2)-1
    $air=($noise-$previousNoise)*[Math]::Exp(-9.5*$t)
    $previousNoise=$noise
    $l=[Math]::Tanh(($body*.34+$air*.22+$latch)*1.35)*.8
    $r=[Math]::Tanh(($body*.31-$air*.2+$latch*1.06)*1.35)*.8
    $samples[$i*2]=[int16]($l*32767); $samples[$i*2+1]=[int16]($r*32767)
}
$stream=[IO.File]::Create($path); $writer=[IO.BinaryWriter]::new($stream); try { $len=$samples.Length*2; $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"));$writer.Write([int](36+$len));$writer.Write([Text.Encoding]::ASCII.GetBytes("WAVEfmt "));$writer.Write([int]16);$writer.Write([int16]1);$writer.Write([int16]2);$writer.Write([int]$rate);$writer.Write([int]($rate*4));$writer.Write([int16]4);$writer.Write([int16]16);$writer.Write([Text.Encoding]::ASCII.GetBytes("data"));$writer.Write([int]$len);foreach($s in $samples){$writer.Write($s)} } finally {$writer.Dispose();$stream.Dispose()}
Write-Output "CANNON_SFX_GENERATION_PASS: $frames stereo frames at ${rate}Hz."
