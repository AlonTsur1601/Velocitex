$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$voiceDirectory = Join-Path $root "assets\audio\voice"
$edgeModule = Join-Path $root ".tools\edge_tts"
$python = (Get-Command python -ErrorAction Stop).Source

if (-not (Test-Path -LiteralPath (Join-Path $edgeModule "edge_tts"))) {
    throw "edge-tts is not available under .tools\edge_tts."
}

New-Item -ItemType Directory -Path $voiceDirectory -Force | Out-Null
$jobs = @(
    @{ File="opening_01_child.mp3"; Role="child"; Text="Mom, can I get one?" },
    @{ File="opening_02_mother.mp3"; Role="mother"; Text="One candy. Then we're going." },
    @{ File="opening_03_child.mp3"; Role="child"; Text="Did it get stuck?" },
    @{ File="opening_04_mother.mp3"; Role="mother"; Text="Give it a moment." },
    @{ File="room01.mp3"; Role="child"; Text="Is it still coming?" },
    @{ File="room02.mp3"; Role="mother"; Text="The machine is taking its time." },
    @{ File="room03.mp3"; Role="child"; Text="I heard something fall in there." },
    @{ File="room04.mp3"; Role="mother"; Text="Maybe the mechanism needs another push." },
    @{ File="room05.mp3"; Role="child"; Text="I think it moved!" },
    @{ File="room06.mp3"; Role="mother"; Text="Now I can hear it rolling." },
    @{ File="room07.mp3"; Role="child"; Text="Why does it sound so sticky?" },
    @{ File="room08.mp3"; Role="mother"; Text="That sounded much faster." },
    @{ File="room09.mp3"; Role="child"; Text="Did it just bounce?" },
    @{ File="room10.mp3"; Role="mother"; Text="That sounded like the whole machine." },
    @{ File="room11.mp3"; Role="child"; Text="It sounds like it stopped falling." },
    @{ File="room12.mp3"; Role="mother"; Text="That sounded like a hard landing." },
    @{ File="room13.mp3"; Role="child"; Text="I can hear air rushing inside." },
    @{ File="room14.mp3"; Role="mother"; Text="Something just clicked into place." },
    @{ File="room15.mp3"; Role="child"; Text="That one sounded really long." },
    @{ File="room16.mp3"; Role="mother"; Text="That sounded like it launched something." },
    @{ File="room17.mp3"; Role="child"; Text="Something is bouncing around in there!" },
    @{ File="room18.mp3"; Role="mother"; Text="I think something just went up." },
    @{ File="room19.mp3"; Role="child"; Text="That was a really loud spring." },
    @{ File="room20.mp3"; Role="mother"; Text="That sounded like the whole launcher assembly." },
    @{ File="room21.mp3"; Role="child"; Text="It suddenly went very quiet." },
    @{ File="room22.mp3"; Role="mother"; Text="It sounds like a tiny ratchet turning." },
    @{ File="room23.mp3"; Role="child"; Text="Something in there just wound up!" },
    @{ File="room24.mp3"; Role="mother"; Text="I hope that cracking sound was normal." },
    @{ File="room25.mp3"; Role="child"; Text="Why does it keep changing how it rolls?" },
    @{ File="room26.mp3"; Role="child"; Text="Those cannons are tracking it through the air!" },
    @{ File="room27.mp3"; Role="child"; Text="Did the candy just change direction?" },
    @{ File="room28.mp3"; Role="child"; Text="It must be close now!" },
    @{ File="ending_finally.mp3"; Role="child"; Text="Finally!" }
)

$spokenOverrides = @{
    "opening_01_child.mp3" = "Mom... can I get one?"
    "opening_02_mother.mp3" = "One candy... then we're going."
    "opening_03_child.mp3" = "Did it get stuck?"
    "opening_04_mother.mp3" = "Give it a moment."
    "room04.mp3" = "Maybe... the mechanism needs another push."
    "room10.mp3" = "That sounded like the whole machine..."
    "room17.mp3" = "Something is bouncing around in there!"
    "room24.mp3" = "I hope that cracking sound was normal..."
    "ending_finally.mp3" = "Finally!"
}

$previousPythonPath = $env:PYTHONPATH
try {
    $env:PYTHONPATH = $edgeModule
    foreach ($job in $jobs) {
        $child = $job.Role -eq "child"
        $spokenText = if ($spokenOverrides.ContainsKey($job.File)) { $spokenOverrides[$job.File] } else { $job.Text }
        $excited = $spokenText.Contains("!")
        $question = $spokenText.Contains("?")
        $hesitant = $spokenText.Contains("...")
        $voice = if ($child) { "en-US-AnaNeural" } else { "en-US-AvaMultilingualNeural" }
        if ($child) {
            $rate = if ($excited) { "+8%" } elseif ($question) { "-5%" } else { "+1%" }
            $pitch = if ($excited) { "+8Hz" } elseif ($question) { "+5Hz" } else { "+3Hz" }
        }
        else {
            $rate = if ($hesitant) { "-8%" } elseif ($question) { "-6%" } else { "-4%" }
            $pitch = if ($excited) { "+6Hz" } else { "+3Hz" }
        }
        $output = Join-Path $voiceDirectory $job.File
        & $python -m edge_tts --voice $voice --rate=$rate --pitch=$pitch --text $spokenText --write-media $output
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) {
            throw "Natural voice generation failed for $($job.File)."
        }
    }
}
finally {
    $env:PYTHONPATH = $previousPythonPath
}

Write-Output "NATURAL_STORY_VOICES_PASS: $($jobs.Count) context-shaped neural clips generated with en-US-AnaNeural / en-US-AvaMultilingualNeural."
