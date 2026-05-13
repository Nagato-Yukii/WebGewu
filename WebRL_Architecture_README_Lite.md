# WebRL Architecture (Lite)

`WebRL_workspace` 当前采用 **Facade + POCO** 架构：  
- **Facade**（`MonoBehaviour/Agent`）负责 Unity 生命周期、Inspector 字段、跨模块编排。  
- **POCO**（纯 C#）负责业务逻辑（解析、装配、计算、驱动），便于维护和测试。  

---

## 1. 关键入口（先看这 4 个）

- `Scripts/WebRtcModelCommandBridge.cs`  
  WebRTC 入口，接收网页消息并转给场景/实验层。

- `SceneDirector.cs`  
  场景总调度，负责场景切换、命令队列、训练状态、相机绑定。

- `Scripts/ExperimentDirector.cs`  
  实验装配入口，负责加载机器人与配置、解析技能、生成实例并挂接 Agent。

- `Scripts/NeuralVesselAgent.cs`  
  ML-Agents 入口，负责观测、动作应用、步态与物理时序。

---

## 2. Core 拆分目录

- `Scripts/SceneDirectorCore/*`：场景路由/切换/队列/训练/相机。  
- `Scripts/ExperimentDirectorCore/*`：命令解析/资源加载/技能解析/出生位/相机解析/运行时装配。  
- `Scripts/NeuralVesselCore/*`：观测构建/动作积分/步态合成/关节驱动/状态恢复。  
- `Scripts/WebRtcBridgeMessageProcessor.cs` + `Scripts/WebRtcBridgeTelemetrySender.cs`：协议处理与遥测发送。  

---

## 3. 新增机器人怎么改（最短路径）

1. 扩展 `RobotConfig`/`RobotSpecies` 与资源（Prefab + Data）。  
2. 改 `ExperimentDirectorSkillResolver.cs`，加入新 skillType 映射。  
3. 改 `NeuralVesselGaitSynthesizer.cs`，加入该物种步态分支。  
4. 必要时改 `WorldLineDebugger.cs` 增加调试按钮。  

---

## 4. 最小回归检查

1. 场景流：`menu -> WebRL_Laboratory/WebTinkerRL -> menu`。  
2. 运行流：至少加载 1 个机器人并切换 2 个技能。  
3. Agent 流：观测维度正确、动作驱动正常、Episode 重置可恢复初始状态。  
4. WebRTC 流：命令可达、遥测可回发。  

完整版本见：`WebRL_Architecture_README.md`  
