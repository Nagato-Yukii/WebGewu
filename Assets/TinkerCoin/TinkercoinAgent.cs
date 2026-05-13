using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.Sentis;
using System.Threading.Tasks;
using System;

public struct TinkerTrainingTelemetrySnapshot
{
    public bool trainEnabled;
    public float cumulativeReward;
    public float stepReward;
    public int episodeStepCount;
    public int totalTrainingStepCount;
    public int totalFalls;
    public int totalCoins;
    public float liftAssistCurriculum;
    public float currentLiftAssistForce;
    public bool episodeEnded;
}

public class TinkercoinAgent : Agent
{
    public static event Action<TinkerTrainingTelemetrySnapshot> TelemetryUpdated;
    static bool s_HasRequestedTrainingMode;
    static bool s_RequestedTrainingMode;

    int tp = 0;
    int tt = 0;
    //int tf = 0;
    bool wasFallen = false;

    public bool enablecoin = true;
    public List<Transform> coin = new List<Transform>();
    [Header("Runtime")]
    [Tooltip("Agent instance id assigned at runtime (0 = original, 1..N = clones).")]
    public int agentId = 0;
    [Tooltip("Total number of agents (original + clones). Used for spawn layout.")]
    public int agentCount = 1;
    public bool wasd = false;
    public bool keyboard = false;
    public bool fixbody = false;
    public bool train;
    float uf1 = 0;
    float uf2 = 0;
    float[] u = new float[12] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    float[] utotal = new float[12] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    int T1 = 50;
    int tp0 = 0;
    
    Transform body;
    public int ObservationNum;
    public int ActionNum;

    List<float> P0 = new List<float>();
    List<float> W0 = new List<float>();
    List<Transform> bodypart = new List<Transform>();
    Vector3 pos0;
    Quaternion rot0;
    List<Vector3> coinp0 = new List<Vector3>();
    List<Quaternion> coinr0 = new List<Quaternion>();
    List<Transform> coinParent0 = new List<Transform>();
    ArticulationBody[] arts = new ArticulationBody[40];
    ArticulationBody[] acts = new ArticulationBody[20];
    GameObject robot;

    float[] kb = new float[12] { 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 };
    public float vr = 0;
    public float wr = 0;
    public float cr = 0;
    public bool disturb = false;

    [Header("Training Lift Assist")]
    public bool useAdaptiveLiftAssist = true;
    [Tooltip("Initial upward support as a fraction of the robot weight.")]
    [Range(0f, 1f)] public float initialLiftSupportRatio = 0.5f;
    [Tooltip("Manual curriculum scale for lift assist. 1 = full support, 0 = no support.")]
    [Range(0f, 1f)] public float liftAssistCurriculum = 1f;
    [Tooltip("Automatically decay lift assist every N total training steps when no external override is active.")]
    public bool useAutomaticLiftAssistCurriculum = true;
    [Tooltip("Decrease lift assist curriculum by the configured amount every N total Academy training steps.")]
    [Min(1)] public int liftAssistDecayStepInterval = 100000;
    [Tooltip("Do not start decaying lift assist until total training steps reach this threshold.")]
    [Min(0)] public int liftAssistDecayStartStep = 500000;
    [Tooltip("How much lift assist curriculum is removed at each decay interval.")]
    [Range(0f, 1f)] public float liftAssistDecayAmount = 0.2f;
    [Tooltip("Runtime debug value for the total training agent steps used by the automatic lift-assist curriculum.")]
    public int totalTrainingStepCount = 0;
    [Tooltip("Runtime debug value for the current automatic lift-assist curriculum stage.")]
    public int liftAssistCurriculumStage = 0;
    [Tooltip("Tilt the lift assist toward the agent's forward direction by this many degrees.")]
    [Range(0f, 60f)] public float liftAssistForwardTiltDeg = 5f;
    [Tooltip("Extra support added when the robot tilt approaches the fall angle.")]
    [Range(0f, 1f)] public float tiltSupportBoost = 0.18f;
    [Tooltip("Extra support added when the root body is moving downward.")]
    [Range(0f, 1f)] public float fallSpeedSupportBoost = 0.10f;
    [Tooltip("Runtime debug value for the total lift-assist force magnitude applied to the root body.")]
    public float currentLiftAssistForce = 0f;

