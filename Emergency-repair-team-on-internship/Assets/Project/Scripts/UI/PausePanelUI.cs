using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PausePanelUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Player Volumes")]
    [SerializeField] private GameObject volumeEntryPrefab;
    [SerializeField] private Transform volumeEntryContainer;

    [Header("Animation")]
    [SerializeField] private float animInDuration = 0.35f;
    [SerializeField] private float animOutDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private readonly Dictionary<ulong, PlayerVolumeEntry> entries = new();
    private bool subscribed;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (resumeButton != null)
            resumeButton.onClick.AddListener(() => PauseMenu.Instance.ClosePause());

        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => PauseMenu.Instance.OpenSettings());

        if (exitButton != null)
            exitButton.onClick.AddListener(() => PauseMenu.Instance.ReturnToMainMenu());
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshVolumeEntries();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearVolumeEntries();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Start()
    {
        AnimateIn();
    }

    public void AnimateIn()
    {
        transform.localScale = Vector3.one * 0.8f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        transform.DOScale(1f, animInDuration).SetEase(Ease.OutBack, 1.2f);

        if (canvasGroup != null)
            canvasGroup.DOFade(1f, animInDuration * 0.6f);
    }

    public void AnimateOut(Action onComplete)
    {
        transform.DOScale(0.8f, animOutDuration).SetEase(Ease.InBack);

        if (canvasGroup != null)
            canvasGroup.DOFade(0f, animOutDuration * 0.6f);

        DOVirtual.DelayedCall(animOutDuration, () =>
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        });
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        subscribed = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (gameObject.activeInHierarchy)
            RefreshVolumeEntries();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        RemoveVolumeEntry(clientId);
    }

    private void RefreshVolumeEntries()
    {
        if (NetworkManager.Singleton == null)
            return;

        ClearVolumeEntries();

        if (volumeEntryPrefab == null || volumeEntryContainer == null)
            return;

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            ulong clientId = kvp.Key;

            if (clientId == NetworkManager.Singleton.LocalClientId)
                continue;

            AddVolumeEntry(clientId);
        }
    }

    private void AddVolumeEntry(ulong clientId)
    {
        if (entries.ContainsKey(clientId))
            return;

        var playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;

        if (playerObject == null)
            return;

        var nameComp = playerObject.GetComponentInChildren<NetworkPlayerName>();
        var colorComp = playerObject.GetComponentInChildren<NetworkPlayerColor>();

        string playerName = nameComp != null ? nameComp.CurrentName : $"Player {clientId}";
        Color32 color = colorComp != null ? colorComp.CurrentColor : Color.white;

        float savedVolume = PlayerPrefs.GetFloat($"PlayerVolume_{clientId}", 1f);

        var instance = Instantiate(volumeEntryPrefab, volumeEntryContainer);
        var entry = instance.GetComponent<PlayerVolumeEntry>();

        if (entry == null)
        {
            Destroy(instance);
            return;
        }

        entry.Setup(clientId, playerName, color, savedVolume);
        entries[clientId] = entry;

        ApplyVolume(clientId, savedVolume);
    }

    private void RemoveVolumeEntry(ulong clientId)
    {
        if (entries.TryGetValue(clientId, out var entry))
        {
            if (entry != null)
                Destroy(entry.gameObject);

            entries.Remove(clientId);
        }
    }

    private void ClearVolumeEntries()
    {
        foreach (var kvp in entries)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }

        entries.Clear();
    }

    internal static void ApplyVolume(ulong clientId, float volume)
    {
        if (ProximityVoiceSpeaker.TryGetSpeaker(clientId, out var speaker))
        {
            var source = speaker.GetComponent<AudioSource>();

            if (source != null)
                source.volume = volume;
        }

        PlayerPrefs.SetFloat($"PlayerVolume_{clientId}", volume);
        PlayerPrefs.Save();
    }
}
