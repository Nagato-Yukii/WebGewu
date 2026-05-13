#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WebRlPhaseEGuardTools
{
    private const string LegacyCameraTrackerPath = "Assets/WebRL_workspace/Scrips/DynamicCameraTracker.cs";
    private const string TargetCameraTrackerPath = "Assets/WebRL_workspace/Scripts/Control/DynamicCameraTracker.cs";
    private const string LegacyFolderPath = "Assets/WebRL_workspace/Scrips";

    [MenuItem("Tools/WebRL/Phase E/Migrate DynamicCameraTracker Path")]
    public static void MigrateDynamicCameraTrackerPath()
    {
        string targetDirectory = Path.GetDirectoryName(TargetCameraTrackerPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(targetDirectory))
        {
            Debug.LogError("[PhaseE] Invalid target directory.");
            return;
        }

        EnsureFolder(targetDirectory);

        bool hasLegacy = File.Exists(LegacyCameraTrackerPath);
        bool hasTarget = File.Exists(TargetCameraTrackerPath);

        if (!hasLegacy && hasTarget)
        {
            Debug.Log("[PhaseE] DynamicCameraTracker already migrated.");
            return;
        }

        if (!hasLegacy)
        {
            Debug.LogWarning($"[PhaseE] Legacy script not found: {LegacyCameraTrackerPath}");
            return;
        }

        if (hasTarget)
        {
            Debug.LogError($"[PhaseE] Target already exists, stop to avoid overwrite: {TargetCameraTrackerPath}");
            return;
        }

        string error = AssetDatabase.MoveAsset(LegacyCameraTrackerPath, TargetCameraTrackerPath);
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"[PhaseE] MoveAsset failed: {error}");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PhaseE] DynamicCameraTracker migrated. GUID remains stable because .meta moved with asset.");
    }

    [MenuItem("Tools/WebRL/Phase E/Delete Empty Legacy Scrips Folder")]
    public static void DeleteEmptyLegacyScripsFolder()
    {
        if (!AssetDatabase.IsValidFolder(LegacyFolderPath))
        {
            Debug.Log("[PhaseE] Legacy Scrips folder does not exist.");
            return;
        }

        string[] entries = AssetDatabase.FindAssets(string.Empty, new[] { LegacyFolderPath });
        if (entries.Length > 0)
        {
            Debug.LogWarning("[PhaseE] Legacy Scrips folder is not empty. Skip deletion.");
            return;
        }

        bool deleted = AssetDatabase.DeleteAsset(LegacyFolderPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(deleted
            ? "[PhaseE] Deleted empty legacy folder: Assets/WebRL_workspace/Scrips"
            : "[PhaseE] Failed to delete legacy folder.");
    }

    [MenuItem("Tools/WebRL/Phase E/Run Minimal Regression Guard")]
    public static void RunMinimalRegressionGuard()
    {
        int issues = 0;

        issues += RequireAsset("SceneDirector", "Assets/WebRL_workspace/SceneDirector.cs");
        issues += RequireAsset("ExperimentDirector", "Assets/WebRL_workspace/Scripts/ExperimentDirector.cs");
        issues += RequireAsset("NeuralVesselAgent", "Assets/WebRL_workspace/Scripts/NeuralVesselAgent.cs");
        issues += RequireAsset("DynamicCameraTracker", TargetCameraTrackerPath);

        int missingScripts = CountMissingScriptsInLoadedScenes();
        if (missingScripts > 0)
        {
            issues++;
            Debug.LogError($"[PhaseE] Found {missingScripts} missing script reference(s) in loaded scenes.");
        }
        else
        {
            Debug.Log("[PhaseE] No missing scripts found in loaded scenes.");
        }

        if (issues == 0)
        {
            Debug.Log("[PhaseE] Minimal regression guard passed.");
        }
        else
        {
            Debug.LogError($"[PhaseE] Minimal regression guard failed with {issues} issue(s).");
        }
    }

    private static int RequireAsset(string label, string path)
    {
        bool exists = File.Exists(path);
        if (exists)
        {
            Debug.Log($"[PhaseE] OK: {label} -> {path}");
            return 0;
        }

        Debug.LogError($"[PhaseE] Missing {label}: {path}");
        return 1;
    }

    private static int CountMissingScriptsInLoadedScenes()
    {
        int missing = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                missing += CountMissingScriptsRecursive(roots[r].transform);
            }
        }

        return missing;
    }

    private static int CountMissingScriptsRecursive(Transform root)
    {
        int missing = 0;
        Component[] components = root.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                missing++;
            }
        }

        for (int i = 0; i < root.childCount; i++)
        {
            missing += CountMissingScriptsRecursive(root.GetChild(i));
        }

        return missing;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string current = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(current))
        {
            return;
        }

        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, current);
        }
    }
}
#endif
