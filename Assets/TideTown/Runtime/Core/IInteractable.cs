using UnityEngine;

public interface IInteractable
{
	string EntityId { get; }

	Vector3 Position { get; }

	string Prompt(GameState s);

	bool CanInteract(GameState s);

	void Interact(GameState s, InteractionService svc);
}
