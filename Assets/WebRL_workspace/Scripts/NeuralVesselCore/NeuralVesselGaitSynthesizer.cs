using UnityEngine;

public sealed class NeuralVesselGaitSynthesizer
{
    public struct PhaseState
    {
        public int tp;
        public int tq;
        public int settleStepsRemaining;
        public float uf1;
        public float uf2;
        public float uff;
    }

    public bool TryTickSettle(ref PhaseState state)
    {
        if (state.settleStepsRemaining <= 0)
        {
            return false;
        }

        state.settleStepsRemaining--;
        state.tp = 0;
        state.tq = 0;
        state.uf1 = 0f;
        state.uf2 = 0f;
        state.uff = 0f;
        return true;
    }

    public void AdvancePhase(ref PhaseState state, SkillConfig currentSkill)
    {
        int t1 = Mathf.Max(0, currentSkill.T1);
        int t2 = Mathf.Max(0, currentSkill.T2);

        if (t1 > 0)
        {
            state.tp++;
            if (state.tp > 0 && state.tp <= t1)
            {
                float phase = (Mathf.PI * 2f * state.tp) / t1;
                state.uf1 = (-Mathf.Cos(phase) + 1f) * 0.5f;
                state.uf2 = 0f;
            }
            else if (state.tp > t1 && state.tp <= 2 * t1)
            {
                int tp0 = state.tp - t1;
                float phase = (Mathf.PI * 2f * tp0) / t1;
                state.uf1 = 0f;
                state.uf2 = (-Mathf.Cos(phase) + 1f) * 0.5f;
            }

            if (state.tp >= 2 * t1)
            {
                state.tp = 0;
            }
        }
        else
        {
            state.tp = 0;
            state.uf1 = 0f;
            state.uf2 = 0f;
        }

        if (t2 > 0)
        {
            state.tq++;
            float phase = (Mathf.PI * 2f * state.tq) / t2;
            state.uff = (-Mathf.Cos(phase) + 1f) * 0.5f;
            if (state.tq >= t2)
            {
                state.tq = 0;
            }
        }
        else
        {
            state.tq = 0;
            state.uff = 0f;
        }
    }

    public void ApplySpeciesGait(
        RobotConfig config,
        SkillConfig currentSkill,
        SkillSlot currentSlot,
        int actionNum,
        PhaseState phaseState,
        float[] targets)
    {
        switch (config.species)
        {
            case RobotSpecies.Biped:
                ApplyBipedGait(config, currentSkill, currentSlot, actionNum, phaseState, targets);
                break;
            case RobotSpecies.Quadruped:
                ApplyQuadrupedGait(config, currentSkill, currentSlot, phaseState, targets);
                break;
            case RobotSpecies.LegWheeled:
                ApplyLegWheeledGait(config, currentSkill, currentSlot, phaseState, targets);
                break;
        }
    }

    private void ApplyBipedGait(
        RobotConfig config,
        SkillConfig currentSkill,
        SkillSlot currentSlot,
        int actionNum,
        PhaseState phaseState,
        float[] targets)
    {
        if (config.idxParams == null || config.idxParams.Length < 6)
        {
            return;
        }

        if (currentSlot == SkillSlot.BipedJump)
        {
            ApplyTripletOffset(config, currentSkill, actionNum, 0, phaseState.uff, targets);
            if (actionNum == 10 && GetLegTripletCount(config) >= 2)
            {
                CopyTripletTargets(config, actionNum, 0, 1, targets);
            }
            else
            {
                ApplyTripletOffset(config, currentSkill, actionNum, 1, phaseState.uff, targets);
            }
            return;
        }

        ApplyTripletOffset(config, currentSkill, actionNum, 0, phaseState.uf1, targets);
        ApplyTripletOffset(config, currentSkill, actionNum, 1, phaseState.uf2, targets);
    }

