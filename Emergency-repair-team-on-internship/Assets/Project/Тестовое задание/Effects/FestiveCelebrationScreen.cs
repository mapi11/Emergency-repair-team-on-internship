using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class FestiveCelebrationScreen : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform vfxRoot;

    [Header("UI Elements")]
    [SerializeField] private Button triggerButton;
    [SerializeField] private Image darkOverlay;
    [SerializeField] private RectTransform rewardPanel;
    [SerializeField] private CanvasGroup rewardPanelGroup;
    [SerializeField] private Text[] rewardTexts;

    [Header("Sprites")]
    [SerializeField] private Sprite softCircleSprite;
    [SerializeField] private Sprite sparkleSprite;
    [SerializeField] private Sprite streakSprite;

    [Header("Timing")]
    [SerializeField] private float overlayFadeIn = 0.5f;
    [SerializeField] private float delayBeforeVFX = 0.1f;
    [SerializeField] private float delayBeforePanel = 0.3f;
    [SerializeField] private float panelAppear = 0.6f;
    [SerializeField] private float displayDuration = 2.5f;
    [SerializeField] private float panelDisappear = 0.3f;
    [SerializeField] private float overlayFadeOut = 0.3f;

    [Header("Animation Style")]
    [SerializeField] private float overlayAlpha = 0.72f;
    [SerializeField] private float panelOvershoot = 1.45f;

    [Header("Fireworks")]
    [SerializeField] private int fireworkCount = 8;
    [SerializeField] private float fireworkInterval = 0.2f;
    [SerializeField] private int sparksPerFirework = 50;
    [SerializeField] private int raysPerFirework = 20;
    [SerializeField] private float minBurstDistance = 60f;
    [SerializeField] private float maxBurstDistance = 220f;
    [SerializeField] private float burstDuration = 1.1f;

    [Header("Pipes")]
    [SerializeField] private RectTransform leftPipe;
    [SerializeField] private RectTransform rightPipe;
    [SerializeField] private float pipeSlideDuration = 0.6f;
    [SerializeField] private float pipeSlideOffset = 200f;
    [SerializeField] private float pipeTromboneDuration = 10f;
    [SerializeField] private float pipeTromboneOffset = 80f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] fireworkPopClips;
    [SerializeField] private AudioClip[] sparkleClips;
    [SerializeField] private AudioClip finaleClip;
    [SerializeField] private float audioVolume = 0.8f;
    [SerializeField] private float popPitchRandom = 0.08f;
    [SerializeField] private bool playSounds = true;

    [Header("Play")]
    [SerializeField] private bool playOnEnable;

    private readonly List<GameObject> spawnedObjects = new();
    private readonly Dictionary<RectTransform, Vector2> pipeOrigins = new();
    private Sequence mainSequence;

    private readonly Color[] royalPalette =
    {
        new Color32(255, 197, 39, 255),
        new Color32(255, 118, 37, 255),
        new Color32(255, 76, 92, 255),
        new Color32(80, 176, 255, 255),
        new Color32(62, 229, 128, 255),
        new Color32(178, 107, 255, 255),
        new Color32(255, 255, 235, 255)
    };

    private void Awake()
    {
        if (vfxRoot == null)
            vfxRoot = GetComponent<RectTransform>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        CreateRuntimeSpritesIfNeeded();
        SetInitialState();

        if (triggerButton != null)
            triggerButton.onClick.AddListener(PlayCelebration);
    }

    private void OnDestroy()
    {
        if (triggerButton != null)
            triggerButton.onClick.RemoveListener(PlayCelebration);

        ClearImmediate();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            PlayCelebration();
    }

    private void OnDisable()
    {
        ClearImmediate();
    }

    [ContextMenu("Play Celebration")]
    public void PlayCelebration()
    {
        ClearImmediate();

        if (vfxRoot == null)
            return;

        gameObject.SetActive(true);

        mainSequence = DOTween.Sequence();

        SetInitialState();

        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            darkOverlay.DOFade(overlayAlpha, overlayFadeIn).SetEase(Ease.OutQuad);
        }

        mainSequence.AppendInterval(delayBeforeVFX);

        PlayClip(finaleClip, 0.45f);

        mainSequence.AppendCallback(() =>
        {
            SpawnPipes();
            SpawnFireworks();
        });

        float panelTime = delayBeforeVFX + delayBeforePanel;

        mainSequence.InsertCallback(panelTime, ShowRewardPanel);

        float totalFireworkTime = fireworkCount * fireworkInterval;
        float hideTime = panelTime + panelAppear + displayDuration;

        mainSequence.InsertCallback(hideTime, () =>
        {
            HideRewardPanel();
            StopPipes();
            StopFireworks();
        });

        mainSequence.AppendInterval(panelDisappear);

        mainSequence.AppendCallback(() =>
        {
            if (darkOverlay != null)
                darkOverlay.DOFade(0f, overlayFadeOut).SetEase(Ease.OutQuad);
        });

        mainSequence.AppendInterval(overlayFadeOut);

        mainSequence.OnComplete(() =>
        {
            ClearImmediate();
        });
    }

    [ContextMenu("Stop Fast")]
    public void StopCelebrationFast()
    {
        if (mainSequence != null)
        {
            mainSequence.Kill();
            mainSequence = null;
        }

        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = spawnedObjects[i];

            if (obj == null)
                continue;

            obj.transform.DOKill();

            Image image = obj.GetComponent<Image>();

            if (image != null)
            {
                image.DOKill();

                GameObject capturedObj = obj;

                image.DOFade(0f, 0.16f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        if (capturedObj != null)
                            Destroy(capturedObj);
                    });
            }
            else
            {
                Destroy(obj);
            }
        }

        spawnedObjects.Clear();

        SetInitialState();
    }

    public void ClearImmediate()
    {
        if (mainSequence != null)
        {
            mainSequence.Kill();
            mainSequence = null;
        }

        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
            {
                spawnedObjects[i].transform.DOKill();

                Image image = spawnedObjects[i].GetComponent<Image>();

                if (image != null)
                    image.DOKill();

                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();

        SetInitialState();
    }

    private void SetInitialState()
    {
        if (darkOverlay != null)
        {
            Color c = darkOverlay.color;
            c.a = 0f;
            darkOverlay.color = c;
        }

        if (rewardPanel != null)
        {
            rewardPanel.localScale = Vector3.zero;
            rewardPanel.gameObject.SetActive(false);
        }

        if (rewardPanelGroup != null)
            rewardPanelGroup.alpha = 0f;

        if (rewardTexts != null)
        {
            foreach (var t in rewardTexts)
            {
                if (t != null)
                    t.transform.localScale = Vector3.zero;
            }
        }

        if (leftPipe != null)
        {
            if (!pipeOrigins.ContainsKey(leftPipe))
                pipeOrigins[leftPipe] = leftPipe.anchoredPosition;

            leftPipe.gameObject.SetActive(false);
        }

        if (rightPipe != null)
        {
            if (!pipeOrigins.ContainsKey(rightPipe))
                pipeOrigins[rightPipe] = rightPipe.anchoredPosition;

            rightPipe.gameObject.SetActive(false);
        }
    }

    private void ShowRewardPanel()
    {
        if (rewardPanel != null)
        {
            rewardPanel.gameObject.SetActive(true);
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

        if (rewardTexts != null)
        {
            for (int i = 0; i < rewardTexts.Length; i++)
            {
                Text t = rewardTexts[i];

                if (t == null)
                    continue;

                t.transform.localScale = Vector3.zero;

                t.transform
                    .DOScale(1f, 0.4f)
                    .SetDelay(panelAppear * 0.25f + i * 0.12f)
                    .SetEase(Ease.OutBack, 1.7f);
            }
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
                        rewardPanel.gameObject.SetActive(false);
                });
        }

        if (rewardPanelGroup != null)
        {
            rewardPanelGroup
                .DOFade(0f, panelDisappear * 0.7f)
                .SetEase(Ease.InQuad);
        }

        if (rewardTexts != null)
        {
            foreach (var t in rewardTexts)
            {
                if (t != null)
                    t.transform.DOScale(0f, panelDisappear * 0.55f).SetEase(Ease.InBack);
            }
        }
    }

    private void SpawnPipes()
    {
        if (leftPipe != null)
            AnimatePipe(leftPipe, -1f);

        if (rightPipe != null)
            AnimatePipe(rightPipe, 1f);
    }

    private void AnimatePipe(RectTransform pipe, float side)
    {
        if (!pipeOrigins.TryGetValue(pipe, out Vector2 origin))
        {
            pipeOrigins[pipe] = pipe.anchoredPosition;
            origin = pipe.anchoredPosition;
        }

        Vector2 startPos = new Vector2(origin.x + side * pipeSlideOffset * 1.5f, origin.y);

        pipe.anchoredPosition = startPos;
        pipe.gameObject.SetActive(true);

        pipe.DOAnchorPos(origin, pipeSlideDuration)
            .SetEase(Ease.OutSine);

        pipe.DOAnchorPosX(origin.x + side * pipeTromboneOffset, pipeTromboneDuration)
            .SetDelay(pipeSlideDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        SpawnPipeSparkles(pipe, side);
    }

    private void SpawnPipeSparkles(RectTransform pipe, float side)
    {
        if (!pipeOrigins.TryGetValue(pipe, out Vector2 origin))
            origin = pipe.anchoredPosition;

        for (int i = 0; i < 6; i++)
        {
            float delay = i * 0.15f;
            float xOffset = Random.Range(20f, 60f) * side;
            float yOffset = Random.Range(-30f, 30f);

            RectTransform sparkle = CreateImage(
                "Pipe_Sparkle_" + i,
                sparkleSprite,
                origin + new Vector2(xOffset, yOffset),
                Vector2.one * Random.Range(8f, 16f),
                WithAlpha(GetPaletteColor(i + 2), Random.Range(0.5f, 0.8f))
            );

            Image sparkleImage = sparkle.GetComponent<Image>();
            sparkle.localScale = Vector3.zero;

            Vector2 basePos = sparkle.anchoredPosition;

            Sequence seq = DOTween.Sequence();

            seq.AppendInterval(delay);
            seq.Append(sparkle.DOScale(Random.Range(0.8f, 1.3f), 0.2f).SetEase(Ease.OutBack));
            seq.Append(sparkle.DOAnchorPosY(basePos.y + Random.Range(20f, 50f), 0.5f).SetEase(Ease.OutQuad));
            seq.Join(sparkleImage.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
            seq.OnComplete(() => DestroyEffect(sparkle.gameObject));
        }
    }

    private void StopPipes()
    {
        StopPipe(leftPipe, -1f);
        StopPipe(rightPipe, 1f);
    }

    private void StopPipe(RectTransform pipe, float side)
    {
        if (pipe == null)
            return;

        pipe.DOKill();

        if (!pipeOrigins.TryGetValue(pipe, out Vector2 origin))
            origin = pipe.anchoredPosition;

        Vector2 exitPos = new Vector2(
            origin.x + side * pipeSlideOffset * 1.5f,
            origin.y
        );

        pipe.DOAnchorPos(exitPos, 0.25f)
            .SetEase(Ease.InQuad)
            .OnComplete(() => pipe.gameObject.SetActive(false));
    }

    private void SpawnFireworks()
    {
        Sequence fireworkSeq = DOTween.Sequence();

        for (int i = 0; i < fireworkCount; i++)
        {
            float time = i * fireworkInterval;

            Vector2 position = GetFireworkPosition(i);
            Color color = GetPaletteColor(i);

            Vector2 spawnPosition = position;
            Color spawnColor = color;

            int idx = i;

            fireworkSeq.InsertCallback(time, () =>
            {
                SpawnRocketFromSide(spawnPosition, spawnColor);
            });
        }
    }

    private Vector2 GetFireworkPosition(int index)
    {
        Rect rect = vfxRoot.rect;
        float side = (index % 2 == 0) ? -1f : 1f;

        float x = side * Random.Range(rect.width * 0.25f, rect.width * 0.45f);
        float y = Random.Range(rect.yMin * 0.15f, rect.yMax * 0.15f);

        return new Vector2(x, y);
    }

    private void SpawnRocketFromSide(Vector2 burstPosition, Color color)
    {
        Rect rect = vfxRoot.rect;
        float side = Mathf.Sign(burstPosition.x);

        Vector2 startPosition = new Vector2(
            side * (rect.width * 0.5f + 50f),
            rect.yMin - 50f
        );

        SpawnRocketTrail(startPosition, burstPosition, color);
        SpawnRocketTrailSparkles(startPosition, burstPosition, color);

        RectTransform rocketDot = CreateImage(
            "Rocket_Dot",
            softCircleSprite,
            startPosition,
            new Vector2(22f, 22f),
            WithAlpha(Color.white, 0.95f)
        );

        rocketDot
            .DOAnchorPos(burstPosition, 0.45f)
            .SetEase(Ease.OutSine)
            .OnComplete(() =>
            {
                if (rocketDot != null)
                    DestroyEffect(rocketDot.gameObject);

                SpawnFireworkBurst(burstPosition, color);
            });

        rocketDot
            .DOScale(0.15f, 0.45f)
            .SetEase(Ease.InQuad);
    }

    private void SpawnRocketTrailSparkles(Vector2 start, Vector2 end, Color color)
    {
        for (int i = 0; i < 12; i++)
        {
            float t = (i + 1) / 13f;
            Vector2 pos = Vector2.Lerp(start, end, t);
            float delay = t * 0.4f;

            RectTransform spark = CreateImage(
                "Rocket_TrailSpark",
                sparkleSprite,
                pos + Random.insideUnitCircle * 6f,
                Vector2.one * Random.Range(5f, 10f),
                WithAlpha(color, Random.Range(0.3f, 0.6f))
            );

            spark.localScale = Vector3.zero;

            Image sparkImage = spark.GetComponent<Image>();

            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(delay);
            seq.Append(spark.DOScale(Random.Range(0.6f, 1f), 0.1f).SetEase(Ease.OutBack));
            seq.Join(sparkImage.DOFade(0f, 0.25f).SetEase(Ease.InQuad));
            seq.OnComplete(() => DestroyEffect(spark.gameObject));
        }
    }

    private void SpawnFireworkBurst(Vector2 center, Color mainColor)
    {
        PlayRandomClip(fireworkPopClips, Random.Range(0.65f, 0.95f));
        SpawnFlash(center, mainColor);
        SpawnBurstRing(center, mainColor);
        SpawnBurstRays(center, mainColor);
        SpawnBurstSparks(center, mainColor);
    }

    private void SpawnFlash(Vector2 center, Color color)
    {
        RectTransform flash = CreateImage(
            "Firework_Flash",
            softCircleSprite,
            center,
            new Vector2(60f, 60f),
            WithAlpha(Color.white, 0.9f)
        );

        flash.localScale = Vector3.zero;

        Image image = flash.GetComponent<Image>();

        Sequence seq = DOTween.Sequence();
        seq.Join(flash.DOScale(3.2f, 0.2f).SetEase(Ease.OutCubic));
        seq.Join(image.DOFade(0f, 0.35f).SetEase(Ease.InQuad));
        seq.OnComplete(() => { if (flash != null) DestroyEffect(flash.gameObject); });
    }

    private void SpawnBurstRing(Vector2 center, Color color)
    {
        RectTransform ring = CreateImage(
            "Firework_Ring",
            softCircleSprite,
            center,
            new Vector2(20f, 20f),
            WithAlpha(color, 0.5f)
        );

        ring.localScale = Vector3.zero;

        Sequence seq = DOTween.Sequence();
        seq.Join(ring.DOScale(new Vector3(5f, 5f, 1f), 0.5f).SetEase(Ease.OutCubic));
        seq.Join(ring.GetComponent<Image>().DOFade(0f, 0.5f).SetEase(Ease.InQuad));
        seq.OnComplete(() => { if (ring != null) DestroyEffect(ring.gameObject); });
    }

    private void SpawnBurstRays(Vector2 center, Color color)
    {
        for (int i = 0; i < raysPerFirework; i++)
        {
            float angle = 360f / raysPerFirework * i + Random.Range(-6f, 6f);
            Vector2 direction = DirectionFromAngle(angle);

            float distance = Random.Range(minBurstDistance * 0.6f, maxBurstDistance * 0.8f);
            Vector2 endPosition = center + direction * distance;

            Color rayColor = Color.Lerp(color, Color.white, Random.Range(0.2f, 0.5f));

            RectTransform ray = CreateImage(
                "Firework_Ray",
                streakSprite,
                center,
                new Vector2(Random.Range(3f, 6f), Random.Range(35f, 80f)),
                WithAlpha(rayColor, Random.Range(0.6f, 0.9f))
            );

            ray.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            ray.localScale = new Vector3(0.3f, 0.3f, 1f);

            Image image = ray.GetComponent<Image>();
            float duration = Random.Range(0.3f, 0.55f);

            Sequence seq = DOTween.Sequence();
            seq.Join(ray.DOAnchorPos(endPosition, duration).SetEase(Ease.OutSine));
            seq.Join(ray.DOScale(new Vector3(1f, Random.Range(1f, 1.4f), 1f), 0.15f).SetEase(Ease.OutBack));
            seq.Insert(duration * 0.3f, image.DOFade(0f, duration * 0.7f).SetEase(Ease.InQuad));
            seq.OnComplete(() => { if (ray != null) DestroyEffect(ray.gameObject); });
        }
    }

    private void SpawnBurstSparks(Vector2 center, Color color)
    {
        for (int i = 0; i < sparksPerFirework; i++)
        {
            float angle = Random.Range(0f, 360f);
            Vector2 direction = DirectionFromAngle(angle);

            float distance = Random.Range(minBurstDistance, maxBurstDistance);
            float duration = Random.Range(burstDuration * 0.7f, burstDuration * 1.3f);

            Vector2 endPosition = center + direction * distance;
            endPosition.y -= Random.Range(30f, 120f);

            Color sparkColor;

            if (Random.value < 0.6f)
                sparkColor = Color.Lerp(color, Color.white, Random.Range(0.05f, 0.4f));
            else
                sparkColor = GetPaletteColor(Random.Range(0, royalPalette.Length));

            float sparkSize = Random.Range(8f, 26f);
            float startAlpha = Random.Range(0.7f, 1f);

            RectTransform spark = CreateImage(
                "Firework_Spark",
                sparkleSprite,
                center,
                Vector2.one * sparkSize,
                WithAlpha(sparkColor, startAlpha)
            );

            spark.localScale = Vector3.one * Random.Range(0.25f, 0.65f);
            spark.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Image image = spark.GetComponent<Image>();

            Sequence seq = DOTween.Sequence();
            seq.Join(spark.DOAnchorPos(endPosition, duration).SetEase(Ease.OutQuad));
            seq.Join(spark.DORotate(new Vector3(0f, 0f, Random.Range(-360f, 360f)), duration, RotateMode.FastBeyond360));
            seq.Insert(0f, spark.DOScale(Random.Range(0.8f, 1.3f), 0.15f).SetEase(Ease.OutBack));
            seq.Insert(duration * 0.2f, spark.DOScale(0f, duration * 0.8f).SetEase(Ease.InCubic));
            seq.Insert(duration * 0.15f, image.DOFade(0f, duration * 0.85f).SetEase(Ease.InQuad));
            seq.OnComplete(() => { if (spark != null) DestroyEffect(spark.gameObject); });
        }
    }

    private void SpawnRocketTrail(Vector2 start, Vector2 end, Color color)
    {
        Vector2 middle = (start + end) * 0.5f;
        float distance = Vector2.Distance(start, end);
        float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;

        RectTransform trail = CreateImage(
            "Rocket_Trail",
            streakSprite,
            middle,
            new Vector2(8f, distance),
            WithAlpha(color, 0.45f)
        );

        trail.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);

        Image image = trail.GetComponent<Image>();

        Sequence seq = DOTween.Sequence();
        seq.Append(image.DOFade(0f, 0.35f).SetEase(Ease.InQuad));
        seq.Join(trail.DOScale(new Vector3(0.4f, 0.2f, 1f), 0.35f).SetEase(Ease.InQuad));
        seq.OnComplete(() => { if (trail != null) DestroyEffect(trail.gameObject); });
    }

    private void StopFireworks()
    {
        var fireworksCopy = new List<GameObject>(spawnedObjects);

        foreach (var obj in fireworksCopy)
        {
            if (obj == null)
                continue;

            if (obj.name.StartsWith("Firework_") || obj.name.StartsWith("Rocket_"))
            {
                obj.transform.DOKill();

                Image image = obj.GetComponent<Image>();

                if (image != null)
                {
                    image.DOKill();

                    GameObject captured = obj;

                    image.DOFade(0f, 0.15f).SetEase(Ease.InQuad).OnComplete(() =>
                    {
                        if (captured != null)
                            DestroyEffect(captured);
                    });
                }
                else
                {
                    Destroy(obj);
                }
            }
        }
    }

    private RectTransform CreateImage(
        string objectName,
        Sprite sprite,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color
    )
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.SetParent(vfxRoot, false);

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;

        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = false;

        spawnedObjects.Add(obj);

        return rectTransform;
    }

    private void DestroyEffect(GameObject obj)
    {
        if (obj == null)
            return;

        spawnedObjects.Remove(obj);
        Destroy(obj);
    }

    private Color GetPaletteColor(int index)
    {
        return royalPalette[Mathf.Abs(index) % royalPalette.Length];
    }

    private Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void CreateRuntimeSpritesIfNeeded()
    {
        if (softCircleSprite == null)
            softCircleSprite = CreateSoftCircleSprite(96);

        if (sparkleSprite == null)
            sparkleSprite = CreateSparkleSprite(96);

        if (streakSprite == null)
            streakSprite = CreateStreakSprite(16, 96);
    }

    private Sprite CreateSoftCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime_SoftCircle";

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalized = Mathf.Clamp01(distance / radius);
                float alpha = 1f - normalized;
                alpha = alpha * alpha * alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateSparkleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime_Sparkle";

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y) - center;

                float nx = Mathf.Abs(p.x) / half;
                float ny = Mathf.Abs(p.y) / half;

                float vertical = Mathf.Clamp01(1f - nx * 7f) * Mathf.Clamp01(1f - ny * 1.2f);
                float horizontal = Mathf.Clamp01(1f - ny * 7f) * Mathf.Clamp01(1f - nx * 1.2f);

                float diagonalA = Mathf.Clamp01(1f - Mathf.Abs(p.x - p.y) / (half * 0.22f)) *
                                  Mathf.Clamp01(1f - (nx + ny) * 0.85f);

                float diagonalB = Mathf.Clamp01(1f - Mathf.Abs(p.x + p.y) / (half * 0.22f)) *
                                  Mathf.Clamp01(1f - (nx + ny) * 0.85f);

                float circleGlow = Mathf.Clamp01(1f - p.magnitude / half);
                circleGlow = circleGlow * circleGlow * 0.45f;

                float alpha = Mathf.Clamp01(vertical + horizontal + diagonalA * 0.45f + diagonalB * 0.45f + circleGlow);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateStreakSprite(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Runtime_Streak";

        float centerX = width * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float vertical = 1f - Mathf.Abs((y / (float)(height - 1)) - 0.5f) * 2f;
            vertical = Mathf.Pow(vertical, 0.65f);

            for (int x = 0; x < width; x++)
            {
                float horizontal = 1f - Mathf.Abs(x - centerX) / centerX;
                horizontal = Mathf.Clamp01(horizontal);

                float alpha = vertical * horizontal;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void PlayRandomClip(AudioClip[] clips, float volumeMultiplier = 1f)
    {
        if (!playSounds || audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clip == null)
            return;

        float oldPitch = audioSource.pitch;
        audioSource.pitch = Random.Range(1f - popPitchRandom, 1f + popPitchRandom);
        audioSource.PlayOneShot(clip, audioVolume * volumeMultiplier);
        audioSource.pitch = oldPitch;
    }

    private void PlayClip(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (!playSounds || audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, audioVolume * volumeMultiplier);
    }
}
