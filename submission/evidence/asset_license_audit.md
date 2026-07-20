# Asset and license audit

## Current result: final sign-off pending

The repository contains executable code, Godot project files, generated visual/audio assets, and engine/runtime dependencies. The following inventory is based on local evidence; it is not a substitute for the creator confirming ownership and the applicable service terms before submission.

| Asset group | Local evidence | Release status |
| --- | --- | --- |
| Gameplay code, rooms, UI, shaders, and Godot resources | `src/`, `resources/`, `scenes/` | Creator project work; confirm ownership. |
| Project visuals and UI assets | `assets/textures/`, `assets/ui/`, `assets/panoramas/` | Locally present; verify origin/rights before sign-off. |
| Sound effects and music | Generation scripts in `scripts/Generate-*.ps1`; implementation record describes synthesized cues | Verify every shipped WAV/MP3 is generated or otherwise authorized. |
| Story voice audio | `scripts/Generate-StoryAudio.ps1`, `scripts/Generate-NaturalStoryVoices.ps1` | Confirm the generation service terms allow the intended public competition and YouTube use. |
| Godot engine and .NET runtime | Project dependencies/toolchain | Include required notices and comply with their licenses in the release if applicable. |
| Fonts | No separately licensed font file has been identified in the repository audit | Confirm the final exported build does not bundle an unreviewed font. |

## Required creator checks before release

1. Confirm that no asset was copied from a game, website, stock library, or creator without a license that covers this use.
2. Confirm that all generated audio and visual assets may be used publicly on YouTube and in a competition submission under the services used.
3. Identify every third-party runtime and include any required license notices.
4. Select a repository license only after the ownership and dependency review is complete.
5. Record the final decision and any required attribution here before publishing the release or video.
