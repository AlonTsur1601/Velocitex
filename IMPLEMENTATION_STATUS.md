# Velocitex implementation status

## Current unit

- Stage: final all-room playtest corrections, ending credits, audio replacement and visual QA
- Status: complete; implementation, visual QA, full regression and refreshed Windows export all pass
- Usage checkpoint: the account meter is not exposed to the runtime; work is being kept in independently buildable, tested room units

## Completed

- Portable Godot 4.7 .NET downloaded under `.tools/Godot`.
- Executable version and Windows code signature verified.
- Godot C# project, solution, bootstrap scene and core system contracts created.
- Project-local .NET and NuGet cache policy configured.
- Compatibility renderer and 60 Hz physics configured.
- Repeatable PowerShell build and Godot launch scripts created.
- Portable editor placed in self-contained mode via its `_sc_` marker.
- Git repository initialized; tools, caches and generated outputs confirmed ignored.
- Camera-relative rigid-body ground motor added with contact-only ground detection.
- Airborne input is ignored and externally supplied momentum is not clamped to drive speed.
- Independent mouse-look camera rig added with first/third-person switching, camera collision and hold-to-zoom.
- Gray-box movement room added with a floor, walls, ramp and upper platform; room restart is available through the pause menu.
- Runtime input defaults added for WASD/arrows, C, middle mouse, E and R.
- Themed application shell added with main menu, pause menu and explicit camera/input boundaries.
- Main menu now exposes Play, Customize, Advancements, Settings and Quit; future profile features use clear temporary dialogs.
- Pause menu exposes Resume, Restart, Load Game, Customize, Advancements, Settings and Return to Main Menu.
- Settings are split into Video, Gameplay, Controls, Audio and Accessibility tabs.
- Video settings include 30/60/120/Unlimited FPS, resolution, fullscreen, VSync, four quality presets, render scale, MSAA and shadows.
- Gameplay and accessibility settings include sensitivity, invert Y, default camera, camera shake amount, prompts, subtitle options, reduced motion, flash reduction, high contrast and trail visibility.
- Primary keyboard bindings can be remapped while arrow keys remain secondary movement controls; duplicate bindings are rejected.
- Settings persist through `user://settings.cfg`; Master, Music, SFX and Voice buses are configured separately.
- Campaign snapshots use fixed Room Start and Room Complete slots, capped at 60 visible saves for 30 rooms.
- Snapshot writes are flushed to disk and atomically replaced; the previous valid version remains as a hidden recovery backup.
- Invalid snapshots are skipped, valid backups are restored automatically and New Game only removes campaign snapshot files.
- Runtime snapshots include room identity, kind, local timestamp, cumulative play time and a 256x144 thumbnail.
- Play now opens Continue, Load Game, Room Select and New Game; pause-menu Load Game uses the same save browser.
- Loading a Room Start snapshot resumes that room without immediately overwriting the selected record.
- Loading a Room Complete snapshot restores the completion transition toward the next available room.
- The gray-box room now has a visible goal cup and emits a generic RoomRuntime completion signal.
- Completed-room snapshots unlock matching available rooms in Room Select.
- Player-profile persistence is separate from campaign snapshots and settings, so New Game cannot remove cosmetics or advancements.
- The base cosmetic catalog contains 8 colors, 5 patterns and 5 trail choices, all unlocked by default.
- Profile selections are normalized by cosmetic kind and unlock state; invalid selections fall back without discarding unrelated unlocks.
- Profile writes are atomic and retain a prior backup for corruption recovery.
- The advancement catalog contains the 20 planned achievements with concise conditions and one unique locked cosmetic reward each.
- Advancement unlocks are idempotent, reject unknown IDs and add both the advancement and its reward to the persistent profile.
- The cosmetic catalog now contains the 18 base items plus 20 advancement rewards while keeping only the base set initially unlocked.
- A dedicated Advancements overlay is available from both the main menu and pause menu.
- The overlay reads the persistent profile and lists all 20 advancements with their condition, cosmetic reward and Locked or Complete state.
- Advancement progress is summarized at the top, while completed entries also use a distinct label, border and background rather than color alone.
- Closing Advancements returns to the menu it was opened from; opening it during gameplay preserves the paused state.
- The main menu no longer contains a slogan or genre eyebrow; its build label now reports `DEVELOPMENT BUILD / STAGE 6A`.
- The Velocitex wordmark uses a glazed candy ball as the `O` and forces left-to-right layout so Hebrew system locales cannot reverse it.
- A lightweight 3D candy-machine background rolls the candy down a textured caramel ramp behind the menu; Reduced Motion freezes the decorative animation.
- Room entry now shows a themed loading overlay for at least one rendered frame, with room identity, the candy-ball wordmark and a restrained progress animation.
- The boot splash uses the same candy-ball mark in a PNG format supported by Godot exports.
- Reusable brushed-metal, caramel-plate, rubber-chevron and sugar-glaze textures were added and applied to the menu scene, current test room and player candy.
- The visual performance target is now 60 FPS at Medium settings on an average laptop; Low remains available without driving the art direction toward extreme potato hardware.
- Customize is available from both the main menu and pause menu and returns to the correct origin without unpausing gameplay.
- The customization screen shows an isolated rotating 3D candy preview with primary color, secondary color, pattern and trail selectors.
- Selectors contain only cosmetics unlocked in the persistent profile; the complete base set starts with 8 colors, 5 patterns and 5 trail choices.
- The shared candy shader supports every base and advancement pattern, adds subtle sugar grain and keeps cosmetic choices independent from physics.
- Saving a look atomically updates the player profile and immediately applies the same colors, pattern and optional world-space trail to the gameplay candy.
- The accessibility trail toggle suppresses the gameplay trail, and Reduced Motion stops both the menu background and candy-preview rotation.
- New Game now begins with a stylized in-engine opening in which the child and mother operate the candy machine and the player candy falls through a loose maintenance hatch into the stand.
- The opening uses four short English subtitle lines, hides the subtitle panel between lines and has no player-facing skip input.
- The opening is connected to the subtitle accessibility setting and hands control to Room 01 only after the sequence finishes.
- Room 01 is now a distinct coin-mechanism environment rather than a gray box, with a raised rubber start deck, one readable caramel slope, a brushed-metal runout and a collection cup.
- Animated gears, an inserted coin and slot, exposed rails, brackets and overhead braces establish the chapter identity without becoming collision obstacles or adding puzzle rules.
- Room 01 teaches movement, slope momentum and the collection cup through its safe start, route silhouette, ramp, rails and visible destination rather than room text.
- Gameplay rooms do not carry a persistent HUD; room identity remains in the non-playable transition card and pause menu.
- The third-person camera has a full 5.5-metre clearance behind the starting point and the intended route is visible from spawn.
- `resources/solutions/room_01_solution.tres` records the steady-forward Room 01 solution and explicitly holds its final input.
- The Room 01 solution runner resets the real room and requires ten consecutive collection-cup completions within a fixed per-run tick budget.
- Room 02, `The Copper Bend`, is playable and follows Room 01 automatically through the existing completion and loading flow.
- Room 02 uses one long descent, a protected turning basin and a perpendicular copper climb to teach slope speed and a deliberate 90-degree ground turn without adding a new surface rule.
- The Room 02 route communicates its descent, right turn, copper climb and collection cup through geometry, rails, material changes and a visible destination.
- Room 02 is visually distinct from Room 01 through a tall teal-and-copper chamber, a moving coin lift, animated flywheel, riveted wall silhouette and L-shaped route.
- The reusable `copper_rivets.svg` material includes plate seams, scratches and physical rivets, so the new environment is not identified by flat color.
- A small reusable `RoomGeometry` builder now creates textured collision boxes, visual boxes, cylinders and non-colliding gears for later rooms without duplicating scene boilerplate.
- `SolutionTrace` supports compressed input segment durations; Room 02 records a forward descent followed by a right turn while Room 01 remains backward-compatible with its held input.
- `resources/solutions/room_02_solution.tres` completes the real Room 02 collection-cup trigger ten consecutive times within a fixed tick budget.
- Room catalog entries now carry a post-room speaker and dialogue line; Room 01 displays the child's `Is it still coming?` before loading Room 02.
- The main-menu development label and its UI assertion now report `DEVELOPMENT BUILD / STAGE 6B.1`.
- Rooms 01-06 contain no room name, tutorial sentence, objective text or mechanism-status text during normal play.
- Both collection cups now sit inside tall mechanical exit arches with a distinct silhouette, downward light chevrons, emissive strips and local illumination, so the destination is not communicated by color alone.
- Ambient, key and practical lighting was raised across the menu background, customization view, opening and both playable rooms while preserving each chapter's palette.
- The five shared environment textures were rebuilt as original 512-pixel layered materials with modular panel seams, fasteners, glaze, patina, moulded tread, scratches, scuffs and surface variation instead of flat repeating color blocks.
- The candy shader now adds continuous glaze variation, fine and broad sugar grain, rounded crystals and pores through sphere-space noise, making physical rotation readable without a visible UV or mould seam even with the `None` cosmetic pattern.
- The gameplay sphere mesh was raised to 48 radial segments and 24 rings; this remains a negligible geometry cost for the average-laptop performance target.
- The save implementation was audited against the written specification: 30 rooms x 2 fixed snapshots, atomic replacement, retained recovery backups, corruption recovery, thumbnails, Continue, Load Game, Room Select and profile separation are all present and covered by smoke tests.
- The main-menu development label and its UI assertion now report `DEVELOPMENT BUILD / STAGE 6B.1R`.
- Long and narrow box surfaces now use independent per-face world-scaled UVs, so the shared materials tile consistently instead of stretching across the geometry.
- Brushed metal, caramel, copper and rubber now use cleaner reusable base materials; localized grime, scratches, cracks and drips are layered independently so repeated structures do not share identical wear.
- Room 01, Room 02 and the opening use the tiled mesh path, anisotropic mipmapped filtering and intentionally placed wear overlays rather than baking the same damage into every texture repeat.
- The candy shader now has stronger macro, medium and fine surface variation, irregular sugar crust, crystals, pores and a readable mould seam; its metallic/specular response was reduced so it no longer produces a sharp sun-like white reflection.
- Room 01 now begins on a longer ordinary brushed-metal safe platform. Most decorative machinery and environmental wear were moved deeper into the room, leaving a calm spawn area before the slope introduces momentum.
- Room loading now presents the next room number and name in a compact top card that slides and fades in, then clears before control is returned; the normal upper room HUD remains hidden.
- The main menu now cycles through current static room panoramas with slow horizontal motion and crossfades. Selection is randomized while preventing two consecutive panoramas from the same room.
- Four current panorama views cover Rooms 01-02. `scripts/Update-Panoramas.ps1` regenerates them, while `scripts/Test-Panoramas.ps1` rejects missing or stale captures after room or material changes.
- The old live menu ramp was replaced by the panorama system, Reduced Motion freezes its pan, and the backdrop was lightened so the environment remains readable behind the menu panel.
- The Velocitex wordmark keeps the candy-textured sphere as its `O`; the revised mark removes the artificial white highlight and exposes the same irregular sugar identity as the player candy.
- The main-menu development label and its UI assertion now report `DEVELOPMENT BUILD / STAGE 6B.1P`.
- Shared box materials now cover 4.0 metres per texture repeat instead of 2.4 metres, producing substantially larger structural panels while retaining independent world-scaled UVs on every face.
- Grime, scratches, cracks and drips now contain more secondary marks and breakup. Four additional reusable overlays add oil rings, edge scuffs, copper patina and sugar dust.
- Rooms 01-02 and the opening now use denser, location-specific combinations of wear overlays. The Room 01 spawn deck remains deliberately clean while wear accumulates on the slope, runout, walls and exit area.
- All four menu panoramas were regenerated from the revised rooms and the freshness test now tracks every shared overlay source.
- Room 03, `The Open Chute`, is playable and follows Room 02 through the campaign flow with its own child dialogue and paired snapshot integration.
- The room starts on a calm normal platform, uses one caramel descent to build speed, forces a short airborne gap with no mid-air correction, provides a broad teal landing and ends with a short recovery run into the collection cup.
- Two thin illuminated guide hoops and three textured landing chevrons communicate the airborne line without upper-screen instructions or a new surface rule.
- The gap remains physically unavoidable. The Room 03 solution runner additionally rejects any completion that did not record an airborne crossing between the launch edge and landing.
- `resources/solutions/room_03_solution.tres` completes the real room 10 consecutive times using one held-forward input segment.
- Two Room 03 panoramas were added, bringing the randomized main-menu catalog to six current views while preserving the no-consecutive-room rule.
- The main-menu development label and its UI assertion now report `DEVELOPMENT BUILD / STAGE 6B.2`.
- Automated room, candy and panorama capture modes explicitly disable camera input and keep the system pointer visible. Normal gameplay still captures the pointer and connects mouse motion to the camera.
- Shared structural surfaces now use a 12-metre world-space repeat. Their base SVGs are seamless fine-grain materials without baked panel borders, while independent wear overlays continue to provide local grime, scratches, cracks, patina and sugar residue.
- Rooms 01-03 use substantially brighter ambient, directional and broad fill lighting. A second neutral fill covers each destination area without turning the destination itself into a beacon.
- The former luminous exit arches were replaced in all three rooms by the same clearly framed industrial double door, with two physical leaves, a centre seam, handles, threshold and non-emissive header markings.
- Exit-door lighting was reduced to a weak local fill. Each door is placed in the starting camera's line of sight and remains readable from spawn through silhouette, structure and neutral room illumination rather than color or glow.
- All six menu panoramas were regenerated after the material, lighting and exit changes.
- Rooms 01-03 now sit inside complete physical shells with a high non-obstructing ceiling, four enclosing outer walls and a continuous maintenance floor below every playable structure.
- Open voids were replaced by a reusable mechanical hazard floor. Its grate texture uses recessed grinder slots, interlocking teeth, fasteners and striped guards, plus a very weak self-fill so it remains visible in shadow instead of reading as an empty black hole.
- Room 03's failure floor sits close enough beneath the airborne tutorial gap to expose the existing crusher fins, while remaining safely below the intended trajectory.
- Contact with any maintenance hazard floor now restarts the current room immediately; the old low-height checks remain only as an unreachable fallback.
- Ceiling and shell meshes do not cast exterior-blocking shadows, preserving the brighter room lighting while still providing collision and a closed-space silhouette.
- `scripts/Test-RoomShells.ps1` was added to place the real player on every hazard trigger and verify a return to that room's spawn.
- Room 04, `The Relay Lock`, is playable and follows Room 03 through the campaign flow with its own mother dialogue and paired snapshot integration.
- The room introduces the shared `E` interaction through one mechanical lever beside a familiar gentle descent; the known movement remains secondary and no other new rule is introduced.
- The destination door is visible from the safe starting platform through a slatted relay gate. A physical crossed latch communicates the locked state, while lever motion and the rising gate communicate activation without words.
- The sole in-room text is the rebound interaction key in brackets. It appears only when the lever is both inside its usable radius and inside the camera's focus cone, and it respects the prompts and high-contrast settings.
- `MechanicalLever` implements `IInteractable`, exposes deterministic activation/reset behavior and combines a distinct pedestal, moving handle, focus ring, world key label and mechanical animation instead of relying on color.
- Room 04 has the same complete wall, high ceiling and visible maintenance-hazard floor treatment as Rooms 01-03, plus two animated relay-coil silhouettes and localized surface wear.
- `resources/solutions/room_04_solution.tres` contains an explicit interaction action and cannot complete the room without activating the lever.
- Two Room 04 panoramas were added, bringing the randomized main-menu catalog to eight current views, and the development label now reports `DEVELOPMENT BUILD / STAGE 6B.3`.
- Room 05, `The Proof Run`, completes the first five-room chapter without introducing a new mechanic.
- Its calm start places the known `E` lever beside the player, then presents one released start gate, one readable caramel launch slope, one unavoidable airborne gap, a broad landing and the collection door on a single visible axis.
- The real goal requires both the start lever and a recorded airborne crossing, preventing a trace or shortcut from completing only part of the chapter test.
- Room 05 reuses the gaze-gated interaction key and accessibility settings, hides it from menu panoramas and retains the full wall, high ceiling and visible maintenance-hazard floor treatment.
- Two tall inspection arches, copper gate slats, restrained guide hoops and localized wear distinguish the room by silhouette and material without adding puzzle clutter.
- `resources/solutions/room_05_solution.tres` records the lever release followed by the committed forward run and completes the real room 10 consecutive times.
- The campaign catalog, Room 04 mother dialogue, Room 05 Room Start snapshot and room transition are integrated; ten current panoramas now feed the randomized menu and the build label reports `DEVELOPMENT BUILD / STAGE 6B.4`.
- Ground contacts can now carry a real `SurfaceProfile` through `ProfiledSurfaceBody`; the player reports the active surface kind and derives available ground acceleration and braking from its traction while preserving the existing no-air-control rule.
- Standard rooms retain their original traction. The frictionless profile sets both the Godot contact friction and player drive traction to a very low value, so world momentum is preserved and steering or braking becomes deliberately weak instead of being a visual-only effect.
- Room 06, `Glass Drift`, introduces only this new surface after a calm normal start and a shallow familiar approach; the route remains straight and the exit door is visible from spawn.
- The glass is identifiable without color through transparency, diagonal etched grooves, moving sheen bands, a recessed framed silhouette and repeated physical edge clamps.
- The transparent span exposes its sparse underframe and the visible maintenance floor below; enclosing walls, a high ceiling and rails keep the room legible as a closed machine chamber.
- `resources/solutions/room_06_solution.tres` builds speed on the normal approach, releases all movement input while on glass and must coast through the real goal. The room refuses completion unless it detected both the frictionless contact and the no-input coast.
- Room 05 completion dialogue and transition into Room 06 are integrated with the eleventh campaign snapshot. Two Room 06 panoramas bring the menu catalog to twelve views, and the readable build label reports `DEVELOPMENT BUILD / STAGE 7A.1`.
- The hold-`R` restart shortcut was removed from the input map, settings model, settings serialization, controls-rebinding screen and all six room runtimes.
- All bottom `HOLD R` hints were removed. `Esc` still pauses the game and its `Restart Room` button remains connected to the room's existing reset method, making it the only deliberate manual restart route.
- Old `restart_room` keys in an existing settings file are ignored and disappear on the next settings save.
- The six gameplay scene files no longer contain their former room-title, tutorial, instruction or gate-status UI trees. `MechanicalLever` owns the only remaining room `Label3D`, displaying only the rebound interaction key.
- Large structural surfaces now combine seamless 12-metre base tiling with a full-face micro-grain layer and deterministic localized wear. Material families select distinct scratches, grime, edge scuffs, patina, sugar dust, drips or oil marks so neighboring rooms and repeated structures do not share identical damage.
- The shared brushed metal, copper, caramel, rubber, hazard and glass sources include authored pores, flecks, wipe marks, oxidation, fine abrasion and small tonal breakup rather than relying on a flat albedo color.
- All twelve main-menu panoramas were regenerated from the text-free rooms and layered materials. The freshness test now also tracks the micro-grain source.
- The flat SVG pattern sources were replaced by deterministic raster materials after the Godot Compatibility importer proved unable to preserve their dense SVG pattern detail reliably.
- `scripts/Generate-MicroTextures.ps1` now bakes original, seeded and seamless 2048-pixel material maps: industrial concrete with fine aggregate, pores and hairlines; diamond plate with individually worn raised lozenges; and brushed metal with dense directional millimetre-scale abrasion and pits.
- Separate 512-pixel transparent concrete-grain and metal-wear overlays add very small local variation without forcing identical large damage marks onto every repeated surface.
- Material assignment is structural rather than color-based: industrial concrete is limited to large walls, bulkheads and ceilings; diamond plate is limited to broad, thin walkable metal decks; and brushed metal remains on rails, braces, mechanisms and narrower fabricated parts.
- The five new raster materials use mipmapped S3TC imports, while wrapped edge drawing makes their tiling seamless at the 12-metre world-space repeat already used by room geometry.
- All twelve menu panoramas were regenerated after the material replacement so their room views match the current concrete, tread plate and brushed-metal treatment.
- The material motifs were rescaled after an in-game 1280x720 review: diamond lozenges are about one fifth of their former world size, concrete aggregate and pores are millimetre-to-centimetre details, and the longest common metal abrasions are now only a few centimetres instead of tens of centimetres.
- The seamless base tile remains 12 metres to preserve the broad non-repeating surface breakup, while the transparent micro-detail layer repeats every 3 metres so close-range grain stays physically small.
- Former large concrete mottle circles were replaced by 1,800 much smaller low-opacity tonal variations, and automatic localized dirt/scratch decals were reduced to 32 percent of their previous dimensions.
- `SurfaceProfile.LinearDrag` now affects grounded player motion instead of remaining unused configuration data.
- Ground drag applies frame-rate-independent exponential damping only to velocity along the contacted surface; it does not alter gravity, the normal component of a fall or the no-air-control rule.
- The player exposes the selected ground drag alongside surface kind and traction, resets it cleanly, rejects negative values and resolves equal-traction contacts in favor of the profile with meaningful drag.
- Movement smoke mode creates a collision-only sticky pad outside the playable room, launches the real player across it with no input and verifies both sticky-profile detection and measured momentum loss without adding unfinished Room 07 geometry.
- `resources/surfaces/sticky.tres` is now the production `Viscous Caramel` profile with sticky kind, `0.9` contact friction and `2.4` frame-rate-independent grounded linear drag.
- Movement smoke no longer constructs its own duplicate sticky settings; it loads and validates the production resource that Room 07 will use, preventing the tested behavior from drifting away from the shipped configuration.
- `resources/shaders/sticky_caramel.gdshader` and `resources/materials/sticky_caramel.tres` provide the production visual material for Room 07 without adding unfinished room geometry.
- The original procedural material uses only small periodic details: fine caramel grain, tiny pores, bubbles and narrow viscous strands, with seamless integer-frequency sampling and restrained roughness variation.
- Slow UV deformation gives the caramel a viscous motion cue, while the exposed `motion_scale` parameter can be set to zero when Reduced Motion is enabled.
- Movement smoke loads the production material onto its off-room sticky test pad and rejects a missing or invalid shader, keeping the visual and physics resources exercised together.
- `scripts/Generate-StickySfx.ps1` deterministically synthesizes the original `surface_sticky_contact.wav` asset as a 0.42-second, mono, 44.1 kHz, 16-bit viscous contact cue.
- The sound combines a descending soft-clipped body tone, filtered granular texture and two restrained adhesion snaps, avoiding speech, music and third-party audio assets.
- The movement-smoke sticky pad loads the production WAV into a positional `AudioStreamPlayer3D` routed through the existing `SFX` bus and rejects a missing audio resource; actual enter/roll/exit playback behavior remains scoped to Room 07 integration.
- `RoomGeometry.AddBox` now accepts an optional Godot `Material` together with its existing optional `SurfaceProfile`, allowing one production call to create tiled geometry, collision, contact physics and a custom shader without duplicating the room builder.
- The movement-smoke sticky pad now uses this production geometry path with the verified sticky profile and caramel shader; its only remaining test-specific addition is the positional SFX node.
- The sticky contact cue is now true stereo: its low viscous body remains centered for positional clarity, while independently filtered fine texture and the two adhesion snaps receive restrained opposing left/right emphasis.
- The deterministic generator writes an interleaved two-channel PCM WAV with correct stereo channel count, byte rate and block alignment rather than duplicating a mono channel.

