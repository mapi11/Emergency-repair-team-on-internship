using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkPlayerSpawnTeleporter : NetworkBehaviour
{
    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("Teleport")]
    [SerializeField] private float teleportDelay = 0.15f;
    [SerializeField] private bool teleportOnlyOwner = true;

    [Header("Debug")]
    [SerializeField] private bool hasTeleported;
    [SerializeField] private Vector3 lastTeleportPosition;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        TryTeleportIfInLobby();
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != lobbySceneName)
            return;

        TryTeleportIfInLobby();
    }

    private void TryTeleportIfInLobby()
    {
        if (!IsSpawned)
            return;

        if (teleportOnlyOwner && !IsOwner)
            return;

        if (SceneManager.GetActiveScene().name != lobbySceneName)
            return;

        StartCoroutine(TeleportAfterDelay());
    }

    private IEnumerator TeleportAfterDelay()
    {
        yield return new WaitForSeconds(teleportDelay);

        PlayerSpawnPoint spawnPoint = FindSpawnPoint();

        if (spawnPoint == null)
        {
            Debug.LogWarning($"⚠ No PlayerSpawnPoint found in scene {lobbySceneName}");
            yield break;
        }

        TeleportTo(spawnPoint.transform.position, spawnPoint.transform.rotation);
    }

    private PlayerSpawnPoint FindSpawnPoint()
    {
        PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);

        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        Array.Sort(spawnPoints, (a, b) => a.Index.CompareTo(b.Index));

        int spawnIndex = (int)(OwnerClientId % (ulong)spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i].Index == spawnIndex)
            {
                return spawnPoints[i];
            }
        }

        return spawnPoints[spawnIndex];
    }

    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        bool controllerWasEnabled = false;

        if (characterController != null)
        {
            controllerWasEnabled = characterController.enabled;
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
        }

        hasTeleported = true;
        lastTeleportPosition = position;

        Debug.Log(
            $"📍 Player teleported to spawn. " +
            $"OwnerClientId={OwnerClientId}, Position={position}"
        );
    }
}