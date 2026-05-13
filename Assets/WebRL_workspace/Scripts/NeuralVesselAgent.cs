using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class NeuralVesselAgent : Agent
{
    [Header("Runtime Injection")]
    public RobotConfig config;
    public SkillConfig currentSkill;
    public SkillSlot currentSlot = SkillSlot.Unknown;

    private Transform body;
    private ArticulationBody rootBody;
    private readonly List<ArticulationBody> acts = new List<ArticulationBody>();
    private int actionNum;
    private bool rigReady;

    private NeuralVesselObservationBuilder observationBuilder;
    private NeuralVesselJointDriver jointDriver;
    private NeuralVesselActionIntegrator actionIntegrator;
    private NeuralVesselGaitSynthesizer gaitSynthesizer;
    private NeuralVesselStateRestorer stateRestorer;

    private NeuralVesselActionIntegrator.Buffers runtimeBuffers;
    private NeuralVesselGaitSynthesizer.PhaseState phaseState;
    private NeuralVesselStateRestorer.Snapshot initialSnapshot;

    protected override void Awake()
    {
        // Agent base Awake initializes ML-Agents internals first.
        base.Awake();
        EnsureCoreInitialized();
    }

    public void MountSoul(RobotConfig newConfig, SkillConfig skill, SkillSlot slot = SkillSlot.Unknown)
    {
        EnsureCoreInitialized();
        config = newConfig;
        currentSkill = skill;
        currentSlot = slot;

        if (config != null && config.fixedDeltaTime > 0f)
        {
            Time.fixedDeltaTime = config.fixedDeltaTime;
        }
        EnsureRigReady();

        var behaviorParameters = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        SyncBehaviorParameters(behaviorParameters);

        if (currentSkill.policyModel != null)
        {
            InferenceDevice inferenceDevice = ResolveInferenceDevice();
            SetModel("gewu", currentSkill.policyModel, inferenceDevice);
            if (behaviorParameters != null)
            {
                behaviorParameters.BehaviorType = Unity.MLAgents.Policies.BehaviorType.InferenceOnly;
            }
            Debug.Log($"[NeuralVessel] Mounted: {config.robotName} / {currentSlot} / {inferenceDevice}");
        }
        else
        {
            if (behaviorParameters != null)
            {
                behaviorParameters.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
            }
            Debug.LogError($"[NeuralVessel] Missing policy model: {config.robotName} / {currentSlot}");
        }

        ResetRuntimeStateForCurrentSkill();
    }

    public override void Initialize()
    {
        EnsureCoreInitialized();
        RebuildRigCache();
        CacheInitialState();
    }

    public override void OnEpisodeBegin()
    {
        ResetRuntimeStateForCurrentSkill();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        EnsureRigReady();
        if (body == null)
        {
            return;
        }

        var root = rootBody;
        if (root == null)
        {
            return;
        }

        int extraObservationCount = config != null ? Mathf.Max(0, config.extraObservationCount) : 0;
        observationBuilder.Build(body, root, acts, actionNum, extraObservationCount, sensor);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        EnsureRigReady();
        if (config == null || actionNum == 0)
        {
            return;
        }

        ResizeBuffers();
        float stiffness = jointDriver.ResolveDriveValue(currentSkill.driveStiffness, config.defaultDriveStiffness);
        float damping = jointDriver.ResolveDriveValue(currentSkill.driveDamping, config.defaultDriveDamping);
        float forceLimit = jointDriver.ResolveDriveValue(currentSkill.driveForceLimit, config.defaultDriveForceLimit);

        if (phaseState.settleStepsRemaining > 0)
        {
            jointDriver.ApplyInitialPoseTargets(
                acts,
                actionNum,
                initialSnapshot.initialDriveTargets,
                stiffness,
                damping,
                forceLimit);
            return;
        }

        float kk = actionIntegrator.ResolveKk(currentSkill, config);
        actionIntegrator.Integrate(
            actionBuffers,
            runtimeBuffers,
            actionNum,
            kk,
            currentSkill.kbParams,
            currentSkill.kb1Params,
            currentSkill.kb2Params);

        gaitSynthesizer.ApplySpeciesGait(
            config,
            currentSkill,
            currentSlot,
            actionNum,
            phaseState,
            runtimeBuffers.utotal);

        jointDriver.ApplyTargets(acts, runtimeBuffers.utotal, actionNum, stiffness, damping, forceLimit);
    }

    private void FixedUpdate()
    {
        if (gaitSynthesizer.TryTickSettle(ref phaseState))
        {
            return;
        }

        gaitSynthesizer.AdvancePhase(ref phaseState, currentSkill);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActionsOut = actionsOut.ContinuousActions;
        for (int i = 0; i < continuousActionsOut.Length; i++)
        {
            continuousActionsOut[i] = 0f;
        }
    }

    private void SyncBehaviorParameters(BehaviorParameters behaviorParameters)
    {
        if (behaviorParameters == null || config == null)
        {
            return;
        }

        int expectedObservationSize = 9 + 2 * actionNum + Mathf.Max(0, config.extraObservationCount);
        if (behaviorParameters.BrainParameters.VectorObservationSize != expectedObservationSize)
        {
            behaviorParameters.BrainParameters.VectorObservationSize = expectedObservationSize;
        }
    }

    private void ResizeBuffers()
    {
        runtimeBuffers = actionIntegrator.ResizeIfNeeded(runtimeBuffers, actionNum);
        stateRestorer.EnsureDriveTargetBuffer(initialSnapshot, actionNum);
    }

    private void ClearBuffers()
    {
        if (runtimeBuffers.u == null || runtimeBuffers.u.Length == 0)
        {
            return;
        }

        actionIntegrator.Clear(runtimeBuffers);
    }

    private void EnsureRigReady()
    {
        if (rigReady && actionNum > 0 && body != null && rootBody != null)
        {
            return;
        }

        RebuildRigCache();
        if (!initialSnapshot.hasInitialState)
        {
            CacheInitialState();
        }
    }

    private void RebuildRigCache()
    {
        rigReady = false;
        acts.Clear();
        rootBody = null;
        body = null;
        actionNum = 0;

        ArticulationBody[] arts = GetComponentsInChildren<ArticulationBody>();
        if (arts == null || arts.Length == 0)
        {
            ResizeBuffers();
            return;
        }

        rootBody = arts[0];
        body = rootBody.transform;

        for (int i = 0; i < arts.Length; i++)
        {
            if (arts[i].jointType == ArticulationJointType.RevoluteJoint)
            {
                acts.Add(arts[i]);
            }
        }

        actionNum = acts.Count;
        ResizeBuffers();
        rigReady = true;
    }

    private void CacheInitialState()
    {
        stateRestorer.CacheInitialState(initialSnapshot, rootBody, acts, actionNum);
    }

    private InferenceDevice ResolveInferenceDevice()
    {
        // Keep all runtime targets on the CPU/Burst path.
        // This avoids Sentis output readback issues on explicit GPU inference
        // without requiring any ML-Agents package patching.
        return InferenceDevice.Burst;
    }

    private void ResetRuntimeStateForCurrentSkill()
    {
        EnsureRigReady();
        stateRestorer.RestoreRootAndJointState(initialSnapshot, rootBody);

        phaseState.tp = 0;
        phaseState.tq = 0;
        phaseState.settleStepsRemaining = Mathf.Max(0, currentSkill.settleSteps);
        phaseState.uf1 = 0f;
        phaseState.uf2 = 0f;
        phaseState.uff = 0f;
        ClearBuffers();
    }

    private void EnsureCoreInitialized()
    {
        if (observationBuilder != null)
        {
            return;
        }

        observationBuilder = new NeuralVesselObservationBuilder();
        jointDriver = new NeuralVesselJointDriver();
        actionIntegrator = new NeuralVesselActionIntegrator();
        gaitSynthesizer = new NeuralVesselGaitSynthesizer();
        stateRestorer = new NeuralVesselStateRestorer();
        initialSnapshot = stateRestorer.CreateSnapshot();
    }
}
