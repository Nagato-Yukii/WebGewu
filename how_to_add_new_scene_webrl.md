# How to Add a New Scene to WebRL

This guide matches the current `Facade + POCO` architecture under `Assets/WebRL_workspace`.

## Scope
- Add a new gameplay scene that can be loaded from Web commands.
- Keep existing scene flow and WebRTC behavior unchanged.

## 1) Create and Register the Scene
- Create your new scene, for example `MyNewScene`.
- Save it under `Assets/WebRL_workspace/` (or your agreed scene folder).
- Add it to `Build Settings` and keep it enabled.

## 2) Ensure Scene Runtime Requirements
- Scene should contain the runtime objects your mode needs (robot prefab, controllers, camera anchors).
- If the mode uses WebRL command execution, ensure it can provide/resolve `ExperimentDirector` as needed.
- If the mode uses menu camera sync, ensure there is a camera compatible with `SceneDirector` camera binding logic.

## 3) Register Scene Alias Routing
- Open `Assets/WebRL_workspace/SceneDirector.cs`.
- In the serialized fields, add a field for your scene name if needed:
- Example: `[SerializeField] private string myNewSceneName = "MyNewScene";`
- Open `Assets/WebRL_workspace/Scripts/SceneDirectorCore/SceneDirectorSceneRouter.cs`.
- Add aliases mapping to your new scene name in `TryResolveSceneName(...)`.

## 4) Verify Scene Switching Behavior
- From menu scene, trigger a load request for the new target.
- Confirm:
- Scene loads successfully (additive flow remains valid).
- Duplicate request protection still works.
- Return-to-menu flow still works.

## 5) Wire Web Command Entry
- If using envelope command path (`scene.load`), ensure payload uses the alias you just added.
- If using legacy command path (`loadScene`), ensure target string also maps via the same router.
- Frontend side (if used): update scene selector options and payload target values consistently.

## 6) Camera and Input Validation
- Verify `DynamicCameraTracker` / follow behavior remains correct after entering the new scene.
- Verify management camera sync does not break when switching between old scenes and new scene.
- Verify any scene-specific input script does not conflict with existing global bindings.

## 7) Minimal Regression After Integration
- Run the checklist in `WebRL_Regression_Checklist.md`.
- At minimum, validate:
- Existing scenes still load and return normally.
- New scene can load repeatedly without stuck transition state.
- Web command handling and telemetry are still healthy.

## Notes
- Do not rename existing serialized fields in core facades unless you also migrate references safely.
- Keep scene-specific logic out of God classes; prefer extending router/switcher/core POCO components first.
