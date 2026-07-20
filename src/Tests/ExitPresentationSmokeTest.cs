using Godot;
using System.Reflection;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class ExitPresentationSmokeTest : Node
{
    private static readonly string[] SocketPartNames = { "Cup", "CupBase", "CupFunnel" };
    private static readonly string[] RemovedDecorationPrefixes =
    {
        "ExitGear", "CoinSlot", "InsertedCoin", "CeilingBrace",
        "Flywheel", "CoinLift", "LiftCoin", "OverheadBeam",
    };

    public override async void _Ready()
    {
        int requestedRoom = ResolveRequestedRoom(OS.GetCmdlineUserArgs());
        int firstRoom = requestedRoom > 0 ? requestedRoom : 1;
        int lastRoom = requestedRoom > 0 ? requestedRoom : 28;
        int issues = 0;
        for (int room = firstRoom; room <= lastRoom; room++)
        {
            string scenePath = room == 1
                ? "res://scenes/MovementTestRoom.tscn"
                : $"res://scenes/Room{room:00}.tscn";
            PackedScene? packed = GD.Load<PackedScene>(scenePath);
            if (packed is null)
            {
                Report(room, "scene is missing");
                issues++;
                continue;
            }

            Node roomRoot = packed.Instantiate();
            AddChild(roomRoot);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            issues += AuditRoom(room, roomRoot);
            issues += AuditInnerSideEscape(room, roomRoot);
            issues += AuditExitApproach(room, roomRoot);
            roomRoot.ProcessMode = ProcessModeEnum.Disabled;
            roomRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (issues > 0)
        {
            GD.PushError($"EXIT_PRESENTATION_FAIL: found {issues} missing, duplicated or inactive room exits.");
            await FinishAsync(1);
            return;
        }

        GD.Print(requestedRoom > 0
            ? $"EXIT_PRESENTATION_ROOM_PASS: Room {requestedRoom:00} has a wall-attached, wall-proud and threshold-free frame, a flush platform approach, level lever bases, a requirement-locked physical door, carved opening and enclosed dark corridor."
            : "EXIT_PRESENTATION_PASS: Rooms 01-28 share the same wall-attached, wall-proud and threshold-free frame, flush platform approach, level lever bases, requirement-locked physical door, carved wall opening, statically darkening enclosed corridor, continuous traversal fade and deep transition trigger.");
        await FinishAsync(0);
    }

    private static int ResolveRequestedRoom(string[] args)
    {
        const string prefix = "--exit-presentation-room=";
        string? value = args.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal));
        return value is not null && int.TryParse(value[prefix.Length..], out int room) && room is >= 1 and <= 28
            ? room
            : 0;
    }

    private async Task FinishAsync(int exitCode)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }

    private static int AuditRoom(int room, Node root)
    {
        int issues = 0;
        Area3D? trigger = root.GetNodeOrNull<Area3D>("GoalCup");
        ExitDoor3D? door = root.GetNodeOrNull<ExitDoor3D>("ExitDoor");
        Node3D[] socketParts = root.GetChildren()
            .OfType<Node3D>()
            .Where(node => SocketPartNames.Contains(node.Name.ToString()))
            .ToArray();
        bool hasSocket = socketParts.Length > 0;
        Node[] descendants = EnumerateDescendants(root).ToArray();

        foreach (RouteCheckpoint3D button in descendants.OfType<RouteCheckpoint3D>().Where(checkpoint => checkpoint.IsPhysicalFloorButton))
        {
            MeshInstance3D? inset = button.GetNodeOrNull<MeshInstance3D>("InsetPlate");
            int pipCount = inset?.GetChildren()
                .Count(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal)) ?? 0;
            if (inset is null || pipCount != button.CheckpointIndex + 1)
            {
                Report(room, $"floor button {button.Name} has {pipCount} number pips instead of {button.CheckpointIndex + 1}");
                issues++;
            }

            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
                button.GlobalPosition + (Vector3.Up * 0.25f),
                button.GlobalPosition + (Vector3.Down * (button.TriggerSize.Y + 5.0f)),
                1);
            Godot.Collections.Dictionary hit = button.GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (!hit.TryGetValue("position", out Variant floorPositionVariant))
            {
                Report(room, $"floor button {button.Name} has no visible supporting surface beneath it");
                issues++;
            }
            else if (inset is not null)
            {
                float insetGap = inset.GlobalPosition.Y - floorPositionVariant.AsVector3().Y;
                if (insetGap < 0.05f || insetGap > 0.32f)
                {
                    Report(room, $"floor button {button.Name} is buried or floating by {insetGap:F3} m");
                    issues++;
                }
            }
        }

        foreach (MechanicalLever lever in descendants.OfType<MechanicalLever>())
        {
            if (lever.GetNodeOrNull<Node3D>("Foot") is not null ||
                lever.GetNodeOrNull<CollisionShape3D>("BaseCollision/FootHitbox") is not null)
            {
                Report(room, $"lever {lever.Name} still has the removed broad flat foot");
                issues++;
            }

            bool hasPhysicalHitbox = EnumerateDescendants(lever)
                .OfType<CollisionObject3D>()
                .SelectMany(body => body.GetChildren().OfType<CollisionShape3D>())
                .Any(shape => !shape.Disabled && shape.Shape is not null);
            if (!hasPhysicalHitbox)
            {
                Report(room, $"lever {lever.Name} has no physical hitbox");
                issues++;
            }

            Node3D? pedestal = lever.GetNodeOrNull<Node3D>("Pedestal");
            CollisionShape3D? pedestalHitbox = lever.GetNodeOrNull<CollisionShape3D>("BaseCollision/PedestalHitbox");
            if (pedestal is null || pedestalHitbox is null ||
                !pedestal.Rotation.IsEqualApprox(Vector3.Zero) ||
                !pedestalHitbox.Rotation.IsEqualApprox(Vector3.Zero))
            {
                Report(room, $"lever {lever.Name} pedestal or its hitbox is tilted");
                issues++;
            }

            float? supportingFloorY = FindSupportingFloorY(root, lever);
            if (supportingFloorY is null)
            {
                Report(room, $"lever {lever.Name} has no supporting platform beneath its base");
                issues++;
            }
            else
            {
                float baseGap = lever.GlobalPosition.Y - supportingFloorY.Value;
                if (Mathf.Abs(baseGap) > 0.06f)
                {
                    Report(room, $"lever {lever.Name} base clips or floats by {baseGap:F3} m relative to its supporting platform");
                    issues++;
                }
            }
        }

        if (room <= 2)
        {
            string[] remainingDecorations = descendants
                .Select(node => node.Name.ToString())
                .Where(name => RemovedDecorationPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (remainingDecorations.Length > 0)
            {
                Report(room, $"still contains removed wall decorations: {string.Join(", ", remainingDecorations)}");
                issues++;
            }
        }

        if (room == 2 && descendants.Any(node => node.Name.ToString().StartsWith("MechanicalLatch", StringComparison.Ordinal)))
        {
            Report(room, "still contains raised route-latch jaws on its rolling platforms");
            issues++;
        }

        if (trigger is null || !trigger.Monitoring || trigger.CollisionMask == 0 ||
            !trigger.GetChildren().OfType<CollisionShape3D>().Any(shape => !shape.Disabled && shape.Shape is not null))
        {
            Report(room, "exit trigger is missing or inactive");
            issues++;
        }
        else if (trigger.GetSignalConnectionList(Area3D.SignalName.BodyEntered).Count == 0)
        {
            Report(room, "exit trigger has no completion handler");
            issues++;
        }

        if (door is null || hasSocket)
        {
            Report(room, door is null ? "has no animated exit door" : "still shows a collection socket beside its door");
            issues++;
        }
        else
        {
            if (door.GetNodeOrNull<Node3D>("LeftDoorLeaf") is null ||
                door.GetNodeOrNull<Node3D>("RightDoorLeaf") is null ||
                door.GetNodeOrNull<MeshInstance3D>("LeftDoorPocketMask") is null ||
                door.GetNodeOrNull<MeshInstance3D>("RightDoorPocketMask") is null ||
                door.GetNodeOrNull<Node3D>("ChevronLeft") is null ||
                door.GetNodeOrNull<Node3D>("ChevronRight") is null ||
                door.GetNodeOrNull<Node3D>("DarkOpening") is not null ||
                door.GetNodeOrNull<CanvasLayer>("ExitDarknessLayer") is null ||
                door.GetNodeOrNull<ColorRect>("ExitDarknessLayer/ExitDarknessOverlay") is null ||
                descendants.Any(node => node.Name.ToString().StartsWith("ExitCorridorDarkness", StringComparison.Ordinal)) ||
                door.OpenDistance <= 0.0f ||
                door.CloseDistance <= door.OpenDistance)
            {
                Report(room, "door is missing its standard frame, continuous darkness overlay or valid proximity distances");
                issues++;
            }
            Node3D? leftFrame = door.GetNodeOrNull<Node3D>("LeftFrame");
            Node3D? rightFrame = door.GetNodeOrNull<Node3D>("RightFrame");
            Node3D? header = door.GetNodeOrNull<Node3D>("Header");
            if (door.GetNodeOrNull<Node3D>("Threshold") is not null ||
                leftFrame is null || rightFrame is null || header is null ||
                Mathf.Abs(leftFrame.Position.Z - ExitDoor3D.FrameRoomSideCenterZ) > 0.001f ||
                Mathf.Abs(rightFrame.Position.Z - ExitDoor3D.FrameRoomSideCenterZ) > 0.001f ||
                Mathf.Abs(header.Position.Z - ExitDoor3D.FrameRoomSideCenterZ) > 0.001f)
            {
                Report(room, "door frame is not fully proud of the wall or still has a raised threshold");
                issues++;
            }
            StaticBody3D? frameCollision = door.GetNodeOrNull<StaticBody3D>("FrameCollision");
            if (!HasNamedCollisionBox(frameCollision, "LeftPocketHitbox", new Vector3(1.5f, 4.1f, 0.38f), new Vector3(-3.1f, 2.08f, ExitDoor3D.FrameRoomSideCenterZ)) ||
                !HasNamedCollisionBox(frameCollision, "RightPocketHitbox", new Vector3(1.5f, 4.1f, 0.38f), new Vector3(3.1f, 2.08f, ExitDoor3D.FrameRoomSideCenterZ)) ||
                !HasNamedCollisionBox(frameCollision, "LeftFrameHitbox", new Vector3(0.5f, 4.55f, 0.58f), new Vector3(-2.1f, 2.3f, ExitDoor3D.FrameRoomSideCenterZ)) ||
                !HasNamedCollisionBox(frameCollision, "RightFrameHitbox", new Vector3(0.5f, 4.55f, 0.58f), new Vector3(2.1f, 2.3f, ExitDoor3D.FrameRoomSideCenterZ)) ||
                !HasNamedCollisionBox(frameCollision, "HeaderHitbox", new Vector3(4.7f, 0.58f, 0.58f), new Vector3(0.0f, 4.55f, ExitDoor3D.FrameRoomSideCenterZ)))
            {
                Report(room, "visible door frame or side pocket is missing its matching hitbox");
                issues++;
            }
            int floorButtonCount = descendants.OfType<RouteCheckpoint3D>().Count(button => button.IsPhysicalFloorButton);
            MeshInstance3D[] buttonIndicators = door.GetChildren()
                .OfType<MeshInstance3D>()
                .Where(indicator => indicator.Name.ToString().StartsWith("ButtonRequirementIndicator", StringComparison.Ordinal))
                .ToArray();
            bool hasIndicatorHousing = door.GetNodeOrNull<MeshInstance3D>("ButtonIndicatorHousing") is not null;
            if (buttonIndicators.Length != floorButtonCount || hasIndicatorHousing != (floorButtonCount > 0))
            {
                Report(room, $"door has {buttonIndicators.Length} requirement lights for {floorButtonCount} floor buttons");
                issues++;
            }
            else if (buttonIndicators.Any(indicator =>
                Mathf.Abs(indicator.Position.X) > ExitDoor3D.FrameOuterHalfWidth - 0.2f ||
                indicator.Position.Y < ExitDoor3D.FrameOuterHeight))
            {
                Report(room, "door requirement lights are not contained in the housing above the frame");
                issues++;
            }
            if (!HasCollisionBox(door, "ExitCorridorFloor", new Vector3(ExitDoor3D.CorridorInteriorWidth, 0.24f, ExitDoor3D.CorridorLength + 0.5f)) ||
                !HasCollisionBox(door, "ExitCorridorCeiling", new Vector3(ExitDoor3D.CorridorInteriorWidth, 0.24f, ExitDoor3D.CorridorLength)) ||
                !HasCollisionBox(door, "ExitCorridorLeftWall", new Vector3(0.24f, ExitDoor3D.CorridorInteriorHeight + 0.24f, ExitDoor3D.CorridorLength + 0.64f)) ||
                !HasCollisionBox(door, "ExitCorridorRightWall", new Vector3(0.24f, ExitDoor3D.CorridorInteriorHeight + 0.24f, ExitDoor3D.CorridorLength + 0.64f)) ||
                !HasCollisionBox(door, "ExitCorridorEndWall", new Vector3(ExitDoor3D.CorridorInteriorWidth + 0.48f, ExitDoor3D.CorridorInteriorHeight + 0.24f, 0.24f)))
            {
                Report(room, "dark corridor does not use the standard enclosed dimensions");
                issues++;
            }
            else if (!HasStaticDepthFade(door, "ExitCorridorFloor") ||
                !HasStaticDepthFade(door, "ExitCorridorCeiling") ||
                !HasStaticDepthFade(door, "ExitCorridorLeftWall") ||
                !HasStaticDepthFade(door, "ExitCorridorRightWall"))
            {
                Report(room, "corridor lining does not visibly fade toward black before the player enters");
                issues++;
            }
            else
            {
                PlayerBall? player = descendants.OfType<PlayerBall>().FirstOrDefault();
                if (player is null)
                {
                    Report(room, "has no player for the door proximity check");
                    issues++;
                }
                else
                {
                    RoomRuntime? runtime = root as RoomRuntime;
                    MethodInfo? clearCompletion = typeof(RoomRuntime).GetMethod("ClearCompletionState", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo? completeRoom = typeof(RoomRuntime).GetMethod("CompleteRoom", BindingFlags.Instance | BindingFlags.NonPublic);
                    Node3D leftChevron = door.GetNode<Node3D>("ChevronLeft");
                    Node3D rightChevron = door.GetNode<Node3D>("ChevronRight");
                    Vector3 leftChevronPosition = leftChevron.Position;
                    Vector3 rightChevronPosition = rightChevron.Position;
                    clearCompletion?.Invoke(runtime, null);
                    door.ResetClosed();
                    player.GlobalPosition = door.DoorwayCenter;
                    door._Process(0.3);
                    if (door.OpenAmount > 0.01f)
                    {
                        Report(room, "locked door opened before the room requirements completed");
                        issues++;
                    }

                    CollisionShape3D? closedBlocker = door.GetNodeOrNull<CollisionShape3D>("ClosedDoorBlocker/CollisionShape3D");
                    if (closedBlocker is null || closedBlocker.Disabled)
                    {
                        Report(room, "locked door has no active physical blocker");
                        issues++;
                    }

                    completeRoom?.Invoke(runtime, null);
                    door._Process(0.3);
                    if (door.OpenAmount < 0.99f)
                    {
                        Report(room, "door did not open after the room requirements completed");
                        issues++;
                    }
                    if (leftChevron.Position.DistanceTo(leftChevronPosition) > 0.001f ||
                        rightChevron.Position.DistanceTo(rightChevronPosition) > 0.001f)
                    {
                        Report(room, "frame arrow moved sideways with the door leaves");
                        issues++;
                    }

                    player.GlobalPosition = door.DoorwayCenter + (Vector3.Up * (door.CloseDistance + 2.0f));
                    door._Process(0.5);
                    if (door.OpenAmount > 0.01f)
                    {
                        Report(room, "door did not close when the player moved away");
                        issues++;
                    }

                    float previousDarkness = -0.001f;
                    float midpointDarkness = -1.0f;
                    for (float depth = 0.0f; depth <= ExitDoor3D.CorridorTransitionDepth; depth += 0.25f)
                    {
                        player.GlobalPosition = door.ToGlobal(new Vector3(0.0f, 0.72f, -depth));
                        door._Process(0.0);
                        if (door.DarknessAmount + 0.0001f < previousDarkness)
                        {
                            Report(room, $"corridor darkness decreased at {depth:F2} m depth");
                            issues++;
                            break;
                        }
                        previousDarkness = door.DarknessAmount;
                        if (Mathf.Abs(depth - ((ExitDoor3D.CorridorFadeStartDepth + ExitDoor3D.CorridorFadeEndDepth) * 0.5f)) <= 0.13f)
                        {
                            midpointDarkness = door.DarknessAmount;
                        }
                    }
                    ColorRect darknessOverlay = door.GetNode<ColorRect>("ExitDarknessLayer/ExitDarknessOverlay");
                    if (door.DarknessAmount < 0.999f ||
                        midpointDarkness < 0.35f || midpointDarkness > 0.65f ||
                        !Mathf.IsEqualApprox(darknessOverlay.Color.A, door.DarknessAmount))
                    {
                        Report(room, $"corridor fade was not gradual or fully black before transition (mid={midpointDarkness:F2}, end={door.DarknessAmount:F2})");
                        issues++;
                    }

                    player.GlobalPosition = door.ToGlobal(new Vector3(1.4f, 0.72f, 0.8f));
                    Vector3 manualVelocity = door.GlobalBasis * new Vector3(2.4f, 0.0f, 1.7f);
                    player.LinearVelocity = manualVelocity;
                    if (runtime is null || completeRoom is null || runtime.IsComplete || !runtime.IsExitTraversalPending || !door.TraversalActive)
                    {
                        Report(room, "room completion was not deferred into the corridor");
                        issues++;
                    }
                    door._PhysicsProcess(0.1);
                    if (!door.TraversalActive || player.LinearVelocity.DistanceTo(manualVelocity) > 0.001f)
                    {
                        Report(room, "exit traversal changed the player's velocity before manual corridor entry");
                        issues++;
                    }
                    player.GlobalPosition = door.ToGlobal(new Vector3(0.0f, 0.72f, -ExitDoor3D.CorridorTransitionDepth - 0.1f));
                    door._PhysicsProcess(0.1);
                    if (door.TraversalActive || runtime is null || !runtime.IsComplete || runtime.IsExitTraversalPending)
                    {
                        Report(room, "room did not complete at the standard corridor depth");
                        issues++;
                    }
                }
            }
            if (trigger is not null)
            {
                Vector3 triggerInDoor = door.ToLocal(trigger.GlobalPosition);
                CollisionShape3D? triggerCollision = trigger.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
                bool standardEntranceShape = triggerCollision?.Shape is BoxShape3D entranceBox &&
                    entranceBox.Size.DistanceTo(new Vector3(6.8f, 4.5f, 14.0f)) <= 0.001f;
                if (Mathf.Abs(triggerInDoor.X) > 0.02f ||
                    Mathf.Abs(triggerInDoor.Z - 1.08f) > 0.02f ||
                    !standardEntranceShape)
                {
                    Report(room, $"puzzle-completion trigger is not aligned with the corridor entrance ({triggerInDoor})");
                    issues++;
                }
            }

            Node3D? shell = root.GetNodeOrNull<Node3D>("RoomShell");
            StaticBody3D[] carvedWalls = shell?.GetChildren()
                .OfType<StaticBody3D>()
                .Where(wall => !wall.Visible &&
                    new[] { "LeftWall", "RightWall", "BackWall", "ExitWall" }.Contains(wall.Name.ToString()))
                .ToArray() ?? Array.Empty<StaticBody3D>();
            if (shell is null || carvedWalls.Length != 1 ||
                carvedWalls[0].GetChildren().OfType<CollisionShape3D>().Any(collision => !collision.Disabled) ||
                shell.GetChildren().Count(node => node.Name.ToString().StartsWith($"{carvedWalls[0].Name}Doorway", StringComparison.Ordinal)) < 3)
            {
                Report(room, "room shell does not expose one physically carved doorway into the corridor");
                issues++;
            }
            else
            {
                int backingPieceCount = door.GetChildren().Count(node =>
                    node.Name.ToString().StartsWith("ExitDoorBacking", StringComparison.Ordinal));
                if (backingPieceCount < 2)
                {
                    Report(room, "door frame has no complete structural backing wall");
                    issues++;
                }
                else
                {
                    float frameBackZ = ExitDoor3D.FrameRoomSideCenterZ - (ExitDoor3D.FrameDepth * 0.5f);
                    bool backingTouchesVisibleFrame = door.GetChildren()
                        .OfType<StaticBody3D>()
                        .Where(piece => piece.Name.ToString().StartsWith("ExitDoorBacking", StringComparison.Ordinal))
                        .All(piece => piece.GetChildren().OfType<CollisionShape3D>().Any(collision =>
                            collision.Shape is BoxShape3D box &&
                            Mathf.Abs(
                                piece.Position.Z + collision.Position.Z + (box.Size.Z * 0.5f) - frameBackZ) <= 0.002f));
                    if (!backingTouchesVisibleFrame)
                    {
                        Report(room, "door backing wall does not touch the rear face of the visible frame");
                        issues++;
                    }
                }

                if (!DoorwayPiecesClearVisibleFrame(shell, carvedWalls[0], door))
                {
                    Report(room, "carved wall pieces cover part of the standard frame or header arrow");
                    issues++;
                }
            }
        }

        return issues;
    }

    private static int AuditInnerSideEscape(int room, Node root)
    {
        ExitDoor3D? door = root.GetNodeOrNull<ExitDoor3D>("ExitDoor");
        if (door is null)
        {
            return 0;
        }

        PhysicsDirectSpaceState3D space = door.GetWorld3D().DirectSpaceState;
        int issues = 0;
        foreach (float depth in new[] { 0.65f, 1.55f })
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            Vector3 from = door.ToGlobal(new Vector3(0.0f, 2.0f, -depth));
            Vector3 to = door.ToGlobal(new Vector3(side * 4.0f, 2.0f, -depth));
            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, 1);
            Godot.Collections.Dictionary hit = space.IntersectRay(query);
            string expectedColliderName = side < 0.0f ? "ExitCorridorLeftWall" : "ExitCorridorRightWall";
            string actualColliderName = hit.TryGetValue("collider", out Variant colliderValue) &&
                colliderValue.AsGodotObject() is Node collider
                    ? collider.Name.ToString()
                    : "none";
            if (actualColliderName != expectedColliderName)
            {
                Report(room, $"player-sized inner-side escape remains open at depth {depth:F2} m on the {(side < 0.0f ? "left" : "right")} (ray hit {actualColliderName})");
                issues++;
            }
        }

        return issues;
    }

    private static int AuditExitApproach(int room, Node root)
    {
        ExitDoor3D? door = root.GetNodeOrNull<ExitDoor3D>("ExitDoor");
        if (door is null)
        {
            return 0;
        }

        PhysicsDirectSpaceState3D space = door.GetWorld3D().DirectSpaceState;
        foreach (float side in new[] { -1.45f, 0.0f, 1.45f })
        foreach (float depth in new[] { 0.25f, 0.50f, 0.75f, 1.00f })
        {
            Vector3 from = door.ToGlobal(new Vector3(side, 1.0f, depth));
            Vector3 to = door.ToGlobal(new Vector3(side, -0.45f, depth));
            Godot.Collections.Dictionary hit = space.IntersectRay(
                PhysicsRayQueryParameters3D.Create(from, to, 1));
            if (!hit.TryGetValue("position", out Variant positionValue))
            {
                Report(room, $"exit platform leaves an open floor gap at local ({side:F2}, {depth:F2})");
                return 1;
            }

            Vector3 localHit = door.ToLocal(positionValue.AsVector3());
            if (Mathf.Abs(localHit.Y - 0.12f) > 0.025f)
            {
                Report(room, $"exit approach is not flush at local ({side:F2}, {depth:F2}); floor height is {localHit.Y:F3} m");
                return 1;
            }
        }

        return 0;
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

    private static bool HasCollisionBox(Node root, string name, Vector3 expectedSize)
    {
        StaticBody3D? body = root.GetNodeOrNull<StaticBody3D>(name);
        CollisionShape3D? collision = body?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        return collision?.Shape is BoxShape3D box && box.Size.DistanceTo(expectedSize) <= 0.001f;
    }

    private static bool HasNamedCollisionBox(StaticBody3D? body, string name, Vector3 expectedSize, Vector3 expectedPosition)
    {
        CollisionShape3D? collision = body?.GetNodeOrNull<CollisionShape3D>(name);
        return collision?.Shape is BoxShape3D box &&
            box.Size.DistanceTo(expectedSize) <= 0.001f &&
            collision.Position.DistanceTo(expectedPosition) <= 0.001f;
    }

    private static bool HasStaticDepthFade(Node root, string name)
    {
        MeshInstance3D? mesh = root.GetNodeOrNull<StaticBody3D>(name)?
            .GetChildren()
            .OfType<MeshInstance3D>()
            .FirstOrDefault();
        return mesh?.MaterialOverride is ShaderMaterial material &&
            material.Shader?.Code.Contains("corridor_depth", StringComparison.Ordinal) == true;
    }

    private static bool DoorwayPiecesClearVisibleFrame(Node3D shell, StaticBody3D carvedWall, ExitDoor3D door)
    {
        string prefix = $"{carvedWall.Name}Doorway";
        foreach (StaticBody3D piece in shell.GetChildren().OfType<StaticBody3D>()
            .Where(candidate => candidate.Name.ToString().StartsWith(prefix, StringComparison.Ordinal)))
        {
            foreach (CollisionShape3D collision in piece.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }

                Vector3 half = box.Size * 0.5f;
                Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                foreach (float x in new[] { -half.X, half.X })
                foreach (float y in new[] { -half.Y, half.Y })
                foreach (float z in new[] { -half.Z, half.Z })
                {
                    Vector3 point = door.ToLocal(collision.ToGlobal(new Vector3(x, y, z)));
                    minimum = new Vector3(Mathf.Min(minimum.X, point.X), Mathf.Min(minimum.Y, point.Y), Mathf.Min(minimum.Z, point.Z));
                    maximum = new Vector3(Mathf.Max(maximum.X, point.X), Mathf.Max(maximum.Y, point.Y), Mathf.Max(maximum.Z, point.Z));
                }

                bool overlapsVisibleFrameEnvelope =
                    maximum.X > -ExitDoor3D.FrameOuterHalfWidth + 0.001f &&
                    minimum.X < ExitDoor3D.FrameOuterHalfWidth - 0.001f &&
                    maximum.Y > 0.0f + 0.001f &&
                    minimum.Y < ExitDoor3D.FrameOuterHeight - 0.001f &&
                    maximum.Z > ExitDoor3D.FrameRoomSideCenterZ - (ExitDoor3D.FrameDepth * 0.5f) + 0.001f;
                if (overlapsVisibleFrameEnvelope)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static float? FindSupportingFloorY(Node root, MechanicalLever lever)
    {
        float bestY = float.NegativeInfinity;
        foreach (StaticBody3D body in EnumerateDescendants(root).OfType<StaticBody3D>())
        {
            if (lever.IsAncestorOf(body) ||
                body.Name == "HazardFloor" ||
                body.Name.ToString().Contains("Wall", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }

                Vector3 local = collision.ToLocal(lever.GlobalPosition);
                if (Mathf.Abs(local.X) > (box.Size.X * 0.5f) + 0.05f ||
                    Mathf.Abs(local.Z) > (box.Size.Z * 0.5f) + 0.05f)
                {
                    continue;
                }

                float topY = collision.ToGlobal(new Vector3(local.X, box.Size.Y * 0.5f, local.Z)).Y;
                if (topY <= lever.GlobalPosition.Y + 0.15f)
                {
                    bestY = Mathf.Max(bestY, topY);
                }
            }
        }

        return float.IsNegativeInfinity(bestY) ? null : bestY;
    }

    private static void Report(int room, string message)
    {
        GD.PushError($"EXIT_PRESENTATION: Room {room:00} {message}.");
    }
}
