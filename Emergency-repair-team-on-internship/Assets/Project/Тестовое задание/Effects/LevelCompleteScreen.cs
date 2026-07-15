using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class LevelCompleteScreen : MonoBehaviour
{
    [Header("Match-3 Field")]
    [SerializeField] private Image matchField;

    [Header("UI Elements")]
    [SerializeField] private Button triggerButton;
    [SerializeField] private Image darkOverlay;
    [SerializeField] private RectTransform rewardPanel;
    [SerializeField] private CanvasGroup rewardPanelGroup;
    [SerializeField] private TMP_Text levelCompleteText;
    [SerializeField] private Image[] stars;

    [Header("VFX")]
    [SerializeField] private Match3FireworksVFX fireworksVFX;

    [Header("Animation Timings")]
    [SerializeField] private float overlayFadeIn = 0.5f;
    [SerializeField] private float delayBeforeVFX = 0.12f;
    [SerializeField] private float delayBeforePanel = 0.2f;
    [SerializeField] private float panelAppear = 0.6f;
    [SerializeField] private float displayDuration = 2.5f;
    [SerializeField] private float panelDisappear = 0.3f;
    [SerializeField] private float overlayFadeOut = 0.3f;

    [Header("Animation Style")]
    [SerializeField] private float overlayAlpha = 0.72f;
    [SerializeField] private float panelOvershoot = 1.45f;

    private Coroutine animationCoroutine;

    private void Start()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.AddListener(OnButtonClicked);
        }

        SetInitialState();
    }

    private void OnDestroy()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.RemoveListener(OnButtonClicked);
        }

        KillAllTweens();

        if (fireworksVFX != null)
        {
            fireworksVFX.ClearImmediate();
        }
    }

    private void OnButtonClicked()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        animationCoroutine = StartCoroutine(AnimationSequence());
    }

    private IEnumerator AnimationSequence()
    {
        KillAllTweens();

        if (triggerButton != null)
        {
            triggerButton.gameObject.SetActive(false);
        }

        if (fireworksVFX != null)
        {
            fireworksVFX.ClearImmediate();
        }

        PrepareWindowBeforeShow();

        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            darkOverlay.DOFade(overlayAlpha, overlayFadeIn).SetEase(Ease.OutQuad);
        }

        yield return new WaitForSeconds(delayBeforeVFX);

        if (fireworksVFX != null)
        {
            fireworksVFX.PlayCelebration();
        }

        float remainingDelayBeforePanel = Mathf.Max(
            0f,
            overlayFadeIn + delayBeforePanel - delayBeforeVFX
        );

        yield return new WaitForSeconds(remainingDelayBeforePanel);

        ShowRewardPanel();

        yield return new WaitForSeconds(displayDuration);

        HideRewardPanel();

        if (fireworksVFX != null)
        {
            fireworksVFX.StopCelebrationFast();
        }

        yield return new WaitForSeconds(panelDisappear);

        if (darkOverlay != null)
        {
            darkOverlay.DOFade(0f, overlayFadeOut).SetEase(Ease.OutQuad);
        }

        yield return new WaitForSeconds(overlayFadeOut);

        SetInitialState();

        if (triggerButton != null)
        {
            triggerButton.gameObject.SetActive(true);
        }

        animationCoroutine = null;
    }

    private void SetInitialState()
    {
        if (darkOverlay != null)
        {
            Color color = darkOverlay.color;
            color.a = 0f;
            darkOverlay.color = color;
        }

        if (rewardPanel != null)
        {
            rewardPanel.localScale = Vector3.zero;
            rewardPanel.gameObject.SetActive(false);
        }

        if (rewardPanelGroup != null)
        {
            rewardPanelGroup.alpha = 0f;
        }

        if (levelCompleteText != null)
        {
            levelCompleteText.transform.localScale = Vector3.zero;
        }

        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null)
                    continue;

                stars[i].transform.localScale = Vector3.zero;
                stars[i].transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void PrepareWindowBeforeShow()
    {
        if (rewardPanel != null)
        {
            rewardPanel.gameObject.SetActive(true);
            rewardPanel.localScale = Vector3.zero;
        }

        if (rewardPanelGroup != null)
        {
            rewardPanelGroup.alpha = 0f;
        }

        if (levelCompleteText != null)
        {
            levelCompleteText.transform.localScale = Vector3.zero;
        }

        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null)
                    continue;

                stars[i].transform.localScale = Vector3.zero;
                stars[i].transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void ShowRewardPanel()
    {
        if (rewardPanel != null)
        {
            rewardPanel.localScale = Vector3.zero;

            rewardPanel
                .DOScale(1f, panelAppear)
                .SetEase(Ease.OutBack, panelOvershoot);
        }

        if (rewardPanelGroup != null)
        {
            rewardPanelGroup
                .DOFade(1f, panelAppear * 0.65f)
                .SetEase(Ease.OutQuad);
        }

        if (levelCompleteText != null)
        {
            levelCompleteText.transform.localScale = Vector3.zero;

            levelCompleteText.transform
                .DOScale(1f, 0.4f)
                .SetDelay(panelAppear * 0.25f)
                .SetEase(Ease.OutBack, 1.7f);
        }

        AnimateStarsIn();
    }

    private void AnimateStarsIn()
    {
        if (stars == null)
            return;

        for (int i = 0; i < stars.Length; i++)
        {
            Image star = stars[i];

            if (star == null)
                continue;

            float delay = panelAppear * 0.38f + i * 0.16f;

            star.transform.localScale = Vector3.zero;
            star.transform.localRotation = Quaternion.identity;

            star.transform
                .DOScale(1f, 0.35f)
                .SetDelay(delay)
                .SetEase(Ease.OutBack, 2f);

            star.transform
                .DORotate(new Vector3(0f, 0f, 15f), 0.15f)
                .SetDelay(delay + 0.34f)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.InOutQuad);
        }
    }

    private void HideRewardPanel()
    {
        if (rewardPanel != null)
        {
            rewardPanel
                .DOScale(0f, panelDisappear)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (rewardPanel != null)
                    {
                        rewardPanel.gameObject.SetActive(false);
                    }
                });
        }

        if (rewardPanelGroup != null)
        {
            rewardPanelGroup
                .DOFade(0f, panelDisappear * 0.7f)
                .SetEase(Ease.InQuad);
        }

        if (levelCompleteText != null)
        {
            levelCompleteText.transform
                .DOScale(0f, panelDisappear * 0.55f)
                .SetEase(Ease.InBack);
        }

        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                Image star = stars[i];

                if (star == null)
                    continue;

                star.transform
                    .DOScale(0f, panelDisappear * 0.55f)
                    .SetEase(Ease.InBack);
            }
        }
    }

    private void KillAllTweens()
    {
        if (darkOverlay != null)
        {
            darkOverlay.DOKill();
        }

        if (rewardPanel != null)
        {
            rewardPanel.DOKill();
        }

        if (rewardPanelGroup != null)
        {
            rewardPanelGroup.DOKill();
        }

        if (levelCompleteText != null)
        {
            levelCompleteText.transform.DOKill();
        }

        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null)
                    continue;

                stars[i].transform.DOKill();
            }
        }
    }
}