    float totalRobotMass = 1f;
    float episodeLiftAssistCurriculum = 1f;
    static TinkercoinAgent s_LiftAssistCurriculumSource;
    static bool s_HasWebLiftAssistOverride;
    static float s_WebLiftAssistCurriculum = 1f;
    static int s_TotalTrainingAgentStepCount = 0;
    int m_LastLoggedLiftAssistStage = -1;
    float m_LastStepReward = 0f;
    float m_NextTelemetryTime = 0f;

    public static void SetRequestedTrainingMode(bool enabled)
    {
        s_HasRequestedTrainingMode = true;
        s_RequestedTrainingMode = enabled;
    }

    public static bool TryGetRequestedTrainingMode(out bool enabled)
    {
        enabled = s_RequestedTrainingMode;
        return s_HasRequestedTrainingMode;
    }

    public static void ClearLiftAssistCurriculumOverride()
    {
        s_HasWebLiftAssistOverride = false;
        s_WebLiftAssistCurriculum = 1f;
    }

    public override void Initialize()
    {
        EnsureLiftAssistCurriculumDefaults();
        arts = this.GetComponentsInChildren<ArticulationBody>();
        ActionNum = 0;
        for (int k = 0; k < arts.Length; k++)
        {
            if(arts[k].jointType.ToString() == "RevoluteJoint")
            {
                acts[ActionNum] = arts[k];
                print(acts[ActionNum]);
                ActionNum++;
            }
        }
        totalRobotMass = 0f;
        for (int k = 0; k < arts.Length; k++)
        {
            if (arts[k] != null)
            {
                totalRobotMass += arts[k].mass;
            }
        }
        if (totalRobotMass <= 0f) totalRobotMass = 1f;
        ActionNum = 10;
        body = arts[0].GetComponent<Transform>();
        pos0 = body.position;
        rot0 = body.rotation;
        arts[0].GetJointPositions(P0);
        arts[0].GetJointVelocities(W0);
        coinp0.Clear();
        coinr0.Clear();
        coinParent0.Clear();
        if (enablecoin)
        {
            for (int i = 0; i < coin.Count; i++)
            {
                var c = coin[i];
                coinp0.Add(c != null ? c.position : Vector3.zero);
                coinr0.Add(c != null ? c.rotation : Quaternion.identity);
                coinParent0.Add(c != null ? c.parent : null);
            }
        }
    }

    private bool _isClone = false; 
    void Start()
    {
        EnsureLiftAssistCurriculumDefaults();
        Time.fixedDeltaTime = 0.01f; 
        if (s_HasRequestedTrainingMode)
        {
            train = s_RequestedTrainingMode;
        }
        ApplyTrainingRuntimeState();
        
        int numrob=5;
        if(train)numrob=32;
        if (!_isClone) 
        {
            ClearLiftAssistCurriculumOverride();
            s_TotalTrainingAgentStepCount = 0;
            s_LiftAssistCurriculumSource = this;
            agentId = 0;
            agentCount = Mathf.Max(1, numrob);

            string baseName = gameObject.name;
            gameObject.name = $"{baseName}_Agent_{agentId}";

            for (int i = 1; i < numrob; i++)
            {
                GameObject clone = Instantiate(gameObject); 
                clone.name = $"{baseName}_Agent_{i}";
                var cloneAgent = clone.GetComponent<TinkercoinAgent>();
                cloneAgent._isClone = true;
                cloneAgent.agentId = i;
                cloneAgent.agentCount = agentCount;
            }
        }
        if(agentId>4)
        {
            enablecoin=false;
            for (int i = 0; i < coin.Count; i++)
            {
                if (coin[i] == null) continue;
                coin[i].gameObject.SetActive(false);
            }
        }
    }

    void ApplyTrainingRuntimeState()
    {
        Time.timeScale = train ? 2f : 1f;
    }

    void EnsureLiftAssistCurriculumDefaults()
    {
        useAutomaticLiftAssistCurriculum = true;
        if (liftAssistDecayStepInterval <= 0)
        {
            liftAssistDecayStepInterval = 100000;
        }

        if (liftAssistDecayStartStep < 0)
        {
            liftAssistDecayStartStep = 500000;
        }

        if (liftAssistDecayAmount <= 0f)
        {
            liftAssistDecayAmount = 0.2f;
        }
    }

