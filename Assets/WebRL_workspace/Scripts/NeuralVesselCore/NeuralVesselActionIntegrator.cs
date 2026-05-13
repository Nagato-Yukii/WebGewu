using System;
using Unity.MLAgents.Actuators;
using UnityEngine;

public sealed class NeuralVesselActionIntegrator
{
    public struct Buffers
    {
        public float[] u;
        public float[] ut;
        public float[] utt;
        public float[] utotal;
    }

    public Buffers ResizeIfNeeded(Buffers buffers, int actionNum)
    {
        if (buffers.u != null && buffers.u.Length == actionNum)
        {
            return buffers;
        }

        buffers.u = new float[actionNum];
        buffers.ut = new float[actionNum];
        buffers.utt = new float[actionNum];
        buffers.utotal = new float[actionNum];
        return buffers;
    }

    public void Clear(Buffers buffers)
    {
        Array.Clear(buffers.u, 0, buffers.u.Length);
        Array.Clear(buffers.ut, 0, buffers.ut.Length);
        Array.Clear(buffers.utt, 0, buffers.utt.Length);
        Array.Clear(buffers.utotal, 0, buffers.utotal.Length);
    }

    public void Integrate(
        ActionBuffers actionBuffers,
        Buffers buffers,
        int actionNum,
        float kk,
        float[] kb,
        float[] kb1,
        float[] kb2)
    {
        Array.Clear(buffers.utotal, 0, buffers.utotal.Length);
        var continuousActions = actionBuffers.ContinuousActions;

        for (int i = 0; i < actionNum; i++)
        {
            float action = i < continuousActions.Length ? continuousActions[i] : 0f;
            buffers.u[i] = buffers.u[i] * kk + (1f - kk) * action;
            buffers.ut[i] += buffers.u[i];
            buffers.utt[i] += buffers.ut[i];

            float g0 = ResolveArrayValue(kb, i);
            float g1 = ResolveArrayValue(kb1, i);
            float g2 = ResolveArrayValue(kb2, i);
            buffers.utotal[i] = g0 * buffers.u[i] + g1 * buffers.ut[i] + g2 * buffers.utt[i];
        }
    }

    public float ResolveKk(SkillConfig currentSkill, RobotConfig config)
    {
        float fromSkill = currentSkill.kk;
        float kk = fromSkill > 0f ? fromSkill : config.defaultKk;
        return Mathf.Clamp01(kk);
    }

    public static float ResolveArrayValue(float[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length)
        {
            return 0f;
        }

        return values[index];
    }
}
