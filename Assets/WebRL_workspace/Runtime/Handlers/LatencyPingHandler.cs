using System;
using UnityEngine;

public sealed class LatencyPingHandler : ICommandHandler
{
    private readonly WebRtcModelCommandBridge _bridge;

    public string CommandType => "latency.ping";

    public LatencyPingHandler(WebRtcModelCommandBridge bridge)
    {
        _bridge = bridge;
    }

    public void Handle(string json)
    {
        if (_bridge == null)
        {
            Debug.LogWarning("[LatencyPingHandler] Bridge is null. latency.ping ignored.");
            return;
        }

        var envelope = JsonUtility.FromJson<Envelope<LatencyPingPayload>>(json);
        if (envelope == null)
        {
            Debug.LogWarning("[LatencyPingHandler] Invalid latency.ping envelope.");
            return;
        }

        long unityRxTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int sequence = envelope.payload != null ? envelope.payload.sequence : 0;
        _bridge.SendLatencyPong(envelope.id, envelope.ts, unityRxTs, sequence);
    }
}