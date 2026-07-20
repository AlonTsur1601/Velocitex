using Godot;

namespace Velocitex.Core.Physics;

public interface IImpulseDevice
{
    Vector3 PreviewImpulse(RigidBody3D target);

    bool TryApplyImpulse(RigidBody3D target);
}

