using System;
using Unity.MLAgents;
using UnityEngine;

public sealed class SceneDirectorTrainingCoordinator
{
    public void ApplyWebTinkerTraining(
        bool shouldTrain,
        MlAgentsTrainerRunner trainerRunner,
        string currentLoadedScene,
        string webTinkerSceneName,
        Action<string, bool> loadGameplayScene)
    {
        if (trainerRunner == null)
        {
            Debug.LogWarning("[SceneDirector] MlAgentsTrainerRunner is not available in the bootstrap scene.");
            return;
        }

        if (shouldTrain)
        {
            if (!CanCurrentProcessConnectTrainer())
            {
                TinkercoinAgent.SetRequestedTrainingMode(false);
                Debug.LogWarning(
                    "[SceneDirector] Web Tinker training requires the Unity process to expose an ML-Agents port before Academy initializes. In Player builds, launch Unity with '--mlagents-port <port>' or use a dedicated training worker build.");
                return;
            }

            bool started = trainerRunner.StartTraining();
            if (!started)
            {
                TinkercoinAgent.SetRequestedTrainingMode(false);
                return;
            }

            ResetMlAgentsAcademy("trainer started from web");
            TinkercoinAgent.SetRequestedTrainingMode(true);

            if (string.Equals(currentLoadedScene, webTinkerSceneName, StringComparison.Ordinal))
            {
                loadGameplayScene?.Invoke(webTinkerSceneName, true);
            }
        }
        else
        {
            trainerRunner.StopTraining();
            TinkercoinAgent.SetRequestedTrainingMode(false);
            ResetMlAgentsAcademy("trainer stopped from web");

            if (string.Equals(currentLoadedScene, webTinkerSceneName, StringComparison.Ordinal))
            {
                loadGameplayScene?.Invoke(webTinkerSceneName, true);
            }
        }
    }

    public void ApplyWebTinkerTrainingFlag(
        bool shouldTrain,
        TinkercoinAgent currentTinkerAgent,
        string currentLoadedScene,
        string webTinkerSceneName,
        Action<string, bool> loadGameplayScene)
    {
        Debug.Log($"[SceneDirector] ApplyWebTinkerTrainingFlag called with shouldTrain={shouldTrain}.");

        if (shouldTrain && !CanCurrentProcessConnectTrainer())
        {
            TinkercoinAgent.SetRequestedTrainingMode(false);
            Debug.LogWarning(
                "[SceneDirector] External Web Tinker training requires the Unity process to expose an ML-Agents port before Academy initializes.");
            return;
        }

        TinkercoinAgent.SetRequestedTrainingMode(shouldTrain);
        ResetMlAgentsAcademy(shouldTrain
            ? "external web tinker trainer bootstrap"
            : "external web tinker trainer disabled");

        if (currentTinkerAgent != null)
        {
            currentTinkerAgent.SetTrainingEnabled(shouldTrain);
            Debug.Log($"[SceneDirector] Applied training flag to active TinkercoinAgent in scene '{currentLoadedScene}'.");
        }
        else
        {
            Debug.Log("[SceneDirector] No active TinkercoinAgent bound yet. Training flag stored for next WebTinkerRL bind.");
        }

        if (string.Equals(currentLoadedScene, webTinkerSceneName, StringComparison.Ordinal))
        {
            Debug.Log(
                "[SceneDirector] WebTinkerRL is already active. Forcing scene reload so Academy reconnects with the requested training mode.");
            loadGameplayScene?.Invoke(webTinkerSceneName, true);
        }
    }

    public void StopWebTinkerTrainingForSceneTransition(MlAgentsTrainerRunner trainerRunner)
    {
        TinkercoinAgent.SetRequestedTrainingMode(false);
        if (trainerRunner != null)
        {
            trainerRunner.StopTraining();
        }

        ResetMlAgentsAcademy("web tinker scene transition");
    }

    public static bool CanCurrentProcessConnectTrainer()
    {
        if (Application.isEditor)
        {
            return true;
        }

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--mlagents-port", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static void ResetMlAgentsAcademy(string reason)
    {
        if (!Academy.IsInitialized)
        {
            return;
        }

        try
        {
            Academy.Instance.Dispose();
            Debug.Log($"[SceneDirector] Reset ML-Agents Academy so the communicator can be reinitialized after {reason}.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SceneDirector] Failed to reset ML-Agents Academy after {reason}. {ex.Message}");
        }
    }
}
