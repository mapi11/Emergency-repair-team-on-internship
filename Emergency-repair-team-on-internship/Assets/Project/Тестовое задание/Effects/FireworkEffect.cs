using UnityEngine;

public class FireworkEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private float burstInterval = 0.4f;
    [SerializeField] private int particlesPerBurst = 30;
    [SerializeField] private Vector2 spawnArea = new Vector2(800f, 400f);

    private float burstTimer;
    private bool isPlaying;

    private void Start()
    {
        foreach (var ps in particleSystems)
        {
            if (ps != null)
                ps.Stop();
        }

        isPlaying = false;
    }

    public void Play()
    {
        isPlaying = true;
        burstTimer = 0f;

        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                var emission = ps.emission;
                emission.enabled = true;

                ps.time = 0f;
                ps.Play();
            }
        }

        EmitBurst();
    }

    public void Stop()
    {
        isPlaying = false;

        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                var emission = ps.emission;
                emission.enabled = false;

                ps.Stop();
            }
        }
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        burstTimer += Time.deltaTime;

        if (burstTimer >= burstInterval)
        {
            burstTimer = 0f;
            EmitBurst();
        }
    }

    private void EmitBurst()
    {
        if (particleSystems.Length == 0)
            return;

        Vector3 center = new Vector3(
            Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f),
            Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f),
            0f
        );

        var ps = particleSystems[Random.Range(0, particleSystems.Length)];

        var main = ps.main;
        main.startColor = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);

        ps.transform.localPosition = center;
        ps.Emit(particlesPerBurst);
    }
}
