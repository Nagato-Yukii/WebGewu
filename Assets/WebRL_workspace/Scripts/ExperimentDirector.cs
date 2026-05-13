using System.Text;
using Unity.MLAgents;
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

    private void Awake()
    {
        ResolveCameraTrackerIfNeeded();
    }

    public void SetCameraTracker(DynamicCameraTracker tracker)
    {
        cameraTracker = tracker;
        if (cameraTracker != null && currentRobotInstance != null)
        {
            cameraTracker.SetTarget(ResolveTrackingTransform(currentRobotInstance));
        }
    }

    public void ExecuteWebCommand(string jsonString)
    {
        Debug.Log($"[Director] Receive command: {jsonString}");

        ResolveCameraTrackerIfNeeded();

        WebCommand cmd = JsonUtility.FromJson<WebCommand>(jsonString);
        if (cmd == null || string.IsNullOrWhiteSpace(cmd.robotName) || string.IsNullOrWhiteSpace(cmd.skillType))
        {
            Debug.LogError("[Director] Invalid command payload.");
            return;
        }

        if (currentRobotInstance != null)
        {
            if (cameraTracker != null) cameraTracker.SetTarget(null);
            DeactivateRobotInstance(currentRobotInstance);
            Destroy(currentRobotInstance);
        }

        GameObject prefab = Resources.Load<GameObject>($"Robots/{cmd.robotName}");
        if (prefab == null)
        {
            Debug.LogError($"[Director] Robot prefab not found: {cmd.robotName}");
            return;
        }

        RobotConfig config = Resources.Load<RobotConfig>($"RobotData/{cmd.robotName}_Data");
        if (config == null)
        {
            Debug.LogError($"[Director] Robot data not found: {cmd.robotName}_Data");
            return;
        }

        SkillSlot slot;
        SkillConfig skill = ExtractSkillFromConfig(config, cmd.skillType, out slot);
        if (slot == SkillSlot.Unknown)
        {
            Debug.LogError($"[Director] Unknown skill '{cmd.skillType}' for species {config.species}.");
            return;
        }

        ResolveSpawnPose(prefab, slot, out var spawnPos, out var spawnRot);
        currentRobotInstance = Instantiate(prefab, spawnPos, spawnRot);
        if (cameraTracker != null) cameraTracker.SetTarget(ResolveTrackingTransform(currentRobotInstance));
        NeuralVesselAgent agent = EnsureRuntimeAgent(currentRobotInstance);
        if (agent == null)
        {
            Debug.LogError("[Director] NeuralVesselAgent missing on robot prefab.");
            if (cameraTracker != null) cameraTracker.SetTarget(null);
            Destroy(currentRobotInstance);
            currentRobotInstance = null;
            return;
        }

        agent.MountSoul(config, skill, slot);
    }

    private void ResolveCameraTrackerIfNeeded()
    {
        if (cameraTracker == null)
        {
            var sceneDirector = FindObjectOfType<SceneDirector>(true);
            if (sceneDirector != null && sceneDirector.GlobalCameraTracker != null)
            {
                cameraTracker = sceneDirector.GlobalCameraTracker;
                return;
            }

            var trackers = FindObjectsOfType<DynamicCameraTracker>(true);
            for (int i = 0; i < trackers.Length; i++)
            {
                var tracker = trackers[i];
                if (tracker != null && tracker.gameObject.scene != gameObject.scene)
                {
                    cameraTracker = tracker;
                    return;
                }
            }
        }
    }

    private static NeuralVesselAgent EnsureRuntimeAgent(GameObject robotInstance)
    {
        if (robotInstance == null)
        {
            return null;
        }

        NeuralVesselAgent agent = robotInstance.GetComponent<NeuralVesselAgent>();
        if (agent == null)
        {
            agent = robotInstance.GetComponentInChildren<NeuralVesselAgent>(true);
        }

        if (agent == null)
        {
            agent = robotInstance.AddComponent<NeuralVesselAgent>();
            Debug.LogWarning("[Director] Added runtime NeuralVesselAgent to robot instance.");
        }

        DisableLegacyAgents(robotInstance, agent);
        return agent;
    }

    private static void DisableLegacyAgents(GameObject robotInstance, NeuralVesselAgent runtimeAgent)
    {
        if (robotInstance == null)
        {
            return;
        }

        Agent[] agents = robotInstance.GetComponentsInChildren<Agent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            Agent agent = agents[i];
            if (agent == null || agent == runtimeAgent)
            {
                continue;
            }

            agent.enabled = false;
            Debug.LogWarning($"[Director] Disabled legacy Agent: {agent.GetType().Name}");
        }
    }

    private SkillConfig ExtractSkillFromConfig(RobotConfig config, string skillType, out SkillSlot slot)
    {
        slot = ResolveSkillSlot(config.species, NormalizeSkillType(skillType));

        switch (slot)
        {
            case SkillSlot.BipedWalk: return config.bipedWalk;
            case SkillSlot.BipedRun: return config.bipedRun;
            case SkillSlot.BipedJump: return config.bipedJump;
            case SkillSlot.QuadTrot: return config.quadTrot;
            case SkillSlot.QuadBound: return config.quadBound;
            case SkillSlot.QuadPronk: return config.quadPronk;
            case SkillSlot.WheelDrive: return config.wheelDrive;
            case SkillSlot.WheelWalk: return config.wheelWalk;
            case SkillSlot.WheelJump: return config.wheelJump;
            default: return default;
        }
    }

    private static SkillSlot ResolveSkillSlot(RobotSpecies species, string normalized)
    {
        switch (normalized)
        {
            case "bipedwalk": return SkillSlot.BipedWalk;
            case "bipedrun": return SkillSlot.BipedRun;
            case "bipedjump": return SkillSlot.BipedJump;
            case "quadtrot": return SkillSlot.QuadTrot;
            case "quadbound": return SkillSlot.QuadBound;
            case "quadpronk": return SkillSlot.QuadPronk;
            case "wheeldrive":
            case "legwheeleddrive": return SkillSlot.WheelDrive;
            case "wheelwalk":
            case "legwheeledwalk": return SkillSlot.WheelWalk;
            case "wheeljump":
            case "legwheeledjump": return SkillSlot.WheelJump;

            case "walk":
                if (species == RobotSpecies.Biped) return SkillSlot.BipedWalk;
                if (species == RobotSpecies.LegWheeled) return SkillSlot.WheelWalk;
                if (species == RobotSpecies.Quadruped) return SkillSlot.QuadTrot;
                return SkillSlot.Unknown;

            case "run":
                return species == RobotSpecies.Biped ? SkillSlot.BipedRun : SkillSlot.Unknown;

            case "jump":
                if (species == RobotSpecies.Biped) return SkillSlot.BipedJump;
                if (species == RobotSpecies.LegWheeled) return SkillSlot.WheelJump;
                return SkillSlot.Unknown;

            case "trot": return SkillSlot.QuadTrot;
            case "bound": return SkillSlot.QuadBound;
            case "pronk": return SkillSlot.QuadPronk;
            case "drive": return SkillSlot.WheelDrive;

            default: return SkillSlot.Unknown;
        }
    }

    private static string NormalizeSkillType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static Transform ResolveTrackingTransform(GameObject robotInstance)
    {
        if (robotInstance == null)
        {
            return null;
        }

        ArticulationBody firstBody = null;
        ArticulationBody[] bodies = robotInstance.GetComponentsInChildren<ArticulationBody>();
        for (int i = 0; i < bodies.Length; i++)
        {
            ArticulationBody body = bodies[i];
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

        return firstBody != null ? firstBody.transform : robotInstance.transform;
    }

    private void ResolveSpawnPose(GameObject prefab, SkillSlot slot, out Vector3 position, out Quaternion rotation)
    {
        float skillLift = GetSkillSpawnLift(slot);
        Vector3 totalOffset = spawnOffset + new Vector3(0f, skillLift, 0f);

        if (spawnPoint != null)
        {
            position = spawnPoint.position + totalOffset;
            rotation = spawnPoint.rotation;
            return;
        }

        if (usePrefabAuthoredTransform && prefab != null)
        {
            position = prefab.transform.position + totalOffset;
            rotation = prefab.transform.rotation;
            return;
        }

        position = fallbackSpawnPosition + totalOffset;
        rotation = Quaternion.Euler(fallbackSpawnEuler);
    }

    private float GetSkillSpawnLift(SkillSlot slot)
    {
        switch (slot)
        {
            case SkillSlot.BipedRun:
                return runSpawnLiftY;
            case SkillSlot.BipedJump:
                return jumpSpawnLiftY;
            default:
                return 0f;
        }
    }

    private static void DeactivateRobotInstance(GameObject robotInstance)
    {
        if (robotInstance == null)
        {
            return;
        }

        var articulationBodies = robotInstance.GetComponentsInChildren<ArticulationBody>(true);
        ArticulationBody rootBody = null;
        for (int i = 0; i < articulationBodies.Length; i++)
        {
            var body = articulationBodies[i];
            if (body == null)
            {
                continue;
            }

            if (rootBody == null || body.isRoot)
            {
                rootBody = body;
            }

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        var colliders = robotInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        var renderers = robotInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        if (rootBody != null)
        {
            rootBody.TeleportRoot(new Vector3(0f, -1000f, 0f), rootBody.transform.rotation);
        }
        else
        {
            robotInstance.transform.position = new Vector3(0f, -1000f, 0f);
        }
    }

}
