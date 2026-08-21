using Godot;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Rooms;

public enum RouteCheckpointPressResult
{
    Ignore,
    Denied,
    Activated,
}

public static class RouteCheckpointPressPolicy
{
    public static RouteCheckpointPressResult Apply(
        RouteCheckpoint3D checkpoint,
        PlayerBall player,
        Action dispatchRoomDecision)
    {
        if (checkpoint.IsActivated)
        {
            return RouteCheckpointPressResult.Ignore;
        }

        // A single physical contact can overlap two neighbouring Area3D nodes.
        // Once one shared button path accepts that contact, no later callback in
        // the same physics frame may reinterpret it as an invalid press and flash.
        if (RouteCheckpoint3D.PlayerAlreadyActivatedButtonThisPhysicsFrame(player))
        {
            return RouteCheckpointPressResult.Ignore;
        }

        dispatchRoomDecision();
        if (checkpoint.IsActivated)
        {
            return RouteCheckpointPressResult.Activated;
        }

        // A physical press which the room did not accept is always rejected in
        // the same way: the plate stays raised and flashes red. A successful
        // callback activates and depresses it before this method returns.
        checkpoint.ScheduleDeniedFeedback(player, Engine.GetPhysicsFrames());
        return RouteCheckpointPressResult.Denied;
    }
}