    void SyncLiftAssistRuntimeState()
    {
        EnsureLiftAssistCurriculumDefaults();
        totalTrainingStepCount = GetTotalTrainingStepCount();
        int decayInterval = Mathf.Max(1, liftAssistDecayStepInterval);
        int decayStart = Mathf.Max(0, liftAssistDecayStartStep);
        liftAssistCurriculumStage = totalTrainingStepCount < decayStart
            ? 0
            : Mathf.Max(0, 1 + ((totalTrainingStepCount - decayStart) / decayInterval));
        float currentCurriculum = GetGlobalLiftAssistCurriculum();
        episodeLiftAssistCurriculum = currentCurriculum;
        liftAssistCurriculum = currentCurriculum;
    }

    float GetGlobalLiftAssistCurriculum()
    {
        if (s_HasWebLiftAssistOverride)
        {
            return Mathf.Clamp01(s_WebLiftAssistCurriculum);
        }

        if (useAutomaticLiftAssistCurriculum)
        {
            return GetScheduledLiftAssistCurriculum();
        }

        float academyCurriculum = GetCurriculumFromAcademy();
        if (academyCurriculum >= 0f)
        {
            return academyCurriculum;
        }

        if (s_LiftAssistCurriculumSource != null)
        {
            return Mathf.Clamp01(s_LiftAssistCurriculumSource.liftAssistCurriculum);
        }

        return Mathf.Clamp01(liftAssistCurriculum);
    }

    float GetCurriculumFromAcademy()
    {
        try
        {
            var environmentParameters = Academy.Instance.EnvironmentParameters;
            float curriculumValue = environmentParameters.GetWithDefault(
                "LiftAssistCurriculum",
                environmentParameters.GetWithDefault("liftAssistCurriculum", -1f));
            return curriculumValue < 0f ? -1f : Mathf.Clamp01(curriculumValue);
        }
        catch
        {
            return -1f;
        }
    }

    float GetScheduledLiftAssistCurriculum()
    {
        int totalTrainingStepCount = GetTotalTrainingStepCount();
        int decayInterval = Mathf.Max(1, liftAssistDecayStepInterval);
        int decayStart = Mathf.Max(0, liftAssistDecayStartStep);
        int decayStage = totalTrainingStepCount < decayStart
            ? 0
            : Mathf.Max(0, 1 + ((totalTrainingStepCount - decayStart) / decayInterval));
        float scheduledValue = 1f - decayStage * Mathf.Clamp01(liftAssistDecayAmount);
        if (agentId == 0 && decayStage != m_LastLoggedLiftAssistStage)
        {
            m_LastLoggedLiftAssistStage = decayStage;
            Debug.Log($"[TinkercoinAgent] LiftAssistCurriculum auto schedule => step={totalTrainingStepCount}, stage={decayStage}, value={Mathf.Clamp01(scheduledValue):F2}");
        }
        return Mathf.Clamp01(scheduledValue);
    }

    int GetTotalTrainingStepCount()
    {
        return Mathf.Max(0, s_TotalTrainingAgentStepCount);
    }

    void RecordTrainingStep()
    {
        if (!train)
        {
            return;
        }

        s_TotalTrainingAgentStepCount++;
    }

    public void SetLiftAssistCurriculumFromWeb(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        liftAssistCurriculum = clampedValue;
        episodeLiftAssistCurriculum = clampedValue;
        s_WebLiftAssistCurriculum = clampedValue;
        s_HasWebLiftAssistOverride = true;

        if (s_LiftAssistCurriculumSource == null)
        {
            s_LiftAssistCurriculumSource = this;
        }
        else
        {
            s_LiftAssistCurriculumSource.liftAssistCurriculum = clampedValue;
            s_LiftAssistCurriculumSource.episodeLiftAssistCurriculum = clampedValue;
        }

        PublishTelemetry(true, false);
    }

