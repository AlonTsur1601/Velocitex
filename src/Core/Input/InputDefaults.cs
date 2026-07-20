using Godot;

namespace Velocitex.Core.Input;

public static class InputDefaults
{
    public static readonly StringName MoveForward = "move_forward";
    public static readonly StringName MoveBack = "move_back";
    public static readonly StringName MoveLeft = "move_left";
    public static readonly StringName MoveRight = "move_right";
    public static readonly StringName ToggleCamera = "toggle_camera";
    public static readonly StringName Zoom = "camera_zoom";
    public static readonly StringName Interact = "interact";

    public static void EnsureActions()
    {
        EnsureKeyAction(MoveForward, Key.W, Key.Up);
        EnsureKeyAction(MoveBack, Key.S, Key.Down);
        EnsureKeyAction(MoveLeft, Key.A, Key.Left);
        EnsureKeyAction(MoveRight, Key.D, Key.Right);
        EnsureKeyAction(ToggleCamera, Key.C);
        EnsureKeyAction(Interact, Key.E);
        EnsureMouseAction(Zoom, MouseButton.Middle);
    }

    public static void ApplyPrimaryBindings(
        Key moveForward,
        Key moveBack,
        Key moveLeft,
        Key moveRight,
        Key toggleCamera,
        Key interact)
    {
        RebindPrimary(MoveForward, moveForward, Key.Up);
        RebindPrimary(MoveBack, moveBack, Key.Down);
        RebindPrimary(MoveLeft, moveLeft, Key.Left);
        RebindPrimary(MoveRight, moveRight, Key.Right);
        RebindPrimary(ToggleCamera, toggleCamera);
        RebindPrimary(Interact, interact);
    }

    public static Key GetPrimaryKey(StringName action)
    {
        foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action))
        {
            if (inputEvent is InputEventKey keyEvent)
            {
                return keyEvent.PhysicalKeycode != Key.None
                    ? keyEvent.PhysicalKeycode
                    : keyEvent.Keycode;
            }
        }

        return Key.None;
    }

    public static void RebindPrimary(StringName action, Key primaryKey)
    {
        Key secondaryKey = action == MoveForward ? Key.Up
            : action == MoveBack ? Key.Down
            : action == MoveLeft ? Key.Left
            : action == MoveRight ? Key.Right
            : Key.None;
        RebindPrimary(action, primaryKey, secondaryKey);
    }

    private static void EnsureKeyAction(StringName action, params Key[] keys)
    {
        if (InputMap.HasAction(action))
        {
            return;
        }

        InputMap.AddAction(action);
        foreach (Key key in keys)
        {
            InputMap.ActionAddEvent(action, new InputEventKey
            {
                PhysicalKeycode = key,
            });
        }
    }

    private static void EnsureMouseAction(StringName action, MouseButton button)
    {
        if (InputMap.HasAction(action))
        {
            return;
        }

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton
        {
            ButtonIndex = button,
        });
    }

    private static void RebindPrimary(StringName action, Key primaryKey, Key secondaryKey = Key.None)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }

        InputMap.ActionEraseEvents(action);
        AddKey(action, primaryKey);
        if (secondaryKey != Key.None && secondaryKey != primaryKey)
        {
            AddKey(action, secondaryKey);
        }
    }

    private static void AddKey(StringName action, Key key)
    {
        InputMap.ActionAddEvent(action, new InputEventKey
        {
            PhysicalKeycode = key,
        });
    }
}
