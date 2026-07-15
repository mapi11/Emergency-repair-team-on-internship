using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Match3FireworksVFX : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform vfxRoot;

    [Header("Optional Sprites")]
    [SerializeField] private Sprite softCircleSprite;
    [SerializeField] private Sprite sparkleSprite;
    [SerializeField] private Sprite streakSprite;
    [SerializeField] private Sprite confettiSprite;

    [Header("Timing")]
    [SerializeField] private int fireworkCount = 9;
    [SerializeField] private float fireworkInterval = 0.18f;
    [SerializeField] private float startDelay = 0.05f;

    [Header("Firework Shape")]
    [SerializeField] private int sparksPerFirework = 38;
    [SerializeField] private int raysPerFirework = 16;
    [SerializeField] private float minBurstDistance = 80f;
    [SerializeField] private float maxBurstDistance = 240f;
    [SerializeField] private float burstDuration = 0.85f;

    [Header("Confetti")]
    [SerializeField] private bool spawnConfetti = true;
    [SerializeField] private int confettiCount = 45;

    [Header("Style")]
    [SerializeField] private float glowScale = 1.1f;
    [SerializeField] private float vfxAlpha = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] fireworkPopClips;
    [SerializeField] private AudioClip[] sparkleClips;
    [SerializeField] private AudioClip[] confettiClips;
    [SerializeField] private AudioClip finaleClip;
    [SerializeField] private float audioVolume = 0.8f;
    [SerializeField] private float popPitchRandom = 0.08f;
    [SerializeField] private bool playSounds = true;

    [Header("Debug")]
    [SerializeField] private bool playOnEnable;
    [SerializeField] private int aliveEffects;

    private readonly List<GameObject> spawnedObjects = new();
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
        {
            vfxRoot = GetComponent<RectTransform>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        CreateRuntimeSpritesIfNeeded();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayCelebration();
        }
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
        {
            Debug.LogError("Match3FireworksVFX: VFX Root is missing.");
            return;
        }

        gameObject.SetActive(true);

        mainSequence = DOTween.Sequence();

        PlayClip(finaleClip, 0.45f);

        //SpawnBackgroundMagicGlow();

        if (spawnConfetti)
        {
            mainSequence.InsertCallback(0.05f, SpawnConfettiRain);
        }

        for (int i = 0; i < fireworkCount; i++)
        {
            float time = startDelay + i * fireworkInterval;

            Vector2 position = GetNiceFireworkPosition(i);
            Color color = GetPaletteColor(i);

            Vector2 spawnPosition = position;
            Color spawnColor = color;

            mainSequence.InsertCallback(time, () =>
            {
                SpawnRocketFirework(spawnPosition, spawnColor);
            });
        }

        mainSequence.InsertCallback(startDelay + fireworkCount * fireworkInterval + 0.1f, SpawnFinalSparkles);
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
                        {
                            Destroy(capturedObj);
                        }
                    });
            }
            else
            {
                Destroy(obj);
            }
        }

        spawnedObjects.Clear();
        aliveEffects = 0;
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
                {
                    image.DOKill();
                }

                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();
        aliveEffects = 0;
    }

    private void SpawnRocketFirework(Vector2 burstPosition, Color mainColor)
    {
        Vector2 startPosition = GetRocketStartPosition(burstPosition);

        SpawnRocketTrail(startPosition, burstPosition, mainColor);

        RectTransform rocketDot = CreateImage(
            "Rocket_Dot",
            softCircleSprite,
            startPosition,
            new Vector2(22f, 22f),
            WithAlpha(Color.white, 0.95f)
        );

        rocketDot
            .DOAnchorPos(burstPosition, 0.32f)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                if (rocketDot != null)
                {
                    DestroyEffect(rocketDot.gameObject);
                }

                SpawnFireworkBurst(burstPosition, mainColor);
            });

        rocketDot
            .DOScale(0.35f, 0.32f)
            .SetEase(Ease.InQuad);
    }

    private void SpawnFireworkBurst(Vector2 center, Color mainColor)
    {
        PlayRandomClip(fireworkPopClips, Random.Range(0.65f, 0.95f));

        SpawnFlash(center, mainColor);
        SpawnBurstRays(center, mainColor);
        SpawnBurstSparks(center, mainColor);
        SpawnMicroStars(center);
    }

    private void SpawnFlash(Vector2 center, Color color)
    {
        RectTransform flash = CreateImage(
            "Firework_Flash",
            softCircleSprite,
            center,
            new Vector2(60f, 60f) * glowScale,
            WithAlpha(color, 0.75f * vfxAlpha)
        );

        flash.localScale = Vector3.zero;

        Image image = flash.GetComponent<Image>();

        Sequence sequence = DOTween.Sequence();

        sequence.Join(
            flash.DOScale(2.8f, 0.25f)
                .SetEase(Ease.OutCubic)
        );

        sequence.Join(
            image.DOFade(0f, 0.38f)
                .SetEase(Ease.InQuad)
        );

        sequence.OnComplete(() =>
        {
            if (flash != null)
            {
                DestroyEffect(flash.gameObject);
            }
        });
    }

    private void SpawnBurstRays(Vector2 center, Color color)
    {
        for (int i = 0; i < raysPerFirework; i++)
        {
            float angle = 360f / raysPerFirework * i + Random.Range(-8f, 8f);
            Vector2 direction = DirectionFromAngle(angle);

            float distance = Random.Range(minBurstDistance * 0.65f, maxBurstDistance * 0.85f);
            Vector2 endPosition = center + direction * distance;

            Color rayColor = Color.Lerp(color, Color.white, 0.35f);

            RectTransform ray = CreateImage(
                "Firework_Ray",
                streakSprite,
                center,
                new Vector2(Random.Range(5f, 8f), Random.Range(50f, 90f)),
                WithAlpha(rayColor, 0.85f * vfxAlpha)
            );

            ray.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            ray.localScale = new Vector3(0.4f, 0.4f, 1f);

            Image image = ray.GetComponent<Image>();

            Sequence sequence = DOTween.Sequence();

            sequence.Join(
                ray.DOAnchorPos(endPosition, Random.Range(0.38f, 0.58f))
                    .SetEase(Ease.OutCubic)
            );

            sequence.Join(
                ray.DOScale(new Vector3(1f, 1.2f, 1f), 0.16f)
                    .SetEase(Ease.OutBack)
            );

            sequence.Insert(
                0.12f,
                image.DOFade(0f, 0.42f)
                    .SetEase(Ease.InQuad)
            );

            sequence.OnComplete(() =>
            {
                if (ray != null)
                {
                    DestroyEffect(ray.gameObject);
                }
            });
        }
    }

    private void SpawnBurstSparks(Vector2 center, Color color)
    {
        for (int i = 0; i < sparksPerFirework; i++)
        {
            float angle = Random.Range(0f, 360f);
            Vector2 direction = DirectionFromAngle(angle);

            float distance = Random.Range(minBurstDistance, maxBurstDistance);
            float duration = Random.Range(burstDuration * 0.75f, burstDuration * 1.2f);

            Vector2 endPosition = center + direction * distance;
            endPosition.y -= Random.Range(20f, 90f);

            Color sparkColor;

            if (Random.value < 0.7f)
            {
                sparkColor = Color.Lerp(color, Color.white, Random.Range(0.1f, 0.45f));
            }
            else
            {
                sparkColor = GetPaletteColor(Random.Range(0, royalPalette.Length));
            }

            RectTransform spark = CreateImage(
                "Firework_Spark",
                sparkleSprite,
                center,
                Vector2.one * Random.Range(13f, 28f),
                WithAlpha(sparkColor, Random.Range(0.75f, 1f) * vfxAlpha)
            );

            spark.localScale = Vector3.one * Random.Range(0.35f, 0.75f);
            spark.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Image image = spark.GetComponent<Image>();

            Sequence sequence = DOTween.Sequence();

            sequence.Join(
                spark.DOAnchorPos(endPosition, duration)
                    .SetEase(Ease.OutCubic)
            );

            sequence.Join(
                spark.DORotate(
                    new Vector3(0f, 0f, Random.Range(-260f, 260f)),
                    duration,
                    RotateMode.FastBeyond360
                )
            );

            sequence.Insert(
                0f,
                spark.DOScale(Random.Range(0.9f, 1.25f), 0.18f)
                    .SetEase(Ease.OutBack)
            );

            sequence.Insert(
                duration * 0.28f,
                spark.DOScale(0f, duration * 0.72f)
                    .SetEase(Ease.InQuad)
            );

            sequence.Insert(
                duration * 0.22f,
                image.DOFade(0f, duration * 0.75f)
                    .SetEase(Ease.InQuad)
            );

            sequence.OnComplete(() =>
            {
                if (spark != null)
                {
                    DestroyEffect(spark.gameObject);
                }
            });
        }
    }

    private void SpawnMicroStars(Vector2 center)
    {
        int count = 12;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(30f, 130f);
            Vector2 position = center + offset;

            RectTransform star = CreateImage(
                "Micro_Star",
                sparkleSprite,
                position,
                Vector2.one * Random.Range(10f, 22f),
                WithAlpha(Color.white, Random.Range(0.6f, 0.95f))
            );

            star.localScale = Vector3.zero;

            Image image = star.GetComponent<Image>();

            Sequence sequence = DOTween.Sequence();

            sequence.Append(
                star.DOScale(Random.Range(0.75f, 1.25f), 0.16f)
                    .SetEase(Ease.OutBack)
            );

            sequence.AppendInterval(Random.Range(0.04f, 0.12f));

            sequence.Join(
                star.DORotate(
                    new Vector3(0f, 0f, Random.Range(120f, 260f)),
                    0.35f,
                    RotateMode.FastBeyond360
                )
            );

            sequence.Append(
                image.DOFade(0f, 0.28f)
                    .SetEase(Ease.InQuad)
            );

            sequence.Join(
                star.DOScale(0f, 0.28f)
                    .SetEase(Ease.InBack)
            );

            sequence.OnComplete(() =>
            {
                if (star != null)
                {
                    DestroyEffect(star.gameObject);
                }
            });
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
            new Vector2(10f, distance),
            WithAlpha(color, 0.45f * vfxAlpha)
        );

        trail.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);

        Image image = trail.GetComponent<Image>();

        Sequence sequence = DOTween.Sequence();

        sequence.Append(
            image.DOFade(0f, 0.35f)
                .SetEase(Ease.InQuad)
        );

        sequence.Join(
            trail.DOScale(new Vector3(0.4f, 0.2f, 1f), 0.35f)
                .SetEase(Ease.InQuad)
        );

        sequence.OnComplete(() =>
        {
            if (trail != null)
            {
                DestroyEffect(trail.gameObject);
            }
        });
    }

    private void SpawnConfettiRain()
    {
        PlayRandomClip(confettiClips, 0.45f);

        Rect rect = vfxRoot.rect;

        for (int i = 0; i < confettiCount; i++)
        {
            float startX = Random.Range(rect.xMin + 40f, rect.xMax - 40f);
            float startY = rect.yMax + Random.Range(20f, 180f);

            Vector2 startPosition = new Vector2(startX, startY);

            Vector2 endPosition = new Vector2(
                startX + Random.Range(-120f, 120f),
                rect.yMin - Random.Range(40f, 160f)
            );

            Color color = GetPaletteColor(Random.Range(0, royalPalette.Length));

            RectTransform confetti = CreateImage(
                "Confetti",
                confettiSprite,
                startPosition,
                new Vector2(Random.Range(8f, 15f), Random.Range(16f, 30f)),
                WithAlpha(color, Random.Range(0.65f, 0.95f) * vfxAlpha)
            );

            confetti.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            float delay = Random.Range(0f, 0.7f);
            float duration = Random.Range(1.0f, 1.8f);

            Image image = confetti.GetComponent<Image>();

            Sequence sequence = DOTween.Sequence();

            sequence.AppendInterval(delay);

            sequence.Append(
                confetti.DOAnchorPos(endPosition, duration)
                    .SetEase(Ease.InQuad)
            );

            sequence.Join(
                confetti.DORotate(
                    new Vector3(0f, 0f, Random.Range(-540f, 540f)),
                    duration,
                    RotateMode.FastBeyond360
                )
            );

            sequence.Insert(
                delay + duration * 0.55f,
                image.DOFade(0f, duration * 0.45f)
                    .SetEase(Ease.InQuad)
            );

            sequence.OnComplete(() =>
            {
                if (confetti != null)
                {
                    DestroyEffect(confetti.gameObject);
                }
            });
        }
    }

    private void SpawnBackgroundMagicGlow()
    {
        RectTransform glow = CreateImage(
            "Background_Glow",
            softCircleSprite,
            Vector2.zero,
            new Vector2(520f, 520f),
            WithAlpha(new Color32(48, 147, 255, 255), 0.15f)
        );

        glow.localScale = Vector3.zero;

        Image image = glow.GetComponent<Image>();

        Sequence sequence = DOTween.Sequence();

        sequence.Append(
            glow.DOScale(1.2f * glowScale, 0.45f)
                .SetEase(Ease.OutCubic)
        );

        sequence.Join(
            image.DOFade(0.22f, 0.45f)
        );

        sequence.AppendInterval(0.65f);

        sequence.Append(
            image.DOFade(0f, 0.35f)
                .SetEase(Ease.InQuad)
        );

        sequence.Join(
            glow.DOScale(1.6f * glowScale, 0.35f)
                .SetEase(Ease.InQuad)
        );

        sequence.OnComplete(() =>
        {
            if (glow != null)
            {
                DestroyEffect(glow.gameObject);
            }
        });
    }

    private void SpawnFinalSparkles()
    {
        PlayRandomClip(sparkleClips, 0.65f);

        for (int i = 0; i < 18; i++)
        {
            Vector2 position = new Vector2(
                Random.Range(-280f, 280f),
                Random.Range(-120f, 190f)
            );

            RectTransform sparkle = CreateImage(
                "Final_Sparkle",
                sparkleSprite,
                position,
                Vector2.one * Random.Range(16f, 34f),
                WithAlpha(Color.white, Random.Range(0.5f, 0.9f))
            );

            sparkle.localScale = Vector3.zero;
            sparkle.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Image image = sparkle.GetComponent<Image>();

            Sequence sequence = DOTween.Sequence();

            sequence.Append(
                sparkle.DOScale(Random.Range(0.8f, 1.4f), 0.18f)
                    .SetEase(Ease.OutBack)
            );

            sequence.AppendInterval(Random.Range(0.05f, 0.25f));

            sequence.Append(
                image.DOFade(0f, 0.25f)
                    .SetEase(Ease.InQuad)
            );

            sequence.Join(
                sparkle.DOScale(0f, 0.25f)
                    .SetEase(Ease.InBack)
            );

            sequence.OnComplete(() =>
            {
                if (sparkle != null)
                {
                    DestroyEffect(sparkle.gameObject);
                }
            });
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
        aliveEffects = spawnedObjects.Count;

        return rectTransform;
    }

    private void DestroyEffect(GameObject obj)
    {
        if (obj == null)
            return;

        spawnedObjects.Remove(obj);
        aliveEffects = spawnedObjects.Count;

        Destroy(obj);
    }

    private Vector2 GetNiceFireworkPosition(int index)
    {
        Rect rect = vfxRoot.rect;

        float width = rect.width;
        float height = rect.height;

        Vector2[] preferred =
        {
            new Vector2(-width * 0.33f, height * 0.22f),
            new Vector2(width * 0.33f, height * 0.24f),
            new Vector2(-width * 0.22f, height * 0.05f),
            new Vector2(width * 0.24f, height * 0.03f),
            new Vector2(0f, height * 0.32f),
            new Vector2(-width * 0.42f, -height * 0.04f),
            new Vector2(width * 0.42f, -height * 0.02f),
            new Vector2(0f, height * 0.12f)
        };

        Vector2 basePosition = preferred[index % preferred.Length];

        basePosition.x += Random.Range(-35f, 35f);
        basePosition.y += Random.Range(-28f, 28f);

        return basePosition;
    }

    private Vector2 GetRocketStartPosition(Vector2 burstPosition)
    {
        Rect rect = vfxRoot.rect;

        float x = burstPosition.x * Random.Range(0.35f, 0.65f);
        float y = rect.yMin - Random.Range(20f, 90f);

        return new Vector2(x, y);
    }

    private Color GetPaletteColor(int index)
    {
        return royalPalette[Mathf.Abs(index) % royalPalette.Length];
    }

    private Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        );
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void CreateRuntimeSpritesIfNeeded()
    {
        if (softCircleSprite == null)
        {
            softCircleSprite = CreateSoftCircleSprite(96);
        }

        if (sparkleSprite == null)
        {
            sparkleSprite = CreateSparkleSprite(96);
        }

        if (streakSprite == null)
        {
            streakSprite = CreateStreakSprite(16, 96);
        }

        if (confettiSprite == null)
        {
            confettiSprite = CreateSolidSprite(16, 16);
        }
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

        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
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

        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
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

        return Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private Sprite CreateSolidSprite(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Runtime_Solid";

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private void PlayRandomClip(AudioClip[] clips, float volumeMultiplier = 1f)
    {
        if (!playSounds)
            return;

        if (audioSource == null)
            return;

        if (clips == null || clips.Length == 0)
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
        if (!playSounds)
            return;

        if (audioSource == null)
            return;

        if (clip == null)
            return;

        audioSource.PlayOneShot(clip, audioVolume * volumeMultiplier);
    }
}