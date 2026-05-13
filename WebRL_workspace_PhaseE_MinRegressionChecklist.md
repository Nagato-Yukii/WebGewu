# WebRL Workspace Phase E: Minimal Regression Checklist

## 1) Script Reference Safety
- Confirm `DynamicCameraTracker.cs` exists at `Assets/WebRL_workspace/Scripts/Control/DynamicCameraTracker.cs`.
- Confirm there are **no Missing (Mono Script)** components in loaded scenes.
- Confirm `SceneDirector`, `ExperimentDirector`, `NeuralVesselAgent` scripts still compile and are present.

## 2) Scene Flow
- Start from `GlobalManager` / bootstrap entry.
- Trigger `scene.load` to `WebRL_Laboratory`, then return to menu.
- Trigger `scene.load` to `WebTinkerRL`, then return to menu.
- Verify duplicate scene-load protection still works (no repeated loading race).

## 3) Runtime Robot Flow
- In `WebRL_Laboratory`, issue at least one command for each robot family:
- Biped (`X02Lite` or `OpenLoong`) with `bipedWalk`.
- Quadruped (`Go2`) with `quadTrot`.
- Leg-wheeled (`Go2W` or `Tron1`) with `wheelDrive`.
- Verify spawn, policy mount, camera bind, and control response.

## 4) NeuralVessel Physics/Inference Guard
- Verify episode reset restores root pose and joint state.
- Verify settle steps hold initial drive targets before normal action integration.
- Verify gait phase progression (`T1/T2`) remains stable (no jitter/spike regression).

## 5) WebRTC Bridge Guard
- Send one legacy command and one envelope command.
- Verify command reaches `SceneDirector` / `ExperimentDirector`.
- Verify telemetry (`latency.pong` / Tinker payload if enabled) still sends.

## 6) Optional Tooling
- Run menu action: `Tools/WebRL/Phase E/Run Minimal Regression Guard`.
- If migration not done yet: `Tools/WebRL/Phase E/Migrate DynamicCameraTracker Path`.
- After verification: `Tools/WebRL/Phase E/Delete Empty Legacy Scrips Folder`.
