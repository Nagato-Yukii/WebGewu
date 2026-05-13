using UnityEngine;

public sealed class ExperimentDirectorCommandParser
{
    public bool TryParse(string jsonString, out WebCommand command)
    {
        command = JsonUtility.FromJson<WebCommand>(jsonString);
        if (command == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(command.robotName) &&
               !string.IsNullOrWhiteSpace(command.skillType);
    }
}