## Verification

- `scripts/Build.ps1`: succeeded with 0 warnings and 0 errors.
- `scripts/Invoke-Godot.ps1 -Mode HeadlessCheck`: succeeded.
- Runtime output confirmed Godot `4.7.stable.mono.official.5b4e0cb0f` and 60 Hz physics.
- Authenticode status: valid; signer `Prehensile Tales B.V.`.
- Git ignore verification: `.tools`, `.packages` and `.godot` are excluded.
- Stage 2 build: succeeded with 0 warnings and 0 errors.
- Godot headless import/startup check: succeeded.
- `scripts/Test-Movement.ps1`: passed ground acceleration, external-momentum preservation, no-air-control and camera-mode state checks.
- Stage 3 build: succeeded with 0 warnings and 0 errors.
- Stage 3 Godot headless import/startup check: succeeded.
- `scripts/Test-UI.ps1`: passed menu flow, pause/resume boundary, five-tab layout bounds, complete settings persistence, key rebinding and 120 FPS application.
- Stage 2 movement smoke test passed again after the application-shell integration.
- Stage 4 build: succeeded with 0 warnings and 0 errors.
- Stage 4 Godot headless import/startup check: succeeded.
- `scripts/Test-Saves.ps1`: passed all 60 slots, fixed-slot overwrite, thumbnail output, latest selection, retained backup and corruption recovery.
- `scripts/Test-CampaignFlow.ps1`: passed automatic Room Start, Room Complete and Room Select unlock integration.
- Stage 3 UI and Stage 2 movement smoke tests passed again after save-flow integration.
- Stage 5A build: succeeded with 0 warnings and 0 errors.
- `scripts/Test-Profile.ps1`: passed the 18-item base catalog, JSON round-trip, invalid-selection normalization, advancement preservation and backup recovery.
- Stage 5A.2 build: succeeded with 0 warnings and 0 errors.
- `scripts/Test-Advancements.ps1`: passed 20 unique definitions, 20 unique locked rewards, duplicate rejection and profile persistence.
- The profile smoke test passed again after adding advancement rewards.
- Stage 5B.1 build: succeeded with 0 warnings and 0 errors.
- Stage 5B.1 Godot headless import/startup check: succeeded.
- `scripts/Test-UI.ps1`: passed the 20-row advancement list, persisted progress count, viewport bounds, main-menu return path and pause-menu boundary.
- `scripts/Test-Advancements.ps1`: passed again after the Advancements UI integration.
- `git diff --check`: passed; the UI smoke profile and campaign files were removed after the test.
- Visual identity build: succeeded with 0 warnings and 0 errors.
- Godot imported all SVG and PNG visual assets successfully; the PNG boot splash removed the unsupported-format startup error.
- `scripts/Test-UI.ps1`: passed logo order, slogan removal, Stage 5B label, loading lifecycle, Reduced Motion, menu/game camera boundary and prior UI checks.
- `scripts/Test-Movement.ps1` and `scripts/Test-CampaignFlow.ps1`: passed after textured materials and asynchronous room loading were integrated.
- A six-frame 1280x720 capture rendered successfully on Intel UHD Graphics and visually confirmed the LTR wordmark, rolling ball, textured ramp and readable menu composition.
- Temporary capture frames and audio were removed after inspection; `git diff --check` passed.
- Stage 5B.2 build: succeeded with 0 warnings and 0 errors.
- `scripts/Test-UI.ps1`: passed the 8/8 color selectors, 5 patterns, 5 trails, preview synchronization, profile persistence, main/pause return paths and gameplay candy application.
- `scripts/Test-Profile.ps1` and `scripts/Test-Advancements.ps1`: passed after the shared cosmetic shader integration.
- `scripts/Test-Movement.ps1` and `scripts/Test-CampaignFlow.ps1`: passed after the player material and world-space trail changes.
- A stable 1280x720 viewport capture visually confirmed the isolated 3D preview, Blueberry/Vanilla spiral, Cyan Trail and readable control layout.
- Visual QA captures were removed after inspection; the final headless import and `git diff --check` passed.
- Stage 6A build: succeeded with 0 warnings and 0 errors.
- `scripts/Test-Opening.ps1`: passed scene construction, full unskippable timeline and the handoff after `Finished`.
- `scripts/Test-Room01Solution.ps1`: the same `SolutionTrace` completed the real collection-cup trigger 10 consecutive times.
- `scripts/Test-Movement.ps1`: passed ground acceleration, external-momentum preservation, no-air-control and camera visibility after the Room 01 replacement.
- `scripts/Test-UI.ps1`: passed the updated `DEVELOPMENT BUILD / STAGE 6A` label and all prior menu, settings, customization and 120 FPS checks.
- `scripts/Test-Saves.ps1`, `scripts/Test-CampaignFlow.ps1`, `scripts/Test-Profile.ps1` and `scripts/Test-Advancements.ps1`: all passed after the Stage 6A integration.
- `scripts/Invoke-Godot.ps1 -Mode HeadlessCheck`: succeeded after Godot imported the opening, Room 01 and SolutionTrace resources.
- 1280x720 captures rendered on Intel UHD Graphics and visually confirmed the opening composition, textured coin-mechanism identity, LTR tutorial HUD, camera clearance and straight readable route.
- Stage 6B.1 build: succeeded with 0 warnings and 0 errors.
- `scripts/Test-Room02Solution.ps1`: the two-segment Room 02 `SolutionTrace` completed 10 consecutive runs through the real goal trigger.
- `scripts/Test-CampaignFlow.ps1`: verified the Room 01 child dialogue, Room Complete snapshot, transition to Room 02 and Room 02 Room Start snapshot.
- `scripts/Test-Room01Solution.ps1`: Room 01 still completed 10 consecutive runs after the compressed trace extension.
- `scripts/Test-Movement.ps1`, `scripts/Test-Opening.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1` and `scripts/Test-UI.ps1`: all passed after Room 02 integration.
- `scripts/Invoke-Godot.ps1 -Mode HeadlessCheck`: imported the copper texture and loaded both room scenes successfully.
- A 1280x720 Intel UHD Graphics capture visually confirmed Room 02's distinct color/material family, readable long descent, visible perpendicular exit ramp, stable LTR HUD and unobstructed third-person camera.
- Stage 6B.1R build: succeeded with 0 warnings and 0 errors.
- Godot reimported all five rebuilt SVG materials and the revised candy shader without errors.
- `scripts/Test-Movement.ps1`, `scripts/Test-Room01Solution.ps1` and `scripts/Test-Room02Solution.ps1`: passed; both real room traces still completed 10 consecutive runs.
- `scripts/Test-Saves.ps1`: passed all 60 fixed slots, atomic overwrite, latest selection, thumbnail output and backup recovery after the save audit.
- `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-Opening.ps1` and `scripts/Test-CampaignFlow.ps1`: all passed after the visual revision and stage-label update.
- `scripts/Invoke-Godot.ps1 -Mode HeadlessCheck`: succeeded after the final shader refinement.
- Final 1280x720 captures on Intel UHD Graphics confirmed brighter readable geometry, no upper HUD text, shape-led exit beacons and an organic non-pixelated sugar surface on the gameplay ball.
- Stage 6B.1P build: succeeded with 0 warnings and 0 errors.
- `scripts/Invoke-Godot.ps1 -Mode HeadlessCheck`: succeeded after importing the four wear overlays, tiled mesh path, panorama images and revised menu scene.
- `scripts/Test-Movement.ps1`, `scripts/Test-Room01Solution.ps1`, `scripts/Test-Room02Solution.ps1`, `scripts/Test-Panoramas.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-Opening.ps1` and `scripts/Test-CampaignFlow.ps1`: all passed.
- Both real room `SolutionTrace` files still completed their collection cups 10 consecutive times after the Room 01 simplification and mesh replacement.
- The panorama freshness test passed after regenerating all four views from the final room and material sources; the UI smoke test also proved that an advance cannot repeat the current room key.
- Final 2560x720 room panoramas and 1280x720 UI/gameplay captures rendered on Intel UHD Graphics. Visual inspection confirmed per-face tiling, varied local wear, a calm Room 01 start, the top-entering room card, the candy `O`, and visibly different candy surface orientation across two physical roll frames.
- Stage 6B.1P2 build succeeded with 0 warnings and 0 errors; Godot imported all eight wear overlays without errors.
- Room 01 and Room 02 `SolutionTrace` tests still completed the real collection cups 10 consecutive times after the UV scale and overlay changes.
- `scripts/Test-Panoramas.ps1`, `scripts/Test-Opening.ps1` and `scripts/Test-Movement.ps1` passed after the visual revision.
- Fresh 1280x720 room captures and four 2560x720 panoramas rendered on Intel UHD Graphics. Visual inspection confirmed larger panels, a clean Room 01 spawn and varied localized wear deeper in both rooms.
- Stage 6B.2 build succeeded with 0 warnings and 0 errors; the final Godot headless import/startup check succeeded after importing Room 03 and all six panoramas.
- `scripts/Test-Room03Solution.ps1` crossed the mandatory airborne gap and completed the real collection cup 10 consecutive times.
- `scripts/Test-CampaignFlow.ps1` now validates Room 01 completion, Room 02 completion and transition into Room 03, including assigned dialogue and all five expected snapshots.
- `scripts/Test-Movement.ps1`, all three room solution tests, `scripts/Test-Panoramas.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-Opening.ps1` and `scripts/Test-CampaignFlow.ps1` passed in the final regression run.
- Six final 2560x720 panoramas and 1280x720 Room 03/menu captures rendered on Intel UHD Graphics. The capture runs kept camera input disabled and left no Godot process running afterward.
- The Stage 6B.2 visual-polish build succeeded with 0 warnings and 0 errors; the Godot headless import/startup check succeeded after the seamless texture and lighting changes.
- `scripts/Test-Movement.ps1` and all three room `SolutionTrace` tests passed; Rooms 01-03 each completed their real goal trigger 10 consecutive times after the exit-door replacement.
- `scripts/Test-UI.ps1`, `scripts/Test-CampaignFlow.ps1` and `scripts/Test-Opening.ps1` passed after the visual changes.
- `scripts/Update-Panoramas.ps1` regenerated all six current room views and `scripts/Test-Panoramas.ps1` confirmed that every capture is newer than its room and shared-material sources.
- Fixed 1280x720 spawn captures on Intel UHD Graphics visually confirmed that each double door is visible immediately, destination areas remain readable without strong door glow, and the 12-metre texture repeat has no hard tile boundary.
- The enclosed-shell build succeeded with 0 warnings and 0 errors; Godot imported the new seamless `hazard_grate.svg` material successfully.
- `scripts/Test-RoomShells.ps1` passed in Rooms 01-03 and confirmed that each visible maintenance floor, rather than the hidden fallback height threshold, restarts the player.
- All three room `SolutionTrace` tests still completed their real goals 10 consecutive times after the walls, ceilings and floors were added; Room 03 continued to record the mandatory airborne crossing.
- `scripts/Test-Movement.ps1` and `scripts/Test-CampaignFlow.ps1` passed after the room shells were introduced.
- All six panoramas were regenerated, their imported Godot resources were refreshed, and `scripts/Test-Panoramas.ps1` now includes the hazard-grate source in its freshness check.
- Stage 6B.3 built with 0 warnings and 0 errors; the Godot headless import/startup check imported both new Room 04 panoramas successfully.
- `scripts/Test-Room04Solution.ps1` used the interaction action and completed the real gated collection cup 10 consecutive times.
- `scripts/Test-CampaignFlow.ps1` verified Room 03 completion, its assigned dialogue, transition into Room 04 and all seven expected Room Start/Room Complete snapshots.
- `scripts/Test-RoomShells.ps1` passed in Rooms 01-04 and confirmed that Room 04's visible maintenance floor restarts the player.
- `scripts/Test-Movement.ps1`, all four room solution tests, `scripts/Test-Panoramas.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-Opening.ps1` and `scripts/Test-CampaignFlow.ps1` passed in the final regression run.
- Fixed 1280x720 Room 04 and menu captures on Intel UHD Graphics confirmed the safe start, immediately visible exit door, shape-led lever, slatted gate, eight-panorama menu and current Stage 6B.3 label without connecting the mouse to the camera.
- Stage 6B.4 built with 0 warnings and 0 errors; the final Godot headless import/startup check imported all ten regenerated panoramas successfully.
- `scripts/Test-Room05Solution.ps1` used `E`, recorded the unavoidable airborne crossing and completed the real Room 05 collection cup 10 consecutive times.
- `scripts/Test-CampaignFlow.ps1` verified Room 04 completion, its assigned mother dialogue, transition into Room 05 and all nine expected paired snapshots through Room 05 Start.
- `scripts/Test-RoomShells.ps1` passed in Rooms 01-05; every visible maintenance floor restarted its real player at that room's spawn.
- `scripts/Test-Movement.ps1`, all five room solution tests, `scripts/Test-Panoramas.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-Opening.ps1` and `scripts/Test-CampaignFlow.ps1` passed in the final Stage 6B.4 regression run.
- Fixed 1280x720 Room 05 and menu captures on Intel UHD Graphics confirmed the safe lever start, gate-to-door line of sight, compact bottom interaction prompt, prompt-free Room 05 panoramas, candy `O` and current Stage 6B.4 label; no Godot process remained afterward.
- Stage 7A.1 built with 0 warnings and 0 errors; Godot imported the frictionless profile, new glass texture and all twelve regenerated panoramas successfully.
- `scripts/Test-Room06Solution.ps1` detected the frictionless surface, released movement input while the ball retained speed and completed the real Room 06 goal 10 consecutive times.
- `scripts/Test-CampaignFlow.ps1` verified Room 05 completion, its assigned child dialogue, transition into Room 06 and all eleven expected snapshots through Room 06 Start.
- `scripts/Test-RoomShells.ps1` passed in Rooms 01-06, including Room 06's maintenance floor beneath the transparent span.
- The changed player-ground contact code passed `scripts/Test-Movement.ps1` and all six room solution tests; standard traction, externally supplied momentum and zero airborne control remained intact.
- `scripts/Test-Panoramas.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-Opening.ps1` and `scripts/Test-CampaignFlow.ps1` all passed in the final Stage 7A.1 regression run.
- Fixed 1280x720 Room 06 and menu captures on Intel UHD Graphics confirmed the safe start, visible exit, transparent etched glass, moving sheen, edge clamps, twelve-view panorama menu, candy `O` and readable Stage 7A.1 label; no Godot process remained afterward.
- The restart-shortcut follow-up built with 0 warnings and 0 errors.
- `scripts/Test-UI.ps1` explicitly confirmed that no `restart_room` input action, rebinding row or serialized settings key remains, while the pause-menu `Restart Room` button is still present.
- `scripts/Test-Movement.ps1`, all six room solution tests, `scripts/Test-CampaignFlow.ps1` and `scripts/Test-RoomShells.ps1` passed after removing the per-room keyboard polling.
- All twelve panoramas were regenerated after removing the room hints; `scripts/Test-Panoramas.ps1` confirmed that they are current.
- The candy-texture continuity follow-up built with 0 warnings and 0 errors, and Godot imported the revised shader without errors.
- The deliberate central mould stripe was removed; glaze, grain, crust, crystals and pores now use continuous sphere-space coordinates, while directional patterns use seam-safe spherical or periodic mapping.
- Fixed captures before and after physical rolling showed continuous surface detail at both orientations without the former vertical break.
- `scripts/Test-Movement.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-Profile.ps1` and `scripts/Test-Room01Solution.ps1` passed after the shader correction.
- The text-free visual-language follow-up built with 0 warnings and 0 errors; the final Godot headless import/startup check succeeded with the VRAM-compressed mipmapped micro-grain texture.
- A source audit across all six gameplay scenes and `src/Gameplay` found no room title, tutorial, instruction or gate-status text; the only rendered gameplay label is the gaze-gated interaction key owned by `MechanicalLever`.
- `scripts/Test-UI.ps1`, `scripts/Test-Movement.ps1`, `scripts/Test-CampaignFlow.ps1` and `scripts/Test-RoomShells.ps1` passed after the HUD and material changes.
- All six room solution tests passed 10 consecutive real-goal completions; Rooms 04-05 still required their recorded interaction action and Room 06 still required a frictionless no-input coast.
- `scripts/Update-Panoramas.ps1` regenerated all twelve final views and `scripts/Test-Panoramas.ps1` confirmed that every panorama is newer than the rooms, shared materials and micro-grain overlay.
- Fixed Room 04-05 captures on Intel UHD Graphics confirmed text-free play views, physical crossed gate latches, visible surface scratches and varied local wear without capturing or constraining the system pointer; no Godot process remained afterward.
- `scripts/Generate-MicroTextures.ps1` completed successfully and reproduced all five base/overlay PNGs from fixed seeds with wrapped marks on every boundary.
- The material-detail build completed with 0 warnings and 0 errors; `scripts/Invoke-Godot.ps1 -Mode HeadlessCheck` reimported and loaded the VRAM-compressed mipmapped PNG materials successfully.
- `scripts/Test-UI.ps1`, `scripts/Test-Movement.ps1`, `scripts/Test-Opening.ps1`, `scripts/Test-CampaignFlow.ps1` and `scripts/Test-RoomShells.ps1` all passed after the material resolver and texture replacement.
- Every Room 01-06 `SolutionTrace` still completed its real goal 10 consecutive times; Rooms 04-05 retained their required `E` interaction and Room 06 retained its frictionless no-input coast.
- Fixed 1280x720 Room 01 and Room 04 captures on Intel UHD Graphics visually confirmed dense fine aggregate on concrete, tread plate only on suitable decks, directional abrasion on fabricated metal and readable lighting without flat white SVG-import fallbacks.
- `scripts/Update-Panoramas.ps1` regenerated all twelve views and `scripts/Test-Panoramas.ps1` confirmed that every panorama is current against the five generated textures and their generator source.
- A final source audit found no remaining references to the removed SVG versions, and no Godot process remained after capture and test runs.
- The small-scale refinement regenerated the three base textures at 2048x2048 and retained 512x512 transparent micro overlays; generation completed successfully from fixed seeds.
- A second fixed Room 01/Room 04 visual pass rejected the initial rescale because it exposed oversized concrete circles; the corrected pass confirmed fine tread plate, subtle concrete grain and compact localized wear without those large marks.
- The refinement build completed with 0 warnings and 0 errors; the final headless import/startup check succeeded.
- `scripts/Test-UI.ps1` passed after panorama replacement, and the real Room 01 `SolutionTrace` still completed 10 consecutive runs.
- All twelve panoramas were regenerated from the corrected material scale and `scripts/Test-Panoramas.ps1` passed its source-freshness audit.
- Stage 7A.2a built with 0 warnings and 0 errors; the final Godot headless import/startup check succeeded.
- `scripts/Test-Movement.ps1` measured the sticky pad reducing grounded speed from `10.00 m/s` to `1.76 m/s`, while retaining its prior checks for normal ground acceleration, unclamped external momentum and zero airborne control.
- Every Room 01-06 `SolutionTrace` completed the real goal 10 consecutive times after the player integration change; Room 06 still proved its no-input frictionless coast.
- No room scene, texture or lighting changed in Stage 7A.2a, so the twelve panoramas regenerated and freshness-tested immediately before this unit remain current.
- Stage 7A.2a.1 built with 0 warnings and 0 errors; the final Godot headless startup loaded the new `SurfaceProfile` resource successfully.
- `scripts/Test-Movement.ps1` passed against `resources/surfaces/sticky.tres` and reproduced the verified `10.00 m/s` to `1.76 m/s` momentum reduction.
- No visual source changed in Stage 7A.2a.1, so no panorama regeneration was necessary and the existing twelve captures remain current.
- Stage 7A.2a.2 built with 0 warnings and 0 errors; Godot imported and loaded the production sticky-caramel shader/material without parser or resource errors.
- `scripts/Test-Movement.ps1` passed with the production profile and material attached to the same sticky test body, retaining the measured `10.00 m/s` to `1.76 m/s` slowdown.
- The new material is not referenced by any playable room yet, so the existing twelve panoramas remain accurate and were not regenerated unnecessarily.
- Stage 7A.2a.3 built with 0 warnings and 0 errors; Godot imported `surface_sticky_contact.wav` successfully.
- `scripts/Generate-StickySfx.ps1` completed with 18,522 mono samples at 44.1 kHz, and `scripts/Test-Movement.ps1` passed while loading the production profile, material and WAV on the same test surface.
- Audio-only work does not affect room panoramas, so the current twelve captures remain accurate.
- Stage 7A.2a.4 built with 0 warnings and 0 errors.
- `scripts/Test-Movement.ps1` passed after replacing the hand-built sticky test body with `RoomGeometry.AddBox`, retaining the measured `10.00 m/s` to `1.76 m/s` slowdown.
- Room 01 completed 10 consecutive solution runs through the unchanged standard-material path, while Room 06 completed 10 consecutive frictionless coasts through the existing profiled-surface path.
- No playable-room visual source changed, so the current twelve panoramas remain accurate.
- Stage 7A.2a.5 regenerated 18,522 stereo frames at 44.1 kHz; Godot reimported the revised WAV without errors.
- `scripts/Test-Movement.ps1` passed while loading the stereo asset through the existing positional `SFX` node, and the measured sticky slowdown remained `10.00 m/s` to `1.76 m/s`.
- Stereo audio does not affect visual sources, so all twelve room panoramas remain current.
- Room 07, `Caramel Brake`, is playable and introduces one sticky surface after a calm normal start and familiar approach slope, with the industrial exit door visible from spawn.
- The viscous caramel has its own animated fine-grain shader, physical edge blobs and hanging strands, side vats and pipes, a stereo positional contact sound, and Reduced Motion support, so it is identifiable without relying on color.
- The real goal requires sticky contact. `resources/solutions/room_07_solution.tres` additionally includes a no-input segment and its runner measures the entry and minimum planar speeds, proving meaningful momentum loss before accepting each completion.
- Room 06 completion dialogue and transition into Room 07 are integrated with Room 06 Complete and Room 07 Start snapshots; the development label now reports `DEVELOPMENT BUILD / STAGE 7A.2`.
- Local grime, cracks, scratches, patina and scuffs no longer use an edge fade. The contained-overlay shader reserves real transparent padding around the complete texture shape, while seamless full-surface micro-grain remains tiled independently.
- Automatic wear was enlarged substantially. Wall layers now cover broad areas, with a deterministic subset reaching almost from floor to ceiling, while the contained texture coordinates prevent their rectangular quad boundaries from cutting through a mark.
- The Room 07 panorama pair uses two unobstructed viewpoints, and all fourteen Room 01-07 panoramas were regenerated after the shared wear change.
- Stage 7A.2b built with 0 warnings and 0 errors; the final Godot headless import/startup check loaded the contained overlay shader, Room 07 and all fourteen regenerated panoramas successfully.
- `scripts/Test-Movement.ps1` passed with the measured sticky slowdown of `10.00 m/s` to `1.76 m/s`; every Room 01-07 `SolutionTrace` completed its real goal 10 consecutive times.
- `scripts/Test-RoomShells.ps1` passed for all seven visible hazard floors, and `scripts/Test-CampaignFlow.ps1` verified dialogue plus all thirteen snapshots through Room 07 Start.
- `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-Opening.ps1`, `scripts/Test-UI.ps1` and `scripts/Test-Panoramas.ps1` all passed in the final regression run.
- Fixed 1280x720 Room 04 and Room 07 captures plus the final Room 07 panoramas were visually inspected: the enlarged wall marks remain wholly contained without fading or abrupt quad-edge clipping, the Room 07 start is safe, and its exit remains visible.
- The rounded grime source was rebuilt as five original SVG variants. Every closed blob, circle and stroke has generous transparent source-image margins instead of touching or crossing the texture boundary.
- Grime layers now choose among the five variants with a stable pseudo-random hash of surface name, overlay name and position, giving different walls natural variation without changing their appearance every time a room loads.
- A five-card source preview visually confirmed that every variant is fully contained; a fresh fixed Room 04 capture confirmed the same assets on real walls without engine warnings.
- All fourteen Room 01-07 panoramas were regenerated after the grime-source change. `scripts/Test-Panoramas.ps1`, `scripts/Test-UI.ps1`, `scripts/Test-RoomShells.ps1`, and the Room 01 and Room 07 ten-run solution tests passed.
- `PlayerBall` now applies `SurfaceProfile.Acceleration` as a grounded, surface-local world force. It rotates with the profiled body, is projected onto the contact plane, produces matching rolling torque, is reset cleanly, and is not limited by the player's drive-speed cap.
- The movement smoke surface loads the production accelerator profile, shader and stereo SFX, then verifies that the real player gains momentum with zero input; the measured result was `0.00 m/s` to `13.53 m/s` in one second while all prior sticky and no-air-control checks remained intact.
- Room 08, `Blue Boost`, is playable. It begins on a calm normal deck, presents one straight accelerator conveyor, converts the gained speed into a short familiar climb, and keeps the industrial exit door visible from the starting camera.
- The accelerator is identifiable without color through physical forward chevrons, animated travelling bands, three direction arches, eight rotating side rollers and a rising positional contact sound.
- `resources/surfaces/accelerator.tres` supplies `18 m/s²` of local forward acceleration. The procedural belt shader adds fine metal grain, dark grooves, repeated chevrons and restrained motion that freezes under Reduced Motion.
- `scripts/Generate-AcceleratorSfx.ps1` reproducibly generated 27,342 stereo frames at 44.1 kHz for `surface_accelerator_contact.wav`, using an original rising motor, airflow and pulse texture.
- The real Room 08 goal requires accelerator contact. Its `SolutionTrace` releases all movement input on the belt, and the runner measures at least `7 m/s` of additional forward speed plus a `12 m/s` minimum before accepting each completion.
- Room 07 completion dialogue and transition into Room 08 are integrated with Room 07 Complete and Room 08 Start snapshots; the campaign smoke now verifies all fifteen snapshots through Room 08 Start.
- The main-menu development label reports `DEVELOPMENT BUILD / STAGE 7A.3`. Two Room 08 views bring the randomized panorama catalog to sixteen current images.
- Stage 7A.3 built with 0 warnings and 0 errors; Godot's final headless import/startup check loaded Room 08, its new resources and all sixteen regenerated panoramas successfully.
- Every Room 01-08 `SolutionTrace` completed its real goal 10 consecutive times in the final regression run; Room 08 specifically proved no-input acceleration on every run.
- `scripts/Test-Movement.ps1`, `scripts/Test-RoomShells.ps1`, `scripts/Test-CampaignFlow.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-Opening.ps1`, `scripts/Test-UI.ps1` and `scripts/Test-Panoramas.ps1` all passed.
- Fixed 1280x720 gameplay and 2560x720 panorama captures were visually inspected: the start is safe and uncluttered, the destination is readable, the accelerator's direction is visible from geometry, and both panorama viewpoints are unobstructed.
- `PlayerBall` now applies `SurfaceProfile.BounceMultiplier` on the first meaningful super-elastic contact. It measures the incoming normal velocity, replaces it with a multiplied launch velocity, damps tangent motion according to surface traction for a predictable arc, and prevents repeated contact jitter without adding airborne input.
- `scripts/Test-Movement.ps1` drops the real player onto the production membrane and measured a `11.76 -> 19.40 m/s` launch while retaining every earlier drive, external-momentum, zero-air-control, sticky and accelerator assertion.
- `resources/surfaces/super_elastic.tres` defines the `1.65x` launch response. Its production shader uses a dense geometric membrane, fine rubber grain, pores, restrained emission and a small Reduced-Motion-aware deformation instead of color alone.
- `scripts/Generate-SuperElasticSfx.ps1` reproducibly generated 25,578 stereo frames at 44.1 kHz for the positional spring impact sound.
- Room 09, `Spring Vault`, is playable as the first substantially vertical room. It begins on a calm normal platform, drops the player onto one angled membrane, sends the ball through two visible flight hoops and lands on a high exit deck whose industrial door is visible from spawn.
- The membrane is also identified by raised side frames, four visible coil-and-piston banks, a deforming geometric surface and a positional stereo bounce sound. The room contains no instructional text.
- The real Room 09 goal requires a membrane contact. Its three-segment `SolutionTrace` includes a no-input airborne interval and completed ten consecutive runs while measuring a `11.43 -> 18.85 m/s` super-elastic bounce.
- Room 08 completion dialogue and transition into Room 09 are integrated with Room 08 Complete and Room 09 Start snapshots; the campaign smoke now verifies all seventeen snapshots through Room 09 Start.
- The main-menu development label reports `DEVELOPMENT BUILD / STAGE 7A.4`. Two Room 09 views bring the randomized panorama catalog to eighteen current images.
- A fixed 1280x720 gameplay capture exposed and removed a sightline-blocking drop-frame header before acceptance. The corrected start view keeps the high exit readable, and both final panoramas visibly establish the vertical route and membrane without an obstructed camera.
- Stage 7A.4 built with 0 warnings and 0 errors; the final Godot headless import/startup check loaded Room 09, its new shader, material, profile, stereo WAV and all eighteen panorama imports successfully.
- Every Room 01-09 `SolutionTrace` completed its real goal ten consecutive times in the final regression run, for ninety accepted room completions.
- `scripts/Test-Movement.ps1`, `scripts/Test-RoomShells.ps1`, `scripts/Test-CampaignFlow.ps1`, `scripts/Test-Saves.ps1`, `scripts/Test-Profile.ps1`, `scripts/Test-Advancements.ps1`, `scripts/Test-Opening.ps1`, `scripts/Test-UI.ps1` and `scripts/Test-Panoramas.ps1` all passed after the final import.
- Room 10, `Surface Circuit`, completes the surface chapter without introducing another mechanic. Its single readable axis combines a normal momentum ramp, a flat frictionless coast, a distinct sticky brake, a short accelerator climb, a measured drop onto the super-elastic membrane and a recovery landing at the exit.
- The four stations remain visually separate through glass sheen bands, caramel shader motion, physical accelerator arrows, membrane geometry, side frames, springs and a flight hoop. The destination door is visible from the safe start and no room text was added.
- The real goal rejects any route that does not touch frictionless, sticky, accelerator and super-elastic surfaces. The runner also measures sticky slowdown, accelerator gain and a `1.65x` membrane response rather than accepting trigger-only traversal.
- The final held-forward `SolutionTrace` completed Room 10 ten consecutive times. The accepted measurements included `12.49 -> 11.57 m/s` on sticky caramel, `11.52 -> 19.65 m/s` on the accelerator and `7.16 -> 11.81 m/s` on the membrane.
- Solution-floor contact now fails immediately with position and velocity diagnostics instead of silently restarting inside a long smoke run; this exposed and removed collider seams, surface overlap and incorrect catch geometry during tuning.
- Room 09 completion dialogue and transition into Room 10 are integrated with Room 09 Complete and Room 10 Start snapshots; the campaign smoke verifies nineteen snapshots through Room 10 Start.
- The main-menu development label reports `DEVELOPMENT BUILD / STAGE 7A.5`. Two Room 10 views bring the randomized panorama catalog to twenty current images.
- Stage 7A.5 built with 0 warnings and 0 errors. Room 10 solution, campaign flow, all ten shell hazards, UI and panorama freshness passed after the twenty panorama imports.
- Shared `ForceVolume3D` support now applies data-driven gravity replacement from `ForceVolumeProfile` resources while preserving the fixed 60 Hz physics path.
- Room 11, `Featherfall`, is playable as the low-gravity tutorial. It begins on a safe normal deck, uses one clear launch ramp, crosses a long floating-particle volume and lands on a broad lower deck with the enlarged exit door visible from spawn.
- Low gravity is identified without color by suspended motes, three large vertical trajectory rings, a long visibly slow arc and an original positional stereo entry sound.
- The real Room 11 goal requires low-gravity contact and a measured flight. Its `SolutionTrace` completed ten consecutive runs with about 268 airborne ticks, 5.32 metres of rise and 60 ticks of attempted lateral input producing `0.000` lateral velocity change.
- Room 10 completion dialogue and transition into Room 11 are integrated with Room 10 Complete and Room 11 Start snapshots; the campaign smoke verifies twenty-one snapshots through Room 11 Start.
- The main-menu development label reports `DEVELOPMENT BUILD / STAGE 8A.1`. Two Room 11 views bring the randomized panorama catalog to twenty-two current images.
- Stage 8A.1 built with 0 warnings and 0 errors. Room 11 solution, campaign flow, UI, all eleven shell hazards and panorama freshness passed after the twenty-two panorama imports.
- Room 12, `Heavy Drop`, is playable as the strong-gravity tutorial. A safe high start leads through one familiar descent into a full-height gravity cell, an angled catch ramp and a momentum-powered recovery climb to the visible exit.
- Strong gravity is identified without color by fast downward particles, three horizontal pressure rings, six large mechanical downward arrows, the tall shaft silhouette and an original positional stereo impact cue.
- The real Room 12 goal requires a measured strong-gravity drop. Its `SolutionTrace` completed ten consecutive runs with 67 measured falling ticks and `26.40 m/s` of downward velocity gain before reaching the catch ramp.
- Room 11 completion dialogue and transition into Room 12 are integrated with Room 11 Complete and Room 12 Start snapshots; the campaign smoke verifies twenty-three snapshots through Room 12 Start.
- The main-menu development label reports `DEVELOPMENT BUILD / STAGE 8A.2`. Two Room 12 views bring the randomized panorama catalog to twenty-four current images.
- Stage 8A.2 built with 0 warnings and 0 errors. Every Room 01-12 `SolutionTrace` completed ten consecutive runs, for 120 accepted room completions.
- Final regression passed movement physics, Rooms 01-12 campaign flow, all twelve shell hazards, the complete UI and 120 FPS setting, all 60 save slots and backup recovery, profiles, all 20 advancement definitions, the opening sequence and panorama freshness.
- Room 13, `Crosswind`, introduces one directional wind volume across a simple airborne gap. The safe start and offset landing keep the enlarged exit visible, while physical fan banks, horizontal particle streaks and a positional stereo rush identify direction without text or color dependence.
- The Room 13 `SolutionTrace` completed ten consecutive runs while measuring 107 wind-affected airborne ticks, `5.84 m` of lateral displacement and `6.01 m/s` of lateral velocity gain.
- Room 12 completion dialogue and Room 13 Start are integrated through twenty-five campaign snapshots. The menu reports `STAGE 8A.3`, its two new panoramas are imported, and the Room 13 shell, UI and focused campaign smoke passed.
- Reusable `MomentumRail3D` support now captures a rigid body, preserves at least its configured forward speed along an elevated guide, suppresses gravity only while attached and restores gravity with deterministic release.
- Room 14, `Magnetic Rise`, introduces the rail on one obvious straight climb. Twin beams, nine physical coil rings and a stereo magnetic latch distinguish it without text or color dependence, while the high exit remains visible from the safe start.
- The Room 14 `SolutionTrace` completed ten consecutive runs with 114 attached ticks, `9.70 m` of measured rise and a verified release. Room 13 Complete and Room 14 Start bring campaign coverage to twenty-seven snapshots; UI, the Room 14 shell and two imported panoramas passed focused checks.
- Room 15, `Gravity Circuit`, closes the chapter with three spatially separated legs: a long low-gravity arc, an offset wind landing and a magnetic rail climb. The release deck begins after a clean ballistic gap so the rail cannot collide with its own catch surface.
- The Room 15 goal requires low-gravity contact, wind contact, rail attachment, at least 55 rail ticks and release. Its `SolutionTrace` completed all three legs ten consecutive times.
- Room 14 Complete and Room 15 Start bring campaign coverage to twenty-nine snapshots. The menu reports `STAGE 8A.5`; two Room 15 panoramas, UI, the Room 15 shell and focused campaign smoke passed.
- Reusable `PlayerCannon3D` support now implements both `IInteractable` and `IImpulseDevice`, seats the real player at a fixed muzzle transform and launches it with a deterministic world impulse while retaining the gaze-gated `[ E ]` prompt in normal play only.
- Room 16, `Bullseye`, introduces one wide player cannon on a safe start deck, one clearly framed mid-air target and a broad elevated catch deck whose industrial exit remains visible along the launch axis.
- The real Room 16 goal requires both a cannon firing and a bullseye pass. Its `SolutionTrace` completed ten consecutive runs with only `0.05 m` of target-centre offset.
- An original positional stereo cannon report was generated and imported. The cannon uses a wide barrel, seat, supports and charge ring, so its function is not communicated by color.
- Room 15 completion dialogue and Room 16 Start are integrated through thirty-one campaign snapshots. The menu reports `STAGE 9A.1`; its UI smoke, hazard-floor restart and campaign flow passed.
- Two Room 16 panoramas were captured, imported and visually inspected. Panorama mode disables prompts and player-camera input, and both revised views keep complete silhouettes inside the frame.
- Reusable `InterferenceCannon3D` support uses a six-body projectile pool, fixed 60 Hz cadence, continuous collision detection, a narrow barrel, charging warning lamp, physical projectiles and positional stereo fire audio without allocating a new body per shot.
- Room 17, `Crossfire`, pairs the familiar player cannon with one perpendicular interference lane. The intended solution is chosen on the ground before launch; no airborne correction is introduced.
- Room 17 completed ten consecutive traces while crossing two fired-projectile cycles per run with zero hits. Room 16 Complete and Room 17 Start bring campaign coverage to thirty-three snapshots; two imported panoramas, UI and shell checks passed.
- Reusable `MovingPlatform3D` support provides a physics-synchronized diagonal platform, explicit rigid-body rider transport, exposed guide rollers, side rails and two mechanical safety gates. The rear gate rises after boarding and the forward gate retracts only after docking.
- Room 18, `Rising Transit`, starts on a normal safe deck and carries the player 28 metres forward and 11 metres upward to a visible high exit. The platform starts only after boarding, so the tutorial does not require guessing an arbitrary cycle.
- The Room 18 `SolutionTrace` completed ten consecutive full rides. An original stereo motor cue, Room 17 Complete, Room 18 Start, UI, shell and both revised panoramas are integrated; the menu reports `STAGE 9A.3` and campaign coverage is thirty-five snapshots.
- Reusable `MomentumPiston3D` support implements `IImpulseDevice`, captures the real player in a visible angled cradle, exposes a 24-tick mechanical wind-up and applies a world-space launch without accepting airborne steering.
- Room 19, `Piston Arc`, presents one descending approach to the angled piston, two physical flight rings and a high catch deck whose exit is visible from the safe start.
- The Room 19 `SolutionTrace` armed and fired the piston at `20.20 m/s` for ten consecutive completions. Its positional stereo strike, Room 18 Complete, Room 19 Start, UI, shell and two visually inspected panoramas are integrated; the menu reports `STAGE 9A.4` and campaign coverage is thirty-seven snapshots.
- Room 20, `Ballistic Assembly`, closes the launcher chapter without introducing another mechanic. Its four separated legs require a timed player-cannon launch through a perpendicular interference lane, a complete diagonal-platform ride and a final angled-piston arc to a 30-metre-high exit deck.
- The Room 20 goal rejects any run that skips a device or takes a projectile hit. Its `SolutionTrace` completed all four legs ten consecutive times with zero hits and a measured `20.20 m/s` final piston launch.
- Room 19 Complete, Room 20 Start and Room 20 Complete are integrated. The campaign smoke verifies Rooms 01-20 dialogue, unlocks and all forty paired snapshots; the menu reports `STAGE 9A.5`, and Room 20 UI plus shell checks passed.
- Two Room 20 panoramas were captured, imported and visually inspected. The first shows the full long-form vertical assembly and the final door from the starting side; the second frames the final piston and high destination without room text.
- Final Stage 9A.5 regression built with 0 warnings and 0 errors. All twenty `SolutionTrace` files completed ten consecutive real-room runs, for 200 accepted completions.
- The final regression also passed movement physics, all twenty hazard-floor restarts, all forty campaign snapshots, 60-slot save handling and recovery, profile persistence, all twenty advancement definitions, the opening, UI, settings, 120 FPS and panorama freshness.
- All forty room panoramas were regenerated from current Rooms 01-20 sources, imported by Godot and accepted by the freshness check after the final room and component changes.
- Room 21, `Soft Landing`, introduces a real `Absorbing` surface profile on one isolated lesson: a safe high start, a long momentum-building descent, a porous foam runout and the visible exit beyond it.
- The foam is identifiable without color through a coarse porous texture, inset three-dimensional pores, thick kerbs and a visibly soft slab. The `SolutionTrace` coasted without input and measured speed falling from `13.05` to `1.53 m/s` in ten consecutive completions.
- Room 20 Complete, Room 21 Start and Room 21 Complete are integrated. Campaign flow verifies all forty-two paired snapshots and dialogue; build, UI, Room 21 hazard-floor restart and the focused solution check pass with zero warnings or errors.
- Two Room 21 panoramas were captured and visually inspected, then all forty-two menu panoramas were regenerated, imported and accepted by the freshness check. The menu reports `STAGE 10A.1`.
- Player ground physics now resolves `GripDirection` in world space. One-way surfaces provide full drive traction only with their physical tooth direction and exponentially suppress rollback, while leaving airborne control unchanged.
- Room 22, `Ratchet Rise`, isolates that behavior on one 10.56-metre climb. Six raised chevrons, tilted teeth, a patterned plate and directional geometry distinguish it without color; the goal cannot complete without traversing the profiled surface.
- After removing a small approach lip found by the first smoke run, the Room 22 `SolutionTrace` completed ten consecutive climbs. Campaign flow verifies all forty-four snapshots and both new dialogue transitions; build, UI, shell, 120 FPS and panorama checks pass.
- All forty-four panoramas were regenerated and imported after the Room 22 lighting/chevron correction. Both Room 22 views were inspected, and the menu reports `STAGE 10A.2`.
- Reusable `MomentumBank3D` support now captures the real rigid-body player, preserves measured entry speed, freezes it in a physical cradle, renders charge on an eight-segment flywheel meter and implements both `IInteractable` and `IImpulseDevice`. Only `E` releases the stored launch.
- Room 23, `Flywheel Vault`, builds speed on a long descent, banks `14.56 m/s`, then releases a `21.50 m/s` no-air-control arc through two physical rings to a high catch deck. The goal requires capture, release and the real high landing.
- Its `SolutionTrace` completed ten consecutive runs. Build, shell, UI and campaign flow pass with all forty-six snapshots; two panoramas were visually inspected and all forty-six menu images are current after one automatically retried OneDrive file lock. The menu reports `STAGE 10A.3`.
- Reusable `BrittleBarrier3D` support measures velocity along the impact normal before contact, keeps its real collision body at low speed, and at the threshold disables that collider while animating nine independently textured shards. It exposes the measured impact and a reset-safe broken state.
- Room 24, `Break Point`, uses a familiar arrow-grooved accelerator, a six-segment physical speed gauge and one mandatory cracked barrier. Its trace shattered the real barrier at `34.48 m/s` for ten consecutive completions; low-speed passage is structurally blocked.
- Build, Room 24 shell, campaign flow, UI and all forty-eight snapshots pass. Both Room 24 panoramas were inspected, all forty-eight menu panoramas were regenerated/imported, and the menu reports `STAGE 10A.4`.
- `Gelatin` is now a real elastic surface kind in player physics: it uses its own low-traction profile and `1.28x` impact return while retaining tangential momentum. Its cellular texture and five raised compression ribs distinguish it from the earlier hexagonal membrane.
- Room 25, `Processing Line`, closes the chapter with five separated legs: one-way climb, Momentum Bank release, gelatin landing/bounce, accelerated brittle break and absorbing runout. The exit frame remains visible from the safe start above the long straight assembly.
- The Room 25 `SolutionTrace` completed all five legs ten consecutive times, measuring an `11.99 m/s` bank entry and `22.39 m/s` barrier impact. Build, shell, campaign, UI and all fifty paired snapshots pass.
- Both Room 25 panoramas were inspected and all fifty menu panoramas were regenerated/imported. `Update-Panoramas.ps1` now retries transient OneDrive file locks up to three times; freshness passes and the menu reports `STAGE 10A.5`.
- Room 26, `Vacuum Lift`, introduces suction through one player-cannon launch into a real `ForceVolume3D` vacuum tunnel. Its trace entered the field and measured `5.71 m` of upward displacement for ten consecutive completions.
- Room 27, `Polarity Weave`, uses two mandatory magnetic fields with physical `+` and `-` silhouettes. Its trace crossed both fields and measured `11.49 m` of lateral magnetic displacement for ten consecutive completions.
- Room 28, `Counterweight`, uses a physics-synchronized rising platform connected to a visible cable, pulley and descending counterweight. A thick destination dock prevents tunnelling or false restarts; ten consecutive traces boarded, arrived and exited without falling.
- Room 29, `Core Approach`, combines the familiar player cannon, suction and both polarities in one long core route. Its goal rejects runs that skip any of the four devices, and the complete trace passed ten consecutive times.
- Room 30, `Exact Fare`, introduces no new mechanic. It climbs more than eighteen metres on directional ratchet grip, descends on an accelerator, breaks the fare seal at `41.58 m/s`, crosses an absorbing brake and reaches the final visible exit. The complete trace passed ten consecutive times.
- Solution smoke runs in Rooms 27-30 now fail immediately on hazard-floor contact instead of silently restarting inside an accepted run. Normal gameplay defers the restart safely outside the physics query callback.
- Rooms 26-30 are registered in the campaign catalog with distinct room names, mechanic labels and story dialogue. Campaign flow now verifies every Room Start and Room Complete slot through all sixty snapshots.
- The menu reports `DEVELOPMENT BUILD / STAGE 11A.5`. Two current views per room bring the randomized panorama catalog to sixty imported images, and consecutive menu views still cannot use the same room key.
- Final Stage 11A.5 verification built with 0 warnings and 0 errors. All thirty `SolutionTrace` files completed ten consecutive real-room runs in one pass, for 300 accepted completions.
- The final checks also passed all thirty hazard-floor restarts, all sixty campaign snapshots and dialogue transitions, UI/settings/120 FPS, Godot resource import and panorama freshness.
- The opening now plays four synchronized voice lines and restrained cinematic music through the dedicated Voice and Music buses.
- Every Room Complete transition now plays its matching child or mother voice line; all thirty clips match the campaign catalog dialogue.
- Room 30 now continues into a dedicated unskippable ending rather than the old temporary unavailable dialog. The candy exits the machine, the child says `Finally!`, raises it to his mouth and the scene freezes at contact before returning to the menu.
- The opening and ending sets gained a textured shop backdrop plus simple readable faces and limbs, while the final mouth target was visually reframed so the candy clearly reaches the child.
- Music is scoped to the opening, ending and menu shell. Normal gameplay and room solving contain no background music; their SFX, mechanics and voices remain independently mixed.
- `scripts/Test-Ending.ps1` verifies the final mouth freeze frame and full sequence completion. Build, Godot resource import, opening smoke, ending smoke, campaign flow and UI all pass; the menu reports `DEVELOPMENT BUILD / STAGE 12A`.
- The provisional desktop voices were replaced by 35 neural clips. The child uses a dedicated cute child voice with context-sensitive question/excitement pacing; the mother uses a distinct expressive feminine voice with slower warm or concerned delivery. `scripts/Test-StoryAudio.ps1` loads every clip and audits its duration.
- Both cinematics now take place in a complete stylized shop set with textured floor and walls, trim, display panels, shelving and jars rather than an empty floor plane.
- The candy machine was rebuilt at human scale as a recognizable floor-standing globe machine: transparent candy-filled bowl, cap and collar, textured pedestal, coin plate, slot, crank, chute and delivery opening. The player candy now matches the small gumballs rather than reading as a second head.
- The ending camera starts on the complete scene and eases into a 36-degree close-up. The child's mouth visibly opens from a line to an oval before the smaller candy reaches it, and the freeze frame remains exactly at entry.
- In both cinematics, the child now uses one fixed-length rounded arm identical to the other character arms; it rotates from the shoulder without stretching, detached hands or segmented joints. The mother walks beside the child to the machine and remains with him at the destination.
- Advancement telemetry is connected to real player and room events. All twenty advancement conditions have positive and negative tests, unique original line-art icons and persistent cosmetic rewards.
- Newly earned advancements now enter a non-blocking lower-right notification queue with icon, title and description. The motion respects Reduced Motion, and already-loaded profile unlocks do not replay notifications.
- Player movement now has speed-responsive rolling loops for metal, glass, soft and rubber families, airborne wind, soft and hard landing cues and an elastic-impact cue. Normal rooms retain no background music.
- Room completion no longer opens a confirmation dialog in normal play. A non-interactive mechanical transfer tube now plays the assigned voice line, optional accessible subtitle and original stereo lock/whoosh cue before automatically loading the next room.
- The transfer sequence has a dedicated smoke test, and the legacy dialog path remains isolated to fast automated campaign verification.
- The main-menu build label now reports `DEVELOPMENT BUILD / FINAL POLISH`.
- The candy-ball `O` now uses an ignore-size texture cell sized to the optical cap height of the wordmark instead of expanding to the full logo-row height; the loading mark uses the same corrected proportion.
- The main menu and its profile/settings submenus now play an original restrained 32-second stereo loop through the `Music` bus. It fades in, loops at an explicit full-stream boundary and stops before cinematics, loading or gameplay; headless test runs do not start an unnecessary audio player.
- An official Windows x86_64 export preset and repeatable export script produce `builds/windows/Velocitex/Velocitex.exe` with its required package and .NET runtime files.
- The cinematic arm and companion-walk revision was rebuilt, passed opening and ending smoke tests, captured for visual inspection and exported into a startup-smoked Windows package containing 189 files.
- The revised logo and menu-music package passed build, UI/120 FPS, opening handoff, visible playback/loop and gameplay-silence checks, then exported as a clean startup-smoked 189-file Windows package.
- Customize now presents primary colors, secondary colors and trails as compact visual swatches instead of named dropdown entries. Pattern choices use candy-shaped preview icons that render the actual resulting pattern.
- Every base and advancement cosmetic remains visible in the selector. Locked rewards carry a clear padlock, stay preview-safe, and show the exact earning requirement when clicked instead of changing the equipped appearance.
- The opening and ending candy globes now contain 1,872 tightly packed, efficiently instanced candies at exactly the same cinematic radius as the player candy. The opening drop passes visibly through the open maintenance hatch while a tracked telephoto close-up follows the falling player; build, opening/ending smoke tests, visual capture and the refreshed Windows export all passed.
- Achievement progress no longer depends on avoiding room restarts or deaths. `Fresh Coat` rewards saving a patterned look, `Five-Star Batch` rewards the first five chapter tests, and the Room 18, 19 and 30 achievements now measure their platform, piston-speed and Fare Seal speed-window mechanics directly. Their icon/catalog/UI telemetry, positive solution checks and refreshed Windows export passed.
- Room-to-room loading no longer invokes the full startup loading screen. The rail/tube artwork was removed from the handoff, leaving one centered rotating 256-pixel candy plus the existing dialogue/subtitles; the same candy-only handoff now covers the opening-cinematic to Room 01 load. A dedicated smoke assertion verifies that no full loading overlay appears during Room 01 to Room 02, and the Windows export was refreshed.
- Room Select now enters rooms through the same candy-only handoff rather than the startup loading screen. The handoff backdrop is fully opaque, including its entrance and exit animation, while the full logo loading sequence remains limited to application startup.
- Advancement notifications render above the room-transfer layer, so a newly earned completion advancement remains visible in the lower-right corner. Room 01 completion still grants `Fresh from the Globe`; already-owned advancements correctly do not replay.
- Camera input is restored before the room-name card enters. The top card is non-interactive and no longer delays mouse-look while it animates.
- Rooms 01-03 were rebuilt as longer multi-leg routes. Room 01 now has two distinct descents, two unavoidable flights and an offset grounded correction; Room 02 uses two basins and four deliberate direction changes; Room 03 uses four descending launch-and-landing legs instead of one short gap.
- Reusable `FlightGate3D` support now gives route rings a real trigger, mechanical latch response, activation state and positional SFX. Goals reject routes that skip required gates.
- The isolated flight rings in Rooms 03, 05, 09, 10, 19, 20, 23 and 25 are functional gates positioned on the measured physical trajectory. The remaining torus visuals are attached to active springs, gravity fields, rails, cannon lanes, targets or suction fields rather than pretending to be standalone mechanics.
- All 60 menu panoramas were regenerated at 2560x1440 or higher. Room 01 received two new unobstructed long-room compositions after visual inspection found its old second camera behind the expanded course.
- Final regression completed all 30 `SolutionTrace` files ten consecutive times each for 300 accepted completions, restarted all 30 hazard floors, verified all 60 campaign snapshots, and passed profile, UI, room-transfer, advancement-notification and panorama tests.
- The refreshed Windows package passed a clean build and exported startup smoke with 189 files at `builds/windows/Velocitex/Velocitex.exe`.
- Every `FlightGate3D` now applies a measurable momentum boost instead of acting as an activation-only hoop. The normal profile adds at least `3.0 m/s`, raises slow entries to `15.0 m/s` and preserves lateral aim; an axial mode supports tightly constrained ballistic routes without creating air control.
- Every goal in Rooms 01, 03, 05, 09, 10, 19, 20, 23 and 25 still rejects a run that skips one of its flight gates. Their focused `SolutionTrace` checks completed ten consecutive runs each after the boost revision.
- Room 25 now places its second ring on the descending bounce line immediately before a taller brittle barrier. The ring raises the measured strike to `14.60 m/s`, so the mandatory hoop contributes directly to breaking the route rather than merely ticking a hidden condition.
- Player audio now reports the onset of ordinary contacts with floors, walls and other bodies down to a jitter-safe `0.05 m/s`. Four pooled impact voices play soft or hard stereo cues without cutting off rapid successive hits, while super-elastic impacts retain their dedicated cue.
- Material-specific rolling loops become audible from normal walking-roll speeds instead of remaining near silence until high velocity. The player SFX smoke test verifies normal-speed rolling plus separate floor and side-wall collisions.
- All sixty 2560x1440-or-higher menu panoramas were regenerated after the Room 25 route revision, visually checked for the revised ring/barrier composition and accepted by the freshness test.
- The momentum-ring/contact-audio package built with 0 warnings and 0 errors, passed the focused physics and 90-completion gate-room regression, and exported as a startup-smoked 189-file Windows build at `builds/windows/Velocitex/Velocitex.exe`.
- Manual Room 01 playtesting exposed that the former flight-gate area could overlap the player while its centre was visibly outside the ring. `FlightGate3D` now derives its trigger from the visible aperture and the 1.2-metre player diameter, so activation requires the complete ball to fit through the opening.
- Room 01 now has a dedicated negative bypass smoke: outside-aperture contact does not activate the ring and the collection cup rejects a zero-ring route. The intended trace still passed both rings and completed ten consecutive runs.
- The stricter aperture check was carried through every ring room. Visible rings in Rooms 03, 20, 23 and 25 were enlarged or aligned to the measured flight lines instead of restoring an invisible oversized trigger. Rooms 01, 03, 05, 09, 10, 19, 20, 23 and 25 each completed ten consecutive solution runs with every required ring, for 90 accepted completions.
- The missing rolling audio was traced to the source assets rather than the mixer: all four rolling WAV files contained absolute digital silence. The PowerShell generator had selected integer `Math.Min/Max` overloads and quantized every normalized sample to zero; explicit floating-point clamps now generate real stereo PCM.
- `Generate-PlayerSfx.ps1 -Scope PlayerMotion` regenerates only the four rolling loops and two landing cues, leaving menu and transfer audio untouched. `Test-PlayerSfx.ps1` now rejects silent or effectively inaudible rolling source files before starting Godot.
- Rolling reaches a readable mix level quickly at ordinary movement speeds. Collision onset now distinguishes real landings and side impacts from tiny gravity contacts on modular floor seams; pitch variation was narrowed and low-impact gain was smoothed to remove the reported choppy chatter without limiting feedback to large falls.
- The player SFX smoke verified audible stereo rolling, an ordinary floor landing, an ordinary side-wall collision, four pooled impact voices and suppression of a false impact while crossing two flush floor bodies.
- All sixty menu panoramas were regenerated at 2560x1440 after the visible ring revisions. The affected Room 01, 03, 20, 23 and 25 views were inspected, and the panorama freshness test plus a zero-warning, zero-error build passed.
- The corrected ring and player-audio package was exported and startup-smoked as a 189-file Windows build at `builds/windows/Velocitex/Velocitex.exe`.
- `Continue`, Load Game `Room Start` snapshots and Room Select now share the opaque candy-only handoff. The active room-transfer smoke creates a real Room 01 Start snapshot, invokes Continue, verifies that the candy handoff was visible and the full startup loading overlay was never shown, then verifies the ordinary Room 01-to-02 completion handoff in the same run.
- The four rolling loops were rebuilt with substantial mid-frequency surface texture instead of concentrating most energy below 100 Hz. Source tests now require non-silent PCM plus at least 150 left-channel zero crossings per second, and the Godot smoke verifies that normal-speed playback remains active, advances through the stream and reaches a readable mix level.
- Player collision audio now uses five original modal metal-impact tiers (tap, light, medium, heavy and crash) instead of two noise-heavy landing samples. Tier selection and gain are continuous across impact speed; a 0.42 m/s landing remains below -33 dB while strong crashes can rise smoothly to -1.5 dB.
- The focused package generated and imported eleven player-motion cues, built with zero warnings and zero errors, and passed player SFX, seam suppression, monotonic impact-level and Continue/room-transfer smoke tests.
- Room 01 is no longer solvable by holding forward. Its two mandatory momentum rings occupy opposite lateral lines, with a long grounded crossover between committed flights; the exit door and collection cup were moved to the extended room end.
- The revised Room 01 `SolutionTrace` crossed both visible apertures and completed ten deterministic consecutive runs after a short physics-settle window. Direct cup entry, outside-aperture contact and 2,600 ticks of forward-only input all failed to complete the room.
- Room 02 now records its intended four-bend route through four sequential mechanical floor latches at the two basins, east deck and exit run. The collection cup remains locked unless the latches are crossed in order, while their inset plates and retracting corner jaws communicate progress without room text.
- The unchanged multi-direction Room 02 trace activated all four route latches and completed ten consecutive runs. Direct cup entry and 2,600 ticks of forward-only input both failed.
- Room 03 already met the puzzle-integrity bar: its four descending flights use laterally offset apertures and require separate grounded aim corrections because the ball has no air control. Its thirteen-segment trace completed ten consecutive runs, while direct cup entry and forward-only input both failed.
- Room 04 moved its lever from the spawn line into a left relay bay connected through a broad momentum basin. The player must brake laterally, interact while passing the side mechanism, reverse into the basin, realign and traverse the released gate; a silent solution-test restart bug now fails immediately instead of hiding a fall.
- Room 04's collection cup was widened to match its visible funnel and accept the intended high-speed exit line. The detour trace completed ten consecutive runs, while direct cup entry and forward-only input failed.
- Room 05 was rebuilt from a one-action straight launch into a full chapter test: a side-lever detour, start gate, offset first ring, long grounded crossover, offset second ring and anticipatory braking into the final cup. A 0.25-metre collision step at the second slope entry was removed so high-speed contact no longer creates an unintended vertical launch.
- Room 05's new nine-segment trace activated the lever, recorded airborne travel, crossed both mandatory rings and completed ten deterministic consecutive runs. The final Rooms 01-05 regression produced 50 accepted solutions and rejected direct-goal plus 2,600-tick forward-only routes in all five rooms.
- Room 06 remains a deliberately low-load frictionless introduction, but completion still requires building speed on the glass and proving a no-input coast; direct cup entry and forward-only input are rejected.
- Room 07 now makes its sticky slowdown a mandatory measured condition and routes the player through a long straight approach into a perpendicular side exit. Its four-part trace completed ten consecutive runs, while direct cup entry and forward-only input failed.
- Room 08 now makes measured accelerator gain mandatory, ends the boost lane at a mechanical stop and places the exit on a perpendicular side deck that requires a timed ground turn. Its three-part trace completed ten consecutive runs, while direct cup entry and forward-only input failed.
- Room 09 now requires a deliberate lateral pre-aim before the player commits to the no-control drop. The super-elastic rebound crosses two vertically and laterally aligned sequential apertures before landing; the revised trace measured an `11.40 -> 18.81 m/s` rebound and completed ten consecutive runs. Direct cup entry and 2,600 ticks of forward-only input both failed.
- Room 10 was rebuilt from a straight four-surface conveyor into a long chapter exam with three readable route latches. The player must carry momentum over glass, use the sticky yard to brake and turn left, climb an offset accelerator, commit to a long drop onto the membrane, cross a high rebound ring, land against an end stop and turn right into the side exit. Normal completion now requires measured sticky slowdown, accelerator gain and super-elastic rebound in addition to every surface, latch and ring.
- The final Rooms 06-10 regression produced 50 accepted solutions and rejected direct-goal plus 2,600-tick forward-only routes in all five rooms. The build completed with zero warnings and zero errors.
- The audited Rooms 01-10 package and revised player audio were exported as a validated 189-file Windows build at `builds/windows/Velocitex/Velocitex.exe`.
- Room 11 already met the integrity bar: its low-gravity arc includes a deliberate pre-launch lateral setup and a no-input airborne interval. It passed ten solutions and rejected direct-goal plus forward-only routes unchanged.
- Room 12 now presents a visibly offset strong-gravity catch chute. The player must aim right before committing to the uncontrollable fall, then correct left on the recovery deck; the measured `26.00 m/s` downward gain completed ten consecutive runs while direct and forward-only routes failed.
- Room 13 now uses the wind to land on an east-side latch, an enclosed end bay to force a ground reversal, and a west-side damper lever that must be pulled with `E` before the exit accepts the player. Ten measured wind flights completed and forward-only input cannot operate the lever.
- `MomentumRail3D` capture geometry now follows a fully three-dimensional rail direction instead of supporting vertical pitch only. Rail beams and coils in the revised rooms use the same diagonal basis, keeping the visible path aligned with its physical capture volume.
- Room 14 now requires a right-side rail entry, rides a diagonal rising rail across the chamber, drops from the release into a broad lower deck, activates a far-left landing latch and crosses to the east-side exit. It completed ten runs with a measured `12.02 m` rise; both bypass routes failed.
- Room 15 was rebuilt as a cross-circuit chapter exam: low gravity and wind deliver the player to a broad yard, the rail entry is on the opposite side, the diagonal rail releases toward a right-side landing latch, and the final collection cup is across the upper deck on the far left. The revised long trace completed ten consecutive runs and rejected direct and forward-only routes.
- The final Rooms 11-15 regression produced 50 accepted solutions, rejected both bypass classes in all five rooms and built with zero warnings and zero errors.
- The standard-floor rolling loop no longer uses stacked pitched sine waves or a continuous granular noise bed. It now models a hard sphere exciting three short-lived modes in a metal sheet through sparse irregular contacts, with only a quiet low structural vibration underneath and a click-free seam. Player SFX tests pass; the Windows export is refreshed after each playtest revision.
- Advancement descriptions now consistently use American `meters`. Every candy-pattern swatch was re-audited against its circular silhouette: spirals, horizontal rings, caramel bands, waves and diagonal stripes now terminate at the true circle chord after accounting for stroke width, so edge-crossing motifs meet the border without escaping it. The full pattern grid passed a graphical capture review and the UI smoke test.
- Room 01 now enforces both momentum rings through geometry, not only route state. The first ordinary-speed launch and a retained-momentum 24 m/s second launch both fall to the hazard floor when they avoid their rings; the intended rings raise the two flights to 22 and 34 m/s across substantially enlarged gaps. Ramp, lip and floor endpoints share exact top heights with horizontal overlap only, and a global static-surface seam guard removes small collision-normal lifts without affecting real ramp takeoff, moving platforms or elastic surfaces.
- Room 01's collection-cup prop was removed. The real completion trigger now occupies the exit doorway; its frame is flush with the inner face of the end wall, its threshold is flush with the runout, and both door leaves visibly slide open after the second ring. Ten solution runs, the geometric no-ring test, direct-goal/forward-only bypass tests, movement physics and player-audio seam tests passed.
- The startup loading wordmark now uses the menu wordmark's exact 45-pixel type, 39-pixel candy `O` and matching `VEL`/`CITEX` spacing. Its dedicated smoke measured 2.14 seconds from letter reveal to exit and 2.80 seconds for the complete candy-to-menu sequence.
- Opening and ending player candies no longer use the old `sugar_glaze.svg` sphere material. Both receive the saved `PlayerProfile` and use the same current seamless candy shader, colors and pattern as gameplay; both unskippable scene smokes validate that shader before running.
- Advancement reward icons and Customize swatches now share the same `CosmeticSwatchButton` rendering, dimensions and active-profile preview palette. UI smoke compares every one of the 20 reward icons with its matching Customize icon.
- Closed-room shells now overlap walls, ceiling and hazard floor beyond their visible joins and use thin non-shadow-casting metal corner finishers. The second full shell regression restarted the player cleanly from all 30 hazard floors, and Room 01 retained its ten deterministic ring solutions plus both geometric no-ring rejections.
- All 60 menu panoramas were regenerated after the route and shared-shell changes and passed the 2560x1440 freshness check. The final Windows export passed build and startup smoke with 189 files at `builds/windows/Velocitex/Velocitex.exe`.
- Room 01's intermediate landing between its two mandatory rings is now 33.5 metres long, adding 20 metres of grounded aiming and lateral-braking space without moving the first landing edge or weakening the first no-ring gap. The second ring follows the measured early airborne arc above the next slope and applies a 48 m/s forward-axis boost instead of amplifying downward velocity. The revised trace completed ten consecutive runs, while both no-ring launches, direct door entry and forward-only input remained rejected; both Room 01 panoramas were refreshed and visually checked at 2560x1440.
- Pattern reward icons now mirror the candy shader rather than only the pattern names: the spiral uses three twisted bands, stars use the shader's four-point sparkles, lightning uses repeated narrow bolt tracks, and caramel uses the same wavy vertical bands seen on the candy. Customize and Advancements inherit the correction from their shared `CosmeticSwatchButton` renderer; the UI smoke passed.
- A visual and source-level platform audit covered Rooms 01-30. The remaining unnecessary overlaps were removed from Room 02's first turning basin, Room 07's side-exit extension and Room 08's perpendicular turn deck; adjacent surfaces retain only a small flush connection instead of duplicate coplanar collision areas. Room 04's broad relay junction was retained after its lever-route smoke proved it is a required traversal surface rather than redundant support.
- The Room 04 trace exposed an older lateral-drift regression after the movement revisions. Its final grounded input now includes a `0.25` right correction and again activates the lever and completes ten consecutive runs without changing room geometry.
- Rooms 02, 04, 07 and 08 each completed ten consecutive focused solutions. Rooms 02, 07 and 08 also rejected direct-goal and forward-only bypasses. Build and UI completed with zero warnings or errors, all sixty 2560x1440 panoramas were regenerated and passed freshness validation, and the Windows package was refreshed.

