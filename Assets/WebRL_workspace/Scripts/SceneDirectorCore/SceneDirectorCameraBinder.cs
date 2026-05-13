using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneDirectorCameraBinder
{
    public void AlignGlobalCameraToSceneAnchor(
        DynamicCameraTracker globalCameraTracker,
        Scene scene,
        Transform trackingTarget,
        string[] cameraAnchorNames)
    {
        if (globalCameraTracker == null)
        {
            return;
        }

        Camera anchorCamera = FindSceneCameraAnchor(scene, cameraAnchorNames);
        if (anchorCamera != null)
        {
            globalCameraTracker.transform.SetPositionAndRotation(
                anchorCamera.transform.position,
                anchorCamera.transform.rotation);
            return;
        }

        if (trackingTarget == null)
        {
            return;
        }

        Vector3 desiredPosition = trackingTarget.TransformPoint(new Vector3(-2.2f, 1.6f, -4.2f));
        globalCameraTracker.transform.position = desiredPosition;
        globalCameraTracker.transform.rotation = Quaternion.LookRotation(
            (trackingTarget.position - desiredPosition).normalized,
            Vector3.up);
    }

    public Camera FindSceneCameraAnchor(Scene scene, string[] cameraAnchorNames)
    {
        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        if (cameraAnchorNames != null)
        {
            for (int nameIndex = 0; nameIndex < cameraAnchorNames.Length; nameIndex++)
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null || camera.gameObject.scene != scene)
                    {
                        continue;
                    }

                    if (camera.gameObject.name == "StreamSender Camera")
                    {
                        continue;
                    }

                    if (camera.gameObject.name == cameraAnchorNames[nameIndex])
                    {
                        return camera;
                    }
                }
            }
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.gameObject.scene == scene && camera.gameObject.name != "StreamSender Camera")
            {
                return camera;
            }
        }

        return null;
    }

    public Camera FindManagementCamera(Scene scene, string managementCameraName)
    {
        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        for (int pass = 0; pass < 2; pass++)
        {
            bool activeOnly = pass == 0;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.gameObject.scene != scene)
                {
                    continue;
                }

                if (camera.gameObject.name != managementCameraName)
                {
                    continue;
                }

                if (activeOnly && !camera.gameObject.activeInHierarchy)
                {
                    continue;
                }

                return camera;
            }
        }

        return null;
    }

    public void BindSceneManagementCameraTracking(Camera sceneManagementCamera, Transform trackingTarget)
    {
        if (sceneManagementCamera == null || trackingTarget == null)
        {
            return;
        }

        DynamicCameraTracker tracker = sceneManagementCamera.GetComponent<DynamicCameraTracker>();
        if (tracker != null)
        {
            tracker.enabled = true;
            if (tracker.target != trackingTarget)
            {
                tracker.SetTarget(trackingTarget, true);
            }
            return;
        }

        CameraFollow cameraFollow = sceneManagementCamera.GetComponent<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = trackingTarget;
            cameraFollow.enabled = true;
        }
    }

    public void SyncBootstrapCameraToTransform(DynamicCameraTracker globalCameraTracker, Transform sourceTransform)
    {
        if (globalCameraTracker == null || sourceTransform == null)
        {
            return;
        }

        Transform cameraTransform = globalCameraTracker.transform;
        if (cameraTransform.parent != null)
        {
            cameraTransform.SetParent(null, true);
        }

        cameraTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
    }

    public Transform ResolveTrackingTransform(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return null;
        }

        ArticulationBody[] bodies = rootObject.GetComponentsInChildren<ArticulationBody>(true);
        ArticulationBody firstBody = null;
        for (int i = 0; i < bodies.Length; i++)
        {
            ArticulationBody body = bodies[i];
            if (body == null)
            {
                continue;
            }

            if (firstBody == null)
            {
                firstBody = body;
            }

            if (body.isRoot)
            {
                return body.transform;
            }
        }

        return firstBody != null ? firstBody.transform : rootObject.transform;
    }

    public T FindComponentInScene<T>(Scene scene) where T : Component
    {
        T[] components = Object.FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.gameObject.scene == scene)
            {
                return component;
            }
        }

        return null;
    }
}
