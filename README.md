# Velocitex

Velocitex is a 3D momentum-puzzle game built with Godot 4.7 .NET and C#.

## Local toolchain

The portable Godot editor is stored under `.tools/Godot` and is intentionally excluded from Git. NuGet and .NET CLI caches are also kept inside the project.

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

## Architecture

- `src/Core`: engine-facing contracts and serializable state.
- `src/Core/Save`: atomic two-slot-per-room campaign snapshots with corruption recovery.
- `src/Core/Profile`: base cosmetic catalog and atomic player-profile persistence.
- `src/Gameplay`: rigid-body player simulation, camera and gray-box test rooms.
- `src/UI`: textured menu world, loading flow, 3D candy customization, advancements and tabbed persistent settings.
- `scenes`: Godot scene composition.

See `IMPLEMENTATION_STATUS.md` for the exact continuation point.