## Known issues

- The repository has no commits yet; creating commits was not part of this unit.
- Camera feel and visual framing passed the first visual Room 01 pass but will still need broader playtesting across later room geometries.
- The exact wording of the user's save request from a separate side chat is not available in this task history. The complete written save specification is implemented and verified; any additional side-chat-only rule must be pasted before it can be confirmed.

## Exact next action

Continue the manual playtest from Room 01 in the refreshed Windows build and record any remaining gameplay or visual issue at the room where it appears.

## Planned SFX pass

- Mechanically readable cues accompany every introduced world device; no mechanic depends on color or visuals alone.
- The complete player pass covers speed-responsive rolling, material-specific contact and landing, impacts, airborne wind, switches, doors, cannons, pistons, moving platforms, hazards, collection cups and room transitions.
- Use positional 3D audio for world mechanisms, layered variations and pitch ranges to avoid obvious repetition, and pooled playback with concurrency limits so dense rooms remain clear and efficient.
- Route sounds through the existing SFX bus and verify that important gameplay cues remain distinguishable at practical volume levels without relying on music or voice playback.

## Remaining acceptance criteria for this unit

- Complete a manual first-run campaign playtest and confirm the intended two-hour-plus duration, puzzle clarity and physical feel on the user's machine.

## Next unit after completion

