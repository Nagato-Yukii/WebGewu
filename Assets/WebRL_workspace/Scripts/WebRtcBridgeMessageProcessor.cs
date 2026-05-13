using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WebRtcBridgeMessageProcessor
{
    private readonly WebRtcModelCommandBridge bridge;
    private readonly Func<SceneDirector> sceneDirectorResolver;
    private readonly List<ICommandHandler> commandHandlers = new List<ICommandHandler>();
    private bool handlersInitialized;

    public WebRtcBridgeMessageProcessor(WebRtcModelCommandBridge bridge, Func<SceneDirector> sceneDirectorResolver)
    {
        this.bridge = bridge;
        this.sceneDirectorResolver = sceneDirectorResolver;
    }

    public void ProcessMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        SceneDirector sceneDirector = sceneDirectorResolver != null ? sceneDirectorResolver() : null;
        if (sceneDirector == null)
        {
            Debug.LogWarning("[WebRtcBridge] SceneDirector not found.");
            return;
        }

        EnsureCommandHandlers(sceneDirector);

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

    private void EnsureCommandHandlers(SceneDirector sceneDirector)
    {
        if (handlersInitialized)
        {
            return;
        }

        if (sceneDirector != null)
        {
            commandHandlers.Add(new SceneLoadHandler(sceneDirector));
            commandHandlers.Add(new TrainingFlagHandler(sceneDirector));
        }

        commandHandlers.Add(new LatencyPingHandler(bridge));
        handlersInitialized = true;
        Debug.Log($"[WebRtcBridge] Registered {commandHandlers.Count} envelope handlers.");
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
