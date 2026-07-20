param(
    [ValidateSet("All", "PlayerMotion")]
    [string]$Scope = "All"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "assets\audio\sfx"
New-Item -ItemType Directory -Path $output -Force | Out-Null

$sampleRate = 44100
$channels = 2
$specs = @(
    @{ Name="player_roll_metal_loop.wav"; Duration=1.6; Kind="metal"; Seed=3101 },
    @{ Name="player_roll_glass_loop.wav"; Duration=1.6; Kind="glass"; Seed=3102 },
    @{ Name="player_roll_soft_loop.wav"; Duration=1.6; Kind="soft"; Seed=3103 },
    @{ Name="player_roll_rubber_loop.wav"; Duration=1.6; Kind="rubber"; Seed=3104 },
    @{ Name="player_air_wind_loop.wav"; Duration=2.0; Kind="wind"; Seed=3105 },
    @{ Name="player_land_soft.wav"; Duration=0.42; Kind="land-soft"; Seed=3106 },
    @{ Name="player_land_hard.wav"; Duration=0.68; Kind="land-hard"; Seed=3107 },
    @{ Name="player_impact_metal_tap.wav"; Duration=0.22; Kind="impact-tap"; Seed=3110 },
    @{ Name="player_impact_metal_light.wav"; Duration=0.30; Kind="impact-light"; Seed=3111 },
    @{ Name="player_impact_metal_medium.wav"; Duration=0.42; Kind="impact-medium"; Seed=3112 },
    @{ Name="player_impact_metal_heavy.wav"; Duration=0.58; Kind="impact-heavy"; Seed=3113 },
    @{ Name="player_impact_metal_crash.wav"; Duration=0.82; Kind="impact-crash"; Seed=3114 },
    @{ Name="ui_advancement.wav"; Duration=0.72; Kind="advancement"; Seed=3108 },
    @{ Name="room_transfer.wav"; Duration=3.2; Kind="transfer"; Seed=3109 }
)

if ($Scope -eq "PlayerMotion") {
    $specs = @($specs | Where-Object {
        $_.Kind -in @("metal", "glass", "soft", "rubber", "land-soft", "land-hard") -or
        $_.Kind -like "impact-*"
    })
}

foreach ($spec in $specs) {
    $duration = [double]$spec.Duration
    $frameCount = [int]($sampleRate * $duration)
    $samples = [int16[]]::new($frameCount * $channels)
    $random = [Random]::new([int]$spec.Seed)
    $phaseA = $random.NextDouble() * 2.0 * [Math]::PI
    $phaseB = $random.NextDouble() * 2.0 * [Math]::PI
    $metalBodyL = 0.0
    $metalBodyR = 0.0
    $metalLowL1 = 0.0; $metalLowL2 = 0.0
    $metalLowR1 = 0.0; $metalLowR2 = 0.0
    $metalMidL1 = 0.0; $metalMidL2 = 0.0
    $metalMidR1 = 0.0; $metalMidR2 = 0.0
    $metalHighL1 = 0.0; $metalHighL2 = 0.0
    $metalHighR1 = 0.0; $metalHighR2 = 0.0
    $metalLowRadius = 0.99835
    $metalMidRadius = 0.99670
    $metalHighRadius = 0.99380
    $metalLowCoeffL = 2.0*$metalLowRadius*[Math]::Cos(2.0*[Math]::PI*196.0/$sampleRate)
    $metalLowCoeffR = 2.0*$metalLowRadius*[Math]::Cos(2.0*[Math]::PI*203.0/$sampleRate)
    $metalMidCoeffL = 2.0*$metalMidRadius*[Math]::Cos(2.0*[Math]::PI*612.0/$sampleRate)
    $metalMidCoeffR = 2.0*$metalMidRadius*[Math]::Cos(2.0*[Math]::PI*628.0/$sampleRate)
    $metalHighCoeffL = 2.0*$metalHighRadius*[Math]::Cos(2.0*[Math]::PI*1370.0/$sampleRate)
    $metalHighCoeffR = 2.0*$metalHighRadius*[Math]::Cos(2.0*[Math]::PI*1415.0/$sampleRate)

    for ($i = 0; $i -lt $frameCount; $i++) {
        $t = $i / [double]$sampleRate
        $u = $i / [double]$frameCount
        $a = 2.0 * [Math]::PI * $u
        $left = 0.0
        $right = 0.0
        switch ($spec.Kind) {
            "metal" {
                # Model a hard sphere exciting short-lived modes in a metal sheet.
                # Sparse, irregular contacts avoid both a sandy noise bed and a tonal hum.
                $whiteL = ($random.NextDouble()*2.0)-1.0
                $whiteR = ($random.NextDouble()*2.0)-1.0
                $metalBodyL += ($whiteL-$metalBodyL)*0.0035
                $metalBodyR += ($whiteR-$metalBodyR)*0.0035

                $contact = 0.0
                if ($random.NextDouble() -lt 0.00082) {
                    $contactSign = if ($random.NextDouble() -lt 0.5) { -1.0 } else { 1.0 }
                    $contact = (0.38+($random.NextDouble()*0.62))*$contactSign
                }

                $nextLowL = $metalLowCoeffL*$metalLowL1-($metalLowRadius*$metalLowRadius*$metalLowL2)+(0.070*$contact)
                $nextLowR = $metalLowCoeffR*$metalLowR1-($metalLowRadius*$metalLowRadius*$metalLowR2)+(0.067*$contact)
                $nextMidL = $metalMidCoeffL*$metalMidL1-($metalMidRadius*$metalMidRadius*$metalMidL2)+(0.043*$contact)
                $nextMidR = $metalMidCoeffR*$metalMidR1-($metalMidRadius*$metalMidRadius*$metalMidR2)+(0.041*$contact)
                $nextHighL = $metalHighCoeffL*$metalHighL1-($metalHighRadius*$metalHighRadius*$metalHighL2)+(0.020*$contact)
                $nextHighR = $metalHighCoeffR*$metalHighR1-($metalHighRadius*$metalHighRadius*$metalHighR2)+(0.019*$contact)

                $metalLowL2=$metalLowL1; $metalLowL1=$nextLowL
                $metalLowR2=$metalLowR1; $metalLowR1=$nextLowR
                $metalMidL2=$metalMidL1; $metalMidL1=$nextMidL
                $metalMidR2=$metalMidR1; $metalMidR1=$nextMidR
                $metalHighL2=$metalHighL1; $metalHighL1=$nextHighL
                $metalHighR2=$metalHighR1; $metalHighR1=$nextHighR

                $left = (0.34*$metalBodyL)+(0.56*$nextLowL)+(0.34*$nextMidL)+(0.16*$nextHighL)+(0.010*$contact)
                $right = (0.34*$metalBodyR)+(0.56*$nextLowR)+(0.34*$nextMidR)+(0.16*$nextHighR)+(0.010*$contact)
            }
            "glass" {
                $baseL = 0.22*[Math]::Sin(271*$a)+0.17*[Math]::Sin(599*$a+$phaseA)+0.11*[Math]::Sin(997*$a)+0.06*[Math]::Sin(1601*$a+$phaseB)
                $baseR = 0.22*[Math]::Sin(277*$a)+0.17*[Math]::Sin(593*$a+$phaseB)+0.11*[Math]::Sin(1009*$a)+0.06*[Math]::Sin(1597*$a+$phaseA)
                $shine = 0.08*[Math]::Sin(1871*$a)*(0.55+0.45*[Math]::Sin(23*$a))
                $left=($baseL+$shine)*0.58; $right=($baseR-$shine)*0.58
            }
            "soft" {
                $left=(0.28*[Math]::Sin(87*$a)+0.19*[Math]::Sin(171*$a+$phaseA)+0.11*[Math]::Sin(343*$a)+0.06*[Math]::Sin(521*$a+$phaseB))*0.58
                $right=(0.28*[Math]::Sin(91*$a)+0.19*[Math]::Sin(167*$a+$phaseB)+0.11*[Math]::Sin(347*$a)+0.06*[Math]::Sin(517*$a+$phaseA))*0.58
            }
            "rubber" {
                $pulseL=[Math]::Pow([Math]::Max([Math]::Sin(29*$a),0.0),6)
                $pulseR=[Math]::Pow([Math]::Max([Math]::Sin(31*$a+0.45),0.0),6)
                $left=(0.24*[Math]::Sin(71*$a)+0.15*[Math]::Sin(149*$a)+0.13*[Math]::Sin(293*$a)+0.09*[Math]::Sin(487*$a)+0.18*$pulseL-0.07)*0.58
                $right=(0.24*[Math]::Sin(73*$a)+0.15*[Math]::Sin(151*$a)+0.13*[Math]::Sin(289*$a)+0.09*[Math]::Sin(491*$a)+0.18*$pulseR-0.07)*0.58
            }
            "wind" {
                $swell=0.62+0.38*[Math]::Sin(2*$a-0.8)
                $left=(0.28*[Math]::Sin(17*$a+$phaseA)+0.20*[Math]::Sin(31*$a)+0.13*[Math]::Sin(67*$a+$phaseB)+0.08*[Math]::Sin(127*$a))*$swell
                $right=(0.28*[Math]::Sin(19*$a+$phaseB)+0.20*[Math]::Sin(29*$a)+0.13*[Math]::Sin(71*$a+$phaseA)+0.08*[Math]::Sin(131*$a))*(1.0-$swell*0.18)
            }
            "land-soft" {
                $env=[Math]::Min($t/0.008,1.0)*[Math]::Exp(-$t*10.5)
                $tone=[Math]::Sin(2*[Math]::PI*(145-75*$u)*$t)
                $noise=(($random.NextDouble()*2)-1)*[Math]::Exp(-$t*22)
                $left=$env*(0.50*$tone+0.20*$noise); $right=$env*(0.48*$tone-0.18*$noise)
            }
            "land-hard" {
                $env=[Math]::Min($t/0.004,1.0)*[Math]::Exp(-$t*7.2)
                $body=[Math]::Sin(2*[Math]::PI*(102-45*$u)*$t)+0.34*[Math]::Sin(2*[Math]::PI*238*$t)
                $crack=(($random.NextDouble()*2)-1)*[Math]::Exp(-$t*19)
                $left=$env*(0.52*$body+0.30*$crack); $right=$env*(0.50*$body-0.26*$crack)
            }
            "impact-tap" {
                $env=[Math]::Min($t/0.0015,1.0)*[Math]::Exp(-$t*30.0)
                $click=(($random.NextDouble()*2.0)-1.0)*[Math]::Exp(-$t*190.0)
                $left=$env*(0.35*[Math]::Sin(2*[Math]::PI*760*$t+$phaseA)+0.23*[Math]::Sin(2*[Math]::PI*1180*$t)+0.12*[Math]::Sin(2*[Math]::PI*1840*$t)+0.07*$click)
                $right=$env*(0.34*[Math]::Sin(2*[Math]::PI*748*$t+$phaseB)+0.22*[Math]::Sin(2*[Math]::PI*1205*$t)+0.12*[Math]::Sin(2*[Math]::PI*1815*$t)-0.06*$click)
            }
            "impact-light" {
                $env=[Math]::Min($t/0.0018,1.0)*[Math]::Exp(-$t*20.0)
                $click=(($random.NextDouble()*2.0)-1.0)*[Math]::Exp(-$t*165.0)
                $left=$env*(0.38*[Math]::Sin(2*[Math]::PI*510*$t+$phaseA)+0.25*[Math]::Sin(2*[Math]::PI*835*$t)+0.14*[Math]::Sin(2*[Math]::PI*1360*$t)+0.08*$click)
                $right=$env*(0.37*[Math]::Sin(2*[Math]::PI*498*$t+$phaseB)+0.24*[Math]::Sin(2*[Math]::PI*852*$t)+0.14*[Math]::Sin(2*[Math]::PI*1335*$t)-0.07*$click)
            }
            "impact-medium" {
                $env=[Math]::Min($t/0.002,1.0)*[Math]::Exp(-$t*12.5)
                $click=(($random.NextDouble()*2.0)-1.0)*[Math]::Exp(-$t*145.0)
                $left=$env*(0.42*[Math]::Sin(2*[Math]::PI*340*$t+$phaseA)+0.27*[Math]::Sin(2*[Math]::PI*610*$t)+0.16*[Math]::Sin(2*[Math]::PI*980*$t)+0.09*$click)
                $right=$env*(0.41*[Math]::Sin(2*[Math]::PI*331*$t+$phaseB)+0.26*[Math]::Sin(2*[Math]::PI*625*$t)+0.16*[Math]::Sin(2*[Math]::PI*955*$t)-0.08*$click)
            }
            "impact-heavy" {
                $env=[Math]::Min($t/0.0025,1.0)*[Math]::Exp(-$t*8.2)
                $click=(($random.NextDouble()*2.0)-1.0)*[Math]::Exp(-$t*125.0)
                $left=$env*(0.27*[Math]::Sin(2*[Math]::PI*118*$t)+0.44*[Math]::Sin(2*[Math]::PI*220*$t+$phaseA)+0.28*[Math]::Sin(2*[Math]::PI*410*$t)+0.17*[Math]::Sin(2*[Math]::PI*720*$t)+0.10*$click)
                $right=$env*(0.26*[Math]::Sin(2*[Math]::PI*114*$t)+0.43*[Math]::Sin(2*[Math]::PI*214*$t+$phaseB)+0.27*[Math]::Sin(2*[Math]::PI*422*$t)+0.17*[Math]::Sin(2*[Math]::PI*698*$t)-0.09*$click)
            }
            "impact-crash" {
                $env=[Math]::Min($t/0.003,1.0)*[Math]::Exp(-$t*5.8)
                $click=(($random.NextDouble()*2.0)-1.0)*[Math]::Exp(-$t*105.0)
                $left=$env*(0.30*[Math]::Sin(2*[Math]::PI*92*$t)+0.43*[Math]::Sin(2*[Math]::PI*148*$t+$phaseA)+0.31*[Math]::Sin(2*[Math]::PI*278*$t)+0.20*[Math]::Sin(2*[Math]::PI*515*$t)+0.12*[Math]::Sin(2*[Math]::PI*930*$t)+0.11*$click)
                $right=$env*(0.29*[Math]::Sin(2*[Math]::PI*88*$t)+0.42*[Math]::Sin(2*[Math]::PI*143*$t+$phaseB)+0.30*[Math]::Sin(2*[Math]::PI*287*$t)+0.20*[Math]::Sin(2*[Math]::PI*498*$t)+0.12*[Math]::Sin(2*[Math]::PI*905*$t)-0.10*$click)
            }
            "advancement" {
                $attack=[Math]::Min($t/0.018,1.0); $release=[Math]::Exp(-$t*3.6)
                $first=[Math]::Sin(2*[Math]::PI*659.25*$t)*[Math]::Exp(-$t*7.0)
                $second=if($t -ge 0.17){[Math]::Sin(2*[Math]::PI*987.77*($t-0.17))*[Math]::Exp(-($t-0.17)*6.5)}else{0}
                $left=$attack*$release*(0.34*$first+0.32*$second); $right=$attack*$release*(0.31*$first+0.36*$second)
            }
            "transfer" {
                $lockEnv=[Math]::Exp(-$t*11.0)
                $lock=([Math]::Sin(2*[Math]::PI*118*$t)+0.45*[Math]::Sin(2*[Math]::PI*241*$t))*$lockEnv
                $whooshIn=[Math]::Min([Math]::Max(($t-0.18)/0.6,0),1)
                $whooshOut=[Math]::Min([Math]::Max(($duration-$t)/0.65,0),1)
                $whooshEnv=$whooshIn*$whooshOut
                $leftAir=(0.22*[Math]::Sin(53*$a+$phaseA)+0.16*[Math]::Sin(101*$a)+0.09*[Math]::Sin(211*$a+$phaseB))*$whooshEnv
                $rightAir=(0.22*[Math]::Sin(59*$a+$phaseB)+0.16*[Math]::Sin(97*$a)+0.09*[Math]::Sin(223*$a+$phaseA))*$whooshEnv
                $pulse=[Math]::Pow([Math]::Max([Math]::Sin(2*[Math]::PI*4.2*$t),0),8)*0.12*$whooshEnv
                $left=0.42*$lock+$leftAir+$pulse; $right=0.40*$lock+$rightAir+($pulse*0.82)
            }
        }

        $left=[Math]::Tanh($left*1.4)*0.82
        $right=[Math]::Tanh($right*1.4)*0.82
        $samples[$i*2]=[int16]([Math]::Round([Math]::Max(-1.0,[Math]::Min(1.0,$left))*32767))
        $samples[$i*2+1]=[int16]([Math]::Round([Math]::Max(-1.0,[Math]::Min(1.0,$right))*32767))
    }

    if ($spec.Kind -eq "metal") {
        # Bring the random tail continuously back to the first sample so the loop
        # remains click-free without reintroducing a periodic tone.
        $seamFrames = [Math]::Min(1536, [int]($frameCount/4))
        for ($j = 0; $j -lt $seamFrames; $j++) {
            $alpha = ($j+1.0)/$seamFrames
            $tailFrame = $frameCount-$seamFrames+$j
            $headFrame = $seamFrames-1-$j
            foreach ($channel in 0,1) {
                $tailIndex = ($tailFrame*2)+$channel
                $headIndex = ($headFrame*2)+$channel
                $samples[$tailIndex] = [int16]([Math]::Round(
                    ((1.0-$alpha)*$samples[$tailIndex])+($alpha*$samples[$headIndex])))
            }
        }
    }

    $path = Join-Path $output $spec.Name
    $stream = [IO.File]::Open($path,[IO.FileMode]::Create,[IO.FileAccess]::Write)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $dataLength=$samples.Length*2
        $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"));$writer.Write([int](36+$dataLength));$writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("fmt "));$writer.Write([int]16);$writer.Write([int16]1);$writer.Write([int16]$channels)
        $writer.Write([int]$sampleRate);$writer.Write([int]($sampleRate*$channels*2));$writer.Write([int16]($channels*2));$writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes("data"));$writer.Write([int]$dataLength)
        foreach($sample in $samples){$writer.Write($sample)}
    } finally { $writer.Dispose(); $stream.Dispose() }
}

Write-Output "PLAYER_SFX_GENERATION_PASS: generated $($specs.Count) original stereo cues for scope $Scope."