Address the next concrete manual-playtest report, then rerun the affected room checks and refresh only the assets and Windows package touched by that correction.

## 2026-07-17 — full room seam, exit and route audit

- Reworked the shared pattern swatches so the spiral, stars, lightning and caramel rewards use clear, self-contained symbols that match their candy patterns. Customize and Advancements continue to render the same shared icon implementation, and the UI smoke passed.
- Added strict automated floor-to-ramp seam coverage for Rooms 01-30. The audit rejects both gaps and raised/overlapping endpoints; every nearby horizontal-floor/ramp connection now passes the 0.12 m horizontal and 0.085 m vertical tolerances.
- Standardized the campaign to one functioning exit presentation per room: Room 01 uses only its animated two-leaf door, while Rooms 02-30 use only one collection socket aligned with an active `GoalCup` trigger. The all-room exit presentation smoke passed.
- Aligned Room 16's collection socket with the cannon route's real stopping point. Its target, landing latch and side exit completed ten deterministic runs.
- Removed straight-ahead completions from Rooms 11, 18, 19, 22, 24, 27, 28 and 30 by separating the final landing/mechanic from a side-positioned collection socket. Required runout rails/end stops keep the last grounded correction inside the playable surface.
- Room 28's moving platform now meets the starting floor at the exact same edge and top height instead of leaving a 0.5 m gap and 0.3 m step. Its elevated deck has physical side rails and an end stop.
- Brittle barriers now restore collision through a deferred physics-state change, preventing hazard respawns from changing a collision shape while Godot is flushing queries.
- All 30 `SolutionTrace` files completed ten consecutive runs in one regression pass: 300 accepted solutions total.
- The all-room bypass regression rejected both direct goal entry and 2,600 ticks of forward-only input in all 30 rooms: 60 rejected bypass attempts total.
- Regenerated all 60 room panoramas at 2560x1440 after the geometry and exit changes. The panorama freshness/resolution smoke passed and the changed-room captures were visually inspected.
- Final build completed with 0 warnings and 0 errors. The refreshed Windows package passed its exported startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.7 MB).

