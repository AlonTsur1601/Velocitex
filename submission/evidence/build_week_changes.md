# Build Week changes and evidence

## Scope statement

Velocitex existed before the Build Week submission period. The source repository was first snapshotted on July 20, 2026, so that initial commit is **not** evidence that the game was first created during Build Week. This document isolates the meaningful work recorded during the submission period from the pre-existing project and cites only local, reviewable evidence.

## Pre-existing project

Before Build Week, Velocitex already had its core identity: a Godot/C# Windows momentum-puzzle game, a candy-machine setting, a multi-room campaign direction, and the foundation for rolling movement, camera control, rooms, UI, saves, customization, and advancements. Do not describe the entire game as newly created for the challenge.

## Meaningful Build Week work recorded in the project

The dated July 19 entries in `IMPLEMENTATION_STATUS.md` record the following Build Week extensions and verification work:

- A complete requested gameplay, geometry, and presentation pass across Rooms 01-30, including rail attachment behavior, long airborne cannon courses, brittle-glass presentation, standardized floor buttons, route recalibration, and exit presentation fixes.
- A complete automated verification sweep: zero build warnings/errors; 30 solution traces run ten times each (300/300); bypass rejection; room-shell, surface-connection, hazard, exit/button/frame, campaign-flow, save, UI/subtitle, camera, story, audio, advancement, and panorama checks.
- A refreshed Windows export and packaged startup validation. The recorded package has 189 files and is approximately 373.8 MB before compression.
- Follow-up fixes and regression audits covering first-person trail behavior, camera transfer, subtitle persistence, Room 06 glass, Room 07 charge feedback, Room 09 route visibility, Room 10’s required rebound ring, Room 14 rail layout, Room 19 magnets, Room 20 recalibration, and Room 25’s checkpoint and dialogue corrections.

## Evidence locations

| Evidence | Location | What it supports |
| --- | --- | --- |
| Dated engineering and QA record | `IMPLEMENTATION_STATUS.md` | The changes and test outcomes listed above. |
| Repeatable test commands | `scripts/Test-*.ps1`, `scripts/Build.ps1`, `scripts/Export-Windows.ps1` | The project has explicit build, export, room, UI, save, and campaign verification tooling. |
| Current Windows package | `builds/windows/Velocitex/` | A local test build exists; it must be re-tested immediately before release. |
| Primary Codex session | `/feedback` ID `019f5659-1238-7ce2-927c-da0d7c36a4cc` | The principal development thread supplied for judging. |

## Limits on claims

- The July 20 Git initial snapshot is a repository baseline, not a reconstruction of earlier source history.
- This file does not claim a specific calendar date for an individual source line unless it is shown by a dated local record or session evidence.
- Before submitting, verify that the final video, release archive, and repository state match the build tested by the judges.
