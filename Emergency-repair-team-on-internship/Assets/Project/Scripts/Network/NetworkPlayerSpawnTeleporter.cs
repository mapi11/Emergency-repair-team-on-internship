using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkPlayerSpawnTeleporter : NetworkBehaviour
{
    [SerializeField] private float teleportDelay = 0.15f;
    [SerializeField] private float postTeleportDelay = 1.5f;
    [SerializeField] private bool teleportOnlyOwner = true;

    private CharacterController characterController;
    private PlayerController playerController;
    private Coroutine teleportCoroutine;
    private float spawnTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        spawnTime = Time.time;

        var lobby = FindObjectOfType<PreStartLobbyController>();
        if (lobby != null)
            lobby.NetworkSpawnsDisabled.OnValueChanged += OnSpawnsDisabledChanged;

        ScheduleSpawn();
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        var lobby = FindObjectOfType<PreStartLobbyController>();
        if (lobby != null)
            lobby.NetworkSpawnsDisabled.OnValueChanged -= OnSpawnsDisabledChanged;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        var lobby = FindObjectOfType<PreStartLobbyController>();
        if (lobby != null)
            lobby.NetworkSpawnsDisabled.OnValueChanged -= OnSpawnsDisabledChanged;
    }

    private void ScheduleSpawn()
    {
        if (!IsSpawned) return;
        if (teleportOnlyOwner && !IsOwner) return;

        if (teleportCoroutine != null)
            StopCoroutine(teleportCoroutine);
        teleportCoroutine = StartCoroutine(SpawnRoutine());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleSpawn();
    }

    private IEnumerator SpawnRoutine()
    {
        SetFrozen(true);

        yield return new WaitForSeconds(teleportDelay);

        Transform target = null;
        float deadline = Time.time + 3f;

        while (Time.time < deadline)
        {
            target = FindTargetSpawn();
            if (target != null) break;
            yield return new WaitForSeconds(0.1f);
        }

        if (target == null)
        {
            Debug.LogError($"No spawn target found for OwnerClientId={OwnerClientId}");
            SetFrozen(false);
            yield break;
        }

        TeleportTo(target.position, target.rotation);

        yield return new WaitForSeconds(postTeleportDelay);

        SetFrozen(false);
        DismissConnectionScreen();
    }

    private void DismissConnectionScreen()
    {
        var screen = FindObjectOfType<ConnectionScreenManager>();
        if (screen != null)
            screen.Dismiss();
    }

    private Transform FindTargetSpawn()
    {
        if (AreSpawnsDisabled())
        {
            var lobby = FindObjectOfType<PreStartLobbyController>();
            if (lobby != null)
            {
                Transform spawn = lobby.GetReconnectSpawn(OwnerClientId);
                if (spawn != null)
                    return spawn;
            }
        }

        PlayerSpawnPoint spawnPoint = FindSpawnPoint();
        return spawnPoint != null ? spawnPoint.transform : null;
    }

    private void OnSpawnsDisabledChanged(bool oldValue, bool newValue)
    {
        if (!newValue || !IsOwner) return;
        if (Time.time - spawnTime > 2f) return;

        var lobby = FindObjectOfType<PreStartLobbyController>();
        if (lobby == null) return;

        Transform spawn = lobby.GetReconnectSpawn(OwnerClientId);
        if (spawn != null)
        {
            SetFrozen(true);
            TeleportTo(spawn.position, spawn.rotation);
            SetFrozen(false);
        }
    }

    private void SetFrozen(bool frozen)
    {
        if (playerController != null)
            playerController.SetFrozen(frozen);
    }

    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (characterController != null && characterController.enabled)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }

    private bool AreSpawnsDisabled()
    {
        var lobby = FindObjectOfType<PreStartLobbyController>();
        if (lobby == null) return false;
        if (lobby.IsLockedIn) return true;
        return lobby.NetworkSpawnsDisabled.Value;
    }

    private PlayerSpawnPoint FindSpawnPoint()
    {
        PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        var lobby = FindObjectOfType<PreStartLobbyController>();

        Array.Sort(spawnPoints, (a, b) => a.Index.CompareTo(b.Index));

        int spawnIndex = (int)(OwnerClientId % (ulong)spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i].Index == spawnIndex)
            {
                if (lobby != null && lobby.IsReconnectSpawn(spawnPoints[i].transform))
                    continue;
                return spawnPoints[i];
            }
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (lobby != null && lobby.IsReconnectSpawn(spawnPoints[i].transform))
                continue;
            return spawnPoints[i];
        }

        return null;
    }
}
