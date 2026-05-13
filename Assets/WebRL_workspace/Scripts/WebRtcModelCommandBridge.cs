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
    private readonly List<ICommandHandler> commandHandlers = new List<ICommandHandler>();

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
        EnsureCommandHandlers();
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
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ResolveSceneDirector();
        if (sceneDirector == null)
        {
            Debug.LogWarning("[WebRtcBridge] SceneDirector not found.");
            return;
        }

        EnsureCommandHandlers();
        EnvelopeHeader envelopeHeader = JsonUtility.FromJson<EnvelopeHeader>(message);
        if (envelopeHeader != null && !string.IsNullOrWhiteSpace(envelopeHeader.type))
        {
            Debug.Log($"[WebRtcBridge] Processing envelope '{envelopeHeader.type}' from '{envelopeHeader.source}'.");
            for (int i = 0; i < commandHandlers.Count; i++)
            {
                if (string.Equals(commandHandlers[i].CommandType, envelopeHeader.type, StringComparison.Ordinal))
                {
                    commandHandlers[i].Handle(message);
                    return;
                }
            }

            Debug.LogWarning($"[WebRtcBridge] No handler registered for envelope type '{envelopeHeader.type}'.");
        }

        WebRtcBridgeCommand bridgeCommand = JsonUtility.FromJson<WebRtcBridgeCommand>(message);
        if (bridgeCommand != null && !string.IsNullOrWhiteSpace(bridgeCommand.command))
        {
            Debug.Log($"[WebRtcBridge] Processing legacy command '{bridgeCommand.command}' target='{bridgeCommand.target}'.");
            if (string.Equals(bridgeCommand.command, "loadScene", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(bridgeCommand.target))
                {
                    Debug.LogWarning("[WebRtcBridge] loadScene command is missing a target.");
                    return;
                }

                Debug.Log($"[WebRtcBridge] Processing legacy loadScene target '{bridgeCommand.target}'.");
                sceneDirector.LoadSceneByCommandTarget(bridgeCommand.target);
                return;
            }

            if (string.Equals(bridgeCommand.command, "changeModel", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(bridgeCommand.target))
                {
                    Debug.LogWarning("[WebRtcBridge] changeModel command is missing a target.");
                    return;
                }

                string resolvedSkillType = string.IsNullOrWhiteSpace(bridgeCommand.skillType)
                    ? ResolveFallbackSkill(bridgeCommand.target)
                    : bridgeCommand.skillType;

                if (string.IsNullOrWhiteSpace(resolvedSkillType))
                {
                    Debug.LogWarning($"[WebRtcBridge] Missing skillType for target '{bridgeCommand.target}'.");
                    return;
                }

                WebCommand command = new WebCommand
                {
                    robotName = bridgeCommand.target,
                    skillType = resolvedSkillType
                };
                sceneDirector.ExecuteWebCommand(JsonUtility.ToJson(command));
                return;
            }

            if (string.Equals(bridgeCommand.command, "roboHetuMove", StringComparison.OrdinalIgnoreCase))
            {
                sceneDirector.ApplyRoboHetuWebInput(
                    bridgeCommand.moveX,
                    bridgeCommand.moveY,
                    bridgeCommand.rotate);
                return;
            }

            if (string.Equals(bridgeCommand.command, "roboHetuMode", StringComparison.OrdinalIgnoreCase))
            {
                sceneDirector.ApplyRoboHetuWebMode(bridgeCommand.mode);
                return;
            }

            if (string.Equals(bridgeCommand.command, "tinkerTraining", StringComparison.OrdinalIgnoreCase))
            {
                sceneDirector.ApplyWebTinkerTraining(bridgeCommand.training);
                return;
            }

            if (string.Equals(bridgeCommand.command, "tinkerSetTrainFlag", StringComparison.OrdinalIgnoreCase))
            {
                sceneDirector.ApplyWebTinkerTrainingFlag(bridgeCommand.training);
                return;
            }

            if (string.Equals(bridgeCommand.command, "tinkerLiftAssistCurriculum", StringComparison.OrdinalIgnoreCase))
            {
                sceneDirector.ApplyWebTinkerLiftAssistCurriculum(bridgeCommand.value);
                return;
            }
        }

        WebCommand directCommand = JsonUtility.FromJson<WebCommand>(message);
        if (directCommand != null &&
            !string.IsNullOrWhiteSpace(directCommand.robotName) &&
            !string.IsNullOrWhiteSpace(directCommand.skillType))
        {
            Debug.Log($"[WebRtcBridge] Processing direct WebCommand robot='{directCommand.robotName}' skill='{directCommand.skillType}'.");
            sceneDirector.ExecuteWebCommand(message);
        }
    }

    private void ResolveSceneDirector()
    {
        if (sceneDirector == null)
        {
            sceneDirector = FindObjectOfType<SceneDirector>(true);
        }
    }

    private void EnsureCommandHandlers()
    {
        if (commandHandlers.Count > 0)
        {
            return;
        }

        if (sceneDirector != null)
        {
            commandHandlers.Add(new SceneLoadHandler(sceneDirector));
            commandHandlers.Add(new TrainingFlagHandler(sceneDirector));
        }

        commandHandlers.Add(new LatencyPingHandler(this));
        Debug.Log($"[WebRtcBridge] Registered {commandHandlers.Count} envelope handlers.");
    }

    public void SendLatencyPong(string echoId, long webTs, long unityRxTs, int sequence)
    {
        if (activeChannel == null || activeChannel.ReadyState != RTCDataChannelState.Open)
        {
            return;
        }

        long unityTxTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var message = new Envelope<LatencyPongPayload>
        {
            id = Guid.NewGuid().ToString("N"),
            type = "latency.pong",
            source = "unity",
            ts = unityTxTs,
            payload = new LatencyPongPayload
            {
                echoId = echoId,
                webTs = webTs,
                unityRxTs = unityRxTs,
                unityTxTs = unityTxTs,
                sequence = sequence
            }
        };

        try
        {
            activeChannel.Send(JsonUtility.ToJson(message));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebRtcBridge] Failed to send latency.pong. {ex.Message}");
        }
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
        if (activeChannel == null || activeChannel.ReadyState != RTCDataChannelState.Open)
        {
            return;
        }

        var message = new Envelope<TinkerTelemetryPayload>
        {
            id = Guid.NewGuid().ToString("N"),
            type = "telemetry.tinker",
            source = "unity",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload = new TinkerTelemetryPayload
            {
                trainEnabled = snapshot.trainEnabled,
                cumulativeReward = snapshot.cumulativeReward,
                stepReward = snapshot.stepReward,
                episodeStepCount = snapshot.episodeStepCount,
                totalTrainingStepCount = snapshot.totalTrainingStepCount,
                totalFalls = snapshot.totalFalls,
                totalCoins = snapshot.totalCoins,
                liftAssistCurriculum = snapshot.liftAssistCurriculum,
                currentLiftAssistForce = snapshot.currentLiftAssistForce,
                episodeEnded = snapshot.episodeEnded
            }
        };

        try
        {
            activeChannel.Send(JsonUtility.ToJson(message));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebRtcBridge] Failed to send Tinker telemetry. {ex.Message}");
        }
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

    private static string ResolveFallbackSkill(string robotName)
    {
        switch (robotName)
        {
            case "X02Lite":
            case "OpenLoong":
                return "bipedWalk";
            case "Tron1":
                return "wheelDrive";
            default:
                return string.Empty;
        }
    }
}
