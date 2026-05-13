using System;
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

    private readonly List<float> initialJointPositions = new List<float>();
    private readonly List<float> initialJointVelocities = new List<float>();
    private float[] initialDriveTargets = new float[0];
    private Vector3 initialRootPosition;
    private Quaternion initialRootRotation;
    private bool hasInitialState;

    private int tp;
    private int tq;
    private int settleStepsRemaining;
    private float uf1;
    private float uf2;
    private float uff;

    private float[] u = new float[0];
    private float[] ut = new float[0];
    private float[] utt = new float[0];
    private float[] utotal = new float[0];

    public void MountSoul(RobotConfig newConfig, SkillConfig skill, SkillSlot slot = SkillSlot.Unknown)
    {
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

        var root = body.GetComponent<ArticulationBody>();
        if (root == null)
        {
            return;
        }

        sensor.AddObservation(body.InverseTransformDirection(Vector3.down));
        sensor.AddObservation(body.InverseTransformDirection(root.angularVelocity));
        sensor.AddObservation(body.InverseTransformDirection(root.velocity));

        for (int i = 0; i < actionNum; i++)
        {
            var jointPosition = acts[i].jointPosition;
            var jointVelocity = acts[i].jointVelocity;
            sensor.AddObservation(jointPosition.dofCount > 0 ? jointPosition[0] : 0f);
            sensor.AddObservation(jointVelocity.dofCount > 0 ? jointVelocity[0] : 0f);
        }

        int extraObservationCount = config != null ? Mathf.Max(0, config.extraObservationCount) : 0;
        for (int i = 0; i < extraObservationCount; i++)
        {
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        EnsureRigReady();
        if (config == null || actionNum == 0)
        {
            return;
        }

        ResizeBuffers();
        Array.Clear(utotal, 0, utotal.Length);

        float stiffness = ResolveDriveValue(currentSkill.driveStiffness, config.defaultDriveStiffness);
        float damping = ResolveDriveValue(currentSkill.driveDamping, config.defaultDriveDamping);
        float forceLimit = ResolveDriveValue(currentSkill.driveForceLimit, config.defaultDriveForceLimit);

        if (settleStepsRemaining > 0)
        {
            ApplyInitialPoseTargets(stiffness, damping, forceLimit);
            return;
        }

        var continuousActions = actionBuffers.ContinuousActions;
        float kk = ResolveKk();

        for (int i = 0; i < actionNum; i++)
        {
            float action = i < continuousActions.Length ? continuousActions[i] : 0f;
            u[i] = u[i] * kk + (1f - kk) * action;
            ut[i] += u[i];
            utt[i] += ut[i];

            float kb = ResolveArrayValue(currentSkill.kbParams, i);
            float kb1 = ResolveArrayValue(currentSkill.kb1Params, i);
            float kb2 = ResolveArrayValue(currentSkill.kb2Params, i);
            utotal[i] = kb * u[i] + kb1 * ut[i] + kb2 * utt[i];
        }

        ApplySpeciesGait(utotal);

        for (int i = 0; i < actionNum; i++)
        {
            SetJointTargetDeg(acts[i], utotal[i], stiffness, damping, forceLimit);
        }
    }

    private void FixedUpdate()
    {
        if (settleStepsRemaining > 0)
        {
            settleStepsRemaining--;
            tp = 0;
            tq = 0;
            uf1 = 0f;
            uf2 = 0f;
            uff = 0f;
            return;
        }

        int t1 = Mathf.Max(0, currentSkill.T1);
        int t2 = Mathf.Max(0, currentSkill.T2);

        if (t1 > 0)
        {
            tp++;
            if (tp > 0 && tp <= t1)
            {
                float phase = (Mathf.PI * 2f * tp) / t1;
                uf1 = (-Mathf.Cos(phase) + 1f) * 0.5f;
                uf2 = 0f;
            }
            else if (tp > t1 && tp <= 2 * t1)
            {
                int tp0 = tp - t1;
                float phase = (Mathf.PI * 2f * tp0) / t1;
                uf1 = 0f;
                uf2 = (-Mathf.Cos(phase) + 1f) * 0.5f;
            }

            if (tp >= 2 * t1)
            {
                tp = 0;
            }
        }
        else
        {
            tp = 0;
            uf1 = 0f;
            uf2 = 0f;
        }

        if (t2 > 0)
        {
            tq++;
            float phase = (Mathf.PI * 2f * tq) / t2;
            uff = (-Mathf.Cos(phase) + 1f) * 0.5f;
            if (tq >= t2)
            {
                tq = 0;
            }
        }
        else
        {
            tq = 0;
            uff = 0f;
        }
    }

    private void ApplySpeciesGait(float[] targets)
    {
        switch (config.species)
        {
            case RobotSpecies.Biped:
                ApplyBipedGait(targets);
                break;
            case RobotSpecies.Quadruped:
                ApplyQuadrupedGait(targets);
                break;
            case RobotSpecies.LegWheeled:
                ApplyLegWheeledGait(targets);
                break;
        }
    }

    private void ApplyBipedGait(float[] targets)
    {
        if (config.idxParams == null || config.idxParams.Length < 6)
        {
            return;
        }

        if (currentSlot == SkillSlot.BipedJump)
        {
            ApplyTripletOffset(0, uff, targets);
            if (actionNum == 10 && GetLegTripletCount() >= 2)
            {
                CopyTripletTargets(0, 1, targets);
            }
            else
            {
                ApplyTripletOffset(1, uff, targets);
            }
            return;
        }

        ApplyTripletOffset(0, uf1, targets);
        ApplyTripletOffset(1, uf2, targets);
    }

    private void ApplyQuadrupedGait(float[] targets)
    {
        int legCount = GetLegTripletCount();
        if (legCount <= 0)
        {
            return;
        }

        if (currentSlot == SkillSlot.QuadPronk)
        {
            for (int leg = 0; leg < legCount; leg++)
            {
                ApplyTripletOffset(leg, uff, targets);
            }
            return;
        }

        if (currentSlot == SkillSlot.QuadBound)
        {
            int split = Mathf.Max(1, legCount / 2);
            for (int leg = 0; leg < legCount; leg++)
            {
                ApplyTripletOffset(leg, leg < split ? uf1 : uf2, targets);
            }
            return;
        }

        float[] trotPattern = new float[4] { uf1, uf2, uf2, uf1 };
        for (int leg = 0; leg < legCount; leg++)
        {
            float phase = trotPattern[leg % trotPattern.Length];
            ApplyTripletOffset(leg, phase, targets);
        }
    }

    private void ApplyLegWheeledGait(float[] targets)
    {
        int legCount = GetLegTripletCount();
        if (legCount <= 0)
        {
            return;
        }

        if (currentSlot == SkillSlot.WheelDrive)
        {
            if (config.wheelDrivePoseMode == WheelDrivePoseMode.WithoutPoseOffset)
            {
                return;
            }

            for (int leg = 0; leg < legCount; leg++)
            {
                ApplyTripletOffset(leg, 0f, targets);
            }
            return;
        }

        if (currentSlot == SkillSlot.WheelJump)
        {
            for (int leg = 0; leg < legCount; leg++)
            {
                ApplyTripletOffset(leg, uff, targets);
            }
            return;
        }

        float[] walkPattern = new float[4] { uf1, uf2, uf2, uf1 };
        for (int leg = 0; leg < legCount; leg++)
        {
            float phase = walkPattern[leg % walkPattern.Length];
            ApplyTripletOffset(leg, phase, targets);
        }
    }

    private void ApplyTripletOffset(int legOrder, float phase, float[] targets)
    {
        if (config.idxParams == null)
        {
            return;
        }

        int start = legOrder * 3;
        if (start + 2 >= config.idxParams.Length)
        {
            return;
        }

        var weights = ResolvePhaseWeights();
        if (weights == null)
        {
            return;
        }

        float gaitSignal = currentSkill.dh * phase + currentSkill.d0;
        AddMappedOffset(targets, config.idxParams[start], gaitSignal * weights[0]);
        AddMappedOffset(targets, config.idxParams[start + 1], gaitSignal * weights[1]);
        AddMappedOffset(targets, config.idxParams[start + 2], gaitSignal * weights[2]);
    }

    private void AddMappedOffset(float[] targets, int mappedIndex, float value)
    {
        int jointIndex = ResolveJointIndex(mappedIndex);
        if (jointIndex < 0 || jointIndex >= actionNum)
        {
            return;
        }

        targets[jointIndex] += value * ResolveJointSign(mappedIndex);
    }

    private float ResolveKk()
    {
        float fromSkill = currentSkill.kk;
        float kk = fromSkill > 0f ? fromSkill : config.defaultKk;
        return Mathf.Clamp01(kk);
    }

    private static float ResolveArrayValue(float[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length)
        {
            return 0f;
        }
        return values[index];
    }

    private float[] ResolvePhaseWeights()
    {
        if (currentSkill.phaseWeights != null && currentSkill.phaseWeights.Length >= 3)
        {
            return currentSkill.phaseWeights;
        }

        if (config.defaultPhaseWeights != null && config.defaultPhaseWeights.Length >= 3)
        {
            return config.defaultPhaseWeights;
        }

        return null;
    }

    private static float ResolveDriveValue(float fromSkill, float fromConfig)
    {
        return fromSkill > 0f ? fromSkill : Mathf.Max(0f, fromConfig);
    }

    private void SetJointTargetDeg(ArticulationBody joint, float targetAngle, float stiffness, float damping, float forceLimit)
    {
        var drive = joint.xDrive;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        drive.target = targetAngle;
        joint.xDrive = drive;
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

    private int GetLegTripletCount()
    {
        return config.idxParams == null ? 0 : config.idxParams.Length / 3;
    }

    private void CopyTripletTargets(int sourceLegOrder, int targetLegOrder, float[] targets)
    {
        int sourceStart = sourceLegOrder * 3;
        int targetStart = targetLegOrder * 3;
        if (sourceStart + 2 >= config.idxParams.Length || targetStart + 2 >= config.idxParams.Length)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            int sourceIndex = ResolveJointIndex(config.idxParams[sourceStart + i]);
            int targetIndex = ResolveJointIndex(config.idxParams[targetStart + i]);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= actionNum || targetIndex >= actionNum)
            {
                continue;
            }

            float sourceValue = targets[sourceIndex] * ResolveJointSign(config.idxParams[sourceStart + i]);
            targets[targetIndex] = sourceValue * ResolveJointSign(config.idxParams[targetStart + i]);
        }
    }

    private int ResolveJointIndex(int mappedIndex)
    {
        if (mappedIndex == 0)
        {
            return -1;
        }

        int absIndex = Mathf.Abs(mappedIndex);
        return config.idxIsOneBased ? absIndex - 1 : absIndex;
    }

    private static int ResolveJointSign(int mappedIndex)
    {
        return mappedIndex >= 0 ? 1 : -1;
    }

    private void ResizeBuffers()
    {
        if (u.Length == actionNum)
        {
            return;
        }

        u = new float[actionNum];
        ut = new float[actionNum];
        utt = new float[actionNum];
        utotal = new float[actionNum];
        initialDriveTargets = new float[actionNum];
    }

    private void ClearBuffers()
    {
        Array.Clear(u, 0, u.Length);
        Array.Clear(ut, 0, ut.Length);
        Array.Clear(utt, 0, utt.Length);
        Array.Clear(utotal, 0, utotal.Length);
    }

    private void EnsureRigReady()
    {
        if (rigReady && actionNum > 0 && body != null && rootBody != null)
        {
            return;
        }

        RebuildRigCache();
        if (!hasInitialState)
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
        if (rootBody == null)
        {
            hasInitialState = false;
            return;
        }

        initialRootPosition = rootBody.transform.position;
        initialRootRotation = rootBody.transform.rotation;

        initialJointPositions.Clear();
        initialJointVelocities.Clear();
        rootBody.GetJointPositions(initialJointPositions);
        rootBody.GetJointVelocities(initialJointVelocities);

        for (int i = 0; i < actionNum; i++)
        {
            initialDriveTargets[i] = acts[i].xDrive.target;
        }

        hasInitialState = true;
    }

    private void ApplyInitialPoseTargets(float stiffness, float damping, float forceLimit)
    {
        for (int i = 0; i < actionNum; i++)
        {
            float target = i < initialDriveTargets.Length ? initialDriveTargets[i] : 0f;
            SetJointTargetDeg(acts[i], target, stiffness, damping, forceLimit);
        }
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

        if (hasInitialState && rootBody != null)
        {
            rootBody.TeleportRoot(initialRootPosition, initialRootRotation);
            rootBody.velocity = Vector3.zero;
            rootBody.angularVelocity = Vector3.zero;
            rootBody.SetJointPositions(initialJointPositions);
            rootBody.SetJointVelocities(initialJointVelocities);
        }

        tp = 0;
        tq = 0;
        settleStepsRemaining = Mathf.Max(0, currentSkill.settleSteps);
        uf1 = 0f;
        uf2 = 0f;
        uff = 0f;
        ClearBuffers();
    }
}
