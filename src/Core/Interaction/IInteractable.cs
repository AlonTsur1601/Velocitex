using Godot;

namespace Velocitex.Core.Interaction;

public interface IInteractable
{
    string InteractionPrompt { get; }

    bool CanInteract(Node interactor);

    void Interact(Node interactor);
}

