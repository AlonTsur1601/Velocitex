using Godot;
using System.Reflection;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class DoorDimmingSmokeTest : Node
{
    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts/door-dimming"));
        if (!await CaptureRoom(1, "res://scenes/MovementTestRoom.tscn") ||
            !await CaptureRoom(2, "res://scenes/Room02.tscn"))
        {
            return;
        }

        GD.Print("DOOR_DIMMING_PASS: Rooms 1 and 2 preserve their authored corridor geometry and use the identical shared corridor dimming path.");
        GetTree().Quit(0);
    }

    private async Task<bool> CaptureRoom(int roomNumber, string scenePath)
    {
        RoomRuntime? room = GD.Load<PackedScene>(scenePath)?.Instantiate<RoomRuntime>();
        if (room is null)
        {
            Fail($"Room {roomNumber} could not be instantiated.");
            return false;
        }

        AddChild(room);
        for (int frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        ExitDoor3D? door = room.GetNodeOrNull<ExitDoor3D>("ExitDoor");
        if (door is null)
        {
            Fail($"Room {roomNumber} has no ExitDoor.");
            return false;
        }

        Light3D[] doorLights = EnumerateDescendants(door).OfType<Light3D>().ToArray();
        if (doorLights.Length != 1 || doorLights[0].Name != "DoorFillLight")
        {
            Fail($"Room {roomNumber} does not contain exactly the canonical DoorFillLight.");
            return false;
        }

        MeshInstance3D[] corridorMeshes = EnumerateDescendants(door)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.GetParent().Name.ToString().StartsWith("ExitCorridor", StringComparison.Ordinal))
            .ToArray();
        MeshInstance3D[] depthMeshes = corridorMeshes
            .Where(mesh => mesh.MaterialOverride is ShaderMaterial material &&
                material.Shader?.Code.Contains("MODEL_MATRIX", StringComparison.Ordinal) == true &&
                material.Shader.Code.Contains("corridor_origin_world", StringComparison.Ordinal))
            .ToArray();
        if (corridorMeshes.Length != 5 || depthMeshes.Length != 4)
        {
            Fail($"Room {roomNumber} corridor material coverage is incomplete: meshes={corridorMeshes.Length}, depth_meshes={depthMeshes.Length}.");
            return false;
        }

        MeshInstance3D[] visibleImportedCorridorReferences = EnumerateDescendants(room)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Visible && mesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata))
            .Where(mesh => mesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata)
                .AsNodePath().ToString().Contains("ExitCorridor", StringComparison.Ordinal))
            .ToArray();
        if (visibleImportedCorridorReferences.Length != 5)
        {
            Fail($"Room {roomNumber} did not preserve all five authored corridor surfaces: visible={visibleImportedCorridorReferences.Length}.");
            return false;
        }
        if (visibleImportedCorridorReferences.Any(importedReference =>
        {
            string targetName = importedReference.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata)
                .AsNodePath().ToString();
            if (targetName.EndsWith("ExitCorridorEndWall", StringComparison.Ordinal))
            {
                return false;
            }

            return importedReference.MaterialOverride is not ShaderMaterial material ||
                material.Shader?.Code.Contains("render_mode unshaded, cull_disabled", StringComparison.Ordinal) != true ||
                !material.Shader.Code.Contains("mix(vec3(0.35, 0.39, 0.42), vec3(0.004, 0.006, 0.008), fade)", StringComparison.Ordinal) ||
                !material.Shader.Code.Contains("corridor_depth", StringComparison.Ordinal);
        }))
        {
            Fail($"Room {roomNumber} corridor surfaces did not receive the identical canonical corridor fade.");
            return false;
        }

        PlayerBall player = room.GetNode<PlayerBall>("Player");
        PlayerCameraRig cameraRig = room.GetNode<PlayerCameraRig>("CameraRig");
        cameraRig.SetInputEnabled(false);
        player.Freeze = true;
        player.ResetTo(new Transform3D(Basis.Identity, door.ToGlobal(new Vector3(0.0f, 0.72f, 5.8f))));
        for (int frame = 0; frame < 2; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        MeshInstance3D[] authoredPocketMeshes = EnumerateDescendants(room)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Visible && mesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata))
            .Where(mesh => mesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata)
                .AsNodePath().ToString().EndsWith("FrameCollision", StringComparison.Ordinal))
            .ToArray();
        Transform3D[] authoredPocketTransforms = authoredPocketMeshes
            .Select(mesh => mesh.GlobalTransform)
            .ToArray();
        if (authoredPocketMeshes.Length != 2)
        {
            Fail($"Room {roomNumber} did not load both Blender-authored door pocket pieces: visible={authoredPocketMeshes.Length}.");
            return false;
        }
        MeshInstance3D? authoredClosedDoor = EnumerateDescendants(room)
            .OfType<MeshInstance3D>()
            .SingleOrDefault(mesh => mesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata) &&
                mesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata)
                    .AsNodePath().ToString().EndsWith("ClosedDoorBlocker", StringComparison.Ordinal));
        if (authoredClosedDoor is null)
        {
            Fail($"Room {roomNumber} did not load the Blender-authored closed-door envelope.");
            return false;
        }
        Aabb authoredClosedDoorBounds = BoundsRelativeTo(door, authoredClosedDoor);
        Aabb[] authoredPocketBounds = authoredPocketMeshes
            .Select(mesh => BoundsRelativeTo(door, mesh))
            .OrderBy(bounds => bounds.GetCenter().X)
            .ToArray();
        if (!ValidateDoorState(roomNumber, door, authoredClosedDoorBounds, authoredPocketBounds, 0.0f, expectBlocked: true) ||
            !SaveCapture(roomNumber, "closed"))
        {
            return false;
        }

        door.SetProcess(false);
        typeof(RoomRuntime)
            .GetMethod("CompleteRoom", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(room, null);
        for (int frame = 0; frame < 4; frame++)
        {
            door._Process(1.0 / 60.0);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (door.OpenAmount is < 0.40f or > 0.80f)
        {
            Fail($"Room {roomNumber} exit door skipped the verifiable midpoint: open={door.OpenAmount:F3}.");
            return false;
        }
        if (!ValidateDoorState(roomNumber, door, authoredClosedDoorBounds, authoredPocketBounds, door.OpenAmount, expectBlocked: true) ||
            !ValidateStableAuthoredPockets(roomNumber, authoredPocketMeshes, authoredPocketTransforms) ||
            !SaveCapture(roomNumber, "mid"))
        {
            return false;
        }

        for (int frame = 0; frame < 4; frame++)
        {
            door._Process(1.0 / 60.0);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (door.OpenAmount < 0.95f)
        {
            Fail($"Room {roomNumber} exit door did not open for the corridor capture: open={door.OpenAmount:F3}.");
            return false;
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!ValidateDoorState(roomNumber, door, authoredClosedDoorBounds, authoredPocketBounds, door.OpenAmount, expectBlocked: false) ||
            !ValidateStableAuthoredPockets(roomNumber, authoredPocketMeshes, authoredPocketTransforms) ||
            !SaveCapture(roomNumber, "open", keepLegacyName: true))
        {
            return false;
        }

        GD.Print($"DOOR_DIMMING_CAPTURE: room={roomNumber}, states=closed/mid/open, corridor_meshes={corridorMeshes.Length}, depth_meshes={depthMeshes.Length}, lights={doorLights.Length}.");
        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
    }

    private bool ValidateDoorState(
        int roomNumber,
        ExitDoor3D door,
        Aabb authoredClosedDoorBounds,
        IReadOnlyList<Aabb> authoredPocketBounds,
        float expectedOpenAmount,
        bool expectBlocked)
    {
        MeshInstance3D? leftFrame = door.GetNodeOrNull<MeshInstance3D>("LeftFrame");
        MeshInstance3D? rightFrame = door.GetNodeOrNull<MeshInstance3D>("RightFrame");
        MeshInstance3D? header = door.GetNodeOrNull<MeshInstance3D>("Header");
        MeshInstance3D? leftPocket = door.GetNodeOrNull<MeshInstance3D>("LeftDoorPocketMask");
        MeshInstance3D? rightPocket = door.GetNodeOrNull<MeshInstance3D>("RightDoorPocketMask");
        MeshInstance3D? leftLeaf = door.GetNodeOrNull<MeshInstance3D>("LeftDoorLeaf");
        MeshInstance3D? rightLeaf = door.GetNodeOrNull<MeshInstance3D>("RightDoorLeaf");
        Node3D? leftHandle = door.GetNodeOrNull<Node3D>("LeftHandle");
        Node3D? rightHandle = door.GetNodeOrNull<Node3D>("RightHandle");
        MeshInstance3D? centerSeam = door.GetNodeOrNull<MeshInstance3D>("CenterSeam");
        CollisionShape3D? blocker = door.GetNodeOrNull<CollisionShape3D>("ClosedDoorBlocker/CollisionShape3D");
        float leafWidth = authoredClosedDoorBounds.Size.X * 0.5f;
        Vector3 authoredCenter = authoredClosedDoorBounds.GetCenter();
        float travelDistance = Mathf.Max(
            leafWidth,
            Mathf.Max(
                authoredCenter.X - authoredPocketBounds[0].End.X,
                authoredPocketBounds[1].Position.X - authoredCenter.X));
        float expectedLeftX = authoredCenter.X - (leafWidth * 0.5f) - (travelDistance * expectedOpenAmount);
        float expectedRightX = authoredCenter.X + (leafWidth * 0.5f) + (travelDistance * expectedOpenAmount);
        float expectedLeftHandleX = authoredCenter.X - 0.22f - (travelDistance * expectedOpenAmount);
        float expectedRightHandleX = authoredCenter.X + 0.22f + (travelDistance * expectedOpenAmount);
        bool authoredPocketOrderIsValid = authoredPocketBounds.Count == 2 &&
            authoredPocketBounds[0].GetCenter().X < authoredCenter.X &&
            authoredPocketBounds[1].GetCenter().X > authoredCenter.X;
        Vector3 leftLeafSize = leftLeaf?.Mesh?.GetAabb().Size ?? Vector3.Zero;
        Vector3 rightLeafSize = rightLeaf?.Mesh?.GetAabb().Size ?? Vector3.Zero;
        bool leafMeshesMatchAuthoredDoor =
            Mathf.Abs(leftLeafSize.X - leafWidth) <= 0.02f &&
            Mathf.Abs(rightLeafSize.X - leafWidth) <= 0.02f &&
            Mathf.Abs(leftLeafSize.Y - authoredClosedDoorBounds.Size.Y) <= 0.02f &&
            Mathf.Abs(rightLeafSize.Y - authoredClosedDoorBounds.Size.Y) <= 0.02f;
        Vector3 seamSize = centerSeam?.Mesh?.GetAabb().Size ?? Vector3.Zero;
        bool leavesClearOpening = expectedOpenAmount < 0.95f ||
            (leftLeaf is not null && rightLeaf is not null &&
             leftLeaf.Position.X + (leftLeafSize.X * 0.5f) <= authoredPocketBounds[0].End.X + 0.02f &&
             rightLeaf.Position.X - (rightLeafSize.X * 0.5f) >= authoredPocketBounds[1].Position.X - 0.02f);

        if (leftFrame?.Visible != false || rightFrame?.Visible != false || header?.Visible != true ||
            leftPocket?.Visible != false || rightPocket?.Visible != false ||
            leftLeaf?.Visible != true || rightLeaf?.Visible != true ||
            !leafMeshesMatchAuthoredDoor || !authoredPocketOrderIsValid || !leavesClearOpening ||
            Mathf.Abs(leftLeaf.Position.X - expectedLeftX) > 0.02f ||
            Mathf.Abs(rightLeaf.Position.X - expectedRightX) > 0.02f ||
            Mathf.Abs(leftLeaf.Position.Y - authoredCenter.Y) > 0.02f ||
            Mathf.Abs(rightLeaf.Position.Y - authoredCenter.Y) > 0.02f ||
            leftHandle is null || rightHandle is null ||
            Mathf.Abs(leftHandle.Position.X - expectedLeftHandleX) > 0.02f ||
            Mathf.Abs(rightHandle.Position.X - expectedRightHandleX) > 0.02f ||
            Mathf.Abs(leftHandle.Position.Y - authoredCenter.Y) > 0.02f ||
            Mathf.Abs(rightHandle.Position.Y - authoredCenter.Y) > 0.02f ||
            centerSeam is null ||
            Mathf.Abs(centerSeam.Position.X - authoredCenter.X) > 0.02f ||
            Mathf.Abs(centerSeam.Position.Y - authoredCenter.Y) > 0.02f ||
            Mathf.Abs(seamSize.Y - authoredClosedDoorBounds.Size.Y) > 0.02f ||
            blocker is null || blocker.Disabled == expectBlocked)
        {
            Fail($"Room {roomNumber} door is structurally incomplete at open={expectedOpenAmount:F3}: legacy_side_frames={leftFrame?.Visible}/{rightFrame?.Visible}, header={header?.Visible}, legacy_pockets={leftPocket?.Visible}/{rightPocket?.Visible}, authored_pockets={authoredPocketBounds.Count}, authored_door={authoredClosedDoorBounds}, leaf_meshes={leafMeshesMatchAuthoredDoor} ({leftLeafSize}/{rightLeafSize}), opening_clear={leavesClearOpening}, leaves={leftLeaf?.Position.X:F3}/{rightLeaf?.Position.X:F3}, blocker_disabled={blocker?.Disabled}.");
            return false;
        }

        return true;
    }

    private bool ValidateStableAuthoredPockets(
        int roomNumber,
        IReadOnlyList<MeshInstance3D> meshes,
        IReadOnlyList<Transform3D> transforms)
    {
        for (int index = 0; index < meshes.Count; index++)
        {
            if (!meshes[index].Visible || !meshes[index].GlobalTransform.IsEqualApprox(transforms[index]))
            {
                Fail($"Room {roomNumber} Blender-authored door pocket {meshes[index].Name} moved or disappeared while the door opened.");
                return false;
            }
        }

        return true;
    }

    private bool SaveCapture(int roomNumber, string state, bool keepLegacyName = false)
    {
        string capturePath = keepLegacyName
            ? $"res://artifacts/door-dimming/room{roomNumber:00}.png"
            : $"res://artifacts/door-dimming/room{roomNumber:00}-{state}.png";
        Error saveError = GetViewport().GetTexture().GetImage().SavePng(capturePath);
        if (saveError == Error.Ok)
        {
            return true;
        }

        Fail($"Room {roomNumber} {state} capture failed: {saveError}.");
        return false;
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static Aabb BoundsRelativeTo(Node3D space, MeshInstance3D mesh)
    {
        Aabb local = mesh.Mesh?.GetAabb() ?? default;
        Transform3D transform = space.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        Vector3[] corners =
        {
            local.Position,
            local.Position + new Vector3(local.Size.X, 0, 0),
            local.Position + new Vector3(0, local.Size.Y, 0),
            local.Position + new Vector3(0, 0, local.Size.Z),
            local.Position + new Vector3(local.Size.X, local.Size.Y, 0),
            local.Position + new Vector3(local.Size.X, 0, local.Size.Z),
            local.Position + new Vector3(0, local.Size.Y, local.Size.Z),
            local.End,
        };
        Vector3 first = transform * corners[0];
        Vector3 minimum = first;
        Vector3 maximum = first;
        foreach (Vector3 corner in corners.Skip(1))
        {
            Vector3 transformed = transform * corner;
            minimum = minimum.Min(transformed);
            maximum = maximum.Max(transformed);
        }

        return new Aabb(minimum, maximum - minimum);
    }

    private void Fail(string message)
    {
        GD.PushError($"DOOR_DIMMING_FAIL: {message}");
        GetTree().Quit(1);
    }
}
