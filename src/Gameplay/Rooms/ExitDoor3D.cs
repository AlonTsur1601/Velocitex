using Godot;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Rooms;

public partial class ExitDoor3D : Node3D
{
    public const float CorridorLength = 9.6f;
    public const float CorridorTransitionDepth = 6.2f;
    public const float CorridorFadeStartDepth = 0.35f;
    public const float CorridorFadeEndDepth = 5.35f;
    public const float CorridorInteriorWidth = 3.65f;
    public const float CorridorInteriorHeight = 3.9f;
    public const float CorridorSideWallFrontOffset = 0.52f;
    public const float FrameOuterHalfWidth = 2.35f;
    public const float FrameOuterHeight = 4.84f;
    public const float FrameDepth = 0.58f;
    public const float FrameRoomSideCenterZ = 0.52f;

    private readonly List<(Node3D Part, Vector3 ClosedPosition, float Direction)> _slidingParts = new();
    private readonly List<(RouteCheckpoint3D Button, MeshInstance3D Indicator)> _buttonIndicators = new();
    private MeshInstance3D? _centerSeam;
    private CollisionShape3D? _closedDoorBlocker;
    private ColorRect? _darknessOverlay;
    private PlayerBall? _player;
    private RoomRuntime? _roomRuntime;
    private bool _proximityOpen;
    private bool _traversalActive;
    private float _openAmount;
    private float _darknessAmount;
    private Material? _inactiveButtonIndicatorMaterial;
    private Material? _activeButtonIndicatorMaterial;

    [Export] public float OpenDistance { get; set; } = 8.5f;
    [Export] public float CloseDistance { get; set; } = 10.0f;
    public float OpenAmount => _openAmount;
    public float DarknessAmount => _darknessAmount;
    public bool TraversalActive => _traversalActive;
    public Vector3 DoorwayCenter => GlobalPosition + (GlobalBasis.Y.Normalized() * 2.0f);

