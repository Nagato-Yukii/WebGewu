using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ExperimentDirectorCameraTrackerResolver
{
    public DynamicCameraTracker Resolve(
        DynamicCameraTracker current,
        Scene ownerScene,
        Func<SceneDirector> sceneDirectorResolver,
        Func<DynamicCameraTracker[]> trackerResolver)
    {
        if (current != null)
        {
            return current;
        }

        SceneDirector sceneDirector = sceneDirectorResolver != null ? sceneDirectorResolver() : null;
        if (sceneDirector != null && sceneDirector.GlobalCameraTracker != null)
        {
            return sceneDirector.GlobalCameraTracker;
        }

        DynamicCameraTracker[] trackers = trackerResolver != null
            ? trackerResolver()
            : Array.Empty<DynamicCameraTracker>();

        for (int i = 0; i < trackers.Length; i++)
        {
            DynamicCameraTracker tracker = trackers[i];
            if (tracker != null && tracker.gameObject.scene != ownerScene)
            {
                return tracker;
            }
        }

        return null;
    }
}
