using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public sealed class NeuralVesselObservationBuilder
{
    public void Build(
        Transform body,
        ArticulationBody rootBody,
        IList<ArticulationBody> joints,
        int actionNum,
        int extraObservationCount,
        VectorSensor sensor)
    {
        if (body == null || rootBody == null || sensor == null)
        {
            return;
        }

        sensor.AddObservation(body.InverseTransformDirection(Vector3.down));
        sensor.AddObservation(body.InverseTransformDirection(rootBody.angularVelocity));
        sensor.AddObservation(body.InverseTransformDirection(rootBody.velocity));

        for (int i = 0; i < actionNum; i++)
        {
            var jointPosition = joints[i].jointPosition;
            var jointVelocity = joints[i].jointVelocity;
            sensor.AddObservation(jointPosition.dofCount > 0 ? jointPosition[0] : 0f);
            sensor.AddObservation(jointVelocity.dofCount > 0 ? jointVelocity[0] : 0f);
        }

        int zeros = Mathf.Max(0, extraObservationCount);
        for (int i = 0; i < zeros; i++)
        {
            sensor.AddObservation(0f);
        }
    }
}