### Exact next action

Continue the manual campaign playtest from the refreshed Windows build and report the first room where the intended route, exit readability or physical feel differs from the verified trace.

## 2026-07-17 — Room 01 first-ring physical-gap correction

- Moved the first landing platform and every downstream Room 01 element another 12 metres away from the first launch lip. The first physical gap is now about 23.4 metres, so a 30 m/s launch that avoids the first ring falls to the hazard floor instead of reaching the landing.
- Retuned the first ring to a 42 m/s forward-axis launch. The intended route clears the enlarged gap, lands on the long intermediate deck and preserves enough grounded time to aim for the second ring.
- Retimed the Room 01 `SolutionTrace` around that grounded crossover. It crossed both ring apertures and completed ten deterministic consecutive runs.
- The dedicated ring-requirement smoke rejected outside-aperture activation, a 30 m/s first-ring bypass, a 24 m/s second-ring bypass and direct door entry. The general bypass suite also rejected direct-goal and 2,600-tick forward-only completion.
- The all-room floor/ramp connection and exit-presentation audits still pass. All 60 menu panoramas were regenerated at 2560x1440 and passed their freshness/resolution smoke after the shared Room 01 source changed.
- Build completed with 0 warnings and 0 errors. The exact next action is to continue the manual Room 01 playtest in the refreshed Windows export and confirm that the first gap cannot be cleared without entering the first ring.

