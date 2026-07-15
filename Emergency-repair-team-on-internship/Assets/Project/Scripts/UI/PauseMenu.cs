using Unity.Netcode;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [SerializeField] private GameObject pausePanelPrefab;
    [SerializeField] private Transform pauseSpawnPoint;
    [SerializeField] private GameObject settingsPanelPrefab;
    [SerializeField] private Transform settingsSpawnPoint;

    private GameObject pauseInstance;
    private GameObject settingsInstance;
    private PlayerController playerController;
    private bool returningToMenu;

    public bool IsOpen { get; private set; }
    public bool SettingsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        playerController = GetLocalPlayerController();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

            Instance = null;
        }
    }

    private void Update()
    {
        if (returningToMenu)
            return;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsConnectedClient &&
            !NetworkManager.Singleton.IsServer)
        {
            ReturnToMainMenu();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (settingsInstance != null)
        {
            CloseSettings();
            return;
        }

        if (pauseInstance != null)
            ClosePause();
        else
            OpenPause();
    }

    private void OnDisconnected(ulong clientId)
    {
        if (returningToMenu)
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            return;

        ReturnToMainMenu();
    }

    public void OpenPause()
    {
        if (pauseInstance != null)
            return;

        IsOpen = true;

        if (playerController != null)
            playerController.IsPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Transform parent = pauseSpawnPoint != null && pauseSpawnPoint.gameObject.activeInHierarchy
            ? pauseSpawnPoint
            : transform;

        pauseInstance = Instantiate(pausePanelPrefab, parent);
    }

    public void ClosePause()
    {
        if (pauseInstance == null)
            return;

        var panel = pauseInstance.GetComponent<PausePanelUI>();

        if (panel != null)
        {
            panel.AnimateOut(OnPauseClosed);
        }
        else
        {
            OnPauseClosed();
        }
    }

    private void OnPauseClosed()
    {
        IsOpen = false;

        if (playerController != null)
            playerController.IsPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        pauseInstance = null;
    }

    public void OpenSettings()
    {
        if (settingsInstance != null)
            return;

        SettingsOpen = true;

        Transform parent = settingsSpawnPoint != null ? settingsSpawnPoint : transform;
        settingsInstance = Instantiate(settingsPanelPrefab, parent);
    }

    public void CloseSettings()
    {
        if (settingsInstance == null)
            return;

        var panel = settingsInstance.GetComponent<SettingsPanelUI>();

        if (panel != null)
        {
            panel.AnimateOut(OnSettingsClosed);
        }
        else
        {
            OnSettingsClosed();
        }
    }

    private void OnSettingsClosed()
    {
        SettingsOpen = false;
        settingsInstance = null;
    }

    public void ReturnToMainMenu()
    {
        if (returningToMenu)
            return;

        returningToMenu = true;

        if (playerController != null)
            playerController.IsPaused = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        if (pauseInstance != null)
        {
            Destroy(pauseInstance);
            pauseInstance = null;
        }

        if (settingsInstance != null)
        {
            Destroy(settingsInstance);
            settingsInstance = null;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private static PlayerController GetLocalPlayerController()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localClient = NetworkManager.Singleton.LocalClient;

            if (localClient != null && localClient.PlayerObject != null)
            {
                var pc = localClient.PlayerObject.GetComponentInChildren<PlayerController>();

                if (pc != null)
                    return pc;
            }
        }

        var all = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].IsLocalPlayer)
                return all[i];
        }

        return null;
    }
}
