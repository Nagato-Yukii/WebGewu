using System;
using System.Collections;
using Unity.MLAgentsExamples;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneDirector : MonoBehaviour
{
    private const string ManagementCameraName = "Camera for management";
    private static readonly string[] MenuAliases = { "GlobalManager", "Menu", "Directory" };
    private static readonly string[] CameraAnchorNames = { "Main Camera 2", "Main Camera", "Main Camera (1)", "Local Camera" };

    [SerializeField] private string bootstrapSceneName = "GlobalManager";
    [SerializeField] private string webRlSceneName = "WebRL_Laboratory";
    [SerializeField] private string roboHetuSceneName = "RobotHeTuRender";
    [SerializeField] private string webTinkerSceneName = "WebTinkerRL";
    [SerializeField] private DynamicCameraTracker globalCameraTracker;
    [SerializeField] private MlAgentsTrainerRunner trainerRunner;

    private Camera _bootstrapCamera;
    private string _currentLoadedScene = string.Empty;
    private Coroutine _transitionRoutine;
    private G1moeAgent _currentAgent;
    private TinkercoinAgent _currentTinkerAgent;
    private ExperimentDirector _currentExperimentDirector;
    private string _pendingSceneName = string.Empty;
    private Camera _currentSceneManagementCamera;
    private DynamicCameraTracker _currentSceneManagementTracker;

    private SceneDirectorSceneRouter _sceneRouter;
    private SceneDirectorCommandQueueManager _commandQueueManager;
    private SceneDirectorTrainingCoordinator _trainingCoordinator;
    private SceneDirectorCameraBinder _cameraBinder;
    private SceneDirectorSceneSwitcher _sceneSwitcher;

    public string CurrentLoadedScene => _currentLoadedScene;
    public DynamicCameraTracker GlobalCameraTracker => globalCameraTracker;

    private void Awake()
    {
        var directors = FindObjectsOfType<SceneDirector>(true);
        for (int i = 0; i < directors.Length; i++)
        {
            if (directors[i] != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(bootstrapSceneName))
        {
            bootstrapSceneName = SceneManager.GetActiveScene().name;
        }

        InitializeCoreServices();

        DontDestroyOnLoad(gameObject);
        ResolveGlobalCameraTracker();
        ResolveBootstrapCamera();
        EnsureBootstrapCameraReady();
    }

    private void LateUpdate()
    {
        if (string.IsNullOrEmpty(_currentLoadedScene))
        {
            return;
        }

        if (_currentSceneManagementCamera != null)
        {
            EnsureBootstrapCameraReady();
            _cameraBinder.SyncBootstrapCameraToTransform(globalCameraTracker, _currentSceneManagementCamera.transform);
            return;
        }

        ResolveGlobalCameraTracker();
        EnsureBootstrapCameraReady();
        ResolveCurrentAgentIfNeeded();

        if (_currentAgent == null || globalCameraTracker == null)
        {
            return;
        }

        var trackingTarget = _cameraBinder.ResolveTrackingTransform(_currentAgent.gameObject);
        if (trackingTarget != null && globalCameraTracker.target != trackingTarget)
        {
            globalCameraTracker.SetTarget(trackingTarget, true);
        }
    }

    public void ForwardWebMove(InputAction.CallbackContext context)
    {
        ResolveCurrentAgentIfNeeded();
        if (_currentAgent != null)
        {
            _currentAgent.OnWebMove(context);
        }
    }

    public void ForwardWebRotate(InputAction.CallbackContext context)
    {
        ResolveCurrentAgentIfNeeded();
        if (_currentAgent != null)
        {
            _currentAgent.OnWebRotate(context);
        }
    }

    public void ForwardWebSwitchMode(int mode)
    {
        ResolveCurrentAgentIfNeeded();
        if (_currentAgent != null)
        {
            _currentAgent.OnWebSwitchMode(mode);
        }
    }

    public void ApplyRoboHetuWebInput(float moveX, float moveY, float rotate)
    {
        ResolveCurrentAgentIfNeeded();
        if (_currentAgent == null)
        {
            return;
        }

        _currentAgent.SetWebMoveInput(new Vector2(moveX, moveY));
        _currentAgent.SetWebRotateInput(rotate);
    }

    public void ApplyRoboHetuWebMode(int mode)
    {
        ResolveCurrentAgentIfNeeded();
        if (_currentAgent == null)
        {
            return;
        }

        _currentAgent.SetWebMode(mode);
    }

    public void ApplyWebTinkerTraining(bool shouldTrain)
    {
        ResolveTrainerRunner();
        _trainingCoordinator.ApplyWebTinkerTraining(
            shouldTrain,
            trainerRunner,
            _currentLoadedScene,
            webTinkerSceneName,
            LoadGameplayScene);
    }

    public void ApplyWebTinkerTrainingFlag(bool shouldTrain)
    {
        ResolveCurrentTinkerAgentIfNeeded();
        _trainingCoordinator.ApplyWebTinkerTrainingFlag(
            shouldTrain,
            _currentTinkerAgent,
            _currentLoadedScene,
            webTinkerSceneName,
            LoadGameplayScene);
    }

    public void ApplyWebTinkerLiftAssistCurriculum(float value)
    {
        ResolveCurrentTinkerAgentIfNeeded();
        if (_currentTinkerAgent == null)
        {
            Debug.LogWarning("[SceneDirector] TinkercoinAgent is not available in the active gameplay scene.");
            return;
        }

        _currentTinkerAgent.SetLiftAssistCurriculumFromWeb(value);
    }

    public void ExecuteDimensionalJump(string sceneTarget)
    {
        LoadSceneByCommandTarget(sceneTarget);
    }

    public void LoadSceneByCommandTarget(string sceneTarget)
    {
        LoadSceneByCommandTarget(sceneTarget, false);
    }

    public void LoadSceneByCommandTarget(string sceneTarget, bool forceReload)
    {
        if (IsMenuTarget(sceneTarget))
        {
            ClearPendingWebCommands("returning to menu");
            Debug.Log($"[SceneDirector] Received menu target '{sceneTarget}'. Returning to bootstrap scene.");
            ReturnToMenu();
            return;
        }

        if (!TryResolveSceneName(sceneTarget, out var sceneName))
        {
            Debug.LogWarning($"[SceneDirector] Unknown scene target '{sceneTarget}'.");
            return;
        }

        if (!string.Equals(sceneName, webRlSceneName, StringComparison.Ordinal))
        {
            ClearPendingWebCommands($"switching to scene '{sceneName}'");
        }

        Debug.Log(
            $"[SceneDirector] Resolved scene target '{sceneTarget}' to runtime scene '{sceneName}' (forceReload={forceReload}).");
        LoadGameplayScene(sceneName, forceReload);
    }

    public void ReturnToMenu()
    {
        if (_sceneSwitcher.IsDuplicateMenuTransition(_transitionRoutine, _pendingSceneName, IsMenuTarget))
        {
            Debug.Log($"[SceneDirector] Ignoring duplicate menu transition while '{_pendingSceneName}' is already loading.");
            return;
        }

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        _pendingSceneName = bootstrapSceneName;
        _transitionRoutine = StartCoroutine(ReturnToMenuRoutine());
    }

    public void LoadGameplayScene(string sceneName, bool forceReload = false)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (_sceneSwitcher.IsDuplicateSceneLoadRequest(_transitionRoutine, forceReload, _pendingSceneName, sceneName))
        {
            Debug.Log($"[SceneDirector] Ignoring duplicate scene load request for '{sceneName}' while it is already loading.");
            return;
        }

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        if (!forceReload && _currentLoadedScene == sceneName)
        {
            var loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                BindSceneRuntime(loadedScene);
            }

            return;
        }

        _pendingSceneName = sceneName;
        _transitionRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    public bool ExecuteWebCommand(string jsonString)
    {
        ResolveCurrentExperimentDirectorIfNeeded();
        return _commandQueueManager.TryExecuteOrQueue(
            jsonString,
            _currentExperimentDirector,
            _currentLoadedScene,
            () => _sceneRouter.ShouldAutoRouteQueuedWebCommandsToWebRl(_currentLoadedScene),
            EnsureExperimentDirectorSceneLoaded);
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        yield return _sceneSwitcher.ReturnToMenuRoutine(
            _currentLoadedScene,
            webTinkerSceneName,
            StopWebTinkerTrainingForSceneTransition,
            ClearBindings,
            value => _currentLoadedScene = value,
            bootstrapSceneName,
            value => _pendingSceneName = value,
            () => _transitionRoutine = null);
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return _sceneSwitcher.LoadSceneRoutine(
            _currentLoadedScene,
            webTinkerSceneName,
            sceneName,
            StopWebTinkerTrainingForSceneTransition,
            ClearBindings,
            value => _currentLoadedScene = value,
            BindSceneRuntime,
            value => _pendingSceneName = value,
            () => _transitionRoutine = null);
    }

    private void BindSceneRuntime(Scene scene)
    {
        ResolveGlobalCameraTracker();
        EnsureBootstrapCameraReady();

        _currentAgent = _cameraBinder.FindComponentInScene<G1moeAgent>(scene);
        _currentTinkerAgent = _cameraBinder.FindComponentInScene<TinkercoinAgent>(scene);
        _currentExperimentDirector = _cameraBinder.FindComponentInScene<ExperimentDirector>(scene);

        ResolveTrainerRunner();
        if (_currentTinkerAgent != null)
        {
            bool trainEnabled = trainerRunner != null && trainerRunner.IsTrainingRunning;
            if (TinkercoinAgent.TryGetRequestedTrainingMode(out var requestedTrainingMode))
            {
                trainEnabled = requestedTrainingMode;
            }

            _currentTinkerAgent.SetTrainingEnabled(trainEnabled);
        }

        _currentSceneManagementCamera = _cameraBinder.FindManagementCamera(scene, ManagementCameraName);
        _currentSceneManagementTracker = _currentSceneManagementCamera != null
            ? _currentSceneManagementCamera.GetComponent<DynamicCameraTracker>()
            : null;

        var trackingTarget = _currentAgent != null ? _cameraBinder.ResolveTrackingTransform(_currentAgent.gameObject) : null;
        if (trackingTarget == null && _currentTinkerAgent != null)
        {
            trackingTarget = _cameraBinder.ResolveTrackingTransform(_currentTinkerAgent.gameObject);
        }

        bool useSceneManagementCamera = _currentSceneManagementCamera != null;
        if (useSceneManagementCamera)
        {
            _cameraBinder.BindSceneManagementCameraTracking(_currentSceneManagementCamera, trackingTarget);
            _cameraBinder.SyncBootstrapCameraToTransform(globalCameraTracker, _currentSceneManagementCamera.transform);
        }
        else
        {
            _cameraBinder.AlignGlobalCameraToSceneAnchor(globalCameraTracker, scene, trackingTarget, CameraAnchorNames);
        }

        if (globalCameraTracker != null)
        {
            globalCameraTracker.enabled = !useSceneManagementCamera;
        }

        ApplyCameraTrackerToExperimentDirector();

        if (!useSceneManagementCamera && trackingTarget != null && globalCameraTracker != null)
        {
            globalCameraTracker.SetTarget(trackingTarget, true);
        }
        else if (!useSceneManagementCamera && _currentExperimentDirector == null && globalCameraTracker != null)
        {
            globalCameraTracker.SetTarget(null, true);
        }

        if (useSceneManagementCamera && globalCameraTracker != null)
        {
            Debug.Log(
                $"[SceneDirector] Bound bootstrap stream camera '{globalCameraTracker.name}' to management camera '{_currentSceneManagementCamera.name}' in scene '{scene.name}'.");
        }

        _commandQueueManager.FlushTo(_currentExperimentDirector);
        Debug.Log($"[SceneDirector] Active gameplay scene: {_currentLoadedScene}");
    }

    private void ClearBindings()
    {
        if (_currentAgent != null)
        {
            _currentAgent.ClearWebInput();
        }

        ResolveGlobalCameraTracker();
        EnsureBootstrapCameraReady();
        if (globalCameraTracker != null)
        {
            globalCameraTracker.enabled = true;
            globalCameraTracker.SetTarget(null, true);
        }

        _currentAgent = null;
        _currentTinkerAgent = null;
        _currentExperimentDirector = null;
        _currentSceneManagementCamera = null;
        _currentSceneManagementTracker = null;
    }

    private void ResolveCurrentAgentIfNeeded()
    {
        if (_currentAgent == null && TryGetCurrentLoadedScene(out var scene))
        {
            _currentAgent = _cameraBinder.FindComponentInScene<G1moeAgent>(scene);
        }
    }

    private void ResolveCurrentExperimentDirectorIfNeeded()
    {
        if (_currentExperimentDirector == null && TryGetCurrentLoadedScene(out var scene))
        {
            _currentExperimentDirector = _cameraBinder.FindComponentInScene<ExperimentDirector>(scene);
            ApplyCameraTrackerToExperimentDirector();
        }
    }

    private void ResolveCurrentTinkerAgentIfNeeded()
    {
        if (_currentTinkerAgent == null && TryGetCurrentLoadedScene(out var scene))
        {
            _currentTinkerAgent = _cameraBinder.FindComponentInScene<TinkercoinAgent>(scene);
        }
    }

    private bool TryGetCurrentLoadedScene(out Scene scene)
    {
        scene = default;
        if (string.IsNullOrEmpty(_currentLoadedScene))
        {
            return false;
        }

        scene = SceneManager.GetSceneByName(_currentLoadedScene);
        return scene.IsValid() && scene.isLoaded;
    }

    private void ApplyCameraTrackerToExperimentDirector()
    {
        if (_currentExperimentDirector == null)
        {
            return;
        }

        ResolveGlobalCameraTracker();
        _currentExperimentDirector.SetCameraTracker(
            _currentSceneManagementTracker != null ? _currentSceneManagementTracker : globalCameraTracker);
    }

    private void ClearPendingWebCommands(string reason)
    {
        _commandQueueManager.Clear(reason);
    }

    private void EnsureExperimentDirectorSceneLoaded()
    {
        bool needsWebRlScene = _sceneRouter.NeedsExperimentDirectorWebRlScene(_currentLoadedScene);
        if (!needsWebRlScene)
        {
            return;
        }

        if (_transitionRoutine != null)
        {
            Debug.Log(
                $"[SceneDirector] Waiting for active scene transition before replaying queued web commands. Current='{_currentLoadedScene}', Target='{webRlSceneName}'.");
            return;
        }

        Debug.Log($"[SceneDirector] Auto-loading '{webRlSceneName}' so queued web commands can bind to ExperimentDirector.");
        LoadGameplayScene(webRlSceneName, false);
    }

    private void ResolveGlobalCameraTracker()
    {
        if (globalCameraTracker == null)
        {
            globalCameraTracker = GetComponent<DynamicCameraTracker>();
        }

        if (globalCameraTracker == null)
        {
            var trackers = FindObjectsOfType<DynamicCameraTracker>(true);
            for (int i = 0; i < trackers.Length; i++)
            {
                var tracker = trackers[i];
                if (tracker == null)
                {
                    continue;
                }

                if (tracker.gameObject == gameObject || tracker.transform.root == transform.root)
                {
                    globalCameraTracker = tracker;
                    break;
                }

                if (tracker.gameObject.scene.name == bootstrapSceneName)
                {
                    globalCameraTracker = tracker;
                    break;
                }
            }
        }
    }

    private void ResolveBootstrapCamera()
    {
        if (_bootstrapCamera == null)
        {
            _bootstrapCamera = GetComponent<Camera>();
        }
    }

    private void ResolveTrainerRunner()
    {
        if (trainerRunner == null)
        {
            trainerRunner = GetComponent<MlAgentsTrainerRunner>();
        }

        if (trainerRunner == null)
        {
            trainerRunner = FindObjectOfType<MlAgentsTrainerRunner>(true);
        }

        if (trainerRunner == null)
        {
            trainerRunner = gameObject.AddComponent<MlAgentsTrainerRunner>();
        }
    }

    private void StopWebTinkerTrainingForSceneTransition()
    {
        ResolveTrainerRunner();
        _trainingCoordinator.StopWebTinkerTrainingForSceneTransition(trainerRunner);
    }

    private void EnsureBootstrapCameraReady()
    {
        ResolveBootstrapCamera();
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (_bootstrapCamera != null && !_bootstrapCamera.enabled)
        {
            _bootstrapCamera.enabled = true;
        }
    }

    private bool TryResolveSceneName(string sceneTarget, out string sceneName)
    {
        return _sceneRouter.TryResolveSceneName(sceneTarget, out sceneName);
    }

    private bool IsMenuTarget(string sceneTarget)
    {
        return _sceneRouter.IsMenuTarget(sceneTarget);
    }

    private void InitializeCoreServices()
    {
        _sceneRouter = new SceneDirectorSceneRouter(
            bootstrapSceneName,
            webRlSceneName,
            roboHetuSceneName,
            webTinkerSceneName,
            MenuAliases);
        _commandQueueManager = new SceneDirectorCommandQueueManager();
        _trainingCoordinator = new SceneDirectorTrainingCoordinator();
        _cameraBinder = new SceneDirectorCameraBinder();
        _sceneSwitcher = new SceneDirectorSceneSwitcher();
    }
}
