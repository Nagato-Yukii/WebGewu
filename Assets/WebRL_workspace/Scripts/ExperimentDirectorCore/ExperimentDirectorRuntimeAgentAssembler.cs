using System;
using Unity.MLAgents;
using UnityEngine;

public sealed class ExperimentDirectorRuntimeAgentAssembler
{
    public NeuralVesselAgent EnsureRuntimeAgent(GameObject robotInstance, Action<string> warnLog)
    {
        if (robotInstance == null)
        {
            return null;
        }

        NeuralVesselAgent runtimeAgent = robotInstance.GetComponent<NeuralVesselAgent>();
        if (runtimeAgent == null)
        {
            runtimeAgent = robotInstance.GetComponentInChildren<NeuralVesselAgent>(true);
        }

        if (runtimeAgent == null)
        {
            runtimeAgent = robotInstance.AddComponent<NeuralVesselAgent>();
            warnLog?.Invoke("[Director] Added runtime NeuralVesselAgent to robot instance.");
        }

        DisableLegacyAgents(robotInstance, runtimeAgent, warnLog);
        return runtimeAgent;
    }

    public void DisableLegacyAgents(GameObject robotInstance, NeuralVesselAgent runtimeAgent, Action<string> warnLog)
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
            warnLog?.Invoke($"[Director] Disabled legacy Agent: {agent.GetType().Name}");
        }
    }

    public Transform ResolveTrackingTransform(GameObject robotInstance)
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

    public void DeactivateRobotInstance(GameObject robotInstance)
    {
        if (robotInstance == null)
        {
            return;
        }

        ArticulationBody[] articulationBodies = robotInstance.GetComponentsInChildren<ArticulationBody>(true);
        ArticulationBody rootBody = null;
        for (int i = 0; i < articulationBodies.Length; i++)
        {
            ArticulationBody body = articulationBodies[i];
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

        Collider[] colliders = robotInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        Renderer[] renderers = robotInstance.GetComponentsInChildren<Renderer>(true);
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
