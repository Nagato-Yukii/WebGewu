using UnityEngine;

public sealed class TrainingFlagHandler : ICommandHandler
{
    private readonly SceneDirector _sceneDirector;

    public string CommandType => "training.set_flag";

    public TrainingFlagHandler(SceneDirector sceneDirector)
    {
        _sceneDirector = sceneDirector;
    }

    public void Handle(string json)
    {
        if (_sceneDirector == null)
        {
            Debug.LogWarning("[TrainingFlagHandler] SceneDirector is null. training.set_flag ignored.");
            return;
        }

        var envelope = JsonUtility.FromJson<Envelope<TrainingSetFlagPayload>>(json);
        if (envelope == null || envelope.payload == null)
        {
            Debug.LogWarning("[TrainingFlagHandler] Invalid training.set_flag envelope.");
            return;
        }

        Debug.Log(
            $"[TrainingFlagHandler] training.set_flag received: enabled={envelope.payload.enabled}, source='{envelope.payload.source}'.");
        _sceneDirector.ApplyWebTinkerTrainingFlag(envelope.payload.enabled);
    }
}
