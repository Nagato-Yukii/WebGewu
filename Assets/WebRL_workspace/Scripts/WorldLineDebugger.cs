using System;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldLineDebugger : MonoBehaviour
{
    // Local keyboard/overlay debugger for ExperimentDirector command injection.
    [Serializable]
    private struct DebugSkillOption
    {
        public string label;
        public string skillType;
    }

    [Serializable]
    private struct DebugRobotOption
    {
        public string robotName;
        public DebugSkillOption[] skills;
    }

    private static readonly DebugRobotOption[] BuiltInOptions =
    {
        new DebugRobotOption
        {
            robotName = "X02Lite",
            skills = new[]
            {
                new DebugSkillOption { label = "Walk", skillType = "bipedWalk" },
                new DebugSkillOption { label = "Run", skillType = "bipedRun" },
                new DebugSkillOption { label = "Jump", skillType = "bipedJump" }
            }
        },
        new DebugRobotOption
        {
            robotName = "Go2",
            skills = new[]
            {
                new DebugSkillOption { label = "Trot", skillType = "quadTrot" },
                new DebugSkillOption { label = "Bound", skillType = "quadBound" },
                new DebugSkillOption { label = "Pronk", skillType = "quadPronk" }
            }
        },
        new DebugRobotOption
        {
            robotName = "Go2W",
            skills = new[]
            {
                new DebugSkillOption { label = "Drive", skillType = "wheelDrive" },
                new DebugSkillOption { label = "Walk", skillType = "wheelWalk" },
                new DebugSkillOption { label = "Jump", skillType = "wheelJump" }
            }
        },
        new DebugRobotOption
        {
            robotName = "OpenLoong",
            skills = new[]
            {
                new DebugSkillOption { label = "Walk", skillType = "bipedWalk" },
                new DebugSkillOption { label = "Run", skillType = "bipedRun" },
                new DebugSkillOption { label = "Jump", skillType = "bipedJump" }
            }
        },
        new DebugRobotOption
        {
            robotName = "Tron1",
            skills = new[]
            {
                new DebugSkillOption { label = "Drive", skillType = "wheelDrive" },
                new DebugSkillOption { label = "Walk", skillType = "wheelWalk" },
                new DebugSkillOption { label = "Jump", skillType = "wheelJump" }
            }
        }
    };

    [Header("Target")]
    [SerializeField] private ExperimentDirector director;
    [SerializeField] private bool autoFindDirector = true;

    [Header("UI")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private Rect panelRect = new Rect(16f, 16f, 360f, 250f);

    [Header("Debug")]
    [SerializeField] private bool printHelpOnStart = true;
    [SerializeField] private bool logEachCommand = true;

    private int selectedRobotIndex;
    private string lastCommandLabel = string.Empty;

    private void Awake()
    {
        if (director == null && autoFindDirector)
        {
            director = FindObjectOfType<ExperimentDirector>();
        }

        if (director == null)
        {
            Debug.LogError("[WorldLineDebugger] ExperimentDirector not found.");
        }
    }

    private void Start()
    {
        if (printHelpOnStart)
        {
            Debug.Log("[WorldLineDebugger] F1=X02Lite, F2=Go2, F3=Go2W, F4=OpenLoong, F5=Tron1, 1/2/3=policy");
        }
    }

    private void Update()
    {
        if (director == null)
        {
            return;
        }

        if (Pressed(KeyCode.F1))
        {
            selectedRobotIndex = 0;
        }
        else if (Pressed(KeyCode.F2))
        {
            selectedRobotIndex = 1;
        }
        else if (Pressed(KeyCode.F3))
        {
            selectedRobotIndex = 2;
        }
        else if (Pressed(KeyCode.F4))
        {
            selectedRobotIndex = 3;
        }
        else if (Pressed(KeyCode.F5))
        {
            selectedRobotIndex = 4;
        }

        if (Pressed(KeyCode.Alpha1, KeyCode.Keypad1))
        {
            SendSelectedSkill(0);
        }
        else if (Pressed(KeyCode.Alpha2, KeyCode.Keypad2))
        {
            SendSelectedSkill(1);
        }
        else if (Pressed(KeyCode.Alpha3, KeyCode.Keypad3))
        {
            SendSelectedSkill(2);
        }
    }

    private void OnGUI()
    {
        if (!showOverlay)
        {
            return;
        }

        Rect runtimeRect = new Rect(
            panelRect.x,
            panelRect.y,
            panelRect.width,
            Mathf.Clamp(Screen.height - panelRect.y - 16f, 220f, 340f));

        GUILayout.BeginArea(runtimeRect, GUI.skin.box);
        GUILayout.Label("WebRL Local Debugger");
        GUILayout.Label($"Robot: {GetCurrentRobot().robotName}");

        for (int row = 0; row < BuiltInOptions.Length; row += 3)
        {
            GUILayout.BeginHorizontal();
            for (int i = row; i < Mathf.Min(row + 3, BuiltInOptions.Length); i++)
            {
                DrawRobotButton(i);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);
        GUILayout.Label("Policy");

        DebugRobotOption robot = GetCurrentRobot();
        for (int i = 0; i < robot.skills.Length; i++)
        {
            string buttonLabel = $"{i + 1}. {robot.skills[i].label}";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(22f)))
            {
                SendSelectedSkill(i);
            }
        }

        if (!string.IsNullOrEmpty(lastCommandLabel))
        {
            GUILayout.Space(4f);
            GUILayout.Label(lastCommandLabel);
        }

        GUILayout.EndArea();
    }

    private void DrawRobotButton(int index)
    {
        if (index < 0 || index >= BuiltInOptions.Length)
        {
            return;
        }

        bool isSelected = selectedRobotIndex == index;
        Color oldColor = GUI.backgroundColor;
        if (isSelected)
        {
            GUI.backgroundColor = new Color(0.75f, 0.9f, 1f);
        }

        if (GUILayout.Button(BuiltInOptions[index].robotName, GUILayout.Width(104f), GUILayout.Height(22f)))
        {
            selectedRobotIndex = index;
        }

        GUI.backgroundColor = oldColor;
    }

    private void SendSelectedSkill(int skillIndex)
    {
        DebugRobotOption robot = GetCurrentRobot();
        if (skillIndex < 0 || skillIndex >= robot.skills.Length)
        {
            return;
        }

        Send(robot.robotName, robot.skills[skillIndex].skillType);
    }

    private DebugRobotOption GetCurrentRobot()
    {
        if (selectedRobotIndex < 0 || selectedRobotIndex >= BuiltInOptions.Length)
        {
            selectedRobotIndex = 0;
        }

        return BuiltInOptions[selectedRobotIndex];
    }

    private static bool Pressed(KeyCode key)
    {
        return Input.GetKeyDown(key);
    }

    private static bool Pressed(KeyCode a, KeyCode b)
    {
        return Input.GetKeyDown(a) || Input.GetKeyDown(b);
    }

    private void Send(string robotName, string skillType)
    {
        WebCommand cmd = new WebCommand
        {
            robotName = robotName,
            skillType = skillType
        };

        string json = JsonUtility.ToJson(cmd);
        director.ExecuteWebCommand(json);
        lastCommandLabel = $"{robotName} / {skillType}";

        if (logEachCommand)
        {
            Debug.Log($"[WorldLineDebugger] {json}");
        }
    }
}
