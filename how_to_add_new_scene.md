# How To Add New Scene To WebGewu

这份文档定义的是 WebGewu 的“场景规则”和“接入规范”。

目标不是教你怎么做 Unity 场景本身，而是保证你做完一个新场景后，可以丝滑接到现在的 WebGewu 体系里，而不是变成一个只能本地单跑、无法被 Web 接管的孤岛。

当前 WebGewu 是一个 3+1 场景体系：

- `GlobalManager`
- `WebRL_Laboratory`
- `RobotHeTuRender`
- `WebTinkerRL`

新增场景时，默认你是在扩展这套体系，而不是重新发明一套新的入口方式。

## 一句话原则

新场景必须满足这 4 条原则：

1. 场景由 `SceneDirector` 调入，而不是自己充当应用入口。
2. Web 命令优先复用现有协议和 handler，不要随意发明新链路。
3. 场景必须能被推流相机稳定接管。
4. 场景能力必须能归类到“场景切换、实验控制、远程操控、训练遥测”之一。

## WebGewu 场景规则

下面这些规则建议你直接当成必须遵守的规范。

### 规则 1：所有可接入场景都必须进入 Build Settings

必须做：

- 把新场景加入 `ProjectSettings/EditorBuildSettings.asset`
- 并确保它在需要发布时是 `enabled: 1`

否则 `SceneDirector` 即使收到场景名，也无法正常加载。

### 规则 2：场景必须能被 `SceneDirector.LoadGameplayScene()` 加载

当前 WebGewu 的业务场景全部通过：

- `LoadSceneMode.Additive`

加载。

因此你的新场景必须满足：

- 不依赖自己作为唯一主场景存在
- 不假设启动时会重置整个应用状态
- 不依赖 `Awake/Start` 时“项目只有我一个场景”

如果你的场景只能独占运行，那它就还没有满足 WebGewu 的接入条件。

### 规则 3：场景不要再放第二个 SceneDirector

`SceneDirector` 是全局调度器，不应该在每个业务场景里复制一份。

必须保证：

- 业务场景中不新增新的 `SceneDirector`
- 仍由 `GlobalManager` 持有的 `SceneDirector` 统一调度

### 规则 4：场景相机必须遵守命名规则

最推荐：

- 提供一个名为 `Camera for management` 的摄像机

备选：

- `Main Camera 2`
- `Main Camera`
- `Main Camera (1)`
- `Local Camera`

原因：

- `SceneDirector` 会优先找 `Camera for management`
- 找不到时才按备选名称顺序找锚点相机

如果这些都没有，就只能退化到默认跟随偏移，效果通常不稳定。

### 规则 5：如果场景需要被动态跟随，管理相机应挂跟随组件

推荐二选一：

- `DynamicCameraTracker`
- `CameraFollow`

这样 `SceneDirector` 在发现 tracking target 后，能直接把管理相机绑定过去。

### 规则 6：新场景要明确自己属于哪一类

目前 WebGewu 已有的场景类型可以归成三类：

1. 实验/模型切换场景
   - 代表：`WebRL_Laboratory`
   - 核心组件：`ExperimentDirector`
2. 远程操控场景
   - 代表：`RobotHeTuRender`
   - 核心组件：`G1moeAgent`
3. 训练/遥测场景
   - 代表：`WebTinkerRL`
   - 核心组件：`TinkercoinAgent`

新增场景时，先判断自己属于哪类：

- 如果你是模型/实验类，优先复用 `ExperimentDirector` 路径
- 如果你是手动控制类，优先复用 `SceneDirector.ApplyRoboHetuWebInput()` 路径
- 如果你是训练类，优先复用 `training.set_flag` + telemetry 路径

只有三类都装不下时，才考虑扩新协议和新 handler。

### 规则 7：优先复用 `scene.load`

Web 打开新场景时，应该仍然走：

- `scene.load`

而不是再加新的 HTTP 接口或旁路 RPC。

推荐做法：

- Web 端按钮点击后发 `scene.load`
- payload 里的 `scene` 使用场景别名或真实场景名
- Unity 侧由 `SceneDirector.LoadSceneByCommandTarget()` 解析

### 规则 8：只有确实需要时才新增 DataChannel 消息类型

当前 Unity 已支持的典型消息：

