using System;

[Serializable]
public class EnvelopeHeader
{
    public int v = 1;
    public string id;
    public string type;
    public string source;
    public long ts;
}

[Serializable]
public class Envelope<TPayload>
{
    public int v = 1;
    public string id;
    public string type;
    public string source;
    public long ts;
    public TPayload payload;
}

[Serializable]
public class SceneLoadPayload
{
    public string scene;
    public string mode;
    public bool forceReload;
}

[Serializable]
public class TrainingSetFlagPayload
{
    public bool enabled;
    public string source;
}

[Serializable]
public class LatencyPingPayload
{
    public int sequence;
}

[Serializable]
public class LatencyPongPayload
{
    public string echoId;
    public long webTs;
    public long unityRxTs;
    public long unityTxTs;
    public int sequence;
}

[Serializable]
public class TinkerTelemetryPayload
{
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
