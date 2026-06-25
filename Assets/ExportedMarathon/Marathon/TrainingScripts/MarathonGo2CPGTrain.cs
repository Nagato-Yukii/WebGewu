using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class MarathonGo2CPGTrain : Agent
{
    const int Go2ActionCount = 12;

    int tp = 0;
    int tt = 0;

    public bool fixbody = false;
    public bool train;
    float uf1 = 0f;
    float uf2 = 0f;
    readonly float[] u = new float[Go2ActionCount];
    readonly float[] utotal = new float[Go2ActionCount];
    int tp0 = 0;

    [Header("CPG Direct Controls")]
    public int T1 = 32;
    public float dh = 22.9f;
    public float k0 = 0.5f;
    [Range(0, 1)] public int gait = 0;

    Transform body;
    public int ObservationNum;
    public int ActionNum;

    readonly List<float> P0 = new List<float>();
    readonly List<float> W0 = new List<float>();
    Vector3 pos0;
    Quaternion rot0;
    ArticulationBody[] arts = new ArticulationBody[40];
    readonly ArticulationBody[] acts = new ArticulationBody[Go2ActionCount];

    readonly float[] kb = new float[Go2ActionCount] { 22.9f, 22.9f, 22.9f, 22.9f, 22.9f, 22.9f, 22.9f, 22.9f, 22.9f, 22.9f, 22.9f, 22.9f };
    readonly float[] qsit = new float[Go2ActionCount] { 0f, 1.4f, -2.3f, 0f, 1.4f, -2.3f, 0f, 1.4f, -2.3f, 0f, 1.4f, -2.3f };

    public float vr = 0f;
    public float wr = 0f;

    int agentId = 0;
    int agentCount = 1;

    public void SetInferenceMode()
    {
        train = false;
        fixbody = false;
    }

    public void RestartInferenceEpisode()
    {
        SetInferenceMode();
        if (body == null || arts == null || arts.Length == 0 || arts[0] == null)
        {
            Initialize();
        }

        OnEpisodeBegin();
    }

    public override void Initialize()
    {
        arts = GetComponentsInChildren<ArticulationBody>();
        ActionNum = 0;
        for (int k = 0; k < arts.Length; k++)
        {
            if (arts[k].jointType.ToString() == "RevoluteJoint" && ActionNum < Go2ActionCount)
            {
                acts[ActionNum] = arts[k];
                ActionNum++;
            }
        }
        ActionNum = Go2ActionCount;
        body = arts[0].GetComponent<Transform>();
        pos0 = body.position;
        rot0 = body.rotation;
        P0.Clear();
        W0.Clear();
        arts[0].GetJointPositions(P0);
        arts[0].GetJointVelocities(W0);
    }

    private bool _isClone = false;
    void Start()
    {
        Time.fixedDeltaTime = 0.01f;
        if (train) Time.timeScale = 2;

        int numrob = 1;
        if (train) numrob = 32;
        if (!_isClone)
        {
            agentId = 0;
            agentCount = Mathf.Max(1, numrob);

            string baseName = gameObject.name;
            gameObject.name = $"{baseName}_Agent_{agentId}";

            for (int i = 1; i < numrob; i++)
            {
                GameObject clone = Instantiate(gameObject);
                clone.name = $"{baseName}_Agent_{i}";
                var cloneAgent = clone.GetComponent<MarathonGo2CPGTrain>();
                cloneAgent._isClone = true;
                cloneAgent.agentId = i;
                cloneAgent.agentCount = agentCount;
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        tp = 0;
        tt = 0;
        for (int i = 0; i < ActionNum; i++)
        {
            u[i] = 0f;
            utotal[i] = 0f;
        }

        ObservationNum = 14 + 2 * ActionNum;
        if (fixbody) arts[0].immovable = true;
        if (!fixbody)
        {
            arts[0].TeleportRoot(pos0, rot0);
            arts[0].velocity = Vector3.zero;
            arts[0].angularVelocity = Vector3.zero;
            arts[0].SetJointPositions(P0);
            arts[0].SetJointVelocities(W0);
        }

        vr = 0.8f;
        wr = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(body.InverseTransformDirection(Vector3.down));
        sensor.AddObservation(body.InverseTransformDirection(arts[0].angularVelocity));
        sensor.AddObservation(body.InverseTransformDirection(arts[0].velocity));
        for (int i = 0; i < ActionNum; i++)
        {
            sensor.AddObservation(acts[i].jointPosition[0]);
            sensor.AddObservation(acts[i].jointVelocity[0]);
        }
        sensor.AddObservation(vr);
        sensor.AddObservation(wr);
        sensor.AddObservation(k0);
        sensor.AddObservation(Mathf.Sin(Mathf.PI * tp / T1));
        sensor.AddObservation(Mathf.Cos(Mathf.PI * tp / T1));
    }

    float EulerTrans(float eulerAngle)
    {
        if (eulerAngle <= 180f) return eulerAngle;
        return eulerAngle - 360f;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        for (int i = 0; i < ActionNum; i++) utotal[i] = 0f;
        var continuousActions = actionBuffers.ContinuousActions;
        var kk = 0.9f;

        for (int i = 0; i < ActionNum; i++)
        {
            u[i] = u[i] * kk + (1f - kk) * continuousActions[i];
            utotal[i] = kb[i] * u[i];
            if (fixbody) utotal[i] = 0f;
        }

        float[] uff;
        if (gait == 0)
        {
            uff = new float[Go2ActionCount] { 0f, dh * uf1, -2f * dh * uf1, 0f, dh * uf2, -2f * dh * uf2, 0f, dh * uf2, -2f * dh * uf2, 0f, dh * uf1, -2f * dh * uf1 };
        }
        else
        {
            uff = new float[Go2ActionCount] { 0f, dh * uf1, -2f * dh * uf1, 0f, dh * uf1, -2f * dh * uf1, 0f, dh * uf2, -2f * dh * uf2, 0f, dh * uf2, -2f * dh * uf2 };
        }

        for (int i = 0; i < ActionNum; i++)
        {
            utotal[i] += uff[i] + k0 * qsit[i] * Mathf.Rad2Deg;
            SetJointTargetDeg(acts[i], utotal[i]);
        }
    }

    void SetJointTargetDeg(ArticulationBody joint, float x)
    {
        var drive = joint.xDrive;
        drive.stiffness = 50f;
        drive.damping = 2f;
        drive.forceLimit = 300f;
        drive.target = x;
        joint.xDrive = drive;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
    }

    void FixedUpdate()
    {
        tp++;
        if (tp > 0 && tp <= T1)
        {
            tp0 = tp;
            uf1 = (-Mathf.Cos(Mathf.PI * 2f * tp0 / T1) + 1f) / 2f;
            uf2 = 0f;
        }
        if (tp > T1 && tp <= 2 * T1)
        {
            tp0 = tp - T1;
            uf1 = 0f;
            uf2 = (-Mathf.Cos(Mathf.PI * 2f * tp0 / T1) + 1f) / 2f;
        }
        if (tp >= 2 * T1) tp = 0;

        tt++;

        var wel = body.InverseTransformDirection(arts[0].angularVelocity);
        var startFrameVel = Quaternion.Inverse(rot0) * arts[0].velocity;
        var startFramePos = Quaternion.Inverse(rot0) * (body.position - pos0);
        var targetForward = Vector3.ProjectOnPlane(rot0 * Vector3.forward, Vector3.up).normalized;
        var currentForward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        var headingError = Mathf.Abs(Vector3.SignedAngle(targetForward, currentForward, Vector3.up));
        var liveReward = 1f;
        var oriReward1 = -0.1f * Mathf.Abs(EulerTrans(body.eulerAngles[0]));
        var oriReward2 = -0.1f * Mathf.Min(Mathf.Abs(body.eulerAngles[2]), Mathf.Abs(body.eulerAngles[2] - 360f));
        var welReward = -2f * Mathf.Abs(wel[1] - wr);
        var velReward = startFrameVel.z - 2f * Mathf.Abs(startFrameVel.x);
        var straightReward = -0.25f * Mathf.Abs(startFramePos.x) - 0.02f * headingError;
        var reward = liveReward + oriReward1 + oriReward2 + welReward + velReward + straightReward;

        float fallang = 30f;
        if (train) fallang = 20f;
        bool isFallen = Mathf.Abs(EulerTrans(body.eulerAngles[0])) > fallang || Mathf.Abs(EulerTrans(body.eulerAngles[2])) > fallang;
        bool isYawOffCourse = headingError > 35f;
        AddReward(reward);
        if (train && (isFallen || isYawOffCourse || tt >= 1000)) EndEpisode();
    }
}