- `scene.load`
- `training.set_flag`
- `latency.ping`
- legacy `changeModel`
- legacy `roboHetuMove`
- legacy `roboHetuMode`

如果新场景需求能通过已有消息表达，就不要新增协议。

新增协议的前提是：

- 现有 `scene.load` 和已有命令确实无法表达
- 且这是一个会长期维护的稳定能力

### 规则 9：场景内真正的业务入口组件必须可被发现

`SceneDirector` 当前依赖“按组件发现能力”。

所以新场景如果是：

- 实验类
  - 建议提供 `ExperimentDirector` 或其等价扩展入口
- 远程操控类
  - 需要有明确的受控对象入口
- 训练类
  - 需要有训练状态入口和遥测出口

不要把关键逻辑藏在一堆仅 Inspector 手工拖拽才能找到的对象里，而没有统一入口组件。

## 推荐接入路径

新增场景时，优先选下面三条路径之一。

### 路径 A：作为普通场景接入

适用：

- 只是新增一个可切入展示或交互的业务场景
- 不需要新的复杂协议

做法：

1. 创建场景
2. 加入 Build Settings
3. 提供管理摄像机
4. 在 `SceneDirector` 增加别名解析
5. 在 Web 页面新增入口按钮

### 路径 B：作为 ExperimentDirector 类场景接入

适用：

- 你需要从 Web 发结构化命令到场景
- 场景中要根据命令生成对象、切技能、切状态

做法：

1. 场景内放置一个统一入口控制器
2. 最好复用 `ExperimentDirector`
3. 或者做一个与其职责等价的新控制器
4. 保证 Web 命令只进一个入口对象，不要散落到多个脚本

### 路径 C：作为训练类场景接入

适用：

- 场景要接 `training.set_flag`
- 场景要回传遥测

做法：

1. 提供训练主体组件
2. 提供统一的训练开关入口
3. 提供统一 telemetry 事件出口
4. 保证场景 reload 后仍能被 `SceneDirector` 重新绑定

## 新场景接入操作指南

下面是建议流程。

### 步骤 1：创建场景并决定场景类型

先回答两个问题：

1. 这个场景是实验类、控制类还是训练类？
2. 它能否在 additive 加载下正常运行？

如果第二个问题回答不了，先不要接 Web。

### 步骤 2：给场景准备管理相机

最推荐直接创建：

- `Camera for management`

并确保：

- 它不是 `StreamSender Camera`
- 它在场景里能看到核心内容
- 如果需要跟随，就挂 `DynamicCameraTracker` 或 `CameraFollow`

### 步骤 3：给场景准备统一入口脚本

按场景类型选择：

- 实验类：准备 `ExperimentDirector` 风格入口
- 控制类：准备可被 `SceneDirector` 转发输入的入口
- 训练类：准备训练主体和 telemetry 事件

原则：

- 场景里所有 Web 交互，尽量只通过一个主入口脚本进入

### 步骤 4：加入 Build Settings

把场景加入：

- `ProjectSettings/EditorBuildSettings.asset`

如果不加入，这个场景对 WebGewu 来说就不存在。

### 步骤 5：给 SceneDirector 增加场景别名

修改：

- `Assets/WebRL_workspace/SceneDirector.cs`

通常在：

- `TryResolveSceneName(string sceneTarget, out string sceneName)`

里加你的场景别名映射。

建议：

- 同时支持真实场景名
- 再补 1 到 2 个简短别名

例如：

```csharp
case "MyNewScene":
case "MyScene":
case "MyFeature":
    sceneName = myNewSceneName;
    return true;
```

### 步骤 6：如果需要，给 SceneDirector 增加序列化字段

如果你的场景会像当前 3 个业务场景一样长期存在，推荐在 `SceneDirector` 里增加：

- `[SerializeField] private string myNewSceneName = "MyNewScene";`

这样场景名集中维护，不要在多处写死字符串。

### 步骤 7：在 Web 页新增入口

至少改：

- `WebApp/client/public/receiver/index.html`
- `WebApp/client/public/receiver/js/main.js`

通常做法：

1. 在封面面板新增一个按钮
2. 绑定 `handleSceneSelection('my-panel', 'MyNewScene')`
3. 如果需要，还要新增对应 overlay panel

