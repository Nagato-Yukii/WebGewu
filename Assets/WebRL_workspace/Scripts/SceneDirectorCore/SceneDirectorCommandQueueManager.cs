using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SceneDirectorCommandQueueManager
{
    private readonly Queue<string> pendingWebCommands = new Queue<string>();

    public int PendingCount => pendingWebCommands.Count;

    public bool TryExecuteOrQueue(
        string jsonString,
        ExperimentDirector experimentDirector,
        string currentLoadedScene,
        Func<bool> shouldAutoRouteToWebRl,
        Action ensureExperimentDirectorSceneLoaded)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return false;
        }

        if (experimentDirector == null)
        {
            pendingWebCommands.Enqueue(jsonString);
            Debug.Log(
                $"[SceneDirector] Queued web command because ExperimentDirector is not available yet. Pending={pendingWebCommands.Count}, ActiveScene='{currentLoadedScene}'.");

            if (shouldAutoRouteToWebRl != null && shouldAutoRouteToWebRl())
            {
                ensureExperimentDirectorSceneLoaded?.Invoke();
            }

            return true;
        }

        experimentDirector.ExecuteWebCommand(jsonString);
        return true;
    }

    public void FlushTo(ExperimentDirector experimentDirector)
    {
        if (experimentDirector == null || pendingWebCommands.Count == 0)
        {
            return;
        }

        while (pendingWebCommands.Count > 0)
        {
            string commandJson = pendingWebCommands.Dequeue();
            if (string.IsNullOrWhiteSpace(commandJson))
            {
                continue;
            }

            experimentDirector.ExecuteWebCommand(commandJson);
        }

        Debug.Log("[SceneDirector] Flushed queued web commands after ExperimentDirector became available.");
    }

    public void Clear(string reason)
    {
        if (pendingWebCommands.Count == 0)
        {
            return;
        }

        int clearedCount = pendingWebCommands.Count;
        pendingWebCommands.Clear();
        Debug.Log($"[SceneDirector] Cleared {clearedCount} queued web command(s) while {reason}.");
    }
}