## 2026-07-17 — Room 01 exit-door pocket masks

- Added opaque metal side pockets behind the left and right sides of the Room 01 exit frame. The animated leaves, handles and chevrons still slide normally, but any portion travelling beyond the outer frame edge is concealed behind the pocket masks.
- Extended the exit-presentation smoke to require both concealment masks on the animated Room 01 door.
- Build completed with 0 warnings and 0 errors, the all-room exit-presentation smoke passed, and the Room 01 solution completed ten deterministic consecutive runs unchanged.
- Regenerated both Room 01 menu panoramas after the door-model change. The exact next action is to visually confirm the fully opened door during the next Room 01 playtest.

## 2026-07-17 — all-room door, Room 02 platform and wall-decoration correction

- Replaced every remaining circular collection-socket presentation in Rooms 02-30 with the same readable animated two-leaf exit-door system used by Room 01. Each door is aligned with its active goal trigger, opens only when that room's required mechanics are satisfied and keeps both sliding leaves concealed behind opaque side pockets.
- Fixed Room 03's exit presentation and completion route. Its normal door now opens after all four mandatory flight gates activate, and its `SolutionTrace` completed ten deterministic consecutive runs.
- Removed the decorative wall and ceiling clutter requested by the playtest: the coin/slot, exposed gears and flywheel, floating lift coin, random ceiling braces and overhead beams are no longer built in Rooms 01-02.
- Corrected Room 02's visible platform protrusions by bringing three edge stops fully onto their supporting decks and rendering its route checkpoints as flush floor markers without raised latch jaws or ribs. Its solution still completed ten deterministic consecutive runs.
- The all-room exit-presentation smoke confirms one active goal and exactly one functioning animated door in every room, with no remaining collection-socket visuals. The floor/ramp seam audit passes for Rooms 01-30.
- All 30 solution traces completed ten consecutive runs: 300 accepted solutions. The bypass regression rejected direct goal entry and forward-only input in every room: 60 rejected bypass attempts.
- Regenerated all 60 menu panoramas at 2560x1440 after the geometry, decoration and exit changes; the freshness and resolution smoke passed.
- Build completed with 0 warnings and 0 errors. The refreshed Windows package passed its exported startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.7 MB). The exact next action is to continue the manual campaign playtest from Room 02.

## 2026-07-17 — proximity-operated exit doors

- Removed mechanic-state control from every exit-door animation, including Room 01's former second-ring dependency. Door leaves now begin opening solely when the player comes within 8.5 metres of the doorway and close again after the player moves beyond 10 metres; the separate distances prevent rapid open/close flicker at the boundary.
- Kept each room's puzzle requirements in its completion trigger, so proximity can never substitute for completing the intended route.
- Extended the all-room exit smoke to physically place the player near and far from each of the 30 doors and verify full opening and closing in both directions.
- Build completed with 0 warnings and 0 errors. The proximity-door smoke passed in all 30 rooms, and the complete bypass regression still rejected direct-goal and forward-only completion in all 30 rooms (60 rejected bypass attempts).
- The refreshed Windows package passed its exported startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.7 MB). The exact next action is to continue the manual campaign playtest.

## 2026-07-17 — fixed exit-frame arrow

- Removed both upper chevrons from the exit door's sliding-part list. The arrow above the doorway now remains fixed to the frame while only the two leaves and their handles move sideways.
- Extended the all-room exit smoke to record both arrow-part positions, fully open every door and reject any sideways arrow movement. All 30 doors passed, and the build completed with 0 warnings and 0 errors.
- The refreshed Windows package passed its exported startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.7 MB). The exact next action is to continue the manual campaign playtest.

## 2026-07-17 — shared dark exit corridors

- Standardized Rooms 01-30 on the same physical exit assembly: the proximity-operated two-leaf door now opens into an enclosed 9.6-metre corridor with a floor, ceiling, side walls, a concealed terminal wall and three progressively darker visual layers. The corridor end cannot be seen from the room.
- Cut a real doorway through each room shell and rebuilt the surrounding wall in collision-backed pieces. Local rails and end stops that crossed the doorway are trimmed around the opening instead of blocking the corridor.
- Room completion is now delayed after the puzzle goal activates. The player is drawn a short distance into the dark corridor and the room transition fires only after reaching 6.2 metres of corridor depth; restarting safely cancels a pending traversal.
- Room 01 now uses the same shared exit builder as every later room. The goal entrance volume is standardized across all 30 rooms, while each room's existing mechanic conditions still determine whether entry can complete it.
- Shifted the Room 11 and Room 19 exits slightly across their existing final decks so the common entrance volume cannot be reached by holding forward. Their intended multi-direction traces remain unchanged and pass ten consecutive runs.
- Extended the all-room exit smoke to require the full corridor geometry, darkness layers, carved shell opening, standardized entrance volume, proximity animation, fixed frame arrow, delayed traversal state, automatic centering and completion only at corridor depth. All 30 rooms passed.
- All 30 `SolutionTrace` files completed ten consecutive runs after the corridor integration: 300 accepted solutions. The complete bypass regression rejected direct-goal and 2,600-tick forward-only routes in every room: 60 rejected bypass attempts. The all-room floor/ramp connection audit also passed.
- Regenerated all 60 menu panoramas at 2560x1440 and passed their freshness/resolution smoke.
- Final build completed with 0 warnings and 0 errors. The refreshed Windows package passed its exported startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355 MB).

### Exact next action

Manually play through one exit in the refreshed Windows build and confirm the desired visual timing of the short dark-corridor traversal before the next room appears.

## 2026-07-17 — seamless one-way slope texture

- Rebuilt `one_way_teeth.svg` without the non-periodic full-texture gradient that produced broad visible bands on Room 22's golden slope.
- The replacement uses an exact 128-pixel directional-tooth cadence inside a 512-pixel texture, with matching opposite edges and mipmaps enabled. The repeated surface is seamless at its 12-metre world-space tile boundaries.
- Removed the small light/dark dots and the white centre highlight from every arrow after visual review; only the directional outlines and subtle inset metal brushing remain.
- Regenerated both 2560x1440 menu panoramas for every room using this texture: Rooms 22, 25 and 30.
- Room 22's `SolutionTrace` completed ten consecutive runs, the build passed with 0 warnings and 0 errors, and the refreshed Windows export passed its startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.2 MB).

### Exact next action

Visually confirm the seamless golden slope during the next Room 22 playtest.

## 2026-07-17 — manual exit-corridor traversal

- Removed the exit corridor's automatic lateral centering and forward acceleration. Entering a valid exit no longer changes the player's linear velocity or draws the player through the doorway.
- Proximity door animation, continuous corridor darkening and completion at 6.2 metres of manually travelled corridor depth remain unchanged.
- Updated the all-room exit-presentation smoke to reject any velocity modification after exit activation. Rooms 01-30 passed.
- Build completed with 0 warnings and 0 errors. The refreshed Windows export passed its startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.2 MB).

### Exact next action

Manually enter an exit corridor and confirm that the player remains fully controlled until the room transition begins.

## 2026-07-17 — exit-door fade to black

- Added a smooth full-screen fade driven by manual corridor depth. It begins 0.35 metres inside the doorway and reaches fully opaque black at 5.35 metres, before the existing 6.2-metre transition point.
- The fade does not alter player velocity or control.
- The all-room exit smoke verified monotonic darkening, a mid-fade opacity near 50 percent and full black before transition in Rooms 01-30.
- Build completed with 0 warnings and 0 errors. The refreshed Windows export passed its startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.2 MB).

### Exact next action

Manually enter one exit corridor and confirm that the fade timing feels natural at normal rolling speed.

## 2026-07-17 — all-room platform and slope seam audit

- Audited the complete rolling path in Rooms 01-30 and corrected every detected raised, dropped or separated platform/slope junction. Geometry corrections were required in Rooms 01-13, 15, 18-25 and 30; rooms without a faulty adjoining seam were left unchanged.
- Preserved the intended launch endpoint whenever a ramp correction could affect a ballistic route. Room 09's final launch lip now meets `SafeStart` exactly while retaining its original downstream take-off edge.
- Strengthened `SurfaceConnectionSmokeTest` so short transition lips are included, slope endpoints embedded in a flat deck are measured, and a direct overlap is accepted only when a separate flush connector completely bridges it. Each room is tested in an isolated Godot process.
- The final all-room seam audit passed. Every separated edge is within 0.01 m and every height transition is within 0.01 m; the measured worst height difference was 0.002 m. Larger reported values are intentional coplanar overlaps between flat surfaces and create no raised edge.
- All 30 `SolutionTrace` files completed ten consecutive runs after the final geometry: 300 accepted solutions. The bypass suite rejected both direct-goal and 2,600-tick forward-only attempts in all 30 rooms: 60 rejected bypass attempts. The movement smoke also passed, including diagonal input and synchronized visible rolling.
- Regenerated all 60 menu panoramas from the corrected room geometry. Every panorama passed the current-file and minimum 2560x1440 checks.
- Final build completed with 0 warnings and 0 errors. The refreshed Windows export passed its startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355 MB).

### Exact next action

Manually roll across the longest slope chains in Rooms 02, 03, 10, 12 and 30 in the refreshed Windows build as a final feel check for controller feedback at the now-flush seams.

## 2026-07-17 - complete Rooms 01-30 route and softlock audit

- "All rooms" now explicitly means every campaign room from Room 01 through Room 30. No sampled or partial room range is treated as a complete audit.
- Reworked Rooms 18, 19, 22, 24 and 27 where the previous route could be completed too directly or without enough mechanic evidence. The moving platform now requires boarding, activation and remaining aboard; Room 19 requires its trajectory lever; Room 22 requires the summit release lever; Room 24 requires both brittle barriers and its landing safety lever; Room 27 requires the ordered positive-field, lever and negative-field sequence.
- Fixed moving-platform occupancy bookkeeping so a player who falls during transit is removed instead of being teleported back onto the platform at arrival.
- Added a dedicated Room 04 recovery smoke. Falling into the gap while the bridge is closed respawns at the room start and resets the lever, gate and bridge, eliminating the reported softlock.
- All 30 `SolutionTrace` files completed ten consecutive deterministic runs in one complete regression pass: 300 accepted room completions.
- The expanded bypass suite rejected direct goal entry, 2,600 ticks of forward-only input and six sustained axis/diagonal/loop steering patterns in every one of Rooms 01-30: 240 rejected shortcut attempts.
- Every individual acceleration ring in Rooms 01, 03, 05, 09, 10, 19, 20, 23 and 25 was disabled one at a time. Each disabled ring made its intended route fail on the hazard floor, proving that no campaign ring can be skipped on its intended route.
- The shared exit audit passed for Rooms 01-30: exactly one proximity-operated double door, a carved wall opening, an enclosed continuously darkening corridor, manual traversal, fade to full black and a deep transition trigger in every room.
- The surface audit passed for Rooms 01-30. All adjoining platform/ramp edges stay within the 0.01 m gap and step limits; the measured worst vertical mismatch was 0.002 m. Reported larger horizontal separations are intentional launch gaps or coplanar overlaps, not raised seams.
- The shell audit passed for Rooms 01-30: every hazard floor restarts at that room's spawn. Shared movement physics also passed diagonal ground input, visible roll synchronization, no air control, sticky drag, no-input deceleration and super-elastic bounce.
- Regenerated all 60 menu panoramas at 2560x1440. Updated final-section views for Rooms 22, 24 and 27 were visually inspected, and the complete catalog passed the freshness and resolution smoke.
- Final build completed with 0 warnings and 0 errors. The refreshed Windows export passed its own startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 352.5 MB).

### Known test-harness issue

- The flight-gate and expanded bypass smoke processes can print Godot ObjectDB/resource cleanup warnings after their logical PASS result. The exported game startup smoke is clean; these warnings are confined to forced shutdown of short-lived automated test processes.

### Exact next action

Play the refreshed Windows export through Rooms 01-30 and report the first route that feels different from its verified intended solution. Any future request to audit "all rooms" must again run and report the complete 01-30 suite, never a sample.

## 2026-07-18 - Rooms 02-30 recurring geometry, route and exit audit

- Fixed the reported Rooms 02-05 defects. Every start platform now reaches the inner back wall; Room 02 communicates its four-step sequence graphically; Room 03 physically requires all four acceleration rings and no longer contains the unrelated under-route columns; Room 04 uses a raised barrier and continuous safe floor instead of a lethal recovery recess; and Room 05's second ring is reachable and required. The non-functional spring/arch decorations and slope frames identified in these rooms were removed.
- Standardized the shared exit from both sides of the room wall. Its backing partition now spans the interior and corridor sides, every room has the same proximity-operated double door and carved opening, and the entrance trigger has enough depth to register valid low-speed arrivals before manual traversal of the dark corridor. The door still does not move the player automatically.
- Added physical collision to the shared mechanical lever and verified that every lever rests on a real supporting surface without clipping or floating.
- Audited Rooms 06-30 for the same recurring problems. All start surfaces were extended to the back wall while preserving their puzzle-facing edge. Decorative spring banks were removed from Rooms 09-10; Room 14's rail capture radius was tightened to block a steering bypass; Room 18's moving platform was lengthened for deterministic carriage; levers obstructing the spawn or intended route were moved in Rooms 19 and 27; and Room 24's runout rails were raised to prevent an unintended fall.
- The complete solution regression passed: all 30 rooms completed ten consecutive runs, for 300 accepted completions. The bypass regression rejected direct-goal entry, forward-only input and six sustained steering patterns in every room. Disabling each of the campaign's 19 acceleration rings individually made its intended route fail, proving that every ring is physically required.
- The full shell, exit and surface regressions passed for Rooms 01-30. Hazard floors restart at the correct spawn, every exit has the common door and enclosed dark corridor, every start-wall gap in Rooms 06-30 measures 0.000 m, and the worst measured platform/ramp vertical step across the campaign is 0.002 m.
- Regenerated all 60 menu panoramas at 2560x1440 and passed the freshness/resolution smoke. Visually reviewed the changed early rooms and the later rooms with removed decorations or relocated devices.
- Final build completed with 0 warnings and 0 errors. The refreshed Windows export passed its packaged startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 352.7 MB).

