using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject playerInfoPanelPrefab;
    [SerializeField] private Transform playerListContainer;

    [Header("Role Icons")]
    [SerializeField] private Sprite[] roleSprites;

    [Header("Animation")]
    [SerializeField] private float fillDuration = 1.5f;

    private readonly Dictionary<ulong, GameObject> playerPanels = new();
    private bool isShowing;

    public void Show()
    {
        isShowing = true;
        rootPanel.SetActive(true);

        if (progressSlider != null)
            progressSlider.value = 0f;

        RebuildPlayerList();
        StartCoroutine(AnimateProgress());
    }

    public void Hide()
    {
        isShowing = false;
        rootPanel.SetActive(false);
        ClearPlayerPanels();
    }

    private IEnumerator AnimateProgress()
    {
        float elapsed = 0f;

        while (elapsed < fillDuration && isShowing)
        {
            elapsed += Time.unscaledDeltaTime;
            if (progressSlider != null)
                progressSlider.value = Mathf.Clamp01(elapsed / fillDuration);

            RebuildPlayerList();
            yield return null;
        }

        if (progressSlider != null)
            progressSlider.value = 1f;
    }

    private void RebuildPlayerList()
    {
        if (playerInfoPanelPrefab == null || playerListContainer == null) return;
        if (NetworkManager.Singleton == null) return;

        var clients = NetworkManager.Singleton.ConnectedClientsList;

        foreach (var panel in playerPanels.Values)
            if (panel != null) panel.SetActive(false);

        foreach (var client in clients)
        {
            if (client.PlayerObject == null) continue;

            if (!playerPanels.TryGetValue(client.ClientId, out var panel) || panel == null)
            {
                panel = Instantiate(playerInfoPanelPrefab, playerListContainer);
                playerPanels[client.ClientId] = panel;
            }

            panel.SetActive(true);
            var comp = panel.GetComponent<LoadingScreenPlayerPanel>();
            if (comp == null) continue;

            var nameComp = client.PlayerObject.GetComponent<NetworkPlayerName>();
            var colorComp = client.PlayerObject.GetComponent<NetworkPlayerColor>();
            var roleComp = client.PlayerObject.GetComponent<NetworkPlayerRole>();

            if (comp.nameText != null)
                comp.nameText.text = nameComp != null ? nameComp.CurrentName : $"Player {client.ClientId}";

            if (comp.colorImage != null && colorComp != null)
                comp.colorImage.color = colorComp.CurrentColor;

            if (comp.roleIcon != null)
                SetRoleIcon(comp.roleIcon, roleComp != null ? roleComp.CurrentRole : PlayerRole.None);
        }
    }

    private void SetRoleIcon(Image icon, PlayerRole role)
    {
        int idx = (int)role - 1;

        if (role == PlayerRole.None || roleSprites == null || idx < 0 || idx >= roleSprites.Length || roleSprites[idx] == null)
        {
            icon.enabled = false;
            return;
        }

        icon.sprite = roleSprites[idx];
        icon.enabled = true;
    }

    private void ClearPlayerPanels()
    {
        foreach (var panel in playerPanels.Values)
            if (panel != null) Destroy(panel);

        playerPanels.Clear();
    }
}
