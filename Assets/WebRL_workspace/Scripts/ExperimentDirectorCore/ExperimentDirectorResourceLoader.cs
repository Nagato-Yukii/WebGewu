using UnityEngine;

public sealed class ExperimentDirectorResourceLoader
{
    public bool TryLoad(string robotName, out GameObject prefab, out RobotConfig config)
    {
        prefab = Resources.Load<GameObject>($"Robots/{robotName}");
        config = Resources.Load<RobotConfig>($"RobotData/{robotName}_Data");
        return prefab != null && config != null;
    }
}
