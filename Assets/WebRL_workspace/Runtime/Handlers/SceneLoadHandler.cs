using UnityEngine;

public sealed class SceneLoadHandler : ICommandHandler
{
    private readonly SceneDirector _sceneDirector;

    public string CommandType => "scene.load";

    public SceneLoadHandler(SceneDirector sceneDirector)
    {
        _sceneDirector = sceneDirector;
    }

    public void Handle(string json)
    {
        if (_sceneDirector == null)
        {
            Debug.LogWarning("[SceneLoadHandler] SceneDirector is null. scene.load ignored.");
            return;
        }

        var envelope = JsonUtility.FromJson<Envelope<SceneLoadPayload>>(json);
        if (envelope == null || envelope.payload == null || string.IsNullOrWhiteSpace(envelope.payload.scene))
        {
            Debug.LogWarning("[SceneLoadHandler] Invalid scene.load envelope.");
            return;
        }

        Debug.Log(
            $"[SceneLoadHandler] scene.load received: scene='{envelope.payload.scene}', mode='{envelope.payload.mode}', forceReload={envelope.payload.forceReload}.");
        _sceneDirector.LoadSceneByCommandTarget(envelope.payload.scene, envelope.payload.forceReload);
    }
}
