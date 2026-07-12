using Unity.Netcode;
using UnityEngine;

public abstract class Interactable : NetworkBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Transform handTarget;
    [SerializeField] private bool canInteract = true;

    public Transform HandTarget => handTarget != null ? handTarget : transform;

    public virtual bool CanInteract(PlayerController player)
    {
        return canInteract;
    }

    public virtual void OnHandBegin(PlayerController player)
    {
    }

    public virtual void OnHandHold(PlayerController player, float deltaTime)
    {
    }

    public virtual void OnHandEnd(PlayerController player)
    {
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }
}