### Known test-harness issue

- The bypass, flight-gate, transfer and exit smoke processes can still print ObjectDB/resource cleanup warnings during forced Godot shutdown after their PASS result. Their assertions and exit codes pass, and the exported game startup smoke remains clean.

### Exact next action

Manually replay Rooms 02-05 in the refreshed Windows build, starting with Room 02's graphical switch sequence and ending with Room 05's second ring, and report the first behavior that differs from the intended route verified above.

## 2026-07-18 - shared logo candy stripe containment

- Removed the unintended dark-red vertical seam from the shared candy logo.
- Corrected the intended horizontal red-pink stripe by letting it reach the candy edge and rendering the cream outline afterward as the topmost layer. The stripe now meets the outline without leaving a gap and cannot paint over it.
- Re-rendered the shared 256x256 PNG and changed the main menu and loading screen to use that exact PNG too, so the game UI, notifications and YouTube banner no longer use differently rasterized versions.
- Refreshed the centred candy O in the 2560x1440 YouTube banner without changing its background or wordmark placement.
- UI smoke passed. Final build completed with 0 warnings and 0 errors, and the refreshed Windows package passed export/startup smoke at `builds/windows/Velocitex/Velocitex.exe` (189 files, 355.2 MB).

### Exact next action

Visually confirm the candy O at full size in the main menu, startup loading wordmark and YouTube banner: there should be no vertical line, and the horizontal red-pink stripe should meet the inner edge of the cream outline without covering it.

## 2026-07-18 - current all-room manual-feedback audit (Rooms 01-10 complete)

- Rooms 01-07 were corrected from the latest manual playtest: safe low spawn guards, flush sloped controls, visible wrong-order switch feedback, physically locked exits, reachable required flight gates, raised lever barrier geometry and closed start-wall gaps.
- Room 01's second ring now has a bounded 56 m/s exit speed and the shared door clears fast enough for a high-momentum arrival without throwing the player back into the preceding gap.
- Rooms 08-10 completed an independent 2-3-room audit with no additional room-specific defect found.
- The shared exit now remains closed with a physical collider until the room prerequisites call `CompleteRoom`; it then opens on approach. Its lining visibly darkens along the corridor even before entry, while the existing full-screen traversal fade remains active.
- Frictionless surfaces tint the selected trail toward a lighter version of its own colour instead of replacing every trail with cyan.
- `Five-Star Batch` now unlocks on completing Room 05, and the lightning cosmetic uses a single recognisable bolt in both the candy shader and the shared cosmetic icon.
- Current verification: build 0 warnings/errors; Rooms 01-10 each completed their `SolutionTrace` ten consecutive times; Room 02 wrong-order feedback passed; Room 04 recovery passed; Room 01 ring bypass failed as intended; the shared locked-exit/static-corridor-fade audit passed in Rooms 01-30.

### Known test-harness issue

- Some forced headless exits still print the existing ObjectDB cleanup warning after their logical PASS and zero exit code.

### Exact next action

Finish the already-running independent audits for Rooms 11-19, then assign separate agents to Rooms 20-22, 23-25, 26-28 and 29-30 before the complete regression, panorama refresh and Windows export.

## 2026-07-18 - global mechanical-lever foot removal

- Removed the broad flat `Foot` mesh and its `FootHitbox` from the shared `MechanicalLever`, so the change applies to every lever in Rooms 01-30. The narrow supporting pedestal and the lever's usable collision remain.
- Removed Room 04's obsolete one-off lever-foot hiding override and added a shared exit/presentation assertion that fails if any lever recreates either legacy node.
- Recalibrated Room 05's deterministic trace because it had accidentally used the removed foot as route collision; the room now completes through both required rings without that geometry.
- Build completed with 0 warnings and 0 errors. Lever-using Rooms 04, 05, 13, 18, 19, 22, 24, 26 and the Room 27-30 core suite each completed ten consecutive runs. The shared presentation audit also passed across Rooms 01-30.
- Regenerated all 60 menu panoramas at 2560x1440. The freshness/resolution smoke passed, and visual inspection of the updated Room 04 panorama confirms that only the narrow pedestal remains and the yellow interaction ring is unobstructed.
- Refreshed the Windows package after the removal; export passed with 0 warnings and 0 errors at `builds/windows/Velocitex/Velocitex.exe` (189 files, 354.8 MB).

### Exact next action

Play the refreshed Windows export and inspect any lever at close range; report a room number if anything broader than the narrow pedestal still appears beneath it.

## 2026-07-18 - Rooms 02-06 playtest corrections, sealed exits and transfer/UI polish

- Closed the shared exit's interior side pockets. The corridor side walls now reach the doorway plane instead of beginning deeper inside the tunnel, and the physical exit audit raycasts outward at two depths on both sides of every door in Rooms 01-30 to prove that the shell cannot be escaped there.
- Room 02 now has continuous route guard walls except at intentional launch/landing openings. Button 03 sits to the left of Button 01 from the spawn view; wrong-order input flashes the pressed button as plain solid red with its sequence dots hidden; and the visible door remains physically locked until the four-button order completes.
- Room 03 no longer duplicates the room shell with a rear spawn rail; only the useful side guards remain.
- Lowered the shared narrow lever pedestal and collision so levers rest on their supporting floor after removal of the broad foot. Room 04 continues both guard walls beyond the barrier and requires two graphically numbered pressure plates in order after the lever.
- Room 05 no longer has guard walls on the middle landing. As a chapter test it now requires a three-plate ordered sequence in addition to its lever and both acceleration rings.
- Room 06 now updates frictionless/coast evidence in production gameplay as well as its deterministic test, so its door opens and transfers correctly. Its wall/rail seams are connected; frictionless drive retains low drag while providing enough grounded traction to steer and build momentum; and the intended route now combines the slope, a required acceleration ring and a no-input glass coast.
- All gameplay SFX are muted from the instant room completion begins until the next room is ready. Dialogue remains audible on the separate `Voice` bus. The transfer no longer plays its own SFX over the spoken line.
- Removed `Load Game` and `Customize` from the pause menu; both remain available from the main menu. `Five-Star Batch` is still awarded and persisted at Room 05 completion, but its popup is held until Room 06 has loaded and its room-intro card has cleared.
- Complete regression passed: all 30 `SolutionTrace` files completed ten runs each (300 completions); direct-goal, forward-only and six sustained steering bypasses were rejected in every room; every campaign acceleration ring remained required; all surface, shell, movement and shared-exit checks passed. UI, campaign flow, room transfer, advancement, Room 02 feedback and Room 04 recovery checks also passed.
- Regenerated all 60 menu panoramas at a minimum 2560x1440 and passed the freshness/resolution smoke. Final build completed with 0 warnings and 0 errors, and the refreshed Windows package passed export at `builds/windows/Velocitex/Velocitex.exe` (189 files, 354.7 MB).

### Known test-harness issue

- Several forced headless bypass and flight-gate processes still print the existing Godot ObjectDB/resource cleanup warnings after their logical PASS and zero exit code. The packaged build/export succeeds normally.

### Exact next action

Manually replay Rooms 02-06 in the refreshed Windows export, including approaching each locked door before satisfying its requirements and deliberately pressing Room 02's buttons out of order. Report the first visual or physical behavior that differs from the intended sequence above.

## 2026-07-18 - centered slope arrows and full room-shell containment

- Reworked both directional surface families so slopes show one large centered column of arrows with permanent side margins. The animated accelerator shader no longer repeats three columns, and the one-way ratchet texture no longer contains a four-column grid.
- Audited Rooms 01-30 against their actual interior wall, floor and ceiling planes. Corrected real boundary intersections in Rooms 02, 03, 08, 10, 12, 28 and 29 without changing their intended routes: one-sided shell expansions preserve established start geometry, ring apertures retain visible clearance, the Room 28 pulley clears the ceiling and the Room 29 run ends at the exit-wall face.
- Clamped generated wear overlays to the rotated surface bounds, so dirt and micro-grain details cannot extend past a wall or platform edge.
- Added a permanent all-room containment regression in `RoomShellContainmentSmokeTest`: it checks visible meshes, relevant collision shapes and moving-platform endpoints. Its structural-join allowance is geometry-based and contains no room-specific or object-name bypasses.
- Regenerated all 60 menu panoramas at 2560x1440 after the visual and shell changes; the freshness/resolution smoke passed. Visual checks confirm the accelerator and ratchet slopes now use one centered arrow column.
- Verification passed: build 0 warnings/errors; 300/300 intended SolutionTrace completions; all 240 bypass attempts rejected; every campaign flight gate remained required; surface connections, movement, shared exits, hazard shells and the new containment audit passed in all 30 rooms.
- Refreshed the Windows package at `builds/windows/Velocitex/Velocitex.exe` (189 files, 354.6 MB). The first export attempt correctly stopped because the previous packaged game was still running; after closing only that project process, the clean export passed.

### Known test-harness issue

- Forced headless bypass and flight-gate processes can still print the existing ObjectDB/resource cleanup warning after logical PASS and exit code 0. The packaged build/export succeeds normally.

### Exact next action

Play the refreshed Windows export and inspect the arrowed slopes in Rooms 08, 10, 22, 25 and 30, then approach rings and tunnel-like geometry near walls from first-person view. Report the first room and object that appears cropped, if any.

## 2026-07-18 - independent per-room rework in progress (Rooms 01-06 verified)

- Completed independent room-level reworks for Rooms 02-06 and an additional independent Room 01 audit. Rooms 01-06 now complete their intended traces ten consecutive times, reject direct/forward/constant-steering bypasses, pass surface and shell containment checks, and use the shared sealed exit.
- Room 01 now arms its exit immediately after the legal final landing rather than at the door plane, so a valid high-speed arrival opens the door before impact. Both rings remain individually required, including against left/right wall-bounce attempts.
- Room 02 was rebuilt into a cleaner symmetric route with the same four-button order puzzle; its walls are continuous, wrong-order input flashes plain red and the button dots move with their plates.
- Rooms 04-05 require their lever and ordered button sequences. The Room 05 barrier seals the complete bypass envelope, and Room 05's second button is on the middle platform.
- Room 06 is now a longer four-bridge glass route. Its entry ring is single-use and required, and each frictionless bridge exposes a timed-break contract.
- The shared surface runtime now breaks frictionless glass after sustained contact and restores it on respawn. Super-elastic and gelatin surfaces amplify vertical bounce while preserving tangential momentum. Sticky and elastic visuals are canonical across rooms; accelerator arrows use one centered, physically correct column with local duplicate overlays removed from Rooms 08, 10 and 22.
- The corrected elastic behavior intentionally invalidated old Room 09, 10 and 25 traces; their dedicated room reworks must adapt the route and traces rather than reintroduce momentum loss.
- Reworked the Rings, Licorice Stripes, Sugar Cracks and Caramel Drips candy visuals so the pattern icons and rendered candy use distinct, related silhouettes. Added a dedicated pattern-preview capture argument and inspected real 1600x900 captures.
- The shared exit carving now clears the complete frame/header/arrow envelope, and its backing wall remains behind the visible frame. Achievement-completion popups are deferred until the next room transition finishes; the popup's visible bottom inset now matches its side inset. The pause panel was reduced to fit its smaller button set.
- Room 07 is now a real S-shaped absorbing-surface puzzle with two ordered plates and a required precision stop. `Perfect Stop` uses a much tighter optional hold/radius/speed condition than the normal room solution. Its solution, achievement positive/negative, bypass, surface, containment, shell and exit checks pass.
- Double Bounce telemetry now advances only across two distinct elastic collider instances; repeated hits on one large pad remain at one. Room-transfer SFX is restored immediately after the opaque handoff and before gameplay input returns, with a regression assertion for the playable next-room state.
- Current verification: build 0 warnings/errors; movement, profile, UI, room transfer, advancement, advancement-notification and shared exit tests pass. Known Godot ObjectDB/resource cleanup warnings still appear only after logical PASS with exit code 0 in some forced headless runs.

### In progress

- Room 08 is complete as a three-accelerator switchback with two turns, an `E` lever and a physical barrier. All three accelerators and the lever are required; `Blue Streak` has separate positive/negative coverage, and eight blind/direct bypass patterns fail.
- Room 09 is complete as an ordered double-vault: two floor plates, two physically distinct super-elastic membranes and two centered flight gates are all required. Its long vertical route now lands on a guarded recovery deck before the centered exit. Double Bounce is latched when the second distinct membrane is hit, so touching the normal landing deck cannot erase the earned condition before the door.
- Room 09 verification passed: build 0 warnings/errors; ten consecutive intended completions; direct-goal, forward-only and six constant-steering routes rejected; distinct-surface positive/negative movement assertions; shell containment and the shared exit audit. Its panorama viewpoints now cover the start vault, second membrane arc and final landing instead of showing an uninformative ceiling/backward view.
- Dedicated Room 10 and Room 11 agents are active. Every remaining room through Room 30 will receive its own delegated room audit and room-specific positive/negative tests before the final regression.

### Exact next action

Finish the dedicated Room 10 and Room 11 reworks, then rotate their agent slots through Rooms 12-30 one room at a time. Only after every room trace and bypass check passes, complete the global audio/achievement/cosmetic regression, regenerate useful 2560x1440 panoramas and refresh the Windows export.

## 2026-07-18 - independent Room 11 low-gravity rework complete

- Rebuilt Room 11 into a centered, long-form low-gravity slalom. The player must use the canonical SuperElastic membrane to launch, then actively steer left and right through four widely separated rings before air control is removed at the volume boundary and the normal landing run begins.
- Added shared profile-driven low-gravity air control. `low_gravity.tres` is the only force profile that enables it; `ForceVolume3D` registers and removes its source on entry/exit, and `PlayerBall` clears every source immediately on respawn. Ordinary airborne movement remains input-free.
- The room completion contract requires the amplified SuperElastic bounce, low-gravity contact, measurable lateral air steering, all four ordered rings, successful removal of air control outside the volume and a clean respawn state. Direct goal entry cannot satisfy the route state.
- Replaced the old wall arrangement with one centered continuous route inside the closed shell. The start deck reaches the back wall, the start-to-ramp seam measures exactly `0.000 m` gap and `0.000 m` step, the airborne section uses only the outer room shell, the guarded landing deck is centered and the shared exit door is centered on the final wall without clipping.
- Updated `resources/solutions/room_11_solution.tres` for the deliberate left/right air-steering route and added `scripts/Test-Room11AirControl.ps1` for positive in-volume control plus negative exit/respawn assertions.
- Verification passed: build 0 warnings/errors; Room 11 SolutionTrace 10/10; direct-goal, forward-only and six sustained steering bypasses rejected; low-gravity entry/exit/respawn smoke clean; hazard respawn clean; surface connections clean; shell containment clean; shared exit presentation clean; global movement smoke still confirms zero ordinary air control. Room 15's existing low-gravity/wind/rail trace also remains 10/10 after the shared profile change. The dedicated Room 11 tests exit without ObjectDB/resource leaks.
- Panorama viewpoints were updated to frame the new start-to-slalom and landing-to-launch views, but panorama files were intentionally not regenerated yet; regeneration remains deferred until every room rework is complete.

### Exact next action

Begin the dedicated Room 12 rework, then continue one room at a time through Room 30. After all room-specific tests pass, run the complete regression, regenerate all 2560x1440 panoramas once, and refresh the Windows export.

## 2026-07-18 - Room 10 independent chapter-exam rework complete

- Rebuilt Room 10 as a multi-stage surface exam instead of an automatic straight route. The player must carry slope momentum over timed frictionless glass, brake and weave across two graphically numbered sticky-yard plates in order, enter the only physical opening through the centered accelerator lane, then commit to the airborne finale.
- The finale now contains two physically separate `SuperElastic` bodies. Double Bounce is latched only after distinct collider IDs are observed consecutively; repeated contact with one membrane cannot satisfy either the room or the advancement.
- The acceleration ring is centered on the measured first rebound arc, supplies the speed required to reach the second membrane and is an explicit completion requirement. The second rebound lands on a guarded narrow runout leading to the centered shared exit.
- All adjoining ground surfaces are mathematically flush. Rails remain continuous along every grounded route edge, the sticky-yard front barriers leave only the accelerator opening, and the only open rail spans are the two intentional airborne jumps.
- Removed local accelerator-arrow geometry from the room. The canonical shared material supplies one centered arrow column in its physical travel direction.
- Replaced the old trace with the deterministic `Room 10 - Surface Gauntlet` trace. A short identical warmup before every run removes first-run/reset drift without changing shared player physics.
- Verification passed: build 0 warnings/errors; the real trace completed ten consecutive runs with `double_bounce=True`; direct-goal, forward-only and six sustained steering bypasses were rejected; five adjoining seams measured exactly 0.000 m gap/step; shell containment, hazard restart and the shared Rooms 01-30 exit audit passed. The movement smoke supplied the negative same-collider Double Bounce assertion and passed.

### Exact next action

Complete the independent Room 11 rework, then continue with Room 12. After all room agents finish, regenerate Room 10's 2560x1440 panoramas together with the other changed rooms and refresh the Windows export.

## 2026-07-18 - Room 12 independent strong-gravity rework complete

