using UnityEngine;
using Unity.Sentis;

public enum RobotSpecies
{
    Biped,
    Quadruped,
    LegWheeled
}

public enum WheelDrivePoseMode
{
    WithPoseOffset,
    WithoutPoseOffset
}

public enum SkillSlot
{
    Unknown = 0,
    BipedWalk,
    BipedRun,
    BipedJump,
    QuadTrot,
    QuadBound,
    QuadPronk,
    WheelDrive,
    WheelWalk,
    WheelJump
}

[System.Serializable]
public struct SkillConfig
{
    [Tooltip("ONNX policy model for this skill.")]
    public ModelAsset policyModel;

    [Header("Action Synthesis")]
    public float[] kbParams;
    public float[] kb1Params;
    public float[] kb2Params;
    public float[] phaseWeights;

    [Header("Gait Shape")]
    public float dh;
    public float d0;
    public int T1;
    public int T2;

    [Header("Runtime Dynamics")]
    [Range(0f, 1f)] public float kk;
    [Min(0)] public int settleSteps;
    public float driveStiffness;
    public float driveDamping;
    public float driveForceLimit;
}

[CreateAssetMenu(fileName = "NewRobotConfig", menuName = "RL_Playground/Robot Config")]
public class RobotConfig : ScriptableObject
{
    [Header("Identity")]
    public string robotName;
    public RobotSpecies species;

    [Header("Joint Mapping")]
    [Tooltip("Joint map by leg triplet order: [hip, knee, ankle] * N.")]
    public int[] idxParams;
    [Tooltip("True when idxParams are 1-based indices.")]
    public bool idxIsOneBased = true;

    [Header("Runtime Defaults")]
    [Range(0f, 1f)] public float defaultKk = 0.9f;
    [Min(0.001f)] public float fixedDeltaTime = 0.01f;
    public float defaultDriveStiffness = 2000f;
    public float defaultDriveDamping = 100f;
    public float defaultDriveForceLimit = 300f;
    public float[] defaultPhaseWeights = new float[3] { 1f, -2f, 1f };
    [Min(0)] public int extraObservationCount = 0;
    public WheelDrivePoseMode wheelDrivePoseMode = WheelDrivePoseMode.WithPoseOffset;

    [Space(8)]
    [Header("Biped Skills")]
    public SkillConfig bipedWalk;
    public SkillConfig bipedRun;
    public SkillConfig bipedJump;

    [Space(8)]
    [Header("Quadruped Skills")]
    public SkillConfig quadTrot;
    public SkillConfig quadBound;
    public SkillConfig quadPronk;

    [Space(8)]
    [Header("Leg-Wheeled Skills")]
    public SkillConfig wheelDrive;
    public SkillConfig wheelWalk;
    public SkillConfig wheelJump;
}
