using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneDirectorSceneSwitcher
{
    public bool IsDuplicateMenuTransition(Coroutine transitionRoutine, string pendingSceneName, Func<string, bool> isMenuTarget)
    {
        return transitionRoutine != null && isMenuTarget != null && isMenuTarget(pendingSceneName);
    }

    public bool IsDuplicateSceneLoadRequest(
        Coroutine transitionRoutine,
        bool forceReload,
        string pendingSceneName,
        string sceneName)
    {
        return transitionRoutine != null &&
               !forceReload &&
               string.Equals(pendingSceneName, sceneName, StringComparison.Ordinal);
    }

    public IEnumerator ReturnToMenuRoutine(
        string currentLoadedScene,
        string webTinkerSceneName,
        Action stopWebTinkerTrainingForSceneTransition,
        Action clearBindings,
        Action<string> setCurrentLoadedScene,
        string bootstrapSceneName,
        Action<string> setPendingSceneName,
        Action clearTransitionRoutine)
    {
        if (string.Equals(currentLoadedScene, webTinkerSceneName, StringComparison.Ordinal))
        {
            stopWebTinkerTrainingForSceneTransition?.Invoke();
        }

        clearBindings?.Invoke();

        if (!string.IsNullOrEmpty(currentLoadedScene))
        {
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(currentLoadedScene);
            if (unloadOperation != null)
            {
                yield return unloadOperation;
            }
        }

        setCurrentLoadedScene?.Invoke(string.Empty);
        Scene bootstrapScene = SceneManager.GetSceneByName(bootstrapSceneName);
        if (bootstrapScene.IsValid() && bootstrapScene.isLoaded)
        {
            SceneManager.SetActiveScene(bootstrapScene);
        }
        else
        {
            Debug.LogWarning($"[SceneDirector] Bootstrap scene '{bootstrapSceneName}' is not loaded.");
        }

        Debug.Log("[SceneDirector] Returned to GlobalManager menu.");
        setPendingSceneName?.Invoke(string.Empty);
        clearTransitionRoutine?.Invoke();
    }

    public IEnumerator LoadSceneRoutine(
        string currentLoadedScene,
        string webTinkerSceneName,
        string sceneName,
        Action stopWebTinkerTrainingForSceneTransition,
        Action clearBindings,
        Action<string> setCurrentLoadedScene,
        Action<Scene> bindSceneRuntime,
        Action<string> setPendingSceneName,
        Action clearTransitionRoutine)
    {
        if (string.Equals(currentLoadedScene, webTinkerSceneName, StringComparison.Ordinal) &&
            !string.Equals(sceneName, webTinkerSceneName, StringComparison.Ordinal))
        {
            stopWebTinkerTrainingForSceneTransition?.Invoke();
        }

        clearBindings?.Invoke();

        if (!string.IsNullOrEmpty(currentLoadedScene))
        {
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(currentLoadedScene);
            if (unloadOperation != null)
            {
                yield return unloadOperation;
            }
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            Debug.LogError($"[SceneDirector] Failed to start loading scene '{sceneName}'.");
            setPendingSceneName?.Invoke(string.Empty);
            clearTransitionRoutine?.Invoke();
            yield break;
        }

        yield return loadOperation;

        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
        {
            Debug.LogError($"[SceneDirector] Scene '{sceneName}' was not loaded correctly.");
            setPendingSceneName?.Invoke(string.Empty);
            clearTransitionRoutine?.Invoke();
            yield break;
        }

        SceneManager.SetActiveScene(loadedScene);
        setCurrentLoadedScene?.Invoke(sceneName);
        bindSceneRuntime?.Invoke(loadedScene);
        setPendingSceneName?.Invoke(string.Empty);
        clearTransitionRoutine?.Invoke();
    }
}
