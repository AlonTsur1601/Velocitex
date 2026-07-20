# Velocitex Build Week demo — capture plan

## Capture baseline

- Source executable: `builds/windows/Velocitex/Velocitex.exe`.
- Capture target: 1920×1080 minimum, 60 fps if the capture tool and machine sustain it.
- Capture only the game window. Hide the cursor before each take and close all developer tools, terminals, notifications, and overlays.
- Record separate clean takes with in-game audio enabled. Narration and subtitles are added in editing.
- Do not record or reveal the ending sequence.

## Required takes

| ID | Target duration | Build action | Usable moment |
| --- | ---: | --- | --- |
| G01 | 12–18 s | Room 01 | Third-person descent through acceleration rings into the collection cup. |
| G02 | 10–14 s | Room 02 | Approach, deliberate turn in the basin, and copper climb. |
| G03 | 6–10 s each | Selected early/mid rooms | One clean shot each of elastic bounce, sticky or absorbing stop, and one-way ratchet. |
| G04 | 5–8 s each | Selected mid/late rooms | Ordered plate activation, rail intersection, cannon or moving platform, and magnet/force-volume flight. |
| G05 | 10–15 s | One late room | A multi-step route fragment and a visible success cue; do not show the full solution. |
| U01 | 5–8 s each | Main menu | Main menu, Customize selection/preview, Advancements list, Accessibility settings. |
| U02 | 5–8 s | Play menu | Continue, Load Game, or Room Select; then a clean room transition. |
| E01 | 8–12 s | Repository, not the game | README, implementation record, one build script, one test script, and concise git log. Capture only reviewable project evidence. |
| H01 | 8–12 s | Best room | A clean hero shot for the final call to action and thumbnail candidate. |

## Capture order

1. Record U01 and U02 first from a clean fresh launch.
2. Record G01 and G02 while the early campaign is available.
3. Record G03–G05 as individual takes; restart the room between failed attempts rather than keeping failed footage.
4. Record H01 after choosing the strongest visually clear room.
5. Record E01 last, with no personal paths, account names, browser tabs, terminal prompts, or unreviewable chat content visible.

## Acceptance checks per take

- The game is full-screen or cleanly framed at 16:9.
- No FPS counter, developer UI, cursor, console, notifications, or personal information is visible.
- The mechanism is understandable within the shot and the take contains no accidental pause or failed route.
- In-game audio is clean enough to retain as low-level ambience under narration.
- Save the selected takes with stable names: `G01_room01.mp4`, `G02_room02.mp4`, and so on.
