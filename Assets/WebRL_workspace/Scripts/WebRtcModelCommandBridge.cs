using System;
using System.Collections.Generic;
using System.Text;
using Unity.RenderStreaming;
using Unity.WebRTC;
using UnityEngine;

[Serializable]
public class WebRtcBridgeCommand
{
    public string command;
    public string target;
    public string skillType;
    public float moveX;
    public float moveY;
    public float rotate;
    public int mode;
    public bool training;
    public float value;
}

[Serializable]
public class WebRtcBridgeTelemetryMessage
{
    public string command;
    public bool trainEnabled;
    public float cumulativeReward;
    public float stepReward;
    public int episodeStepCount;
    public int totalTrainingStepCount;
    public int totalFalls;
    public int totalCoins;
    public float liftAssistCurriculum;
    public float currentLiftAssistForce;
    public bool episodeEnded;
}

public class WebRtcModelCommandBridge : MonoBehaviour, IAddChannelHandler, IDisconnectHandler, IDeletedConnectionHandler
{
    [SerializeField] private SceneDirector sceneDirector;
    [SerializeField] private string dataChannelLabel = "input";

    private RTCDataChannel activeChannel;
    private readonly Queue<string> pendingMessages = new Queue<string>();
    private readonly WebRtcBridgeTelemetrySender telemetrySender = new WebRtcBridgeTelemetrySender();
    private WebRtcBridgeMessageProcessor messageProcessor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToStreamingHandlers()
    {
        TryAttach<Broadcast>();
        TryAttach<SingleConnection>();
    }

    private static void TryAttach<T>() where T : Component
    {
        T handler = FindObjectOfType<T>();
        if (handler == null || handler.GetComponent<WebRtcModelCommandBridge>() != null)
        {
            return;
        }

        handler.gameObject.AddComponent<WebRtcModelCommandBridge>();
    }

    private void Awake()
    {
        ResolveSceneDirector();
        messageProcessor = new WebRtcBridgeMessageProcessor(this, ResolveSceneDirectorInstance);
        TinkercoinAgent.TelemetryUpdated += HandleTinkerTelemetryUpdated;
    }

    private void OnDestroy()
    {
        TinkercoinAgent.TelemetryUpdated -= HandleTinkerTelemetryUpdated;
        DetachChannel();
    }

    private void Update()
    {
        while (true)
        {
            string message = null;
            lock (pendingMessages)
            {
                if (pendingMessages.Count == 0)
                {
                    break;
                }

                message = pendingMessages.Dequeue();
            }

            ProcessMessage(message);
        }
    }

    public void OnAddChannel(SignalingEventData eventData)
    {
        RTCDataChannel channel = eventData.channel;
        if (channel == null || !string.Equals(channel.Label, dataChannelLabel, StringComparison.Ordinal))
        {
            return;
        }

        DetachChannel();
        activeChannel = channel;
        activeChannel.OnMessage += HandleChannelMessage;
        Debug.Log($"[WebRtcBridge] Bound DataChannel '{activeChannel.Label}' on {eventData.connectionId}.");
    }

    public void OnDisconnect(SignalingEventData eventData)
    {
        DetachChannel();
    }

    public void OnDeletedConnection(SignalingEventData eventData)
    {
        DetachChannel();
    }

    private void HandleChannelMessage(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return;
        }

        string message = Encoding.UTF8.GetString(bytes);
        if (!LooksLikeJson(message))
        {
            return;
        }

        lock (pendingMessages)
        {
            pendingMessages.Enqueue(message);
        }

        Debug.Log($"[WebRtcBridge] Queued inbound data-channel message ({bytes.Length} bytes).");
        Debug.Log($"[WebRtcBridge] Raw inbound message: {message}");
    }

    private void ProcessMessage(string message)
    {
        if (messageProcessor == null)
        {
            messageProcessor = new WebRtcBridgeMessageProcessor(this, ResolveSceneDirectorInstance);
        }

        messageProcessor.ProcessMessage(message);
    }

    private void ResolveSceneDirector()
    {
        if (sceneDirector == null)
        {
            sceneDirector = FindObjectOfType<SceneDirector>(true);
        }
    }

    private SceneDirector ResolveSceneDirectorInstance()
    {
        ResolveSceneDirector();
        return sceneDirector;
    }

    public void SendLatencyPong(string echoId, long webTs, long unityRxTs, int sequence)
    {
        telemetrySender.SendLatencyPong(activeChannel, echoId, webTs, unityRxTs, sequence);
    }

    private void DetachChannel()
    {
        if (activeChannel == null)
        {
            return;
        }

        activeChannel.OnMessage -= HandleChannelMessage;
        activeChannel = null;
    }

    private void HandleTinkerTelemetryUpdated(TinkerTrainingTelemetrySnapshot snapshot)
    {
        telemetrySender.SendTinkerTelemetry(activeChannel, snapshot);
    }

    private static bool LooksLikeJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                return value[i] == '{';
            }
        }

        return false;
    }
}