- Rebuilt Room 12 as a centered high-gravity puzzle instead of an offset automatic run through three small apertures. The old arbitrary rings, forced landing lane and side-biased exit were removed.
- The player must deliberately arm two visually numbered floor pads in left-right order. A physical barrier remains closed after wrong-order input and opens only after the correct sequence, so holding forward cannot enter the drop.
- The opened route feeds a wide central strong-gravity shaft. The measured fall must gain at least 10 m/s of downward speed and land on the canonical shared SuperElastic membrane; the amplified rebound then carries the player to the raised centered exit deck.
- Completion requires all four independent pieces of route state: both ordered pads, actual strong-gravity volume contact, a measured high-gravity fall and the verified amplified elastic launch. Direct goal entry and any missing mechanic are rejected.
- Replaced the fragmented internal walls with two continuous guarded decks and the closed outer shell. The start deck reaches the back wall, the exit deck reaches the shared dark corridor, all visible/collision geometry stays inside the room and the shared exit is centered at the final wall without clipping.
- Updated `resources/solutions/room_12_solution.tres` and added `scripts/Test-Room12Mechanics.ps1`, including wrong-order negative coverage, gravity-without-elastic negative coverage and elastic-without-gravity negative coverage.
- Verification passed: build 0 warnings/errors; Room 12 SolutionTrace 10/10; direct-goal, forward-only and six sustained steering bypasses rejected; mechanics positive/negative smoke passed; surface connections, shell containment, hazard restart and the shared Rooms 01-30 exit presentation passed.

### Known test-harness issue

- The dedicated mechanics process and shared bypass/exit harnesses can print the existing Godot ObjectDB/resource cleanup warning after their logical PASS and zero exit code. No Room 12 functional check failed.

### Exact next action

Continue the independent room-by-room rework with Room 13. Regenerate Room 12's two 2560x1440 panoramas only after the remaining room geometry is stable, together with the final all-room panorama refresh.

## 2026-07-19 - final Rooms 20-30 rework, shared presentation fixes and full campaign audit

- Completed the remaining independent room reworks. Rooms 20-25 retain their multi-stage mechanic routes with corrected containment and useful framing. Room 26 is now a four-gate alternating vacuum slalom that requires its valve, player cannon, active steering, measured rise and exit landing. Room 27 uses four alternating polarity fields and gates with a mandatory reversal lever. Room 28 requires boarding, two balance latches and arrival on its moving counterweight platform. Room 29 requires opposite lateral polarity crossings around physical chicanes, and Room 30 requires three ordered weave checkpoints before its calibrated `Exact Fare` break and absorbing stop.
- Corrected the shared exit assembly in Rooms 01-30. The complete frame and header sit proud of the room-side wall, the floor remains threshold-free through the doorway, the corridor stays sealed and every lever pedestal/handle base remains level rather than rolled onto its side. The all-room exit presentation smoke verifies these properties in every room.
- Replaced the reported device and surface SFX with newly synthesized stereo cues, including player/interference cannons, pistons, moving platforms, sticky and accelerator contacts, the SuperElastic bounce and strong-gravity entry. The player SFX smoke loads every replacement through the `SFX` bus and verifies audible PCM, minimum duration, impact tiers and pooled playback.
- Reworked the ending handoff. Candy-to-mouth contact now starts a full black fade; the exact requested four-line credits fade in at large scale, remain on black and fade out; the shared startup loading presentation then appears before the main menu. The ending smoke measured the credits at `0.84` of viewport width and `0.69` of viewport height and verified the complete ordering.
- Replaced Room 12's ambiguous elongated gravity particles, which read as upside-down `Y` shapes, with billboarded filled downward arrows using a dedicated transparent texture. Both Room 12 panoramas were refreshed and visually inspected; the markers now have a straight shaft, broad triangular head and unambiguous downward point.
- Fixed a Room 13 state regression found by the complete solution run: a brief second contact at the edge of the wind field can no longer erase an already verified pulsing-wind flight. Its intended lever, accelerator, two-gate wind route, clean-wind achievement and three animated fan banks all pass again.
- Removed two final rolling-surface steps without changing their routes: Room 09's start surface now meets its original launch lip without the former `0.017 m` step, and Room 21's start deck now meets its descent without the former `0.275 m` step. Both rooms still complete ten consecutive deterministic solutions.
- Regenerated all 61 current panorama captures at 2560x1440 and visually scanned contact sheets for Rooms 01-30. Rooms 11-13 follow their playable routes, Room 26 visibly fills its shaft with four vacuum gates and support frames, and Room 27's second panorama was reframed away from an empty wall to show its polarity slalom.
- Final verification passed: build `0` warnings/errors; all 30 intended traces completed ten times (`300/300`); direct-goal, forward-only and six sustained steering bypasses were rejected in all 30 rooms (`240` rejected patterns); all meshes/collision shapes remain inside their room shells; every adjoining rolling surface is flush; all 30 hazard floors restart correctly; all 30 exits pass the wall-proud/threshold-free/level-base audit; ending, campaign flow, SFX and panorama checks pass. The Room 20 Medium/720p performance smoke averaged `164.1 FPS` with a `54.4 FPS` one-percent low on Intel UHD Graphics.
- Refreshed the clean Windows package after removing temporary QA contact sheets. Packaged startup passed at `builds/windows/Velocitex/Velocitex.exe` with 189 files and a total size of `360.2 MB`.

### Known test-harness issue

- Some forced headless bypass, ending and visual smoke processes still print the existing Godot ObjectDB/resource cleanup warning after their logical PASS and exit code 0. Build, panorama capture and packaged startup do not use those forced shutdown conditions.

### Exact next action

Play the refreshed Windows export from Room 01 through the ending and report the first visual, audio or route behavior that differs from the verified campaign. In Room 12, confirm that every strong-gravity marker reads immediately as a downward arrow during motion.

## 2026-07-19 - recurring mechanic visual consistency audit

- Audited recurring gameplay devices and profiled surfaces across Rooms 01-30. Doors, levers, player cannons, interference cannons and brittle barriers already build their complete appearance from one shared runtime class, so their repeated instances remain identical without room-local visual copies.
- Replaced the ten room-local ordered-button marker implementations with one shared `RoomGeometry.AddSequencePips` builder. Rooms 02, 04, 05, 07, 09, 10, 12, 14, 21 and 22 now use the same circular inset markers, dimensions, spacing, height, material, emission and `SequencePip` naming, together with one canonical copper button frame.
- Standardized every `FlightGate3D` on one canonical copper frame material while retaining radius changes required by each physical opening. Standardized Frictionless, Absorbing and OneWayGrip surfaces by physics kind, so later-room tints can no longer make the same mechanic look unrelated to its introduction.
- Preserved deliberate semantic distinctions: positive/negative polarity signs and gates remain pink/blue, and mechanics with genuinely different behavior retain their own silhouette and cues.
- Verification passed: build `0` warnings/errors; ordered-button mechanics passed in Rooms 02, 04, 05, 12, 21 and 22; flight-gate boost geometry passed; surface connections passed in Rooms 01-30; Room 30 completed its ratchet/absorber route ten times with the canonical materials; all 61 panoramas were regenerated at 2560x1440 and passed the freshness check.

### Current unrelated regression findings

- The complete solution sweep currently stops at Room 18 because its existing `Moving With It` clean-transit condition records a `5.58 m` lateral offset. Rooms 01-17 passed before that stop, and Rooms 19-28 plus Room 30 passed when run separately.
- Room 29 currently reaches both polarity fields and both ordered gates but times out beside its exit. The Room 10 no-boost negative harness also reports that its route remains completable without the ring boost. None of these paths use the visual builders changed in this audit.

### Exact next action

Play the refreshed Windows export and compare ordered floor buttons, flight rings, frictionless glass, absorbing foam and one-way ratchet surfaces when they recur in later rooms. Separately repair the known Room 18, Room 29 and Room 10 negative-harness route regressions before claiming a fresh `300/300` full-suite pass.

## 2026-07-19 - first-person, camera, glass, caramel and Spring Vault follow-up

- First-person mode now hides both the candy mesh and its complete particle trail, including particles already alive at high speed. Returning to third person restores the configured trail normally.
- The currently selected camera mode now survives direct room-to-room handoffs. Starting again after returning to the main menu, or launching the game again, uses the configured default camera instead of the previous room's temporary choice.
- Verified the existing `Accessibility / Subtitles` toggle end to end: it is exposed in Settings, persists through `SettingsStore` and suppresses story/transfer subtitles when disabled. The UI smoke now explicitly saves, reloads and displays the disabled state.
- Room 06 moving glass sheen is tied to its own timed glass body. It disappears on break and returns only when that glass is restored on respawn.
- Room 07 buttons are immediate ordered floor buttons again. The 0.8-second progressive charge now belongs to the precision stop ring after them, with twelve visible radial charge segments; its intended solution passed ten consecutive completions.
- Room 09's two start buttons were raised out of the start deck and are visibly readable from spawn. Bounce evidence is now latched instead of being overwritten by a later marginal contact, and goal entry refreshes the final bounce before evaluating completion.
- Room 09's second membrane was extended across the first arc's landing range. Its second flight ring now occupies a full physical aperture, preventing routes under or around it; the intended two-membrane/two-ring solution still passed ten consecutive completions.
- The shared exit backing wall now overlaps the rear of every header by 0.14 m, sealing the intermittent top slit without covering the visible frame or arrow. The Rooms 01-30 exit presentation audit passed after the change.
- Regenerated all 61 panoramas at 2560x1440. Build, movement, Room 06, Room 07, Room 09, camera-transfer, UI, opening, ending, exit-presentation and panorama checks passed.

### Current unrelated regression findings

- The previously recorded Room 18, Room 29 and Room 10 no-boost regression findings were not changed or reclassified by this focused follow-up.

### Exact next action

Export and launch the refreshed Windows package, then manually verify Room 06 glass break sheen, Room 07 ring charging, Room 09's visible start buttons and mandatory second vault, and the top edge of several exit frames from low first-person angles.

## 2026-07-19 - Room 19 post-piston magnet course and exit fix

- Replaced Room 19's two invisible velocity-changing flight-ring requirements with a bounded low-gravity section and two large floating magnets. The red and blue magnets face the airborne route from opposite walls, apply alternating lateral magnetic force, and include animated force streaks that visibly show each push direction.
- The magnetic fields do not register an air-control source. Only the bounded low-gravity volume enables airborne steering, and its exit callback verifies that the source has been removed before the room can complete, preventing altered movement controls from leaking past the course.
- Room completion and the exit door now depend on the mechanics the player can actually see: the ordered setup plates, armed and fired piston, low-gravity entry, both magnetic fields in order, and clean low-gravity exit. The obsolete flight-ring condition no longer blocks the door.
- Verification passed: build 0 warnings/errors; Room 19 mechanics and Piston Perfect positive/negative tests passed; the full intended Room 19 solution opened completion ten consecutive times. Both Room 19 panoramas were regenerated and visually inspected; both magnets and their opposing force streaks are visible from the piston approach.

### Exact next action

Continue the remaining requested global and room-specific fixes, beginning with the exclusive rail-attachment guard and the Room 15 low-gravity/rail rework, then refresh the Windows export after the next stable batch.

## 2026-07-19 - complete requested gameplay, geometry and presentation follow-up

- Made rail attachment exclusive per player while retaining bidirectional travel. Extended Room 15's low-gravity flight into a six-ring route whose rings preserve horizontal momentum and only cancel downward loss; corrected both rail exits so they land above the final deck. Extended Rooms 17 and 20 into long airborne cannon gauntlets with 24 and 12 active cannons respectively, removed the floating lane cylinders and gave every cannon a permanent physical hitbox.
- Rebuilt brittle glass around one intact, correctly tiled pane and hidden cracked shards. Breaking now removes the intact sheen, reveals the shards briefly and plays the regenerated stereo glass-shatter SFX. Room 24's non-boost path no longer breaks its optional pane, and all accelerator arrow animations move with the route direction.
- Standardized every floor button after room construction: each button is snapped onto a real supporting surface, receives the correct sequence-pip count, flashes red when entered out of order and produces one matching requirement light above the exit. The expanded Rooms 01-30 exit audit verifies button support, numbering, lights, wall-proud side/header frames, matching frame hitboxes and threshold-free doorways.
- Corrected the remaining geometry defects found by the full sweep: Room 17's start deck reaches the rear wall, Room 19's low-gravity volume stays inside its side walls, and Room 23's second descent button now rests visibly on its slope. All adjoining rolling surfaces and all relevant visible/collision geometry now remain connected and contained in every room shell.
- Recalibrated Rooms 09, 18, 20, 23 and 29. Room 09 reaches its relocated second button and returns to the centered exit; Room 18's clean-transit allowance matches the usable platform without accepting rail contact; Room 20's longer platform route reliably accepts both numbered plates; Room 23 waits for all eight bank-charge segments before its full-speed release while early release remains possible but insufficient; Room 29 steers back into its exit after both polarity crossings.
- Kept Room 30 completion testing focused on its required ratchet, three-stage weave, glass break and absorbing stop. `Exact Fare` remains an optional 40-44 m/s advancement rather than invalidating an otherwise complete legal route.
- Verification passed: build `0` warnings/errors; all 30 SolutionTraces completed ten times (`300/300`); all direct-goal, forward-only and six sustained-steering bypasses were rejected; surface seams, shell containment, hazard restarts, exit/button presentation, movement, player SFX, flight gates, UI/subtitle settings, camera transfer, opening, ending and campaign-flow checks passed. All 61 panoramas were regenerated at 2560x1440 and passed freshness/resolution validation. Room 20 Medium/720p averaged `135.3 FPS` with a `44.1 FPS` one-percent low on Intel UHD Graphics.
- Exported and packaged the refreshed Windows build at `builds/windows/Velocitex/Velocitex.exe` (`189` files, `373.8 MB`) after the final build and panorama refresh.

### Known test-harness issue

- Several forced headless smoke processes still print the existing Godot ObjectDB/resource cleanup warning after their logical PASS and zero exit code. No functional assertion in the final suite failed.

### Exact next action

Play the refreshed Windows export from Room 01 through the ending and report the first remaining visual, audio or route mismatch.

## 2026-07-19 - final request-by-request gap audit

- Rechecked the complete accumulated feedback list against the current runtime and tests instead of relying on the earlier completion notes. Moved Room 25's first numbered checkpoint off the accelerator and onto the normal start deck. Replaced its mismatched child line with `Why does it keep changing how it rolls?`, regenerated only `room25.mp3`, and updated the campaign assertion and both voice-generation manifests.
- Fixed three regressions exposed by the audit. Room 16's landing latch is now far enough off center that a straight cannon landing cannot activate it, while the intended grounded steering route still does. Room 22's trace now reaches and operates the top lever after the revised one-way surfaces. Room 23's Full Account award now depends on an actual full 32 m/s charge/release rather than an unrelated route-stage state; a partial 24 m/s release remains sufficient to play and finish the room.
- Removed the last detected surface step: Room 14's incoming and outgoing interchange extensions were 0.8 m below the central platform and are now exactly flush. The four-route stay/switch rail logic and Perfect Switch distinction still pass.
- Made Room 10's rebound ring mechanically necessary by raising the second membrane, its supporting tower, final runout, rails and wall-mounted exit together. The boosted route still completes ten times; with the ring's boost disabled, the player now falls before the raised second membrane.
- Refreshed stale QA coverage after the Room 19 magnet replacement and Room 25 surface-relay rebuild so the flight-gate suite tests only rooms that still contain momentum-critical flight gates. Hardened older Room 04, UI, flight-gate and campaign wrappers so Godot cleanup warnings no longer hide successful zero-exit assertions.
- Final verification passed: build `0` warnings/errors; all 30 SolutionTraces completed ten times (`300/300`); all 30 rooms rejected direct-goal, forward-only and six sustained-steering bypasses; all shell containment, rolling-surface connection, hazard-floor and exit/button/frame checks passed. Movement/trail, camera transfer/reset, subtitles/UI, 35 story clips, player SFX, ending credits/loading/menu return, momentum-critical flight rings, all 21 advancement paths, and all 30 campaign transitions with 60 snapshots passed. The menu preview at 1920x1080 confirms that the former orange top strip is absent.

### Exact next action

The verified Windows package was exported successfully with 189 files (`373.8 MB`) and passed packaged startup. Launch `builds/windows/Velocitex/Velocitex.exe` and use it for the next manual campaign playthrough.

## 2026-07-19 - door-header, floor-button and Room 14 rail cleanup

- Sealed the remaining top slit above every shared exit frame by increasing the backing/header overlap to `0.50 m` while keeping the visible frame fully proud of the wall and preserving the threshold-free corridor. The complete Rooms 01-30 exit audit passes frame attachment, matching hitboxes, platform contact, carved openings and sealed corridors.
- Made floor-button placement wait for the first registered physics frame before snapping to its supporting surface. Physical buttons now trigger from actual plate-height contact, including while climbing a ramp, without depending on an oversized area entry. Tall airborne route gates are classified separately, so they keep working as flight gates and no longer create false door requirement lights.
- Recalibrated Room 20 after the moving-platform plates were reset on departure. Its deterministic route now centers after transit, enters the level piston and clears both piston-flight gates through the forty-cannon low-gravity course.
- Kept Room 19's rear blue magnet field within the room shell by trimming only its excess rear depth; the two-magnet low-gravity route and Piston Perfect positive/negative cases still pass.
- Rebuilt Room 14's rail presentation as four straight, parallel color-coded incoming and outgoing paths. Only the matching colored floor guides intersect on the enlarged central interchange, where the player can deliberately change routes. The same-color route awards Perfect Switch; the different-color correction route completes but withholds it. All three final plates are seated into their platform rather than protruding.
- Refreshed all 61 menu panoramas at 2560x1440 after the final geometry/material changes.
- Final verification passed: build `0` warnings/errors; all 30 SolutionTraces completed ten times (`300/300`); all rooms rejected direct-goal, forward-only and six sustained-steering bypasses; all Rooms 01-30 exit presentations, surface connections and shell-containment checks passed; Room 02 red denied-feedback and four-button ordering passed; player SFX and gameplay music-scope checks passed; panorama freshness/resolution passed.

### Exact next action

The refreshed Windows package was exported successfully with 189 files (`373.8 MB`) and passed its packaged-startup check. Use `builds/windows/Velocitex/Velocitex.exe` for the next manual campaign playthrough.
