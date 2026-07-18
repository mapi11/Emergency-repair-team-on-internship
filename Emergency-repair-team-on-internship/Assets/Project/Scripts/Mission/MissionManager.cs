using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissionManager : NetworkBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string missionSceneName = "MissionScene";
    [SerializeField] private string lobbySceneName = "LobbyScene";

    [Header("UI")]
    [SerializeField] private LoadingScreenManager loadingScreen;

    [Header("Settings")]
    [SerializeField] private float loadingDuration = 1.5f;

    private static MissionManager instance;
    private static readonly HashSet<string> pendingSceneKeys = new();

    private readonly NetworkList<FixedString64Bytes> despawnedSceneKeys = new(
        new System.Collections.Generic.List<FixedString64Bytes>(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public static MissionManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<MissionManager>();
            return instance;
        }
        private set => instance = value;
    }

    public bool IsMissionActive { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        if (loadingScreen == null)
            loadingScreen = FindFirstObjectByType<LoadingScreenManager>();
    }

    public override void OnNetworkSpawn()
    {
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        foreach (string key in pendingSceneKeys)
            despawnedSceneKeys.Add(new FixedString64Bytes(key));
        pendingSceneKeys.Clear();

        if (NetworkManager.Singleton?.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;

        if (!IsServer)
            CleanupDespawnedSceneObjects();
    }

    public static void RegisterDespawnedSceneObject(string sceneKey)
    {
        if (string.IsNullOrEmpty(sceneKey))
            return;

        if (instance != null && instance.IsSpawned)
        {
            instance.despawnedSceneKeys.Add(new FixedString64Bytes(sceneKey));
        }
        else
        {
            pendingSceneKeys.Add(sceneKey);
        }
    }

    private void CleanupDespawnedSceneObjects()
    {
        var allItems = FindObjectsByType<PickableItem>(FindObjectsSortMode.None);

        foreach (var item in allItems)
        {
            string key = item.GetSceneKey();

            foreach (FixedString64Bytes fs in despawnedSceneKeys)
            {
                if (fs.ToString() == key)
                {
                    Destroy(item.gameObject);
                    break;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton?.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartMissionServerRpc()
    {
        if (IsMissionActive) return;
        IsMissionActive = true;
        StartTransitionClientRpc();
        NetworkManager.Singleton.SceneManager.LoadScene(missionSceneName, LoadSceneMode.Single);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReturnToLobbyServerRpc()
    {
        if (!IsMissionActive) return;
        IsMissionActive = false;
        StartTransitionClientRpc();
        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    [ClientRpc]
    private void StartTransitionClientRpc()
    {
        SetLocalPlayerFrozen(true);

        if (loadingScreen != null)
            loadingScreen.Show();

        if (IsMissionActive)
            LockLocalPlayerRoleSlots();
        else
            UnlockLocalPlayerRoleSlots();
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
            StartCoroutine(UnfreezeAfterDelay());
    }

    private IEnumerator UnfreezeAfterDelay()
    {
        yield return new WaitForSeconds(loadingDuration);
        SetLocalPlayerFrozen(false);
        if (loadingScreen != null)
            loadingScreen.Hide();
    }

    public void SetLocalPlayerFrozen(bool frozen)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null)
            return;

        var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (playerObj == null) return;

        var controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
            controller.SetFrozen(frozen);
    }

    private void LockLocalPlayerRoleSlots()
    {
        if (NetworkManager.Singleton.LocalClient?.PlayerObject == null) return;
        var inv = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Inventory>();
        if (inv != null) inv.LockRoleSlots();
    }

    private void UnlockLocalPlayerRoleSlots()
    {
        if (NetworkManager.Singleton.LocalClient?.PlayerObject == null) return;
        var inv = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Inventory>();
        if (inv != null) inv.UnlockRoleSlots();
    }
}
