# gewu (WebRL Unity Side)

## 项目定位

本仓库当前聚焦 Unity 侧 `WebRL_workspace`：  
- 通过 WebRTC 接收 Web 端控制命令并回传遥测。  
- 管理场景切换（`GlobalManager` / `WebRL_Laboratory` / `RobotHeTuRender` / `WebTinkerRL`）。  
- 运行 WebRL 机器人装配与 ML-Agents 推理/训练流程。  

---

## 当前架构（已完成重构）

项目已从 God Class 改造为 **Facade + POCO**：

- Facade（Unity 生命周期与对外接口）
  - `Assets/WebRL_workspace/SceneDirector.cs`
  - `Assets/WebRL_workspace/Scripts/ExperimentDirector.cs`
  - `Assets/WebRL_workspace/Scripts/NeuralVesselAgent.cs`
  - `Assets/WebRL_workspace/Scripts/WebRtcModelCommandBridge.cs`

- POCO Core（纯 C# 业务逻辑）
  - `Assets/WebRL_workspace/Scripts/SceneDirectorCore/*`
  - `Assets/WebRL_workspace/Scripts/ExperimentDirectorCore/*`
  - `Assets/WebRL_workspace/Scripts/NeuralVesselCore/*`
  - `Assets/WebRL_workspace/Scripts/WebRtcBridgeMessageProcessor.cs`
  - `Assets/WebRL_workspace/Scripts/WebRtcBridgeTelemetrySender.cs`

---

## 关键目录

- `Assets/WebRL_workspace/`：WebRL 主工作区（场景、脚本、资源）
- `Assets/WebRL_workspace/Runtime/Protocol`：协议数据结构（Envelope）
- `Assets/WebRL_workspace/Runtime/Handlers`：协议命令处理器
- `Assets/WebRL_workspace/Resources/Robots`：机器人预制体
- `Assets/WebRL_workspace/Resources/RobotData`：机器人配置资产

---

## 运行入口

推荐从 `GlobalManager` 启动，`SceneDirector` 负责统一调度。

常见命令链路：
1. Web 端通过 DataChannel 发送命令（legacy 或 envelope）。
2. `WebRtcModelCommandBridge` 接收并分发。
3. `SceneDirector` 切场景 / 转发命令到 `ExperimentDirector`。
4. `ExperimentDirector` 加载机器人并调用 `NeuralVesselAgent.MountSoul(...)`。
5. `NeuralVesselAgent` 执行观测、动作积分、步态合成、关节驱动。

---

## 文档索引

- 架构完整版：`WebRL_Architecture_README.md`
- 架构精简版：`WebRL_Architecture_README_Lite.md`
- 新增场景指南：`how_to_add_new_scene_webrl.md`
- 回归检查清单：`WebRL_Regression_Checklist.md`

---

## 依赖与协作约定

- 当前重构目标是“功能零损耗”，优先保持公共接口和 Inspector 挂载稳定。
- 新增业务优先落到 Core POCO，Facade 只做生命周期与编排。
- 修改后请至少执行一次 `WebRL_Regression_Checklist.md`。

---

## 状态说明

- `WebRL_workspace` 重构（Phase A-E）已完成并通过本地运行回归。  
- 仓库中历史遗留的临时/个人配置文件已清理，文档入口已统一到本 README。  
