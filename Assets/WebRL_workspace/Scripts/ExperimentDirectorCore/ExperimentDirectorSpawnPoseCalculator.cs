using UnityEngine;

public sealed class ExperimentDirectorSpawnPoseCalculator
{
    public struct Settings
    {
        public Transform spawnPoint;
        public bool usePrefabAuthoredTransform;
        public Vector3 fallbackSpawnPosition;
        public Vector3 fallbackSpawnEuler;
        public Vector3 spawnOffset;
        public float runSpawnLiftY;
        public float jumpSpawnLiftY;
    }

    private Settings settings;

    public ExperimentDirectorSpawnPoseCalculator(Settings settings)
    {
        this.settings = settings;
    }

    public void UpdateSettings(Settings newSettings)
    {
        settings = newSettings;
    }

    public void ResolveSpawnPose(GameObject prefab, SkillSlot slot, out Vector3 position, out Quaternion rotation)
    {
        float skillLift = GetSkillSpawnLift(slot);
        Vector3 totalOffset = settings.spawnOffset + new Vector3(0f, skillLift, 0f);

        if (settings.spawnPoint != null)
        {
            position = settings.spawnPoint.position + totalOffset;
            rotation = settings.spawnPoint.rotation;
            return;
        }

        if (settings.usePrefabAuthoredTransform && prefab != null)
        {
            position = prefab.transform.position + totalOffset;
            rotation = prefab.transform.rotation;
            return;
        }

        position = settings.fallbackSpawnPosition + totalOffset;
        rotation = Quaternion.Euler(settings.fallbackSpawnEuler);
    }

    public float GetSkillSpawnLift(SkillSlot slot)
    {
        switch (slot)
        {
            case SkillSlot.BipedRun:
                return settings.runSpawnLiftY;
            case SkillSlot.BipedJump:
                return settings.jumpSpawnLiftY;
            default:
                return 0f;
        }
    }
}
