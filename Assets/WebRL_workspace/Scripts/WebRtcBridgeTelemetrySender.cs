using System;
using Unity.WebRTC;
using UnityEngine;

public sealed class WebRtcBridgeTelemetrySender
{
    public void SendLatencyPong(RTCDataChannel channel, string echoId, long webTs, long unityRxTs, int sequence)
    {
        if (channel == null || channel.ReadyState != RTCDataChannelState.Open)
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
            channel.Send(JsonUtility.ToJson(message));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebRtcBridge] Failed to send latency.pong. {ex.Message}");
        }
    }

    public void SendTinkerTelemetry(RTCDataChannel channel, TinkerTrainingTelemetrySnapshot snapshot)
    {
        if (channel == null || channel.ReadyState != RTCDataChannelState.Open)
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
            channel.Send(JsonUtility.ToJson(message));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebRtcBridge] Failed to send Tinker telemetry. {ex.Message}");
        }
    }
}
