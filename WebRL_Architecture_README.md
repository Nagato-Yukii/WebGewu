# WebRL Workspace Architecture README

## 1) 重构概述

`WebRL_workspace` 在重构前的主要问题是典型 God Class：  
- 单文件同时承担网络协议、场景切换、资源装配、ML 推理、物理驱动等多重职责。  
- 高耦合导致修改一个行为容易影响多个模块，回归成本高。  
- 方法过长、重复逻辑多，定位问题和扩展新机器人都非常困难。  

本次采用 `Facade + POCO` 模式后：  
- **Facade（MonoBehaviour/Agent）** 只保留 Unity 生命周期、Inspector 引用和对外接口。  
- **POCO（纯 C# 类）** 承担可测试、可复用的业务逻辑。  
- 在保持功能零损耗、时序不变、序列化兼容的前提下，整体实现了高内聚低耦合。  

---

## 2) 核心模块层级结构图

```text
WebRL_workspace
├─ SceneDirector.cs                                  (Facade: 场景总调度)
├─ Scripts
│  ├─ WebRtcModelCommandBridge.cs                    (Facade: WebRTC 桥接入口)
│  ├─ ExperimentDirector.cs                          (Facade: 实验装配入口)
│  ├─ NeuralVesselAgent.cs                           (Facade: ML-Agents 执行入口)
│  ├─ WorldLineDebugger.cs                           (调试工具)
│  ├─ Control
│  │  ├─ DynamicCameraTracker.cs                     (相机跟随控制)
│  │  └─ Mouse_CameraRotate.cs
│  ├─ SceneDirectorCore
│  │  ├─ SceneDirectorSceneRouter.cs
│  │  ├─ SceneDirectorCommandQueueManager.cs
│  │  ├─ SceneDirectorTrainingCoordinator.cs
│  │  ├─ SceneDirectorCameraBinder.cs
│  │  └─ SceneDirectorSceneSwitcher.cs
│  ├─ ExperimentDirectorCore
│  │  ├─ ExperimentDirectorCommandParser.cs
│  │  ├─ ExperimentDirectorResourceLoader.cs
│  │  ├─ ExperimentDirectorSkillResolver.cs
│  │  ├─ ExperimentDirectorSpawnPoseCalculator.cs
│  │  ├─ ExperimentDirectorCameraTrackerResolver.cs
│  │  └─ ExperimentDirectorRuntimeAgentAssembler.cs
│  ├─ NeuralVesselCore
│  │  ├─ NeuralVesselObservationBuilder.cs
│  │  ├─ NeuralVesselJointDriver.cs
│  │  ├─ NeuralVesselActionIntegrator.cs
│  │  ├─ NeuralVesselGaitSynthesizer.cs
│  │  └─ NeuralVesselStateRestorer.cs
│  ├─ WebRtcBridgeMessageProcessor.cs
│  └─ WebRtcBridgeTelemetrySender.cs
└─ Runtime
   ├─ Protocol/Envelope.cs
   └─ Handlers/*.cs                                  (协议命令处理器)
```

### 分层说明

1. **WebRtcModelCommandBridge 层（网络与协议解耦）**  
   Facade 负责通道生命周期与 Unity 事件，POCO 负责消息解析、命令分发、遥测发送。

2. **SceneDirector 层（场景切换、Web 队列、相机绑定）**  
   Facade 维持公共 API 和协程时序，POCO 按职责拆分场景路由、加载切换、命令排队、训练状态协调、相机绑定。

3. **ExperimentDirector 层（预制体加载、技能装配、生成点计算）**  
   Facade 负责串联流程；POCO 负责命令解析、资源查找、技能解析、出生位计算、相机跟踪器选择、运行时 Agent 装配。

4. **NeuralVesselAgent 层（步态合成、物理动作积分、状态快照）**  
   Facade 保留 ML-Agents 生命周期方法；POCO 承担观测组装、动作积分、步态相位、关节驱动、状态缓存恢复。

---

## 3) POCO 文件职责一览（每个一句话）

### WebRTC Bridge Core

- `WebRtcBridgeMessageProcessor.cs`：解析 WebRTC 入站消息并路由到 Scene/Experiment 层命令入口。  
- `WebRtcBridgeTelemetrySender.cs`：统一封装延迟与训练状态等遥测消息发送逻辑。  

### SceneDirector Core

