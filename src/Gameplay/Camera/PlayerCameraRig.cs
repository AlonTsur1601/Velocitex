using Godot;
using Velocitex.Core.Input;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Camera;

public partial class PlayerCameraRig : Node3D
{
    public static readonly StringName CameraRigGroup = "camera_rig";

    [Export] public float MouseSensitivity { get; set; } = 0.0025f;
    [Export] public float FollowSharpness { get; set; } = 18.0f;
    [Export] public float ThirdPersonDistance { get; set; } = 5.5f;
    [Export] public float NormalFov { get; set; } = 70.0f;
    [Export] public float ZoomFov { get; set; } = 48.0f;
    public bool InvertY { get; set; }

    public PlayerBall? Target { get; private set; }
    public Node3D MovementBasis => _yawPivot;
    public bool IsFirstPerson { get; private set; }
    public bool IsTrailLayerVisible => _camera.GetCullMaskValue(2);
    public bool InputEnabled { get; private set; } = true;

    private Node3D _yawPivot = null!;
    private Node3D _pitchPivot = null!;
    private SpringArm3D _springArm = null!;
    private Camera3D _camera = null!;
    private float _yaw;
    private float _pitch = -0.25f;
    private float _initialYaw;
    private float _initialPitch;

    public override void _Ready()
    {
        AddToGroup(CameraRigGroup);
        _yawPivot = GetNode<Node3D>("YawPivot");
        _pitchPivot = GetNode<Node3D>("YawPivot/PitchPivot");
        _springArm = GetNode<SpringArm3D>("YawPivot/PitchPivot/SpringArm3D");
        _camera = GetNode<Camera3D>("YawPivot/PitchPivot/SpringArm3D/Camera3D");
        _camera.SetCullMaskValue(2, true);

        _initialYaw = _yaw;
        _initialPitch = _pitch;

        _yawPivot.Rotation = new Vector3(0.0f, _yaw, 0.0f);
        _pitchPivot.Rotation = new Vector3(_pitch, 0.0f, 0.0f);

        if (IsAutomatedCaptureRun())
        {
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }
        else if (InputEnabled && DisplayServer.GetName() != "headless")
        {
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;
        }
    }

    public override void _Process(double delta)
    {
        if (Target is not null)
        {
            Vector3 desiredPosition = Target.GlobalPosition + (Vector3.Up * 0.25f);
            float followWeight = 1.0f - Mathf.Exp(-FollowSharpness * (float)delta);
            GlobalPosition = GlobalPosition.Lerp(desiredPosition, followWeight);
        }

        float desiredDistance = IsFirstPerson ? 0.05f : ThirdPersonDistance;
        float desiredFov = Godot.Input.IsActionPressed(InputDefaults.Zoom) ? ZoomFov : NormalFov;
        float transitionWeight = 1.0f - Mathf.Exp(-14.0f * (float)delta);
        _springArm.SpringLength = Mathf.Lerp(_springArm.SpringLength, desiredDistance, transitionWeight);
        _camera.Fov = Mathf.Lerp(_camera.Fov, desiredFov, transitionWeight);

        if (InputEnabled && Godot.Input.IsActionJustPressed(InputDefaults.ToggleCamera))
        {
            SetFirstPerson(!IsFirstPerson);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!InputEnabled)
        {
            return;
        }

        if (@event is InputEventMouseMotion motion &&
            Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            float verticalDirection = InvertY ? 1.0f : -1.0f;
            _pitch = Mathf.Clamp(
                _pitch + (motion.Relative.Y * MouseSensitivity * verticalDirection),
                Mathf.DegToRad(-75.0f),
                Mathf.DegToRad(75.0f));
            _yawPivot.Rotation = new Vector3(0.0f, _yaw, 0.0f);
            _pitchPivot.Rotation = new Vector3(_pitch, 0.0f, 0.0f);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton button &&
            button.ButtonIndex == MouseButton.Left &&
            button.Pressed &&
            Godot.Input.MouseMode != Godot.Input.MouseModeEnum.Captured)
        {
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
    }

    public void Follow(PlayerBall target)
    {
        if (Target is not null)
        {
            Target.ResetPerformed -= ResetView;
        }

        Target = target;
        Target.ResetPerformed += ResetView;
        GlobalPosition = target.GlobalPosition + (Vector3.Up * 0.25f);
        _springArm.AddExcludedObject(target.GetRid());
        target.SetFirstPersonView(IsFirstPerson);
    }

    public void ResetView()
    {
        _yaw = _initialYaw;
        _pitch = _initialPitch;
        _yawPivot.Rotation = new Vector3(0.0f, _yaw, 0.0f);
        _pitchPivot.Rotation = new Vector3(_pitch, 0.0f, 0.0f);
        if (Target is not null)
        {
            GlobalPosition = Target.GlobalPosition + (Vector3.Up * 0.25f);
        }
    }

    public override void _ExitTree()
    {
        if (Target is not null)
        {
            Target.ResetPerformed -= ResetView;
        }
    }

    public void SetFirstPerson(bool firstPerson)
    {
        IsFirstPerson = firstPerson;
        _camera.SetCullMaskValue(2, true);
        Target?.SetFirstPersonView(firstPerson);
    }

    public void SetInputEnabled(bool enabled)
    {
        InputEnabled = enabled;
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        Godot.Input.MouseMode = enabled && !IsAutomatedCaptureRun()
            ? Godot.Input.MouseModeEnum.Captured
            : Godot.Input.MouseModeEnum.Visible;
    }

    public bool IsLookingAt(Vector3 globalPoint, float coneDegrees = 24.0f)
    {
        Vector3 toPoint = globalPoint - _camera.GlobalPosition;
        if (toPoint.LengthSquared() <= 0.0001f)
        {
            return true;
        }

        Vector3 forward = -_camera.GlobalBasis.Z.Normalized();
        float minimumDot = Mathf.Cos(Mathf.DegToRad(Mathf.Clamp(coneDegrees, 1.0f, 89.0f)));
        return forward.Dot(toPoint.Normalized()) >= minimumDot;
    }

    private static bool IsAutomatedCaptureRun()
    {
        return Array.Exists(
            OS.GetCmdlineUserArgs(),
            argument => argument.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                        argument.StartsWith("--panorama-capture=", StringComparison.OrdinalIgnoreCase));
    }
}
