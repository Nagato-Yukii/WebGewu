using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System;

public class MarathonG1CPGView : Agent
{
    [Header("CPG Parameters (双足步态调节)")]
    public int T1 = 40;              // 步态半周期
    public float d0 = 10f;           // 基础关节角度偏移
    public float dh = 30f;           // 腿部摆动幅度
    
    [Header("Arm Kinematics (手臂协同)")]
    [Range(0f, 3f)]
    public float vr = 1.5f;          // 模拟前进速度，直接决定手臂摆动的幅度

    int tp = 0;
    float uf1 = 0;
    float uf2 = 0;
    
    int ActionNum = 0;
    ArticulationBody[] acts = new ArticulationBody[20]; // 腿部关节数组
    ArticulationBody[] arms = new ArticulationBody[10]; // 手臂关节数组

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

    public void SetVr(float value)
    {
        vr = Mathf.Clamp(value, 0f, 3f);
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
        var allArts = this.GetComponentsInChildren<ArticulationBody>();
        ActionNum = 0;
        int armnum = 0;
        
        for (int k = 0; k < allArts.Length; k++)
        {
            if (allArts[k].jointType.ToString() == "RevoluteJoint")
            {
                string jointName = allArts[k].gameObject.name.ToLower();
                // 筛选腿部关节 (Hip, Knee, Ankle)
                if (jointName.Contains("hip") || jointName.Contains("knee") || jointName.Contains("ankle"))
                {
                    acts[ActionNum] = allArts[k];
                    ActionNum++;
                }
                // 筛选手臂关节 (Shoulder, Elbow, Wrist)
                else if (jointName.Contains("shoulder") || jointName.Contains("elbow") || jointName.Contains("wrist"))
                {
                    arms[armnum] = allArts[k];
                    armnum++;
                }
            }
        }
        
        // 2. 强制锁定根节点，让机器人悬空不动
        if (allArts.Length > 0)
        {
            allArts[0].immovable = true;
        }
    }

    void Start()
    {
        Time.fixedDeltaTime = 0.01f;
    }

    void FixedUpdate()
    {
        // ================= 1. 腿部 CPG 信号生成 =================
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

        // ================= 2. 腿部信号映射 =================
        float[] utotal = new float[12];
        
        // 原脚本中写死的映射索引 (映射到双腿的特定关节)
        int[] idx = new int[6] { -1, -4, -5, -7, -10, -11 };
        
        // 如果模拟速度 vr 归零，则停止摆腿
        float current_dh = (vr == 0) ? 0 : dh;

        utotal[Mathf.Abs(idx[0]) - 1] += (current_dh * uf1 + d0) * Mathf.Sign(idx[0]);
        utotal[Mathf.Abs(idx[1]) - 1] -= 2 * (current_dh * uf1 + d0) * Mathf.Sign(idx[1]);
        utotal[Mathf.Abs(idx[2]) - 1] += (current_dh * uf1 + d0) * Mathf.Sign(idx[2]);
        
        utotal[Mathf.Abs(idx[3]) - 1] += (current_dh * uf2 + d0) * Mathf.Sign(idx[3]);
        utotal[Mathf.Abs(idx[4]) - 1] -= 2 * (current_dh * uf2 + d0) * Mathf.Sign(idx[4]);
        utotal[Mathf.Abs(idx[5]) - 1] += (current_dh * uf2 + d0) * Mathf.Sign(idx[5]);

        // 原代码中的硬限幅保护
        utotal[1] = Mathf.Clamp(utotal[1], -200f, 0f);
        utotal[7] = Mathf.Clamp(utotal[7], 0f, 200f);

        // ================= 3. 手臂协同信号生成 =================
        // 手臂直接使用 Mathf.Sin 随时间摆动，摆幅受模拟速度 vr 影响
        float armSwing = Mathf.Clamp(vr, 0, 3) * 20 * Mathf.Sin(Mathf.PI * tp / T1);
        float[] uarm = new float[10] { 
            armSwing, -10, 0, 80, 0,      // 左臂/右臂
            -armSwing, 10, 0, 80, 0       // 对称的反向摆动
        };

        // ================= 4. 驱动物理关节 =================
        // 驱动腿部
        for (int i = 0; i < 12; i++) 
        {
            if (acts[i] != null) SetJointTargetDeg(acts[i], utotal[i]);
        }
        // 驱动手臂
        for (int i = 0; i < 10; i++) 
        {
            if (arms[i] != null) SetJointTargetDeg(arms[i], uarm[i]);
        }
    }

    void SetJointTargetDeg(ArticulationBody joint, float x)
    {
        var drive = joint.xDrive;
        drive.stiffness = 100f;  // 较高的刚度以支撑双足
        drive.damping = 5f;
        drive.target = x;
        joint.xDrive = drive;
    }
}
