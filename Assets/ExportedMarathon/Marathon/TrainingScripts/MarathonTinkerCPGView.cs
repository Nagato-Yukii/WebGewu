using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System;

public class MarathonTinkerCPGView : Agent
{
    [Header("CPG Parameters (实时调节)")]
    public int T1 = 25;            // 步态周期参数
    public float d0 = 30f;         // 基础角度偏移
    public float dh = 20f;         // 摆动幅度参数

    int tp = 0;
    float uf1 = 0;
    float uf2 = 0;
    
    int ActionNum = 0;
    ArticulationBody[] acts = new ArticulationBody[12];

    public void SetT1(int value)
    {
        T1 = Mathf.Max(1, value);
    }

    public void SetD0(float value)
    {
        d0 = value;
    }

    public void SetDh(float value)
    {
        dh = Mathf.Max(0f, value);
    }

    public void ResetPhase()
    {
        tp = 0;
        uf1 = 0f;
        uf2 = 0f;
    }

    public override void Initialize()
    {
        // 1. 获取所有旋转关节
        var arts = this.GetComponentsInChildren<ArticulationBody>();
        ActionNum = 0;
        for (int k = 0; k < arts.Length; k++)
        {
            if (arts[k].jointType.ToString() == "RevoluteJoint")
            {
                acts[ActionNum] = arts[k];
                ActionNum++;
            }
        }
        
        // 2. 强制锁定根节点，让机器人悬空不动
        if (arts.Length > 0)
        {
            arts[0].immovable = true;
        }
    }

    void Start()
    {
        Time.fixedDeltaTime = 0.01f;
    }

    void FixedUpdate()
    {
        // ================= 1. CPG 信号生成 =================
        tp++;
        if (tp > 0 && tp <= T1)
        {
            int tp0 = tp;
            uf1 = (-Mathf.Cos(Mathf.PI * 2 * tp0 / T1) + 1f) / 2f;
            uf2 = 0;
        }
        else if (tp > T1 && tp <= 2 * T1)
        {
            int tp0 = tp - T1;
            uf1 = 0;
            uf2 = (-Mathf.Cos(Mathf.PI * 2 * tp0 / T1) + 1f) / 2f;
        }
        if (tp >= 2 * T1) tp = 0;

        // ================= 2. 信号映射到关节 =================
        float[] utotal = new float[12];
        int[] idx = new int[6] { -2, -3, 4, 7, 8, -9 };
        
        utotal[Mathf.Abs(idx[0])] += (dh * uf1 + d0) * Mathf.Sign(idx[0]);
        utotal[Mathf.Abs(idx[1])] -= 2 * (dh * uf1 + d0) * Mathf.Sign(idx[1]);
        utotal[Mathf.Abs(idx[2])] += (dh * uf1 + d0) * Mathf.Sign(idx[2]);
        utotal[Mathf.Abs(idx[3])] += (dh * uf2 + d0) * Mathf.Sign(idx[3]);
        utotal[Mathf.Abs(idx[4])] -= 2 * (dh * uf2 + d0) * Mathf.Sign(idx[4]);
        utotal[Mathf.Abs(idx[5])] += (dh * uf2 + d0) * Mathf.Sign(idx[5]);

        // ================= 3. 驱动物理关节 =================
        for (int i = 0; i < ActionNum; i++) 
        {
            if (acts[i] != null) 
            {
                SetJointTargetDeg(acts[i], utotal[i]);
            }
        }
    }

    void SetJointTargetDeg(ArticulationBody joint, float x)
    {
        var drive = joint.xDrive;
        drive.stiffness = 2000f;
        drive.damping = 100f;
        drive.forceLimit = 300f;
        drive.target = x;
        joint.xDrive = drive;
    }
}
