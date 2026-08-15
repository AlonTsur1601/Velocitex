using Godot;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Rooms;

internal static class BlenderRoomEdits
{
    private const string EditDirectory = "res://resources/blender_room_edits";

    public static void Apply(Node3D room, int roomNumber)
    {
        string path = $"{EditDirectory}/Room{roomNumber:00}.json";
        if (!Godot.FileAccess.FileExists(path))
        {
            return;
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        Variant parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError($"BLENDER_ROOM_EDIT_FAIL: invalid JSON in {path}");
            return;
        }

        Godot.Collections.Dictionary data = parsed.AsGodotDictionary();
        ApplyWalls(room, ReadArray(data, "walls"));
        ApplyPlatforms(room, ReadArray(data, "platforms"));
    }

    private static void ApplyWalls(Node3D room, Godot.Collections.Array edits)
    {
        Node? wallsRoot = room.GetNodeOrNull<Node>("EditableWalls");
        if (wallsRoot is null)
        {
            return;
        }

        HashSet<string> exportedNames = new(StringComparer.Ordinal);
        foreach (Variant value in edits)
        {
            Godot.Collections.Dictionary edit = value.AsGodotDictionary();
            string name = edit["name"].AsString();
            exportedNames.Add(name);
            if (wallsRoot.GetNodeOrNull<StaticBody3D>(name) is not StaticBody3D wall)
            {
                GD.PushError($"BLENDER_ROOM_EDIT_FAIL: missing wall {room.Name}/{name}");
                continue;
            }

            ApplyBoxEdit(room, wall, edit, resetBoxChildren: true);
        }

        foreach (StaticBody3D removedWall in wallsRoot.GetChildren()
            .OfType<StaticBody3D>()
            .Where(wall => !exportedNames.Contains(wall.Name.ToString()))
            .ToArray())
        {
            wallsRoot.RemoveChild(removedWall);
            removedWall.QueueFree();
        }
    }

    private static void ApplyPlatforms(Node3D room, Godot.Collections.Array edits)
    {
        List<StaticBody3D> platforms = new();
        CollectPlatformBodies(room, platforms);
        bool hasExportedExitShell = false;
        foreach (Variant value in edits)
        {
            Godot.Collections.Dictionary edit = value.AsGodotDictionary();
            string exportedName = edit["name"].AsString();
            hasExportedExitShell |= exportedName.EndsWith("_ExitCorridorFloor", StringComparison.Ordinal);
            int separator = exportedName.IndexOf('_', 4);
            if (!exportedName.StartsWith("REF_", StringComparison.Ordinal) ||
                separator < 0 ||
                !int.TryParse(exportedName.AsSpan(4, separator - 4), out int index) ||
                index < 0 ||
                index >= platforms.Count)
            {
                GD.PushError($"BLENDER_ROOM_EDIT_FAIL: invalid platform reference {exportedName}");
                continue;
            }

            // The Blender reference scene also shows collision boxes belonging to
            // mechanisms. RoomGeometry boxes and the explicitly named shared-exit
            // shell are editable; moving any other hitbox would detach it from its owner.
            StaticBody3D platform = platforms[index];
            if (!platform.HasMeta(RoomGeometry.StickyRollSfxMetadata) &&
                !IsEditableExitReference(platform, exportedName[(separator + 1)..]))
            {
                continue;
            }

            ApplyBoxEdit(room, platform, edit, resetBoxChildren: false);
        }

        if (!hasExportedExitShell)
        {
            ApplyDefaultExitPolish(room);
        }
    }

    private static void ApplyDefaultExitPolish(Node3D room)
    {
        ResizeAndMove(room, "ExitCorridorFloor", new Vector3(1.29f, 1.0f, 0.99f), new Vector3(0.0f, 0.0f, 0.055f));
        ResizeAndMove(room, "ExitCorridorCeiling", new Vector3(1.29f, 1.0f, 0.99f), new Vector3(0.0f, 0.0f, 0.055f));
        ResizeAndMove(room, "ExitCorridorLeftWall", Vector3.One, new Vector3(-0.29f, 0.0f, 0.09f));
        ResizeAndMove(room, "ExitCorridorRightWall", Vector3.One, new Vector3(0.29f, 0.0f, 0.09f));
        ResizeAndMove(room, "ExitCorridorEndWall", new Vector3(1.14f, 1.06f, 1.0f), Vector3.Zero);
        ResizeAndMove(room, "ClosedDoorBlocker", new Vector3(1.15f, 0.97f, 1.0f), new Vector3(0.0f, -0.05f, 0.0f));
        ResizeAndMove(room, "ExitDoorBackingAbove", new Vector3(1.0f, 1.014f, 1.0f), new Vector3(0.0f, -0.10f, 0.0f));
    }

