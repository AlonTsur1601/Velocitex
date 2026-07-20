# Velocitex

## Short description

Velocitex is a Windows 3D momentum-puzzle game in which you guide a candy through a 28-room candy-machine world. Every slope, surface, launch, turn, and stop is a physical decision: build speed, preserve it, redirect it, and reach the collection cup.

## Long description

Velocitex turns momentum into the language of a puzzle game. You play as a candy ball dropped into a machine, then roll through 30 handcrafted rooms where geometry and material behavior teach the rules without a persistent gameplay HUD.

The campaign begins with readable slopes and safe runouts, then adds deliberate turns, slippery and sticky surfaces, elastic membranes, accelerators, one-way ratchets, pressure plates, rails, cannons, force volumes, moving platforms, magnets, and momentum-storage devices. Later rooms combine those systems while keeping the intended route visible through structure, silhouettes, physical cues, and material treatment.

The Windows build includes first- and third-person cameras, cosmetic candy customization, 20 advancements, room-start and room-complete save snapshots, a room selector, configurable audio and video settings, remappable primary controls, subtitles, reduced motion, flash reduction, high contrast, and trail-visibility options.

Velocitex was built in Godot 4.7 .NET with C#. Codex was part of the development workflow across the game’s implementation, investigation, and verification loop. GPT-5.6, used through Codex, helped translate product constraints into C# and Godot changes, reason about physics and collision cases, and build repeatable tests and export checks. The creator retained control of product, design, and engineering decisions.

The project existed before Build Week. The submission therefore documents the meaningful Build Week extensions separately in `submission/evidence/build_week_changes.md`, with session and implementation evidence in `submission/evidence/codex_and_gpt56_evidence.md`.

## How to test

Download the free Windows build from the project’s GitHub Releases page, extract the entire ZIP, and run `Velocitex.exe`. Keep the executable beside `Velocitex.pck` and the `data_Velocitex_windows_x86_64` folder.

For a short test: start a new campaign, complete Room 01 by rolling down the slope and through the acceleration rings to the collection cup, open the pause menu with Esc, then inspect Customize, Advancements, and Settings from the main menu. Full controls and judge instructions are in the repository README.
