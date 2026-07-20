using Godot;
using Velocitex.Gameplay.Camera;

namespace Velocitex.UI.Visual;

public readonly record struct PanoramaView(string Id, Vector3 CameraPosition, Vector3 Target, float Fov = 56.0f);

public partial class PanoramaCaptureController : Node
{
    private const int CaptureFrame = 36;
    private static readonly Vector2I CaptureSize = new(2560, 1440);
    private PanoramaView _view;
    private int _frames;
    private SubViewport _captureViewport = null!;

    public static bool TryAttach(Node3D room, IReadOnlyList<PanoramaView> views)
    {
        string? argument = Array.Find(
            OS.GetCmdlineUserArgs(),
            value => value.StartsWith("--panorama-capture=", StringComparison.Ordinal));
        if (argument is null)
        {
            return false;
        }

        string id = argument["--panorama-capture=".Length..];
        PanoramaView? selected = views.FirstOrDefault(view => view.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (selected is null || string.IsNullOrWhiteSpace(selected.Value.Id))
        {
            GD.PushError($"PANORAMA_CAPTURE_FAIL: unknown view '{id}'.");
            room.GetTree().Quit(1);
            return true;
        }

        room.GetNodeOrNull<CanvasLayer>("Hud")?.Hide();
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        foreach (Node node in room.GetTree().GetNodesInGroup(PlayerCameraRig.CameraRigGroup))
        {
            if (node is PlayerCameraRig cameraRig)
            {
                cameraRig.SetInputEnabled(false);
            }
        }

        foreach (Node node in room.GetTree().GetNodesInGroup("player_ball"))
        {
            if (node is Node3D player)
            {
                player.Visible = false;
                player.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        PanoramaCaptureController controller = new()
        {
            Name = "PanoramaCaptureController",
            _view = selected.Value,
        };
        room.AddChild(controller);
        return true;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _captureViewport = new SubViewport
        {
            Name = "HighResolutionPanoramaViewport",
            Size = CaptureSize,
            World3D = GetViewport().World3D,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X,
        };
        AddChild(_captureViewport);
        Camera3D camera = new()
        {
            Name = "PanoramaCamera",
            Position = _view.CameraPosition,
            Fov = _view.Fov,
            Current = true,
        };
        _captureViewport.AddChild(camera);
        camera.LookAt(_view.Target, Vector3.Up);
    }

    public override void _Process(double delta)
    {
        if (++_frames < CaptureFrame)
        {
            return;
        }

        string directory = ProjectSettings.GlobalizePath("res://assets/panoramas");
        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (directoryError != Error.Ok && directoryError != Error.AlreadyExists)
        {
            GD.PushError($"PANORAMA_CAPTURE_FAIL: could not create '{directory}' ({directoryError}).");
            GetTree().Quit(1);
            return;
        }

        string path = System.IO.Path.Combine(directory, $"{_view.Id}.png");
        Error saveError = _captureViewport.GetTexture().GetImage().SavePng(path);
        if (saveError != Error.Ok)
        {
            GD.PushError($"PANORAMA_CAPTURE_FAIL: {_view.Id} could not be saved ({saveError}).");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"PANORAMA_CAPTURE_PASS: {_view.Id} -> {path}");
        GetTree().Quit(0);
    }
}
