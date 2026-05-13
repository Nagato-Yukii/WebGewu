using System;

public sealed class SceneDirectorSceneRouter
{
    private readonly string bootstrapSceneName;
    private readonly string webRlSceneName;
    private readonly string roboHetuSceneName;
    private readonly string webTinkerSceneName;
    private readonly string[] menuAliases;

    public SceneDirectorSceneRouter(
        string bootstrapSceneName,
        string webRlSceneName,
        string roboHetuSceneName,
        string webTinkerSceneName,
        string[] menuAliases)
    {
        this.bootstrapSceneName = bootstrapSceneName;
        this.webRlSceneName = webRlSceneName;
        this.roboHetuSceneName = roboHetuSceneName;
        this.webTinkerSceneName = webTinkerSceneName;
        this.menuAliases = menuAliases ?? Array.Empty<string>();
    }

    public bool IsMenuTarget(string sceneTarget)
    {
        if (string.IsNullOrWhiteSpace(sceneTarget))
        {
            return false;
        }

        string trimmed = sceneTarget.Trim();
        for (int i = 0; i < menuAliases.Length; i++)
        {
            if (trimmed == menuAliases[i])
            {
                return true;
            }
        }

        return false;
    }

    public bool TryResolveSceneName(string sceneTarget, out string sceneName)
    {
        sceneName = string.Empty;
        if (string.IsNullOrWhiteSpace(sceneTarget))
        {
            return false;
        }

        switch (sceneTarget.Trim())
        {
            case "WebRL_Laboratory":
            case "WebRLLaboratory":
            case "WebRL":
                sceneName = webRlSceneName;
                return true;
            case "RoboHeTu":
            case "RobotHeTu":
            case "RobotHeTuRender":
                sceneName = roboHetuSceneName;
                return true;
            case "WebTinkerRL":
            case "WebTinker":
            case "TinkerRL":
            case "Tinker":
                sceneName = webTinkerSceneName;
                return true;
            default:
                sceneName = sceneTarget.Trim();
                return true;
        }
    }

    public bool ShouldAutoRouteQueuedWebCommandsToWebRl(string currentLoadedScene)
    {
        return string.IsNullOrEmpty(currentLoadedScene) ||
               IsMenuTarget(currentLoadedScene) ||
               string.Equals(currentLoadedScene, bootstrapSceneName, StringComparison.Ordinal);
    }

    public bool NeedsExperimentDirectorWebRlScene(string currentLoadedScene)
    {
        return string.IsNullOrEmpty(currentLoadedScene) ||
               IsMenuTarget(currentLoadedScene) ||
               !string.Equals(currentLoadedScene, webRlSceneName, StringComparison.Ordinal);
    }

    public bool IsWebTinkerScene(string currentLoadedScene)
    {
        return string.Equals(currentLoadedScene, webTinkerSceneName, StringComparison.Ordinal);
    }
}