- `SceneDirectorSceneRouter.cs`：根据目标标识决策应进入哪个场景分支。  
- `SceneDirectorCommandQueueManager.cs`：在目标系统未就绪时缓存 Web 命令并在可用时回放。  
- `SceneDirectorTrainingCoordinator.cs`：协调训练开关、Trainer 生命周期与跨场景训练状态一致性。  
- `SceneDirectorCameraBinder.cs`：管理全局/场景相机跟踪器的绑定与同步。  
- `SceneDirectorSceneSwitcher.cs`：封装异步场景加载、激活、回退菜单流程与重复请求防抖。  

### ExperimentDirector Core

- `ExperimentDirectorCommandParser.cs`：把字符串命令解析成结构化 `WebCommand` 并做基础合法性判断。  
- `ExperimentDirectorResourceLoader.cs`：按机器人名加载 Prefab 与配置资产。  
- `ExperimentDirectorSkillResolver.cs`：根据机器人种类与 skillType 解析具体 Skill 配置与槽位。  
- `ExperimentDirectorSpawnPoseCalculator.cs`：计算机器人生成位置和朝向。  
- `ExperimentDirectorCameraTrackerResolver.cs`：选择最合适的 `DynamicCameraTracker` 并回填绑定。  
- `ExperimentDirectorRuntimeAgentAssembler.cs`：确保运行实例具备并正确获取 `NeuralVesselAgent`。  

### NeuralVessel Core

- `NeuralVesselObservationBuilder.cs`：按既定顺序组装观测向量（姿态、角速度、线速度、关节状态、补零位）。  
- `NeuralVesselJointDriver.cs`：统一关节驱动参数解析与目标角写入。  
- `NeuralVesselActionIntegrator.cs`：执行动作滤波与积分（`u/ut/utt/utotal`）并控制缓冲区复用。  
- `NeuralVesselGaitSynthesizer.cs`：维护步态相位状态并根据物种/技能注入步态偏移。  
- `NeuralVesselStateRestorer.cs`：缓存初始姿态快照并在回合重置时无偏恢复。  

---

## 4) 扩展指南：新增一种机器人（示例：六足 Hexapod）

下面是推荐修改顺序（尽量小步提交，每步可回归）：

1. **数据层扩展（先做）**  
- 扩展 `RobotSpecies`（如新增 `Hexapod`）。  
- 在 `RobotConfig` 中补齐六足所需参数（`idxParams` 映射、默认相位权重、驱动参数等）。  
- 新增对应 `RobotData` 资产与 `Resources/Robots` 预制体。  

2. **技能解析扩展**  
- 修改 `ExperimentDirectorSkillResolver.cs`：新增 hexapod 技能类型到 `SkillConfig`/`SkillSlot` 的映射。  
- 如需新增技能槽位，扩展 `SkillSlot` 枚举并保持旧值兼容。  

3. **步态逻辑扩展（核心）**  
- 修改 `NeuralVesselGaitSynthesizer.cs`：  
  - 在 `ApplySpeciesGait` 中新增 `Hexapod` 分支。  
  - 新增六足步态相位模式（如 tripod gait / wave gait）并复用现有 `ApplyTripletOffset` 或新增 hexapod 专用偏移函数。  
  - 保持 `idxParams` 映射规则一致，避免关节索引偏移。  

4. **实验装配与调试入口**  
- `ExperimentDirectorResourceLoader.cs`：确认资源命名规范可加载新机器人。  
- `WorldLineDebugger.cs`：按需加入新机器人与技能快捷入口（仅调试增强，不影响正式逻辑）。  

5. **回归验证（必须）**  
- 场景切换：menu -> lab -> menu。  
- 运行时：加载 hexapod 并切换至少 2 个技能。  
- Agent：验证 `CollectObservations` 维度、`OnActionReceived` 驱动、`OnEpisodeBegin` 恢复。  
- WebRTC：验证命令链路和遥测链路都能覆盖 hexapod。  

---

## 附：本架构的协作约束（建议团队长期遵守）

- Facade 脚本保留 Unity 生命周期、序列化字段、跨模块编排，不承载复杂业务细节。  
- 新增业务优先落到 POCO；只有和 Unity 对象生命周期强绑定时才放在 Facade。  
- 不在 `Update/FixedUpdate` 内做无必要分配和 `GetComponent` 查找。  
- 任何影响模型推理/物理时序的修改必须附带最小回归记录。  