    private static void ResizeAndMove(Node3D room, string name, Vector3 sizeScale, Vector3 localOffset)
    {
        if (room.FindChild(name, recursive: true, owned: false) is not StaticBody3D body ||
            body.GetChildren().OfType<CollisionShape3D>().FirstOrDefault(child => child.Shape is BoxShape3D) is not CollisionShape3D collision ||
            collision.Shape is not BoxShape3D box)
        {
            return;
        }

        Vector3 previousSize = box.Size;
        Vector3 size = previousSize * sizeScale;
        body.Position += localOffset;
        box.Size = size;
        MeshInstance3D? visual = body.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
        if (visual is not null && IsBoxVisual(visual, previousSize))
        {
            visual.Mesh = SurfaceMeshFactory.CreateTiledBox(size);
        }
    }

    private static bool IsEditableExitReference(StaticBody3D body, string referenceName)
    {
        if (!body.Name.ToString().Equals(referenceName, StringComparison.Ordinal))
        {
            return false;
        }

        return referenceName is
            "ExitCorridorFloor" or
            "ExitCorridorCeiling" or
            "ExitCorridorLeftWall" or
            "ExitCorridorRightWall" or
            "ExitCorridorEndWall" or
            "ClosedDoorBlocker" or
            "ExitDoorBackingLeft" or
            "ExitDoorBackingRight" or
            "ExitDoorBackingBelow" or
            "ExitDoorBackingAbove";
    }

    private static void ApplyBoxEdit(
        Node3D room,
        StaticBody3D body,
        Godot.Collections.Dictionary edit,
        bool resetBoxChildren)
    {
        CollisionShape3D? collision = body.GetChildren()
            .OfType<CollisionShape3D>()
            .FirstOrDefault(child => child.Shape is BoxShape3D);
        if (collision?.Shape is not BoxShape3D box)
        {
            return;
        }

        Vector3 size = ReadVector3(edit["size"].AsGodotArray());
        Transform3D geometryTransform = ReadTransform(edit["transform"].AsGodotArray());
        Transform3D targetGlobal = room.GlobalTransform * geometryTransform;
        body.GlobalTransform = resetBoxChildren
            ? targetGlobal
            : targetGlobal * collision.Transform.AffineInverse();
        Vector3 previousSize = box.Size;
        box.Size = size;

        MeshInstance3D? visual = body.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
        if (visual is not null && IsBoxVisual(visual, previousSize))
        {
            visual.Mesh = SurfaceMeshFactory.CreateTiledBox(size);
            if (resetBoxChildren)
            {
                visual.Transform = Transform3D.Identity;
                collision.Transform = Transform3D.Identity;
            }
        }

        if (body.HasMeta(RoomGeometry.BarrierBaseSeamSizeMetadata))
        {
            body.SetMeta(RoomGeometry.BarrierBaseSeamSizeMetadata, size);
            body.SetMeta(RoomGeometry.BarrierBaseSeamOffsetMetadata, Vector3.Zero);
        }
    }

    private static bool IsBoxVisual(MeshInstance3D visual, Vector3 collisionSize)
    {
        if (visual.Mesh is null)
        {
            return false;
        }

        Vector3 meshSize = visual.Mesh.GetAabb().Size;
        return meshSize.IsEqualApprox(collisionSize) ||
            new Vector3(
                Mathf.Abs(meshSize.X - collisionSize.X),
                Mathf.Abs(meshSize.Y - collisionSize.Y),
                Mathf.Abs(meshSize.Z - collisionSize.Z)).Length() < 1.0f;
    }

    private static void CollectPlatformBodies(Node node, List<StaticBody3D> output)
    {
        if (node is StaticBody3D body &&
            !body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata) &&
            body.GetChildren().OfType<CollisionShape3D>().Any(child => child.Shape is BoxShape3D))
        {
            output.Add(body);
        }

        foreach (Node child in node.GetChildren())
        {
            CollectPlatformBodies(child, output);
        }
    }

    private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary data, string key)
    {
        return data.TryGetValue(key, out Variant value)
            ? value.AsGodotArray()
            : new Godot.Collections.Array();
    }

    private static Vector3 ReadVector3(Godot.Collections.Array values)
    {
        return new Vector3(
            values[0].AsSingle(),
            values[1].AsSingle(),
            values[2].AsSingle());
    }

    private static Transform3D ReadTransform(Godot.Collections.Array values)
    {
        Basis basis = new(
            new Vector3(values[0].AsSingle(), values[1].AsSingle(), values[2].AsSingle()),
            new Vector3(values[3].AsSingle(), values[4].AsSingle(), values[5].AsSingle()),
            new Vector3(values[6].AsSingle(), values[7].AsSingle(), values[8].AsSingle()));
        return new Transform3D(
            basis,
            new Vector3(values[9].AsSingle(), values[10].AsSingle(), values[11].AsSingle()));
    }
}
