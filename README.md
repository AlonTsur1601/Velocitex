# Velocitex

Velocitex is a Windows 3D momentum-puzzle game built with Godot 4.7 .NET and C#. You control a candy through a 28-room candy-machine world where slopes, surface materials, launches, and deliberate turns determine whether you reach the exit.

## Demo video

Watch the Velocitex demo video on YouTube:  
[Watch the public demo video](https://youtu.be/zOM79yaOCXk)

## Play the Windows build

The downloadable Windows build is distributed through the [GitHub Releases page](https://github.com/AlonTsur1601/Velocitex/releases/latest). Once a release is published, use the latest release asset below.

1. Download `Velocitex_Windows.zip` from the latest release.
2. Extract the entire ZIP to a writable local folder.
3. Run `Velocitex.exe`.

Do not move `Velocitex.exe` away from `Velocitex.pck` or the accompanying `data_Velocitex_windows_x86_64` folder.

### System requirements

- Windows 10 or Windows 11, 64-bit.
- A 64-bit CPU and graphics hardware capable of running Godot's Compatibility renderer.
- Keyboard and mouse.

## Controls

- **WASD** or **Arrow Keys** - move while grounded.
- **Mouse** - look around.
- **C** - toggle first-person and third-person camera.
- **Middle Mouse** - hold to zoom.
- **E** - interact with a highlighted device.
- **Esc** - open the pause menu; use **Restart Room** there to restart the current room.

## What to try

- Start a new campaign and follow the room-to-room loading cards.
- Build speed on slopes, then preserve it through the exit route.
- Learn how frictionless, sticky, elastic, absorbing, accelerator, and one-way surfaces change movement.
- Use switches, levers, pressure plates, rails, cannons, force volumes, moving platforms, and momentum devices without relying on a persistent gameplay HUD.
- Open **Customize**, **Advancements**, and **Settings** from the main menu. Settings include video, gameplay, controls, audio, and accessibility options.
- Test campaign snapshots through **Play > Continue** or **Load Game**.

## Judge testing path

For a concise functional test, launch the Windows build and verify the following:

1. In Room 01, roll down the initial slope, pass through the required acceleration rings, and reach the exit.
2. Open the pause menu with **Esc**, then verify that the restart action returns the player to the room start.
3. From the main menu, inspect **Customize**, **Advancements**, and **Settings**.
4. Complete a room, return to the menu, and verify that campaign progress is available through Continue or Load Game.

Automated verification recorded in `IMPLEMENTATION_STATUS.md` includes a clean build, packaged startup smoke, 30 solution traces completed ten times each (300 total completions), bypass rejection, campaign-flow checks, save checks, UI checks, and room-shell/surface checks. These automated checks do not replace the manual gameplay path above.

## Architecture

- `src/Core` - engine-facing contracts, input, settings, profiles, saves, room definitions, and physics profiles.
- `src/Gameplay` - player physics, camera, interactions, rooms, reusable devices, and surface behavior.
- `src/UI` - menus, loading flow, customization, advancements, accessibility, and panorama presentation.
- `src/Story` - opening and ending sequences.
- `resources` - Godot resources, shaders, materials, force volumes, surface profiles, and deterministic solution traces.
- `scripts` - repeatable PowerShell build, export, capture, and smoke-test commands.

## Build and local verification

The project uses a portable local Godot toolchain under `.tools/Godot`; that toolchain is intentionally excluded from source control.

```powershell
.\scripts\Build.ps1
.\scripts\Invoke-Godot.ps1 -Mode HeadlessCheck
.\scripts\Test-Movement.ps1
.\scripts\Test-UI.ps1
.\scripts\Test-Saves.ps1
.\scripts\Test-CampaignFlow.ps1
.\scripts\Test-Profile.ps1
.\scripts\Test-Advancements.ps1
.\scripts\Invoke-Godot.ps1 -Mode Editor
```

## How Codex was used

Velocitex was developed in collaboration with Codex across one primary development thread and two additional major threads. Codex was used to turn product decisions into implementation work, build the portable Godot/C# workflow, implement and iterate on player movement and room mechanics, create repeatable PowerShell test and export scripts, investigate failing physics and room-route cases, and maintain the implementation record.

The primary Build Week `/feedback` Session ID is:

```text
019f5659-1238-7ce2-927c-da0d7c36a4cc
```

Additional major development-thread IDs:

```text
019f78f6-08f5-70a0-9302-28f446158ff7
019f7f4b-9c5e-7272-8eb9-545b3d9d4af0
```

## How GPT-5.6 was used

GPT-5.6 was used through Codex as a substantial part of the development workflow: translating design constraints into C# and Godot changes, reasoning through momentum and collision issues, generating and refining repeatable verification coverage, and helping evaluate room progression, UI flow, accessibility, and release-readiness decisions. The documented implementation and verification milestones in `IMPLEMENTATION_STATUS.md`, together with the listed Codex session IDs, are the project evidence for that workflow.

Product, design, and engineering decisions remained under the creator's direction. Examples include the no-checkpoint-within-a-room recovery rule, surface language that does not rely only on color, the 28-room progression, camera behavior, and the focus on real build and smoke-test verification rather than build success alone.

## Project history and Build Week evidence

Velocitex existed before the Build Week submission period. The submission materials therefore distinguish pre-existing work from meaningful work completed during the period and must cite dated, verifiable Codex/GPT-5.6 evidence. See `SUBMISSION_CHECKLIST.md` and the planned evidence documents before making submission claims.

## Assets and licensing

All assets included in a release or submission must be verified as original, generated under terms that allow this use, or used under a documented compatible license. A complete asset-license audit is still required before the final Build Week submission and before selecting a repository license. Do not reuse repository assets outside the project until that audit and license decision are complete.

## Status

`IMPLEMENTATION_STATUS.md` records completed milestones, known test-harness behavior, and the exact current continuation point.
