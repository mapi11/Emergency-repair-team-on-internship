using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PausePanelUI : MonoBehaviour
{
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [SerializeField] private float animInDuration = 0.35f;
    [SerializeField] private float animOutDuration = 0.2f;

    private CanvasGroup canvasGroup;

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
}
