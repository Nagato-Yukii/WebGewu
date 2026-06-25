using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;
using System.Collections.Generic;

public class MarathonTinkerCPGTrain : Agent
{
    const int TinkerActionCount = 10;

    int tp = 0;
    int tt = 0;
    //int tf = 0;

    public bool fixbody = false;
    public bool train;
    float uf1 = 0;
    float uf2 = 0;
    readonly float[] u = new float[TinkerActionCount];
    readonly float[] utotal = new float[TinkerActionCount];
    int tp0 = 0;

    [Header("CPG Direct Controls")]
    public int T1 = 20;
    public float d0 = 30f;
    public float dh = 40f;
    
    Transform body;
    public int ObservationNum;
    public int ActionNum;

    List<float> P0 = new List<float>();
    List<float> W0 = new List<float>();
    Vector3 pos0;
    Quaternion rot0;
    ArticulationBody[] arts = new ArticulationBody[40];
    ArticulationBody[] acts = new ArticulationBody[TinkerActionCount];

    readonly float[] kb = new float[TinkerActionCount] { 15, 30, 40, 15, 40,   15, 30, 40, 15, 40 };
    public float vr = 0;
    public float wr = 0;

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
        arts = this.GetComponentsInChildren<ArticulationBody>();
        ActionNum = 0;
        for (int k = 0; k < arts.Length; k++)
        {
            if(arts[k].jointType.ToString() == "RevoluteJoint" && ActionNum < TinkerActionCount)
            {
                acts[ActionNum] = arts[k];
                ActionNum++;
            }
        }
        ActionNum = 10;
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
        if(train)Time.timeScale = 2;   
        
