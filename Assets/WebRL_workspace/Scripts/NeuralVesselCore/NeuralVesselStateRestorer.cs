using System.Collections.Generic;
using UnityEngine;

public sealed class NeuralVesselStateRestorer
{
    public sealed class Snapshot
    {
        public readonly List<float> initialJointPositions = new List<float>();
        public readonly List<float> initialJointVelocities = new List<float>();
        public float[] initialDriveTargets = new float[0];
        public Vector3 initialRootPosition;
        public Quaternion initialRootRotation;
        public bool hasInitialState;
    }

    public Snapshot CreateSnapshot()
    {
        return new Snapshot();
    }

    public void EnsureDriveTargetBuffer(Snapshot snapshot, int actionNum)
    {
        if (snapshot.initialDriveTargets != null && snapshot.initialDriveTargets.Length == actionNum)
        {
            return;
        }

        snapshot.initialDriveTargets = new float[actionNum];
    }

    public void CacheInitialState(
        Snapshot snapshot,
        ArticulationBody rootBody,
        IList<ArticulationBody> joints,
        int actionNum)
    {
        if (rootBody == null)
        {
            snapshot.hasInitialState = false;
            return;
        }

        snapshot.initialRootPosition = rootBody.transform.position;
        snapshot.initialRootRotation = rootBody.transform.rotation;

        snapshot.initialJointPositions.Clear();
        snapshot.initialJointVelocities.Clear();
        rootBody.GetJointPositions(snapshot.initialJointPositions);
        rootBody.GetJointVelocities(snapshot.initialJointVelocities);

        EnsureDriveTargetBuffer(snapshot, actionNum);
        for (int i = 0; i < actionNum; i++)
        {
            snapshot.initialDriveTargets[i] = joints[i].xDrive.target;
        }

        snapshot.hasInitialState = true;
    }

    public void RestoreRootAndJointState(Snapshot snapshot, ArticulationBody rootBody)
    {
        if (!snapshot.hasInitialState || rootBody == null)
        {
            return;
        }

        rootBody.TeleportRoot(snapshot.initialRootPosition, snapshot.initialRootRotation);
        rootBody.velocity = Vector3.zero;
        rootBody.angularVelocity = Vector3.zero;
        rootBody.SetJointPositions(snapshot.initialJointPositions);
        rootBody.SetJointVelocities(snapshot.initialJointVelocities);
    }
}
