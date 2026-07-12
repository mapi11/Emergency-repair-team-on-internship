using UnityEngine;

public abstract class SlopInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Transform handTarget;
    [SerializeField] private bool canInteract = true;

    public Transform HandTarget => handTarget != null ? handTarget : transform;

    public virtual bool CanInteract(SlopPlayerController player)
    {
        return canInteract;
    }

    public virtual void OnHandBegin(SlopPlayerController player)
    {
    }

    public virtual void OnHandHold(SlopPlayerController player, float deltaTime)
    {
    }

    public virtual void OnHandEnd(SlopPlayerController player)
    {
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }
}