    public void SetTrainingEnabled(bool enabled)
    {
        EnsureLiftAssistCurriculumDefaults();
        train = enabled;
        if (enabled)
        {
            ClearLiftAssistCurriculumOverride();
        }
        ApplyTrainingRuntimeState();
        SyncLiftAssistRuntimeState();
        PublishTelemetry(true, false);
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
        wasFallen = false;
        SyncLiftAssistRuntimeState();
        for (int i = 0; i< 12; i++) u[i] = 0;
        
        
        float px;
        float py=0;
        float pz=0;
        px = 1.4f* (agentId % Mathf.Max(1, agentCount)) ;
        if(agentId>4)py=-20;
        Vector3 localOffset = new Vector3(px, py, pz);
        Vector3 worldOffset = rot0* localOffset;
        Vector3 newPos = pos0 + worldOffset;
        if (enablecoin)
        {
            // Reset coins to initial parent + initial pose, then apply this agent's world offset
            for (int i = 0; i < coin.Count; i++)
            {
                var c = coin[i];
                if (c == null) continue;

                var parent0 = i < coinParent0.Count ? coinParent0[i] : null;
                c.SetParent(parent0, worldPositionStays: true);

                Vector3 p0 = i < coinp0.Count ? coinp0[i] : c.position;
                Quaternion r0 = i < coinr0.Count ? coinr0[i] : c.rotation;
                c.position = p0 + worldOffset;
                c.rotation = r0;
            }
        }

        ObservationNum = 9 + 2 * ActionNum;
        if (fixbody) arts[0].immovable = true;
        if (!fixbody)
        {
            arts[0].TeleportRoot(newPos, rot0);
            arts[0].velocity = Vector3.zero;
            arts[0].angularVelocity = Vector3.zero;
            arts[0].SetJointPositions(P0);
            arts[0].SetJointVelocities(W0);
        }
        //if(train)
        {
            vr=0.5f;
            wr=0;

            /*if(Random.Range(0,3)==0)vr = Random.Range(0.3f,0.6f)*(Random.Range(0,2)*2-1);
            else if(Random.Range(0,2)==0) wr = Random.Range(0.3f,0.6f)*(Random.Range(0,2)*2-1);*/
            
            cr = 0.5f;//Random.Range(0.1f,0.7f);
            
        }
        /*else
        {
            vr=0;
            wr=0;
            cr=0.5f;
        }*/

        PublishTelemetry(true, false);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (body == null || arts == null || arts.Length == 0 || arts[0] == null)
        {
            return;
        }

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
        sensor.AddObservation(cr);
        sensor.AddObservation(Mathf.Sin(3.14f * 1 * tp / T1));
        sensor.AddObservation(Mathf.Cos(3.14f * 1 * tp / T1));
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
        for (int i = 0; i < 12; i++) utotal[i] = 0;
        var continuousActions = actionBuffers.ContinuousActions;
        var kk = 0.9f;
        
        for (int i = 0; i < ActionNum; i++)
        {
            u[i] = u[i] * kk + (1 - kk) * continuousActions[i];
            utotal[i] = 2 * kb[i] * u[i];
            if (fixbody) utotal[i] = 0;
        }
        

        int[] idx = new int[6] { -3, 4, 5, 8, -9, -10 };
        kb = new float[10] { 15, 30, 40, 15, 40,   15, 30, 40, 15, 40};
        T1 = 30;
        float d0 = cr*180f/3.14f;//10;
        float dh = 40;
        if(vr==0 && wr==0 && !fixbody)
        {
            /*if(Mathf.Abs(EulerTrans(body.eulerAngles[0])) < 10f && Mathf.Abs(EulerTrans(body.eulerAngles[2])) < 10f)
                dh=0;///////////////////////////////////////////////////
            else dh=40;*/

            /*if(tt>450 && tt<600)dh=0;
            if(tt>840)dh=0;
            if(keyboard)dh=0;*/
        }
        
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
        drive.stiffness = 50f;
        drive.damping = 2f;
        //drive.forceLimit = 300f;
        drive.target = x;
        joint.xDrive = drive;
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        
    }

