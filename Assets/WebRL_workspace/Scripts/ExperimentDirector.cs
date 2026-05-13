using UnityEngine;

[System.Serializable]
public class WebCommand
{
    public string robotName;
    public string skillType;
}

public class ExperimentDirector : MonoBehaviour
{
    private GameObject currentRobotInstance;
    [SerializeField] private DynamicCameraTracker cameraTracker;
    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool usePrefabAuthoredTransform = true;
    [SerializeField] private Vector3 fallbackSpawnPosition = Vector3.zero;
    [SerializeField] private Vector3 fallbackSpawnEuler = Vector3.zero;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private float runSpawnLiftY = 0.04f;
    [SerializeField] private float jumpSpawnLiftY = 0.12f;

    private ExperimentDirectorCommandParser _commandParser;
    private ExperimentDirectorResourceLoader _resourceLoader;
    private ExperimentDirectorSkillResolver _skillResolver;
    private ExperimentDirectorSpawnPoseCalculator _spawnPoseCalculator;
    private ExperimentDirectorCameraTrackerResolver _cameraTrackerResolver;
    private ExperimentDirectorRuntimeAgentAssembler _runtimeAgentAssembler;

    private void Awake()
    {
        _commandParser = new ExperimentDirectorCommandParser();
        _resourceLoader = new ExperimentDirectorResourceLoader();
        _skillResolver = new ExperimentDirectorSkillResolver();
        _spawnPoseCalculator = new ExperimentDirectorSpawnPoseCalculator(BuildSpawnSettings());
        _cameraTrackerResolver = new ExperimentDirectorCameraTrackerResolver();
        _runtimeAgentAssembler = new ExperimentDirectorRuntimeAgentAssembler();

        ResolveCameraTrackerIfNeeded();
    }

    public void SetCameraTracker(DynamicCameraTracker tracker)
    {
        cameraTracker = tracker;
        if (cameraTracker != null && currentRobotInstance != null)
        {
            cameraTracker.SetTarget(_runtimeAgentAssembler.ResolveTrackingTransform(currentRobotInstance));
        }
    }

    public void ExecuteWebCommand(string jsonString)
    {
        Debug.Log($"[Director] Receive command: {jsonString}");

        ResolveCameraTrackerIfNeeded();

        if (!_commandParser.TryParse(jsonString, out var cmd))
        {
            Debug.LogError("[Director] Invalid command payload.");
            return;
        }

        if (currentRobotInstance != null)
        {
            if (cameraTracker != null)
            {
                cameraTracker.SetTarget(null);
            }

            _runtimeAgentAssembler.DeactivateRobotInstance(currentRobotInstance);
            Destroy(currentRobotInstance);
            currentRobotInstance = null;
        }

        _resourceLoader.TryLoad(cmd.robotName, out var prefab, out var config);
        if (prefab == null)
        {
            Debug.LogError($"[Director] Robot prefab not found: {cmd.robotName}");
            return;
        }

        if (config == null)
        {
            Debug.LogError($"[Director] Robot data not found: {cmd.robotName}_Data");
            return;
        }

        if (!_skillResolver.TryResolveSkill(config, cmd.skillType, out var slot, out var skill))
        {
            Debug.LogError($"[Director] Unknown skill '{cmd.skillType}' for species {config.species}.");
            return;
        }

        _spawnPoseCalculator.UpdateSettings(BuildSpawnSettings());
        _spawnPoseCalculator.ResolveSpawnPose(prefab, slot, out var spawnPos, out var spawnRot);
        currentRobotInstance = Instantiate(prefab, spawnPos, spawnRot);

        if (cameraTracker != null)
        {
            cameraTracker.SetTarget(_runtimeAgentAssembler.ResolveTrackingTransform(currentRobotInstance));
        }

        NeuralVesselAgent agent = _runtimeAgentAssembler.EnsureRuntimeAgent(currentRobotInstance, Debug.LogWarning);
        if (agent == null)
        {
            Debug.LogError("[Director] NeuralVesselAgent missing on robot prefab.");
            if (cameraTracker != null)
            {
                cameraTracker.SetTarget(null);
            }

            Destroy(currentRobotInstance);
            currentRobotInstance = null;
            return;
        }

        agent.MountSoul(config, skill, slot);
    }

    private void ResolveCameraTrackerIfNeeded()
    {
        cameraTracker = _cameraTrackerResolver.Resolve(
            cameraTracker,
            gameObject.scene,
            () => FindObjectOfType<SceneDirector>(true),
            () => FindObjectsOfType<DynamicCameraTracker>(true));
    }

    private ExperimentDirectorSpawnPoseCalculator.Settings BuildSpawnSettings()
    {
        return new ExperimentDirectorSpawnPoseCalculator.Settings
        {
            spawnPoint = spawnPoint,
            usePrefabAuthoredTransform = usePrefabAuthoredTransform,
            fallbackSpawnPosition = fallbackSpawnPosition,
            fallbackSpawnEuler = fallbackSpawnEuler,
            spawnOffset = spawnOffset,
            runSpawnLiftY = runSpawnLiftY,
            jumpSpawnLiftY = jumpSpawnLiftY
        };
    }
}
