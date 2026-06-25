using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System;
public class MarathonGo2CPGView : Agent
{
    [Header("CPG Parameters (实时调节)")]
    public int T1 = 32;               // 步态周期参数
    public float dh = 22.9f;          // 摆动幅度参数 (原 0.4 * 180 / 3.14)
    public float k0 = 0.5f;           // 基础蹲姿比例
    [Range(0, 1)] public int gait = 0;// 步态模式 (0 或 1)

    int tp = 0;
    float uf1 = 0;
    float uf2 = 0;
    
    int ActionNum = 0;
    ArticulationBody[] acts = new ArticulationBody[12];

    // 预设的基础蹲姿角度 (原为弧度)
    float[] qsit = new float[12] { 0f, 1.4f, -2.3f, 0f, 1.4f, -2.3f, 0f, 1.4f, -2.3f, 0f, 1.4f, -2.3f };

    public void SetT1(int value)
    {
        T1 = Mathf.Max(1, value);
    }

    public void SetDh(float value)
    {
        dh = Mathf.Max(0f, value);
    }

    public void SetK0(float value)
    {
        k0 = Mathf.Max(0f, value);
    }

    public void SetGait(int value)
    {
        gait = Mathf.Clamp(value, 0, 1);
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
        float[] uff = new float[12];

        // 不同的对角线步态映射
        if (gait == 0)
        {
            uff = new float[12] { 0, dh * uf1, (dh * uf1) * -2, 0, dh * uf2, (dh * uf2) * -2, 0, dh * uf2, (dh * uf2) * -2, 0, dh * uf1, (dh * uf1) * -2 };
        }
        else if (gait == 1)
        {
            uff = new float[12] { 0, dh * uf1, (dh * uf1) * -2, 0, dh * uf1, (dh * uf1) * -2, 0, dh * uf2, (dh * uf2) * -2, 0, dh * uf2, (dh * uf2) * -2 };
        }

        // 叠加 CPG 摆动波形与基础蹲姿偏置
        for (int i = 0; i < 12; i++) 
        {
            // 原代码使用 180f/3.14f 进行弧度转角度，这里直接用内置常量 Mathf.Rad2Deg 更标准
            utotal[i] = uff[i] + k0 * qsit[i] * Mathf.Rad2Deg;
        }

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
        // 保留了原脚本中特定的 PD 参数
        drive.stiffness = 50f;
        drive.damping = 2f;
        drive.target = x;
        joint.xDrive = drive;
    }
}
