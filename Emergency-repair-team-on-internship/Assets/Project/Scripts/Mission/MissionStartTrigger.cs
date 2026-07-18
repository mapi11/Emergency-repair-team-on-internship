using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class MissionStartTrigger : NetworkBehaviour
{
    [SerializeField] private Key startKey = Key.M;
    [SerializeField] private Key returnKey = Key.N;

    public void StartMission()
    {
        if (!IsOwner) return;

        if (MissionManager.Instance == null)
        {
            Debug.LogError("[MissionStartTrigger] MissionManager.Instance is NULL!");
            return;
        }

        if (!MissionManager.Instance.IsSpawned)
        {
            Debug.LogWarning("[MissionStartTrigger] MissionManager not spawned yet");
            return;
        }

        MissionManager.Instance.StartMissionServerRpc();
    }

    public void ReturnToLobby()
    {
        if (!IsOwner) return;

        if (MissionManager.Instance == null)
        {
            Debug.LogError("[MissionStartTrigger] MissionManager.Instance is NULL!");
            return;
        }

        if (!MissionManager.Instance.IsSpawned)
        {
            Debug.LogWarning("[MissionStartTrigger] MissionManager not spawned yet");
            return;
        }

        MissionManager.Instance.ReturnToLobbyServerRpc();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[startKey].wasPressedThisFrame)
            StartMission();

        if (Keyboard.current[returnKey].wasPressedThisFrame)
            ReturnToLobby();
    }
}
