param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$voiceDirectory = Join-Path $root "assets\audio\voice"
$musicDirectory = Join-Path $root "assets\audio\music"
New-Item -ItemType Directory -Path $voiceDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $musicDirectory -Force | Out-Null

Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices = $synth.GetInstalledVoices() | Where-Object Enabled | ForEach-Object { $_.VoiceInfo.Name }
$motherVoice = if ($voices -contains "Microsoft Zira Desktop") { "Microsoft Zira Desktop" } else { $voices[0] }
$childVoice = if ($voices -contains "Microsoft David Desktop") { "Microsoft David Desktop" } else { $voices[-1] }

$jobs = @(
    @{ File="opening_01_child.wav"; Role="child"; Text="Mom, can I get one?" },
    @{ File="opening_02_mother.wav"; Role="mother"; Text="One candy. Then we're going." },
    @{ File="opening_03_child.wav"; Role="child"; Text="Did it get stuck?" },
    @{ File="opening_04_mother.wav"; Role="mother"; Text="Give it a moment." },
    @{ File="room01.wav"; Role="child"; Text="Is it still coming?" },
    @{ File="room02.wav"; Role="mother"; Text="The machine is taking its time." },
    @{ File="room03.wav"; Role="child"; Text="I heard something fall in there." },
    @{ File="room04.wav"; Role="mother"; Text="Maybe the mechanism needs another push." },
    @{ File="room05.wav"; Role="child"; Text="I think it moved!" },
    @{ File="room06.wav"; Role="mother"; Text="Now I can hear it rolling." },
    @{ File="room07.wav"; Role="child"; Text="Why does it sound so sticky?" },
    @{ File="room08.wav"; Role="mother"; Text="That sounded much faster." },
    @{ File="room09.wav"; Role="child"; Text="Did it just bounce?" },
    @{ File="room10.wav"; Role="mother"; Text="That sounded like the whole machine." },
    @{ File="room11.wav"; Role="child"; Text="It sounds like it stopped falling." },
    @{ File="room12.wav"; Role="mother"; Text="That sounded like a hard landing." },
    @{ File="room13.wav"; Role="child"; Text="I can hear air rushing inside." },
    @{ File="room14.wav"; Role="mother"; Text="Something just clicked into place." },
    @{ File="room15.wav"; Role="child"; Text="That one sounded really long." },
    @{ File="room16.wav"; Role="mother"; Text="That sounded like it launched something." },
    @{ File="room17.wav"; Role="child"; Text="Something is bouncing around in there!" },
    @{ File="room18.wav"; Role="mother"; Text="I think something just went up." },
    @{ File="room19.wav"; Role="child"; Text="That was a really loud spring." },
    @{ File="room20.wav"; Role="mother"; Text="That sounded like the whole launcher assembly." },
    @{ File="room21.wav"; Role="child"; Text="It suddenly went very quiet." },
    @{ File="room22.wav"; Role="mother"; Text="It sounds like a tiny ratchet turning." },
    @{ File="room23.wav"; Role="child"; Text="Something in there just wound up!" },
    @{ File="room24.wav"; Role="mother"; Text="I hope that cracking sound was normal." },
    @{ File="room25.wav"; Role="child"; Text="Why does it keep changing how it rolls?" },
    @{ File="room26.wav"; Role="child"; Text="Those cannons are tracking it through the air!" },
    @{ File="room27.wav"; Role="child"; Text="Did the candy just change direction?" },
    @{ File="room28.wav"; Role="child"; Text="It must be close now!" },
    @{ File="ending_finally.wav"; Role="child"; Text="Finally!" }
)

try {
    foreach ($job in $jobs) {
        $isChild = $job.Role -eq "child"
        $synth.SelectVoice($(if ($isChild) { $childVoice } else { $motherVoice }))
        $escaped = [System.Security.SecurityElement]::Escape($job.Text)
        $prosody = if ($isChild) { 'pitch="+28%" rate="+8%"' } else { 'pitch="-3%" rate="-4%"' }
        $ssml = "<speak version='1.0' xml:lang='en-US'><prosody $prosody>$escaped</prosody></speak>"
        $path = Join-Path $voiceDirectory $job.File
        $synth.SetOutputToWaveFile($path)
        $synth.SpeakSsml($ssml)
        $synth.SetOutputToNull()
    }
}
finally {
    $synth.Dispose()
}

function Write-AmbientMusic {
    param([string]$Path)
    $rate = 44100
    $duration = 32.0
    $frames = [int]($rate * $duration)
    $samples = [int16[]]::new($frames * 2)
    $notes = @(55.0, 65.406, 73.416, 82.407, 73.416, 65.406, 61.735, 65.406)
    $random = [Random]::new(30030)
    for ($i = 0; $i -lt $frames; $i++) {
        $t = $i / [double]$rate
        $bar = [int]($t / 4.0) % $notes.Count
        $phase = ($t % 4.0) / 4.0
        $fade = [Math]::Sin([Math]::PI * [Math]::Min(1.0, $phase * 2.0)) * [Math]::Sin([Math]::PI * [Math]::Min(1.0, (1.0 - $phase) * 2.0))
        $rootTone = [Math]::Sin(2.0 * [Math]::PI * $notes[$bar] * $t) * 0.13
        $fifth = [Math]::Sin(2.0 * [Math]::PI * ($notes[$bar] * 1.5) * $t + 0.7) * 0.055
        $mechanical = [Math]::Sin(2.0 * [Math]::PI * 0.5 * $t) * [Math]::Sin(2.0 * [Math]::PI * 220.0 * $t) * 0.012
        $sparkPhase = $t % 8.0
        $sparkEnvelope = if ($sparkPhase -lt 1.8) { [Math]::Sin([Math]::PI * $sparkPhase / 1.8) } else { 0.0 }
        $spark = [Math]::Sin(2.0 * [Math]::PI * 329.63 * $t) * $sparkEnvelope * 0.028
        $air = (($random.NextDouble() * 2.0) - 1.0) * 0.004
        $body = (($rootTone + $fifth) * $fade) + $mechanical + $spark + $air
        $left = [Math]::Tanh(($body + ($spark * 0.14)) * 1.2) * 0.82
        $right = [Math]::Tanh(($body - ($spark * 0.14)) * 1.2) * 0.82
        $samples[$i * 2] = [int16]([Math]::Round($left * 32767.0))
        $samples[($i * 2) + 1] = [int16]([Math]::Round($right * 32767.0))
    }
    $stream = [IO.File]::Create($Path)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $dataLength = $samples.Length * 2
        $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([int](36 + $dataLength)); $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("fmt ")); $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]2)
        $writer.Write([int]$rate); $writer.Write([int]($rate * 4)); $writer.Write([int16]4); $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes("data")); $writer.Write([int]$dataLength); foreach ($sample in $samples) { $writer.Write($sample) }
    }
    finally { $writer.Dispose(); $stream.Dispose() }
}

Write-AmbientMusic -Path (Join-Path $musicDirectory "machine_ambient.wav")
Write-Output "STORY_AUDIO_GENERATION_PASS: $($jobs.Count) voice clips and one 32-second stereo music loop generated with $childVoice / $motherVoice."
