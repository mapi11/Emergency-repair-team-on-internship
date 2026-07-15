using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProximityVoiceManager : MonoBehaviour
{
    private const string VoiceToServerMessage = "Voice_To_Server";
    private const string VoiceToClientsMessage = "Voice_To_Clients";

    [Header("Input")]
    [SerializeField] private Key pushToTalkKey = Key.V;

    [Header("Microphone")]
    [SerializeField] private int recordingLengthSeconds = 10;
    [SerializeField] private int chunkMilliseconds = 10;

    [Header("Network")]
    [SerializeField] private int maxPacketBytes = 1000;

    public static ProximityVoiceManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool micStarted;
    [SerializeField] private bool isTalking;
    [SerializeField] private string currentMicrophoneName;
    [SerializeField] private int sampleRate;
    [SerializeField] private int chunkSamples;
    [SerializeField] private float lastAmplitude;

    private AudioClip microphoneClip;
    private int microphoneReadPosition;
    private float[] captureBuffer;
    private byte[] sendBuffer;

    private bool handlersRegistered;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        StopMicrophone();
        UnregisterMessageHandlers();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsClient)
            return;

        if (!NetworkManager.Singleton.IsListening)
            return;

        EnsureMessageHandlersRegistered();
        EnsureMicrophoneStarted();

        if (!micStarted)
            return;

        bool pushToTalkPressed =
            Keyboard.current != null &&
            Keyboard.current[pushToTalkKey].isPressed;

        isTalking = pushToTalkPressed;

        if (!pushToTalkPressed)
        {
            microphoneReadPosition = Microphone.GetPosition(currentMicrophoneName);
            UpdateLocalMouth(0f);
            return;
        }

        CaptureAndSendVoice();
    }

    private void EnsureMicrophoneStarted()
    {
        if (micStarted)
            return;

        VoiceChatSettings.Load();

        string selectedMicrophone = VoiceChatSettings.GetSelectedOrDefaultMicrophoneName();

        if (string.IsNullOrWhiteSpace(selectedMicrophone))
        {
            Debug.LogWarning("🎙 No microphone selected or available.");
            return;
        }

        currentMicrophoneName = selectedMicrophone;

        sampleRate = AudioSettings.outputSampleRate;

        if (sampleRate <= 0)
        {
            sampleRate = 48000;
        }

        int desiredChunkSamples = Mathf.Max(
    160,
    Mathf.RoundToInt(sampleRate * (chunkMilliseconds / 1000f))
);

        // 1 sample PCM16 = 2 bytes.
        // У Netcode лимит сообщения около 1264 bytes,
        // поэтому держим voice payload примерно до 1000 bytes.
        int safePayloadBytes = Mathf.Clamp(maxPacketBytes, 320, 1000);
        int maxChunkSamplesByPacket = safePayloadBytes / 2;

        chunkSamples = Mathf.Min(desiredChunkSamples, maxChunkSamplesByPacket);

        captureBuffer = new float[chunkSamples];
        sendBuffer = new byte[chunkSamples * 2];

        Debug.Log(
            $"🎙 Voice chunk configured. " +
            $"SampleRate={sampleRate}, " +
            $"DesiredSamples={desiredChunkSamples}, " +
            $"FinalSamples={chunkSamples}, " +
            $"PacketBytes={sendBuffer.Length}"
        );

        microphoneClip = Microphone.Start(
            currentMicrophoneName,
            true,
            recordingLengthSeconds,
            sampleRate
        );

        microphoneReadPosition = 0;
        micStarted = true;

        Debug.Log($"🎙 Microphone started: {currentMicrophoneName}, SampleRate={sampleRate}, ChunkSamples={chunkSamples}");
    }

    private void StopMicrophone()
    {
        if (!micStarted)
            return;

        if (!string.IsNullOrWhiteSpace(currentMicrophoneName))
        {
            Microphone.End(currentMicrophoneName);
        }

        microphoneClip = null;
        micStarted = false;

        Debug.Log("🎙 Microphone stopped");
    }

    public void RestartMicrophone()
    {
        StopMicrophone();
        micStarted = false;
        microphoneReadPosition = 0;
    }

    private void CaptureAndSendVoice()
    {
        if (microphoneClip == null)
            return;

        int microphonePosition = Microphone.GetPosition(currentMicrophoneName);

        if (microphonePosition < 0)
            return;

        int availableSamples = GetAvailableSamples(
            microphoneReadPosition,
            microphonePosition,
            microphoneClip.samples
        );

        while (availableSamples >= chunkSamples)
        {
            ReadMicrophoneChunk(microphoneReadPosition, captureBuffer);

            microphoneReadPosition += chunkSamples;

            if (microphoneReadPosition >= microphoneClip.samples)
            {
                microphoneReadPosition -= microphoneClip.samples;
            }

            availableSamples -= chunkSamples;

            float amplitude = CalculateAmplitude(captureBuffer);
            lastAmplitude = amplitude;

            UpdateLocalMouth(amplitude);

            ConvertFloatSamplesToPcm16(captureBuffer, sendBuffer);

            SendVoicePacket(sendBuffer, sendBuffer.Length);
        }
    }

    private int GetAvailableSamples(int readPosition, int writePosition, int totalSamples)
    {
        if (writePosition >= readPosition)
        {
            return writePosition - readPosition;
        }

        return totalSamples - readPosition + writePosition;
    }

    private void ReadMicrophoneChunk(int startPosition, float[] destination)
    {
        if (microphoneClip == null)
            return;

        int totalSamples = microphoneClip.samples;

        if (startPosition + destination.Length <= totalSamples)
        {
            microphoneClip.GetData(destination, startPosition);
            return;
        }

        int firstPartLength = totalSamples - startPosition;
        int secondPartLength = destination.Length - firstPartLength;

        float[] firstPart = new float[firstPartLength];
        float[] secondPart = new float[secondPartLength];

        microphoneClip.GetData(firstPart, startPosition);
        microphoneClip.GetData(secondPart, 0);

        Array.Copy(firstPart, 0, destination, 0, firstPartLength);
        Array.Copy(secondPart, 0, destination, firstPartLength, secondPartLength);
    }

    private float CalculateAmplitude(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        float sum = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i];
            sum += sample * sample;
        }

        return Mathf.Sqrt(sum / samples.Length);
    }

    private void ConvertFloatSamplesToPcm16(float[] samples, byte[] bytes)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Mathf.Clamp(samples[i], -1f, 1f);
            short value = (short)(clamped * short.MaxValue);

            bytes[i * 2] = (byte)(value & 0xff);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xff);
        }
    }

    private void SendVoicePacket(byte[] pcmData, int length)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (length <= 0)
            return;

        if (length > maxPacketBytes)
        {
            Debug.LogWarning($"🎙 Voice packet skipped. Size={length}, Max={maxPacketBytes}");
            return;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            RelayVoiceToClients(NetworkManager.Singleton.LocalClientId, pcmData, length);
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(
            sizeof(int) + length,
            Allocator.Temp
        );

        writer.WriteValueSafe(length);
        writer.WriteBytesSafe(pcmData, length);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            VoiceToServerMessage,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.UnreliableSequenced
        );
    }

    private void EnsureMessageHandlersRegistered()
    {
        if (handlersRegistered)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.CustomMessagingManager == null)
            return;

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            VoiceToClientsMessage,
            OnVoiceFromServer
        );

        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
                VoiceToServerMessage,
                OnVoiceToServer
            );
        }

        handlersRegistered = true;

        Debug.Log("🎙 Voice message handlers registered");
    }

    private void UnregisterMessageHandlers()
    {
        if (!handlersRegistered)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.CustomMessagingManager == null)
            return;

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(
            VoiceToClientsMessage
        );

        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(
                VoiceToServerMessage
            );
        }

        handlersRegistered = false;
    }

    private void OnVoiceToServer(ulong senderClientId, FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        reader.ReadValueSafe(out int length);

        if (length <= 0 || length > maxPacketBytes)
            return;

        byte[] pcmData = new byte[length];
        reader.ReadBytesSafe(ref pcmData, length);

        RelayVoiceToClients(senderClientId, pcmData, length);
    }

    private void RelayVoiceToClients(ulong speakerClientId, byte[] pcmData, int length)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == speakerClientId)
                continue;

            using FastBufferWriter writer = new FastBufferWriter(
                sizeof(ulong) + sizeof(int) + length,
                Allocator.Temp
            );

            writer.WriteValueSafe(speakerClientId);
            writer.WriteValueSafe(length);
            writer.WriteBytesSafe(pcmData, length);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                VoiceToClientsMessage,
                clientId,
                writer,
                NetworkDelivery.UnreliableSequenced
            );
        }
    }

    private void OnVoiceFromServer(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong speakerClientId);
        reader.ReadValueSafe(out int length);

        if (length <= 0 || length > maxPacketBytes)
            return;

        byte[] pcmData = new byte[length];
        reader.ReadBytesSafe(ref pcmData, length);

        if (NetworkManager.Singleton != null)
        {
            if (speakerClientId == NetworkManager.Singleton.LocalClientId)
                return;
        }

        if (ProximityVoiceSpeaker.TryGetSpeaker(speakerClientId, out ProximityVoiceSpeaker speaker))
        {
            speaker.EnqueuePcm16(pcmData);
        }
    }

    private void UpdateLocalMouth(float amplitude)
    {
        if (NetworkManager.Singleton == null)
            return;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (ProximityVoiceSpeaker.TryGetSpeaker(localClientId, out ProximityVoiceSpeaker speaker))
        {
            speaker.SetLocalAmplitude(amplitude);
        }
    }
}