    private void ApplyQuadrupedGait(
        RobotConfig config,
        SkillConfig currentSkill,
        SkillSlot currentSlot,
        PhaseState phaseState,
        float[] targets)
    {
        int legCount = GetLegTripletCount(config);
        if (legCount <= 0)
        {
            return;
        }

        if (currentSlot == SkillSlot.QuadPronk)
        {
            for (int leg = 0; leg < legCount; leg++)
            {
                ApplyTripletOffset(config, currentSkill, targets.Length, leg, phaseState.uff, targets);
            }
            return;
        }

        if (currentSlot == SkillSlot.QuadBound)
        {
            int split = Mathf.Max(1, legCount / 2);
            for (int leg = 0; leg < legCount; leg++)
            {
                ApplyTripletOffset(
                    config,
                    currentSkill,
                    targets.Length,
                    leg,
                    leg < split ? phaseState.uf1 : phaseState.uf2,
                    targets);
            }
            return;
        }

        float[] trotPattern = { phaseState.uf1, phaseState.uf2, phaseState.uf2, phaseState.uf1 };
        for (int leg = 0; leg < legCount; leg++)
        {
            ApplyTripletOffset(config, currentSkill, targets.Length, leg, trotPattern[leg % trotPattern.Length], targets);
        }
    }

    private void ApplyLegWheeledGait(
        RobotConfig config,
        SkillConfig currentSkill,
        SkillSlot currentSlot,
        PhaseState phaseState,
        float[] targets)
    {
        int legCount = GetLegTripletCount(config);
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
                ApplyTripletOffset(config, currentSkill, targets.Length, leg, 0f, targets);
            }
            return;
        }

        if (currentSlot == SkillSlot.WheelJump)
        {
            for (int leg = 0; leg < legCount; leg++)
            {
                ApplyTripletOffset(config, currentSkill, targets.Length, leg, phaseState.uff, targets);
            }
            return;
        }

        float[] walkPattern = { phaseState.uf1, phaseState.uf2, phaseState.uf2, phaseState.uf1 };
        for (int leg = 0; leg < legCount; leg++)
        {
            ApplyTripletOffset(config, currentSkill, targets.Length, leg, walkPattern[leg % walkPattern.Length], targets);
        }
    }

    private void ApplyTripletOffset(
        RobotConfig config,
        SkillConfig currentSkill,
        int actionNum,
        int legOrder,
        float phase,
        float[] targets)
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

        float[] weights = ResolvePhaseWeights(config, currentSkill);
        if (weights == null)
        {
            return;
        }

        float gaitSignal = currentSkill.dh * phase + currentSkill.d0;
        AddMappedOffset(config, actionNum, targets, config.idxParams[start], gaitSignal * weights[0]);
        AddMappedOffset(config, actionNum, targets, config.idxParams[start + 1], gaitSignal * weights[1]);
        AddMappedOffset(config, actionNum, targets, config.idxParams[start + 2], gaitSignal * weights[2]);
    }

    private void AddMappedOffset(
        RobotConfig config,
        int actionNum,
        float[] targets,
        int mappedIndex,
        float value)
    {
        int jointIndex = ResolveJointIndex(config, mappedIndex);
        if (jointIndex < 0 || jointIndex >= actionNum)
        {
            return;
        }

        targets[jointIndex] += value * ResolveJointSign(mappedIndex);
    }

    private float[] ResolvePhaseWeights(RobotConfig config, SkillConfig currentSkill)
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

    private int GetLegTripletCount(RobotConfig config)
    {
        return config.idxParams == null ? 0 : config.idxParams.Length / 3;
    }

    private void CopyTripletTargets(
        RobotConfig config,
        int actionNum,
        int sourceLegOrder,
        int targetLegOrder,
        float[] targets)
    {
        int sourceStart = sourceLegOrder * 3;
        int targetStart = targetLegOrder * 3;
        if (sourceStart + 2 >= config.idxParams.Length || targetStart + 2 >= config.idxParams.Length)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            int sourceIndex = ResolveJointIndex(config, config.idxParams[sourceStart + i]);
            int targetIndex = ResolveJointIndex(config, config.idxParams[targetStart + i]);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= actionNum || targetIndex >= actionNum)
            {
                continue;
            }

            float sourceValue = targets[sourceIndex] * ResolveJointSign(config.idxParams[sourceStart + i]);
            targets[targetIndex] = sourceValue * ResolveJointSign(config.idxParams[targetStart + i]);
        }
    }

    public int ResolveJointIndex(RobotConfig config, int mappedIndex)
    {
        if (mappedIndex == 0)
        {
            return -1;
        }

        int absIndex = Mathf.Abs(mappedIndex);
        return config.idxIsOneBased ? absIndex - 1 : absIndex;
    }

    public int ResolveJointSign(int mappedIndex)
    {
        return mappedIndex >= 0 ? 1 : -1;
    }
}
