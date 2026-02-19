public interface IRaycastInteractable
{
    string GetInteractionPrompt(PlayerController player);
    bool CanInteract(PlayerController player);
    void Interact(PlayerController player);
}

