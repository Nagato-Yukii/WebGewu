using System.Collections.Generic;
using UnityEngine;

public sealed class NeuralVesselJointDriver
{
    public float ResolveDriveValue(float fromSkill, float fromConfig)
    {
        return fromSkill > 0f ? fromSkill : Mathf.Max(0f, fromConfig);
    }

    public void SetJointTargetDeg(
        ArticulationBody joint,
        float targetAngle,
        float stiffness,
        float damping,
        float forceLimit)
    {
        var drive = joint.xDrive;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        drive.target = targetAngle;
        joint.xDrive = drive;
    }

    public void ApplyTargets(
        IList<ArticulationBody> joints,
        float[] targets,
        int actionNum,
        float stiffness,
        float damping,
        float forceLimit)
    {
        for (int i = 0; i < actionNum; i++)
        {
            SetJointTargetDeg(joints[i], targets[i], stiffness, damping, forceLimit);
        }
    }

    public void ApplyInitialPoseTargets(
        IList<ArticulationBody> joints,
        int actionNum,
        float[] initialDriveTargets,
        float stiffness,
        float damping,
        float forceLimit)
    {
        for (int i = 0; i < actionNum; i++)
        {
            float target = i < initialDriveTargets.Length ? initialDriveTargets[i] : 0f;
            SetJointTargetDeg(joints[i], target, stiffness, damping, forceLimit);
        }
    }
}