### 步骤 8：如果场景需要专属控制 UI，再加一个 panel

当前已有：

- `webrl-panel`
- `robohetu-panel`
- `webtinker-panel`

新场景如果需要专属 UI，建议照这个模式扩：

1. `index.html` 新增 panel 容器
2. `main.js` 的 `setActivePanel()` / `resetOverlayState()` / `showSceneSelector()` 增加新分支
3. 给 panel 增加回目录按钮

### 步骤 9：如果需要新协议，再同时改 Web 和 Unity

要加新 DataChannel 消息时，至少同步改：

- `WebApp/client/public/receiver/js/protocol/envelope.js`
- `WebApp/client/public/receiver/js/transport/rtc-channel.js`
- `gewu/Assets/WebRL_workspace/Runtime/Protocol/Envelope.cs`
- `gewu/Assets/WebRL_workspace/Scripts/WebRtcModelCommandBridge.cs`

如果是长期能力，建议：

1. 在 Unity 新增独立 handler
2. 不要全塞进 legacy command 分支

## 推荐模板

### 模板 1：纯展示/跳转场景

适合：

- 只需要切进去展示内容
- 不需要复杂交互

最低要求：

- 加入 Build Settings
- `SceneDirector` 能解析场景名
- 场景里有 `Camera for management`

### 模板 2：带专属控制面板的场景

适合：

- Web 打开场景后，还要出现一套场景专属按钮/状态栏

最低要求：

- 模板 1 的全部要求
- Web 侧新增 panel
- Unity 侧有统一控制入口

### 模板 3：训练/遥测场景

适合：

- 需要训练开关
- 需要实时回传状态

最低要求：

- 模板 2 的全部要求
- 训练入口
- telemetry 出口
- reload 后可重绑定

## 不推荐的做法

### 1. 场景自己管整个应用生命周期

不要：

- 在新场景里自己再做一个总入口
- 假设切进来后可以把别的系统都重置掉

### 2. 场景和 Web 端直接各写各的字符串协议

不要：

- Web 随便发一个 JSON
- Unity 随便在某个脚本里手写解析

应该走统一协议层和统一桥接入口。

### 3. 没有统一控制脚本

不要：

- 一个按钮打到一个脚本
- 另一个按钮再打到另一个脚本
- 最后没有任何统一入口

这会让后续接入和排错非常痛苦。

### 4. 完全依赖默认摄像机回退

不要指望：

- 没有 `Camera for management`
- 也没有固定命名锚点
- 还能稳定在 Web 中得到正确视角

### 5. 在场景里复制现有主链路组件

不要复制：

- `SceneDirector`
- Render Streaming 总桥接逻辑

这些都应该是全局单例式能力。

## 新场景接入检查清单

集成前请逐项确认：

1. 场景已加入 Build Settings。
2. 场景支持 additive 加载。
3. 场景内没有重复 `SceneDirector`。
4. 场景有 `Camera for management` 或符合规则的相机锚点。
5. 场景有统一入口脚本。
6. `SceneDirector.TryResolveSceneName()` 已加别名。
7. Web 页面已加入口按钮。
8. 如有专属 UI，panel 已加显示/隐藏逻辑。
9. 如有新协议，Web 和 Unity 两侧都已同步改。
10. 从 `GlobalManager` 进入、返回目录、再次进入都能正常工作。

## 最小接入示例

如果你只是想把一个新场景接到当前 WebGewu，最小改动通常只有这几步：

1. 创建 `MyNewScene.unity`
2. 放一个 `Camera for management`
3. 加入 Build Settings
4. 在 `SceneDirector` 里加 `MyNewScene` 别名
5. 在 `receiver/index.html` 加一个按钮
6. 在 `main.js` 里绑定 `handleSceneSelection(..., 'MyNewScene')`

这样它至少能被打开和被推流。

如果后续还要交互，再逐步加控制入口，而不是一开始就扩散协议。

## 最后的建议

对 WebGewu 来说，一个“合格的新场景”不只是内容能跑，而是要满足：

- 能被 `SceneDirector` 正确加载
- 能被 Web 正确进入
- 能被相机正确接管
- 能在退出再进入时状态仍然稳定

先接通这条链路，再谈场景复杂度。这样扩展才不会把现在的 3+1 主链路做散。
