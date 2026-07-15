using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ProximityVoiceSpeaker : NetworkBehaviour
{
    private static readonly Dictionary<ulong, ProximityVoiceSpeaker> Speakers = new();

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private int maxQueuedSamples = 96000;
    [SerializeField] private float volume = 1f;

    [Header("Distance")]
    [SerializeField] private bool useManualDistanceAttenuation = true;
    [SerializeField] private float minDistance = 1.2f;
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private float manualDistanceVolume = 1f;

    [Header("Mouth")]
    [SerializeField] private Transform mouthTransform;
    [SerializeField] private Vector3 closedMouthScale = Vector3.one;
    [SerializeField] private Vector3 openMouthScale = new Vector3(1f, 1.8f, 1f);
    [SerializeField] private float mouthSensitivity = 18f;
    [SerializeField] private float mouthSmooth = 18f;
    [SerializeField] private float mouthCloseDelay = 0.12f;

    [Header("Debug")]
    [SerializeField] private float currentAmplitude;
    [SerializeField] private float targetMouthAmount;
    [SerializeField] private int queuedSamplesDebug;
    [SerializeField] private float distanceToListenerDebug;
    [SerializeField] private bool hasListenerDebug;

    private readonly Queue<float> sampleQueue = new();
    private readonly object queueLock = new();

    private AudioClip streamingClip;
    private float lastVoiceTime;
    private Vector3 mouthStartScale;

    public static bool TryGetSpeaker(ulong clientId, out ProximityVoiceSpeaker speaker)
    {
        return Speakers.TryGetValue(clientId, out speaker);
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        SetupAudioSource();

        if (mouthTransform != null)
        {
            mouthStartScale = mouthTransform.localScale;
            closedMouthScale = mouthStartScale;
        }
    }

    public override void OnNetworkSpawn()
    {
        Speakers[OwnerClientId] = this;

        SetupAudioSource();
        StartStreamingAudioClip();

        Debug.Log($"🎙 Voice speaker registered. OwnerClientId={OwnerClientId}");
    }

    public override void OnNetworkDespawn()
    {
        if (Speakers.ContainsKey(OwnerClientId) && Speakers[OwnerClientId] == this)
        {
            Speakers.Remove(OwnerClientId);
        }

        StopStreamingAudioClip();
    }

    private void OnDestroy()
    {
        StopStreamingAudioClip();
    }

    private void Update()
    {
        UpdateManualDistanceVolume();
        UpdateMouth();
    }

    public void EnqueuePcm16(byte[] pcmData)
    {
        if (pcmData == null || pcmData.Length == 0)
            return;

        float sum = 0f;
        int sampleCount = pcmData.Length / 2;

        lock (queueLock)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short value = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
                float sample = value / 32768f;

                sampleQueue.Enqueue(sample);

                sum += sample * sample;
            }

            while (sampleQueue.Count > maxQueuedSamples)
            {
                sampleQueue.Dequeue();
            }

            queuedSamplesDebug = sampleQueue.Count;
        }

        float amplitude = Mathf.Sqrt(sum / Mathf.Max(1, sampleCount));

        SetAmplitude(amplitude);
    }

    public void SetLocalAmplitude(float amplitude)
    {
        SetAmplitude(amplitude);
    }

    private void SetAmplitude(float amplitude)
    {
        currentAmplitude = amplitude;
        targetMouthAmount = Mathf.Clamp01(amplitude * mouthSensitivity);

        if (targetMouthAmount > 0.02f)
        {
            lastVoiceTime = Time.time;
        }
    }

    private void UpdateMouth()
    {
        if (mouthTransform == null)
            return;

        if (Time.time - lastVoiceTime > mouthCloseDelay)
        {
            targetMouthAmount = 0f;
        }

        Vector3 targetScale = Vector3.Lerp(
            closedMouthScale,
            openMouthScale,
            targetMouthAmount
        );

        mouthTransform.localScale = Vector3.Lerp(
            mouthTransform.localScale,
            targetScale,
            Time.deltaTime * mouthSmooth
        );
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = volume;

        audioSource.spatialBlend = 1f;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        audioSource.dopplerLevel = 0f;
        audioSource.spread = 0f;
    }

    private void StartStreamingAudioClip()
    {
        if (audioSource == null)
            return;

        int sampleRate = AudioSettings.outputSampleRate;

        if (sampleRate <= 0)
        {
            sampleRate = 48000;
        }

        streamingClip = AudioClip.Create(
            $"VoiceStream_{OwnerClientId}",
            sampleRate,
            1,
            sampleRate,
            true,
            OnAudioClipRead,
            OnAudioClipSetPosition
        );

        audioSource.clip = streamingClip;
        audioSource.loop = true;
        audioSource.Play();

        Debug.Log($"🎙 Streaming AudioClip started. OwnerClientId={OwnerClientId}, SampleRate={sampleRate}");
    }

    private void StopStreamingAudioClip()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        streamingClip = null;

        lock (queueLock)
        {
            sampleQueue.Clear();
            queuedSamplesDebug = 0;
        }
    }

    private void OnAudioClipRead(float[] data)
    {
        if (data == null)
            return;

        float distanceVolume = manualDistanceVolume;
        float finalVolume = volume * distanceVolume;

        lock (queueLock)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float sample = 0f;

                if (sampleQueue.Count > 0)
                {
                    sample = sampleQueue.Dequeue();
                }

                data[i] = sample * finalVolume;
            }

            queuedSamplesDebug = sampleQueue.Count;
        }
    }

    private void OnAudioClipSetPosition(int position)
    {
        // Ничего не делаем.
    }

    private void UpdateManualDistanceVolume()
    {
        if (!useManualDistanceAttenuation)
        {
            manualDistanceVolume = 1f;
            return;
        }

        if (IsOwner)
        {
            manualDistanceVolume = 0f;
            distanceToListenerDebug = 0f;
            return;
        }

        AudioListener listener = FindActiveAudioListener();

        if (listener == null)
        {
            hasListenerDebug = false;
            manualDistanceVolume = 1f;
            return;
        }

        hasListenerDebug = true;

        float distance = Vector3.Distance(transform.position, listener.transform.position);
        distanceToListenerDebug = distance;

        if (distance <= minDistance)
        {
            manualDistanceVolume = 1f;
            return;
        }

        if (distance >= maxDistance)
        {
            manualDistanceVolume = 0f;
            return;
        }

        float t = Mathf.InverseLerp(minDistance, maxDistance, distance);

        manualDistanceVolume = Mathf.SmoothStep(1f, 0f, t);
    }

    private AudioListener FindActiveAudioListener()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];

            if (listener == null)
                continue;

            if (!listener.enabled)
                continue;

            if (!listener.gameObject.activeInHierarchy)
                continue;

            return listener;
        }

        return null;
    }
}