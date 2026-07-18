using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkPlayerSpawnTeleporter : NetworkBehaviour
{
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
        TryTeleportToSpawn();
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
        TryTeleportToSpawn();
    }

    private void TryTeleportToSpawn()
    {
        if (!IsSpawned)
            return;

        if (teleportOnlyOwner && !IsOwner)
            return;

        StartCoroutine(TeleportAfterDelay());
    }

    private IEnumerator TeleportAfterDelay()
    {
        yield return new WaitForSeconds(teleportDelay);

        PlayerSpawnPoint spawnPoint = FindSpawnPoint();

        if (spawnPoint == null)
        {
            Debug.Log($"No PlayerSpawnPoint found in scene {SceneManager.GetActiveScene().name}");
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
                return spawnPoints[i];
        }

        return spawnPoints[spawnIndex];
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        bool controllerWasEnabled = false;

        if (characterController != null)
        {
            controllerWasEnabled = characterController.enabled;
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (characterController != null)
            characterController.enabled = controllerWasEnabled;

        hasTeleported = true;
        lastTeleportPosition = position;

        Debug.Log($"Player teleported to spawn. OwnerClientId={OwnerClientId}, Position={position}");
    }
}