        int numrob=1;
        if(train)numrob=32;
        //if(fixbody || keyboard)numrob=0;
        if (!_isClone) 
        {
            // Original agent gets id 0; clones get 1..(numrob-1)
            agentId = 0;
            agentCount = Mathf.Max(1, numrob);

            // Keep a stable base name so clones don't inherit already-suffixed names
            string baseName = gameObject.name;
            gameObject.name = $"{baseName}_Agent_{agentId}";

            for (int i = 1; i < numrob; i++)
            {
                GameObject clone = Instantiate(gameObject); 
                //clone.transform.position = transform.position + new Vector3(i * 2f, 0, 0);
                clone.name = $"{baseName}_Agent_{i}";
                var cloneAgent = clone.GetComponent<MarathonTinkerCPGTrain>();
                cloneAgent._isClone = true;
                cloneAgent.agentId = i;
                cloneAgent.agentCount = agentCount;
            }
        }
    }
    void ChangeLayerRecursively(GameObject obj, int targetLayer)
    {
        obj.layer = targetLayer;
        foreach (Transform child in obj.transform)ChangeLayerRecursively(child.gameObject, targetLayer);
    }

    public override void OnEpisodeBegin()
    {
        tp = 0;
        tt = 0;
        //tf = 0;
        for (int i = 0; i < ActionNum; i++)
        {
            u[i] = 0;
            utotal[i] = 0;
        }
        
        
        Vector3 newPos = pos0;

        ObservationNum = 15 + 2 * ActionNum;
        if (fixbody) arts[0].immovable = true;
        if (!fixbody)
        {
            arts[0].TeleportRoot(newPos, rot0);
            arts[0].velocity = Vector3.zero;
            arts[0].angularVelocity = Vector3.zero;
            arts[0].SetJointPositions(P0);
            arts[0].SetJointVelocities(W0);
        }
        vr = 0.6f;
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
        sensor.AddObservation(d0 / 180f);
        sensor.AddObservation(Mathf.Sin(Mathf.PI * tp / T1));
        sensor.AddObservation(Mathf.Cos(Mathf.PI * tp / T1));
    }
    float EulerTrans(float eulerAngle)
    {
        if (eulerAngle <= 180)
            return eulerAngle;
        else
            return eulerAngle - 360f;
    }
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        for (int i = 0; i < ActionNum; i++) utotal[i] = 0;
        var continuousActions = actionBuffers.ContinuousActions;
        var kk = 0.9f;
        
        for (int i = 0; i < ActionNum; i++)
        {
            u[i] = u[i] * kk + (1 - kk) * continuousActions[i];
            utotal[i] = 2 * kb[i] * u[i];
            if (fixbody) utotal[i] = 0;
        }
        

        int[] idx = new int[6] { -3, 4, 5, 8, -9, -10 };
        
        utotal[Mathf.Abs(idx[0]) - 1] += (dh * uf1 + d0) * Mathf.Sign(idx[0]);
        utotal[Mathf.Abs(idx[1]) - 1] += 2 * (dh * uf1 + d0) * Mathf.Sign(idx[1]);
        utotal[Mathf.Abs(idx[2]) - 1] += (dh * uf1 + d0) * Mathf.Sign(idx[2]);
        utotal[Mathf.Abs(idx[3]) - 1] += (dh * uf2 + d0) * Mathf.Sign(idx[3]);
        utotal[Mathf.Abs(idx[4]) - 1] += 2 * (dh * uf2 + d0) * Mathf.Sign(idx[4]);
        utotal[Mathf.Abs(idx[5]) - 1] += (dh * uf2 + d0) * Mathf.Sign(idx[5]);

        //utotal[1] = Mathf.Clamp(utotal[1], -200f, 0f);
        //utotal[7] = Mathf.Clamp(utotal[7], 0f, 200f);
        for (int i = 0; i < ActionNum; i++) SetJointTargetDeg(acts[i], utotal[i]);
    }
    void SetJointTargetDeg(ArticulationBody joint, float x)
    {
        var drive = joint.xDrive;
        //drive.stiffness = 2000f;
        //drive.damping = 100f;
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
            uf1 = (-Mathf.Cos(Mathf.PI * 2 * tp0 / T1) + 1f) / 2f;
            uf2 = 0;
        }
        if (tp > T1 && tp <= 2 * T1)
        {
            tp0 = tp - T1;
            uf1 = 0;
            uf2 = (-Mathf.Cos(Mathf.PI * 2 * tp0 / T1) + 1f) / 2f;
        }
        if (tp >= 2 * T1) tp = 0;

        tt++;

        var wel = body.InverseTransformDirection(arts[0].angularVelocity);
        var startFrameVel = Quaternion.Inverse(rot0) * arts[0].velocity;
        var startFramePos = Quaternion.Inverse(rot0) * (body.position - pos0);
        var targetForward = Vector3.ProjectOnPlane(rot0 * Vector3.forward, Vector3.up).normalized;
        var currentForward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        var headingError = Mathf.Abs(Vector3.SignedAngle(targetForward, currentForward, Vector3.up));
        var live_reward = 1f;
        var ori_reward1 = -0.1f * Mathf.Abs(EulerTrans(body.eulerAngles[0]));
        var ori_reward2 = -0.1f * Mathf.Min(Mathf.Abs(body.eulerAngles[2]), Mathf.Abs(body.eulerAngles[2] - 360f));
        var wel_reward = -2f * Mathf.Abs(wel[1] - wr);
        var vel_reward = startFrameVel.z - 2f * Mathf.Abs(startFrameVel.x);
        var straight_reward = -0.25f * Mathf.Abs(startFramePos.x) - 0.02f * headingError;
        var reward = live_reward + (ori_reward1 + ori_reward2) * 1 + wel_reward + vel_reward + straight_reward;
       
        float fallang=30f;
        if(train)fallang=20f;
        bool isFallen = Mathf.Abs(EulerTrans(body.eulerAngles[0])) > fallang || Mathf.Abs(EulerTrans(body.eulerAngles[2])) > fallang;
        bool isYawOffCourse = headingError > 35f;
        AddReward(reward);
        if (train && (isFallen || isYawOffCourse || tt >= 1000)) EndEpisode();
    }

}
