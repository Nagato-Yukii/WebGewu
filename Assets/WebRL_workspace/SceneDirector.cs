using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.MLAgents;
using Unity.MLAgentsExamples;

// 场景调度器
public class SceneDirector : MonoBehaviour
{
    private const string ManagementCameraName = "Camera for management"; // 每一个子场景的被调用摄像头统一取名为camera for management，最优先被当作被推流相机
    private static readonly string[] MenuAliases = { "GlobalManager", "Menu", "Directory" }; // 菜单场景的名称和别名
    private static readonly string[] CameraAnchorNames = { "Main Camera 2", "Main Camera", "Main Camera (1)", "Local Camera" }; // 如果没有Camera for management，按序查找这几个相机推流

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
    private readonly Queue<string> _pendingWebCommands = new Queue<string>();
    private string _pendingSceneName = string.Empty;
    private Camera _currentSceneManagementCamera;
    private DynamicCameraTracker _currentSceneManagementTracker;

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
            SyncBootstrapCameraToTransform(_currentSceneManagementCamera.transform);
            return;
        }

        ResolveGlobalCameraTracker();
        EnsureBootstrapCameraReady();
        ResolveCurrentAgentIfNeeded();

        if (_currentAgent == null || globalCameraTracker == null)
        {
            return;
        }

        var trackingTarget = ResolveTrackingTransform(_currentAgent.gameObject);
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
        if (trainerRunner == null)
        {
            Debug.LogWarning("[SceneDirector] MlAgentsTrainerRunner is not available in the bootstrap scene.");
            return;
        }

        if (shouldTrain)
        {
            if (!CanCurrentProcessConnectTrainer())
            {
                TinkercoinAgent.SetRequestedTrainingMode(false);
                Debug.LogWarning("[SceneDirector] Web Tinker training requires the Unity process to expose an ML-Agents port before Academy initializes. In Player builds, launch Unity with '--mlagents-port <port>' or use a dedicated training worker build.");
                return;
            }

            bool started = trainerRunner.StartTraining();
            if (!started)
            {
                TinkercoinAgent.SetRequestedTrainingMode(false);
                return;
            }

            ResetMlAgentsAcademy("trainer started from web");
            TinkercoinAgent.SetRequestedTrainingMode(true);

            if (_currentLoadedScene == webTinkerSceneName)
            {
                LoadGameplayScene(webTinkerSceneName, true);
            }
        }
        else
        {
            trainerRunner.StopTraining();
            TinkercoinAgent.SetRequestedTrainingMode(false);
            ResetMlAgentsAcademy("trainer stopped from web");

            if (_currentLoadedScene == webTinkerSceneName)
            {
                LoadGameplayScene(webTinkerSceneName, true);
            }
        }
    }

    public void ApplyWebTinkerTrainingFlag(bool shouldTrain)
    {
        Debug.Log($"[SceneDirector] ApplyWebTinkerTrainingFlag called with shouldTrain={shouldTrain}.");

        if (shouldTrain && !CanCurrentProcessConnectTrainer())
        {
            TinkercoinAgent.SetRequestedTrainingMode(false);
            Debug.LogWarning("[SceneDirector] External Web Tinker training requires the Unity process to expose an ML-Agents port before Academy initializes.");
            return;
        }

        TinkercoinAgent.SetRequestedTrainingMode(shouldTrain);
        ResetMlAgentsAcademy(shouldTrain
            ? "external web tinker trainer bootstrap"
            : "external web tinker trainer disabled");

        ResolveCurrentTinkerAgentIfNeeded();
        if (_currentTinkerAgent != null)
        {
            _currentTinkerAgent.SetTrainingEnabled(shouldTrain);
            Debug.Log($"[SceneDirector] Applied training flag to active TinkercoinAgent in scene '{_currentLoadedScene}'.");
        }
        else
        {
            Debug.Log($"[SceneDirector] No active TinkercoinAgent bound yet. Training flag stored for next WebTinkerRL bind.");
        }

        if (_currentLoadedScene == webTinkerSceneName)
        {
            Debug.Log("[SceneDirector] WebTinkerRL is already active. Forcing scene reload so Academy reconnects with the requested training mode.");
            LoadGameplayScene(webTinkerSceneName, true);
        }
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
        if (_transitionRoutine != null && IsMenuTarget(_pendingSceneName))
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

        if (_transitionRoutine != null &&
            !forceReload &&
            string.Equals(_pendingSceneName, sceneName, StringComparison.Ordinal))
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
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return false;
        }

        ResolveCurrentExperimentDirectorIfNeeded();
        if (_currentExperimentDirector == null)
        {
            _pendingWebCommands.Enqueue(jsonString);
            Debug.Log($"[SceneDirector] Queued web command because ExperimentDirector is not available yet. Pending={_pendingWebCommands.Count}, ActiveScene='{_currentLoadedScene}'.");
            if (ShouldAutoRouteQueuedWebCommandsToWebRl())
            {
                EnsureExperimentDirectorSceneLoaded();
            }
            return true;
        }

        _currentExperimentDirector.ExecuteWebCommand(jsonString);
        return true;
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        if (_currentLoadedScene == webTinkerSceneName)
        {
            StopWebTinkerTrainingForSceneTransition();
        }

        ClearBindings();

        if (!string.IsNullOrEmpty(_currentLoadedScene))
        {
            var unloadOperation = SceneManager.UnloadSceneAsync(_currentLoadedScene);
            if (unloadOperation != null)
            {
                yield return unloadOperation;
            }
        }

        _currentLoadedScene = string.Empty;
        var bootstrapScene = SceneManager.GetSceneByName(bootstrapSceneName);
        if (bootstrapScene.IsValid() && bootstrapScene.isLoaded)
        {
            SceneManager.SetActiveScene(bootstrapScene);
        }
        else
        {
            Debug.LogWarning($"[SceneDirector] Bootstrap scene '{bootstrapSceneName}' is not loaded.");
        }
        Debug.Log("[SceneDirector] Returned to GlobalManager menu.");
        _pendingSceneName = string.Empty;
        _transitionRoutine = null;
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (_currentLoadedScene == webTinkerSceneName && sceneName != webTinkerSceneName)
        {
            StopWebTinkerTrainingForSceneTransition();
        }

        ClearBindings();

        if (!string.IsNullOrEmpty(_currentLoadedScene))
        {
            var unloadOperation = SceneManager.UnloadSceneAsync(_currentLoadedScene);
            if (unloadOperation != null)
            {
                yield return unloadOperation;
            }
        }

        var loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            Debug.LogError($"[SceneDirector] Failed to start loading scene '{sceneName}'.");
            _pendingSceneName = string.Empty;
            _transitionRoutine = null;
            yield break;
        }

        yield return loadOperation;

        var loadedScene = SceneManager.GetSceneByName(sceneName);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
        {
            Debug.LogError($"[SceneDirector] Scene '{sceneName}' was not loaded correctly.");
            _pendingSceneName = string.Empty;
            _transitionRoutine = null;
            yield break;
        }

        SceneManager.SetActiveScene(loadedScene);
        _currentLoadedScene = sceneName;
        BindSceneRuntime(loadedScene);
        _pendingSceneName = string.Empty;
        _transitionRoutine = null;
    }

    private void BindSceneRuntime(Scene scene)
    {
        ResolveGlobalCameraTracker();
        EnsureBootstrapCameraReady();

        _currentAgent = FindComponentInScene<G1moeAgent>(scene);
        _currentTinkerAgent = FindComponentInScene<TinkercoinAgent>(scene);
        _currentExperimentDirector = FindComponentInScene<ExperimentDirector>(scene);
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
        _currentSceneManagementCamera = FindManagementCamera(scene);
        _currentSceneManagementTracker = _currentSceneManagementCamera != null
            ? _currentSceneManagementCamera.GetComponent<DynamicCameraTracker>()
            : null;
        var trackingTarget = _currentAgent != null ? ResolveTrackingTransform(_currentAgent.gameObject) : null;
        if (trackingTarget == null && _currentTinkerAgent != null)
        {
            trackingTarget = ResolveTrackingTransform(_currentTinkerAgent.gameObject);
        }
        bool useSceneManagementCamera = _currentSceneManagementCamera != null;

        if (useSceneManagementCamera)
        {
            BindSceneManagementCameraTracking(trackingTarget);
            SyncBootstrapCameraToTransform(_currentSceneManagementCamera.transform);
        }
        else
        {
            AlignGlobalCameraToSceneAnchor(scene, trackingTarget);
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

        FlushPendingWebCommands();
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
            _currentAgent = FindComponentInScene<G1moeAgent>(scene);
        }
    }

    private void ResolveCurrentExperimentDirectorIfNeeded()
    {
        if (_currentExperimentDirector == null && TryGetCurrentLoadedScene(out var scene))
        {
            _currentExperimentDirector = FindComponentInScene<ExperimentDirector>(scene);
            ApplyCameraTrackerToExperimentDirector();
        }
    }

    private void ResolveCurrentTinkerAgentIfNeeded()
    {
        if (_currentTinkerAgent == null && TryGetCurrentLoadedScene(out var scene))
        {
            _currentTinkerAgent = FindComponentInScene<TinkercoinAgent>(scene);
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

    private void FlushPendingWebCommands()
    {
        if (_currentExperimentDirector == null || _pendingWebCommands.Count == 0)
        {
            return;
        }

        while (_pendingWebCommands.Count > 0)
        {
            string commandJson = _pendingWebCommands.Dequeue();
            if (string.IsNullOrWhiteSpace(commandJson))
            {
                continue;
            }

            _currentExperimentDirector.ExecuteWebCommand(commandJson);
        }

        Debug.Log("[SceneDirector] Flushed queued web commands after ExperimentDirector became available.");
    }

    private void ClearPendingWebCommands(string reason)
    {
        if (_pendingWebCommands.Count == 0)
        {
            return;
        }

        int clearedCount = _pendingWebCommands.Count;
        _pendingWebCommands.Clear();
        Debug.Log($"[SceneDirector] Cleared {clearedCount} queued web command(s) while {reason}.");
    }

    private bool ShouldAutoRouteQueuedWebCommandsToWebRl()
    {
        return string.IsNullOrEmpty(_currentLoadedScene) ||
               IsMenuTarget(_currentLoadedScene) ||
               string.Equals(_currentLoadedScene, bootstrapSceneName, StringComparison.Ordinal);
    }

    private void EnsureExperimentDirectorSceneLoaded()
    {
        bool needsWebRlScene =
            string.IsNullOrEmpty(_currentLoadedScene) ||
            IsMenuTarget(_currentLoadedScene) ||
            !string.Equals(_currentLoadedScene, webRlSceneName, StringComparison.Ordinal);

        if (!needsWebRlScene)
        {
            return;
        }

        if (_transitionRoutine != null)
        {
            Debug.Log($"[SceneDirector] Waiting for active scene transition before replaying queued web commands. Current='{_currentLoadedScene}', Target='{webRlSceneName}'.");
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
        TinkercoinAgent.SetRequestedTrainingMode(false);
        ResolveTrainerRunner();
        if (trainerRunner != null)
        {
            trainerRunner.StopTraining();
        }
        ResetMlAgentsAcademy("web tinker scene transition");
    }

    private static bool CanCurrentProcessConnectTrainer()
    {
        if (Application.isEditor)
        {
            return true;
        }

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--mlagents-port", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetMlAgentsAcademy(string reason)
    {
        if (!Academy.IsInitialized)
        {
            return;
        }

        try
        {
            Academy.Instance.Dispose();
            Debug.Log($"[SceneDirector] Reset ML-Agents Academy so the communicator can be reinitialized after {reason}.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SceneDirector] Failed to reset ML-Agents Academy after {reason}. {ex.Message}");
        }
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

    private bool IsMenuTarget(string sceneTarget)
    {
        if (string.IsNullOrWhiteSpace(sceneTarget))
        {
            return false;
        }

        var trimmed = sceneTarget.Trim();
        for (int i = 0; i < MenuAliases.Length; i++)
        {
            if (trimmed == MenuAliases[i])
            {
                return true;
            }
        }

        return false;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        var components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component != null && component.gameObject.scene == scene)
            {
                return component;
            }
        }

        return null;
    }

    private void AlignGlobalCameraToSceneAnchor(Scene scene, Transform trackingTarget)
    {
        if (globalCameraTracker == null)
        {
            return;
        }

        var anchorCamera = FindSceneCameraAnchor(scene);
        if (anchorCamera != null)
        {
            globalCameraTracker.transform.SetPositionAndRotation(
                anchorCamera.transform.position,
                anchorCamera.transform.rotation);
            return;
        }

        if (trackingTarget == null)
        {
            return;
        }

        var desiredPosition = trackingTarget.TransformPoint(new Vector3(-2.2f, 1.6f, -4.2f));
        globalCameraTracker.transform.position = desiredPosition;
        globalCameraTracker.transform.rotation = Quaternion.LookRotation(
            (trackingTarget.position - desiredPosition).normalized,
            Vector3.up);
    }

    private static Camera FindSceneCameraAnchor(Scene scene)
    {
        var cameras = FindObjectsOfType<Camera>(true);
        for (int nameIndex = 0; nameIndex < CameraAnchorNames.Length; nameIndex++)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera == null || camera.gameObject.scene != scene)
                {
                    continue;
                }

                if (camera.gameObject.name == "StreamSender Camera")
                {
                    continue;
                }

                if (camera.gameObject.name == CameraAnchorNames[nameIndex])
                {
                    return camera;
                }
            }
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            var camera = cameras[i];
            if (camera != null && camera.gameObject.scene == scene && camera.gameObject.name != "StreamSender Camera")
            {
                return camera;
            }
        }

        return null;
    }

    private static Camera FindManagementCamera(Scene scene)
    {
        var cameras = FindObjectsOfType<Camera>(true);
        for (int pass = 0; pass < 2; pass++)
        {
            bool activeOnly = pass == 0;
            for (int i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera == null || camera.gameObject.scene != scene)
                {
                    continue;
                }

                if (camera.gameObject.name != ManagementCameraName)
                {
                    continue;
                }

                if (activeOnly && !camera.gameObject.activeInHierarchy)
                {
                    continue;
                }

                return camera;
            }
        }

        return null;
    }

    private void BindSceneManagementCameraTracking(Transform trackingTarget)
    {
        if (_currentSceneManagementCamera == null || trackingTarget == null)
        {
            return;
        }

        var tracker = _currentSceneManagementCamera.GetComponent<DynamicCameraTracker>();
        if (tracker != null)
        {
            tracker.enabled = true;
            if (tracker.target != trackingTarget)
            {
                tracker.SetTarget(trackingTarget, true);
            }
            return;
        }

        var cameraFollow = _currentSceneManagementCamera.GetComponent<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = trackingTarget;
            cameraFollow.enabled = true;
        }
    }

    private static Transform ResolveTrackingTransform(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return null;
        }

        ArticulationBody[] bodies = rootObject.GetComponentsInChildren<ArticulationBody>(true);
        ArticulationBody firstBody = null;
        for (int i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            if (body == null)
            {
                continue;
            }

            if (firstBody == null)
            {
                firstBody = body;
            }

            if (body.isRoot)
            {
                return body.transform;
            }
        }

        return firstBody != null ? firstBody.transform : rootObject.transform;
    }

    private void SyncBootstrapCameraToTransform(Transform sourceTransform)
    {
        if (globalCameraTracker == null || sourceTransform == null)
        {
            return;
        }

        Transform cameraTransform = globalCameraTracker.transform;
        if (cameraTransform.parent != null)
        {
            cameraTransform.SetParent(null, true);
        }

        cameraTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
    }

}
