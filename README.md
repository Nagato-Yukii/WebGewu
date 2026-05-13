# gewu 云渲染协同方案说明

## 1. 项目定位

`gewu` 是本仓库里的 Unity 侧运行时项目，负责：

- 渲染 WebRTC 视频流
- 接收 Web 端控制指令
- 驱动场景切换
- 运行 WebRL / RoboHeTu / WebTinkerRL 等场景
- 在 WebTinkerRL 中接入 ML-Agents 训练

本项目与 `WebApp` 共同组成一套“浏览器控制 + Unity 本地执行 + WebRTC 推流”的云渲染方案。

---

## 2. 整体架构

### 2.1 角色划分

- `WebApp`
  - 浏览器端页面
  - 负责发起 WebRTC 连接
  - 通过 DataChannel 发送控制命令
  - 接收 Unity 侧遥测

- `gewu`
  - Unity 运行时
  - 负责实际加载场景、运行机器人、执行训练
  - 将画面通过 Render Streaming 推送到浏览器
  - 通过 `WebRtcModelCommandBridge` 处理控制指令

- 中转设施
  - `frps`：只负责端口映射
  - `coturn`：只负责 ICE/TURN 中继
  - 不承载业务逻辑

---

## 3. 核心链路

### 3.1 视频流

1. 浏览器打开 `WebApp`
2. Web 端通过 WebRTC 与 Unity 建立连接
3. Unity 将渲染画面推送到浏览器

### 3.2 控制流

1. 浏览器通过 DataChannel 发送 JSON 指令
2. Unity 中的 `WebRtcModelCommandBridge` 接收消息
3. `SceneDirector` 根据消息切换场景或转发业务命令
4. 对于 WebTinkerRL，Unity 还会回传训练遥测

---

## 4. 当前支持的场景

- `GlobalManager`
  - 启动场景 / 目录场景
  - 不承载具体实验逻辑

- `WebRL_Laboratory`
  - WebRL 主实验场景
  - 需要 `ExperimentDirector`

- `RobotHeTuRender`
  - RoboHeTu 场景
  - 接收 `roboHetuMove` / `roboHetuMode`

- `WebTinkerRL`
  - Tinker 训练场景
  - 只走训练，不走推理

---

## 5. 关键脚本

### Unity 侧

- [Assets/WebRL_workspace/SceneDirector.cs](./Assets/WebRL_workspace/SceneDirector.cs)
  - 统一场景切换入口
  - 管理当前激活场景
  - 管理 `ExperimentDirector` / `TinkercoinAgent` / `G1moeAgent` 绑定
  - 对 WebRL 模型命令做延迟排队与自动路由

- [Assets/WebRL_workspace/Scripts/WebRtcModelCommandBridge.cs](./Assets/WebRL_workspace/Scripts/WebRtcModelCommandBridge.cs)
  - WebRTC DataChannel 消息入口
  - 兼容 envelope 协议与旧版裸 JSON 命令
  - 负责向 Web 回传 Tinker 遥测

- [Assets/WebRL_workspace/Runtime/Handlers/SceneLoadHandler.cs](./Assets/WebRL_workspace/Runtime/Handlers/SceneLoadHandler.cs)
  - 处理 `scene.load`

- [Assets/WebRL_workspace/Scripts/MlAgentsTrainerRunner.cs](./Assets/WebRL_workspace/Scripts/MlAgentsTrainerRunner.cs)
  - 训练进程协同封装

- [Assets/TinkerCoin/TinkercoinAgent.cs](./Assets/TinkerCoin/TinkercoinAgent.cs)
  - WebTinkerRL 训练主体
  - 负责 `LiftAssistCurriculum` 自动衰减
  - 回传 reward / step / curriculum 遥测

- [Assets/TinkerCoin/TinkercoinFallCounter.cs](./Assets/TinkerCoin/TinkercoinFallCounter.cs)
  - 场景内统计 coin / fall

### Web 侧对应文件

- `WebApp/client/public/receiver/js/main.js`
- `WebApp/client/public/receiver/js/app/scene-controller.js`
- `WebApp/client/public/receiver/js/protocol/envelope.js`

---

## 6. 当前协议设计

### 6.1 新协议

浏览器优先发送 envelope：

```json
{
  "v": 1,
  "id": "env-xxx",
  "type": "scene.load",
  "source": "web",
  "ts": 1710000000000,
  "payload": {
    "scene": "RoboHeTu",
    "mode": "additive",
    "forceReload": false
  }
}
```