    float GetLiftAssistForce()
    {
        if (!train || !useAdaptiveLiftAssist || fixbody || body == null || arts == null || arts.Length == 0 || arts[0] == null)
        {
            return 0f;
        }

        float curriculumScale = episodeLiftAssistCurriculum;
        if (curriculumScale <= 0f)
        {
            return 0f;
        }

        float pitch = Mathf.Abs(EulerTrans(body.eulerAngles.x));
        float roll = Mathf.Abs(EulerTrans(body.eulerAngles.z));
        float tilt01 = Mathf.InverseLerp(5f, 20f, Mathf.Max(pitch, roll));
        float downwardSpeed01 = Mathf.InverseLerp(0f, 1.5f, Mathf.Max(0f, -arts[0].velocity.y));

        float supportRatio = initialLiftSupportRatio;
        supportRatio += tiltSupportBoost * tilt01;
        supportRatio += fallSpeedSupportBoost * downwardSpeed01;
        supportRatio = Mathf.Clamp(supportRatio, 0f, 0.5f);

        return totalRobotMass * Mathf.Abs(Physics.gravity.y) * supportRatio * curriculumScale;
    }

    Vector3 GetLiftAssistDirection()
    {
        if (body == null)
        {
            return Vector3.up;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 1e-6f)
        {
            flatForward = Vector3.forward;
        }
        flatForward.Normalize();

        float tiltRad = liftAssistForwardTiltDeg * Mathf.Deg2Rad;
        return (Vector3.up * Mathf.Cos(tiltRad) + flatForward * Mathf.Sin(tiltRad)).normalized;
    }

    void ApplyAdaptiveLiftAssist()
    {
        float verticalSupportForce = GetLiftAssistForce();
        if (verticalSupportForce <= 0f)
        {
            currentLiftAssistForce = 0f;
            return;
        }

        Vector3 forceDir = GetLiftAssistDirection().normalized;
        float upDot = Vector3.Dot(forceDir, Vector3.up);
        if (upDot <= 0.01f)
        {
            forceDir = Vector3.up;
            upDot = 1f;
        }

        currentLiftAssistForce = verticalSupportForce / upDot;
        arts[0].AddForce(forceDir * currentLiftAssistForce, ForceMode.Force);
    }

    void AttachCoinsToBody()
    {
        if (!enablecoin) return;
        if (body == null) return;
        if (coin == null || coin.Count == 0) return;

        // n = how many coins are already attached to body (for stacking)
        int n = 0;
        for (int i = 0; i < coin.Count; i++)
        {
            var c = coin[i];
            if (c == null) continue;

            // Only attach once; once attached, keep it as a child of body.
            if (c.parent != body && body.position.x < c.position.x)
            {
                c.SetParent(body, worldPositionStays: false);
                c.localRotation = Quaternion.identity;
                TinkercoinFallCounter.ReportCoinCollected(this);
            }

            if (c.parent == body)
            {
                c.localPosition = new Vector3(0f, 0.05f * n + 0.27f, 0f);
                n++;
            }
        }
    }

