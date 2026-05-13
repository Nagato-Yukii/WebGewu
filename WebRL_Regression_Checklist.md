# WebRL Regression Checklist

This checklist is for ongoing verification after code changes, package changes, and scene updates.

## 1) Compile and Reference Safety
- Open Unity and ensure there are no compile errors.
- Confirm there are no `Missing (Mono Script)` components in loaded scenes.
- Confirm these key scripts exist and are bound where expected:
- `Assets/WebRL_workspace/SceneDirector.cs`
- `Assets/WebRL_workspace/Scripts/ExperimentDirector.cs`
- `Assets/WebRL_workspace/Scripts/NeuralVesselAgent.cs`
- `Assets/WebRL_workspace/Scripts/Control/DynamicCameraTracker.cs`

## 2) Scene Flow
- Start from `GlobalManager` (bootstrap/menu entry).
- Trigger `scene.load` to `WebRL_Laboratory`, then return to menu.
- Trigger `scene.load` to `WebTinkerRL`, then return to menu.
- Verify duplicate scene-load protection still works (no repeated load race).

## 3) Runtime Robot Flow
- In `WebRL_Laboratory`, execute at least one command for each family:
- Biped (`X02Lite` or `OpenLoong`) with `bipedWalk`.
- Quadruped (`Go2`) with `quadTrot`.
- Leg-wheeled (`Go2W` or `Tron1`) with `wheelDrive`.
- Verify spawn, model mount, camera bind, and control response.

## 4) NeuralVessel Agent Guard
- Verify `OnEpisodeBegin` restores root pose and joint state.
- Verify settle-steps path keeps initial drive targets before normal integration.
- Verify gait phase progression (`T1/T2`) remains stable (no jitter/spikes).
- Verify `CollectObservations` and `OnActionReceived` run without dimension mismatch or runtime warnings.

## 5) WebRTC Command and Telemetry Guard
- Send one legacy command (`changeModel` or `loadScene`) and verify execution.
- Send one envelope command (`scene.load` or `training.set_flag`) and verify execution.
- Verify telemetry path still works (`latency.pong`, and Tinker telemetry when applicable).

## 6) Optional Editor Guard Tool
- Run `Tools/WebRL/Phase E/Run Minimal Regression Guard`.
- Treat this as a fast sanity check, not a full replacement for the checklist above.