### 6.2 兼容旧协议

Unity 仍兼容旧版裸命令：

```json
{ "command": "loadScene", "target": "RoboHeTu" }
{ "command": "changeModel", "target": "X02Lite", "skillType": "bipedWalk" }
{ "command": "roboHetuMove", "moveX": 0, "moveY": 1, "rotate": 0 }
```

---

## 7. WebTinkerRL 训练链路

### 7.1 训练方式

当前方案采用：

- Web 端选择 `WebTinkerRL`
- 本地启动训练脚本
- 启动 `mlagents-learn`
- 延迟后进入 `WebTinkerRL` 场景
- 直接将训练画面推流到浏览器

### 7.2 LiftAssistCurriculum

当前规则：

- 从 `500000` training steps 后开始衰减
- 每 `100000` steps 下降 `0.2`
- Inspector 中可见：
  - `liftAssistCurriculum`
  - `totalTrainingStepCount`
  - `liftAssistCurriculumStage`

### 7.3 Web 展示

WebTinkerRL 的浏览器面板只保留：

- `LiftAssistCurriculum`
- `Reward`

coin / fall 只在 Unity 场景内显示。

---

## 8. 场景切换策略

### 8.1 正常流程

1. 浏览器点击场景按钮
2. Web 端发送 `scene.load`
3. Unity `SceneDirector` 解析目标场景
4. 当前场景卸载
5. 目标场景以 Additive 方式加载
6. 绑定当前场景中的运行时对象

### 8.2 已做的保护

- 同一场景正在切换时，重复请求会被忽略
- WebRL 模型命令如果在 `GlobalManager` 提前到达，会先排队
- 若当前没有 `ExperimentDirector` 且仍处于 bootstrap 阶段，Unity 会自动路由到 `WebRL_Laboratory`
- 切出 WebRL 时会清空待执行的 WebRL 命令，避免误拉回

---

## 9. 典型问题与原因

### 9.1 浏览器能看视频，但 Unity 不响应场景切换

常见原因：

- DataChannel 只收到了裸 JSON 控制，没有收到 `scene.load`
- 浏览器环境对 envelope 生成能力不完整
- Web 端与 Unity 端协议版本不一致

### 9.2 出现两个同名场景

常见原因：

- 同一次切场景请求被重复发送
- 旧兼容逻辑与新逻辑同时触发
- Additive 模式下重复加载相同 scene

当前代码已经增加去重保护。

### 9.3 `ExperimentDirector is not available`

含义：

- 当前还在 `GlobalManager`
- 但 WebRL 的模型命令已经先到了

当前策略是：

- 先排队
- 再自动切到 `WebRL_Laboratory`
- 等 `ExperimentDirector` 就绪后补执行

---

## 10. 调试入口

### Unity Console 重点日志

- `[WebRtcBridge] Raw inbound message: ...`
- `[WebRtcBridge] Processing envelope 'scene.load' ...`
- `[WebRtcBridge] Processing legacy command 'loadScene' ...`
- `[SceneDirector] Resolved scene target ...`
- `[SceneDirector] Active gameplay scene: ...`
- `[SceneDirector] Ignoring duplicate scene load request ...`

### 重点检查文件

- [Assets/WebRL_workspace/Scripts/WebRtcModelCommandBridge.cs](./Assets/WebRL_workspace/Scripts/WebRtcModelCommandBridge.cs)
- [Assets/WebRL_workspace/SceneDirector.cs](./Assets/WebRL_workspace/SceneDirector.cs)
- [Assets/TinkerCoin/TinkercoinAgent.cs](./Assets/TinkerCoin/TinkercoinAgent.cs)
- [Assets/TinkerCoin/TinkercoinFallCounter.cs](./Assets/TinkerCoin/TinkercoinFallCounter.cs)

---

## 11. 当前方案结论

这套方案的本质不是“云服务器执行业务”，而是：

- Unity 本地执行业务
- Web 侧发控制
- WebRTC 负责画面与 DataChannel
- 中转层只负责连通性

因此，问题排查的重点应放在：

- Web 指令是否真的发出
- Unity 是否按预期解析
- 场景是否已正确绑定运行时对象

而不是把问题归因到 `frps` 或 `coturn` 的业务处理能力。