    void FixedUpdate()
    {
        if (enablecoin) AttachCoinsToBody();
        RecordTrainingStep();
        SyncLiftAssistRuntimeState();

        tp++;
        if (tp > 0 && tp <= T1)
        {
            tp0 = tp;
            uf1 = (-Mathf.Cos(3.14f * 2 * tp0 / T1) + 1f) / 2f;
            uf2 = 0;
        }
        if (tp > T1 && tp <= 2 * T1)
        {
            tp0 = tp - T1;
            uf1 = 0;
            uf2 = (-Mathf.Cos(3.14f * 2 * tp0 / T1) + 1f) / 2f;
        }
        if (tp >= 2 * T1) tp = 0;

        tt++;
        if(keyboard && wasd)
        {
            float v=0.01f;
            if(Input.GetKey(KeyCode.W))vr=Mathf.MoveTowards(vr, 0.6f, v);
            else if(Input.GetKey(KeyCode.S))vr=Mathf.MoveTowards(vr, -0.6f, v);
            else vr=Mathf.MoveTowards(vr, 0f, v);

            if(Input.GetKey(KeyCode.A))wr=Mathf.MoveTowards(wr, -0.6f, v);
            else if(Input.GetKey(KeyCode.D))wr=Mathf.MoveTowards(wr, 0.6f, v);
            else wr=Mathf.MoveTowards(wr, 0f, v);

            //if(Input.GetKey(KeyCode.Q))cr=Mathf.MoveTowards(cr, 0.1f, v/3f);
            //else if(Input.GetKey(KeyCode.E))cr=Mathf.MoveTowards(cr, 0.7f, v/3f);
        }
        if(keyboard && !wasd)
        {
            float v=0.01f;
            if(Input.GetKey(KeyCode.UpArrow))vr=Mathf.MoveTowards(vr, 0.6f, v);
            else if(Input.GetKey(KeyCode.DownArrow))vr=Mathf.MoveTowards(vr, -0.6f, v);
            else vr=Mathf.MoveTowards(vr, 0f, v);

            if(Input.GetKey(KeyCode.LeftArrow))wr=Mathf.MoveTowards(wr, -0.6f, v);
            else if(Input.GetKey(KeyCode.RightArrow))wr=Mathf.MoveTowards(wr, 0.6f, v);
            else wr=Mathf.MoveTowards(wr, 0f, v);

            //if(Input.GetKey(KeyCode.Q))cr=Mathf.MoveTowards(cr, 0.1f, v/3f);
            //else if(Input.GetKey(KeyCode.E))cr=Mathf.MoveTowards(cr, 0.7f, v/3f);
        }

        Vector3 randomForce=new Vector3(Random.Range(-1f, 1f),0,Random.Range(-1f, 1f));
        if(Random.Range(0, 100)==1 && disturb)arts[0].AddForce(2*randomForce, ForceMode.Impulse);
        ApplyAdaptiveLiftAssist();

        var vel = body.InverseTransformDirection(arts[0].velocity);
        var wel = body.InverseTransformDirection(arts[0].angularVelocity);
        var live_reward = 1f;
        var ori_reward1 = -0.1f * Mathf.Abs(EulerTrans(body.eulerAngles[0]));
        var ori_reward2 = -0.1f * Mathf.Min(Mathf.Abs(body.eulerAngles[2]), Mathf.Abs(body.eulerAngles[2] - 360f));
        var wel_reward = 1 - 4*Mathf.Abs(wel[1] - wr);
        var vel_reward = 1 + 0*vel[2] - 4*Mathf.Abs(vel[2] - vr) + 0*Mathf.Clamp(vel[2],-5f,1.5f) - Mathf.Abs(vel[0]);
        var reward = live_reward + (ori_reward1 + ori_reward2) * 1 +  wel_reward * 1 + vel_reward;
       
        float fallang=30f;
        if(train)fallang=40f;
        bool isFallen = Mathf.Abs(EulerTrans(body.eulerAngles[0])) > fallang || Mathf.Abs(EulerTrans(body.eulerAngles[2])) > fallang;
        if (isFallen)
        {
            // Count a "fall" only once per transition into fallen state (not every frame).
            if (!wasFallen)
            {
                if (agentId <= 4)
                {
                    TinkercoinFallCounter.ReportFall(this);
                }
            }
            wasFallen = true;
            //tf++;
            //if(train)
            reward=0;
            m_LastStepReward = reward;
            AddReward(reward);
            PublishTelemetry(true, true);
            //if(tf>100)EndEpisode();
            EndEpisode();
            return;
        }
        else
        {
            wasFallen = false;
        }
        m_LastStepReward = reward;
        AddReward(reward);
        if(train && tt>1000)
        {
            PublishTelemetry(true, true);
            EndEpisode();
            return;
        }

        PublishTelemetry();
    }

    void PublishTelemetry(bool force = false, bool episodeEnded = false)
    {
        if (agentId != 0)
        {
            return;
        }

        if (!force && Time.unscaledTime < m_NextTelemetryTime)
        {
            return;
        }

        m_NextTelemetryTime = Time.unscaledTime + 0.2f;
        SyncLiftAssistRuntimeState();
        TelemetryUpdated?.Invoke(new TinkerTrainingTelemetrySnapshot
        {
            trainEnabled = train,
            cumulativeReward = GetCumulativeReward(),
            stepReward = m_LastStepReward,
            episodeStepCount = tt,
            totalTrainingStepCount = GetTotalTrainingStepCount(),
            totalFalls = TinkercoinFallCounter.TotalFalls,
            totalCoins = TinkercoinFallCounter.TotalCoins,
            liftAssistCurriculum = GetGlobalLiftAssistCurriculum(),
            currentLiftAssistForce = currentLiftAssistForce,
            episodeEnded = episodeEnded
        });
    }

}