    public override void _Ready()
    {
        foreach ((string name, float direction) in new[]
        {
            ("LeftDoorLeaf", -1.0f), ("LeftHandle", -1.0f),
            ("RightDoorLeaf", 1.0f), ("RightHandle", 1.0f),
        })
        {
            Node3D part = GetNode<Node3D>(name);
            _slidingParts.Add((part, part.Position, direction));
        }

        _centerSeam = GetNode<MeshInstance3D>("CenterSeam");
        _closedDoorBlocker = GetNodeOrNull<CollisionShape3D>("ClosedDoorBlocker/CollisionShape3D");
        _player = GetTree().GetFirstNodeInGroup(PlayerBall.PlayerGroup) as PlayerBall;
        _roomRuntime = GetParentOrNull<RoomRuntime>();
        CanvasLayer darknessLayer = new()
        {
            Name = "ExitDarknessLayer",
            Layer = 10,
        };
        AddChild(darknessLayer);
        _darknessOverlay = new ColorRect
        {
            Name = "ExitDarknessOverlay",
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        darknessLayer.AddChild(_darknessOverlay);
        _darknessOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ApplyVisual();
    }

    public override void _Process(double delta)
    {
        UpdateButtonIndicators();
        if (!IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup(PlayerBall.PlayerGroup) as PlayerBall;
        }

        if (IsInstanceValid(_player))
        {
            float distance = _player!.GlobalPosition.DistanceTo(DoorwayCenter);
            bool unlocked = _roomRuntime?.IsExitTraversalPending == true || _roomRuntime?.IsComplete == true;
            if (_proximityOpen)
            {
                _proximityOpen = unlocked && distance <= CloseDistance;
            }
            else
            {
                _proximityOpen = unlocked && distance <= OpenDistance;
            }
        }
        else
        {
            _proximityOpen = false;
        }

        UpdateContinuousDarkness();

        // The approach trigger starts several metres before the frame.  A fast
        // momentum route still needs the leaves fully clear before impact.
        float animationSpeed = _proximityOpen ? 8.5f : 3.4f;
        _openAmount = Mathf.MoveToward(_openAmount, _proximityOpen ? 1.0f : 0.0f, (float)delta * animationSpeed);
        UpdateClosedDoorBlocker();
        ApplyVisual();
    }

    public void ConfigureButtonIndicators(
        IEnumerable<(RouteCheckpoint3D Button, MeshInstance3D Indicator)> indicators,
        Material inactiveMaterial,
        Material activeMaterial)
    {
        _buttonIndicators.Clear();
        _buttonIndicators.AddRange(indicators);
        _inactiveButtonIndicatorMaterial = inactiveMaterial;
        _activeButtonIndicatorMaterial = activeMaterial;
        UpdateButtonIndicators();
    }

    private void UpdateButtonIndicators()
    {
        if (_inactiveButtonIndicatorMaterial is null || _activeButtonIndicatorMaterial is null)
        {
            return;
        }

        foreach ((RouteCheckpoint3D button, MeshInstance3D indicator) in _buttonIndicators)
        {
            if (IsInstanceValid(button) && IsInstanceValid(indicator))
            {
                indicator.MaterialOverride = button.IsActivated
                    ? _activeButtonIndicatorMaterial
                    : _inactiveButtonIndicatorMaterial;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_traversalActive || !IsInstanceValid(_player))
        {
            return;
        }

        Vector3 localPosition = ToLocal(_player!.GlobalPosition);
        if (localPosition.Z <= -CorridorTransitionDepth)
        {
            _traversalActive = false;
            _roomRuntime?.CompleteExitTraversal();
        }
    }

    public void BeginExitTraversal()
    {
        _traversalActive = true;
    }

    public void CancelExitTraversal()
    {
        _traversalActive = false;
    }

    public void ResetClosed()
    {
        _traversalActive = false;
        _proximityOpen = false;
        _openAmount = 0.0f;
        _darknessAmount = 0.0f;
        UpdateClosedDoorBlocker(forceClosed: true);
        ApplyVisual();
        ApplyDarkness();
    }

    private void UpdateContinuousDarkness()
    {
        float target = 0.0f;
        if (IsInstanceValid(_player))
        {
            Vector3 localPosition = ToLocal(_player!.GlobalPosition);
            bool withinCorridor =
                Mathf.Abs(localPosition.X) <= (CorridorInteriorWidth * 0.5f) + 0.8f &&
                localPosition.Y >= -1.0f &&
                localPosition.Y <= CorridorInteriorHeight + 1.0f &&
                localPosition.Z <= 0.35f &&
                localPosition.Z >= -CorridorLength;
            if (withinCorridor)
            {
                float depth = -localPosition.Z;
                float progress = Mathf.Clamp(
                    (depth - CorridorFadeStartDepth) / (CorridorFadeEndDepth - CorridorFadeStartDepth),
                    0.0f,
                    1.0f);
                target = Mathf.SmoothStep(0.0f, 1.0f, progress);
            }
        }

        _darknessAmount = target;
        ApplyDarkness();
    }

    private void ApplyDarkness()
    {
        if (IsInstanceValid(_darknessOverlay))
        {
            _darknessOverlay!.Color = new Color(0.0f, 0.0f, 0.0f, _darknessAmount);
        }
    }

    private void UpdateClosedDoorBlocker(bool forceClosed = false)
    {
        if (!IsInstanceValid(_closedDoorBlocker))
        {
            return;
        }

        bool shouldBlock = forceClosed || _openAmount < 0.82f;
        if (_closedDoorBlocker!.Disabled == shouldBlock)
        {
            _closedDoorBlocker.SetDeferred(CollisionShape3D.PropertyName.Disabled, !shouldBlock);
        }
    }

    private void ApplyVisual()
    {
        foreach ((Node3D part, Vector3 closedPosition, float direction) in _slidingParts)
        {
            if (IsInstanceValid(part))
            {
                part.Position = closedPosition + (Vector3.Right * direction * 1.72f * _openAmount);
            }
        }

        if (IsInstanceValid(_centerSeam))
        {
            _centerSeam!.Visible = _openAmount < 0.18f;
        }
    }
}
