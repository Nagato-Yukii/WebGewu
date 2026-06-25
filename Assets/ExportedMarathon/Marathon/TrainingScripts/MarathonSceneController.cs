using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MarathonSceneController : MonoBehaviour
{
    private const string CanvasName = "MarathonRuntimeCanvas";

    private enum UiMode
    {
        Main,
        RobotSelect,
        CpgControls,
        Running
    }

    private readonly List<GameObject> viewObjects = new List<GameObject>();
    private readonly List<GameObject> playObjects = new List<GameObject>();
    private readonly List<SliderBinding> activeBindings = new List<SliderBinding>();

    private GameObject tinkerViewObject;
    private GameObject g1ViewObject;
    private GameObject go2ViewObject;
    private GameObject tinkerPlayObject;
    private GameObject g1PlayObject;
    private GameObject go2PlayObject;
    private GameObject activeViewObject;
    private GameObject activeTinkerPlayObject;
    private GameObject activeG1PlayObject;
    private GameObject activeGo2PlayObject;

    private MarathonTinkerCPGView tinkerView;
    private MarathonG1CPGView g1View;
    private MarathonGo2CPGView go2View;
    private MarathonTinkerCPGTrain tinkerPlay;
    private MarathonG1CPGTrain g1Play;
    private MarathonGo2CPGTrain go2Play;

    private Canvas canvas;
    private GameObject mainPanel;
    private GameObject robotPanel;
    private GameObject controlsPanel;
    private GameObject runPanel;
    private Text titleText;
    private Text statusText;
    private Transform controlRows;
    private UiMode uiMode = UiMode.Main;
    private string activeRobotName = string.Empty;
    private string status = "Select a mode.";
    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private Camera sceneCamera;
    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;
    private bool hasInitialCameraPose;
    private bool marathonCameraMoving;
    private Coroutine focusRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapCurrentScene()
    {
        TryCreateForScene(SceneManager.GetActiveScene());
    }

    private static void TryCreateForScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "GaitMarathon")
        {
            return;
        }

        if (FindObjectOfType<MarathonSceneController>(true) != null)
        {
            return;
        }

        var controllerObject = new GameObject("MarathonSceneController");
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        controllerObject.AddComponent<MarathonSceneController>();
    }

    private void Awake()
    {
        ResolveSceneObjects();
        CaptureInitialCameraPose();
        ShowMainMenu();
    }

    private void Update()
    {
        for (int i = 0; i < activeBindings.Count; i++)
        {
            activeBindings[i].RefreshLabel();
        }

        if (marathonCameraMoving && sceneCamera != null)
        {
            sceneCamera.transform.position += Vector3.forward * (0.2f * Time.deltaTime);
        }
    }

    private void OnGUI()
    {
        EnsureGuiStyles();

        GUILayout.BeginArea(new Rect(24f, 24f, 380f, Screen.height - 48f), boxStyle);
        GUILayout.Label("Marathon", titleStyle);
        GUILayout.Space(8f);

        switch (uiMode)
        {
            case UiMode.Main:
                DrawMainMenu();
                break;
            case UiMode.RobotSelect:
                DrawRobotSelect();
                break;
            case UiMode.CpgControls:
                DrawCpgControls();
                break;
            case UiMode.Running:
                DrawRunningPanel();
                break;
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label(status, labelStyle);
        GUILayout.EndArea();
    }

    public void ShowMainMenu()
    {
        uiMode = UiMode.Main;
        activeRobotName = string.Empty;
        SetPanel(mainPanel, true);
        SetPanel(robotPanel, false);
        SetPanel(controlsPanel, false);
        SetPanel(runPanel, false);
        SetAllViews(false);
        SetAllPlayObjects(false);
        DestroyRuntimeInstances();
        marathonCameraMoving = false;
        SetStatus("Select a mode.");
    }

    public void ShowRobotSelect()
    {
        uiMode = UiMode.RobotSelect;
        activeRobotName = string.Empty;
        SetPanel(mainPanel, false);
        SetPanel(robotPanel, true);
        SetPanel(controlsPanel, false);
        SetPanel(runPanel, false);
        SetAllViews(false);
        SetAllPlayObjects(false);
        DestroyRuntimeInstances();
        marathonCameraMoving = false;
        SetStatus("Choose a robot for CPG view.");
    }

    public void ShowTinkerView()
    {
        uiMode = UiMode.CpgControls;
        activeRobotName = "Tinker";
        SetActiveView(tinkerViewObject);
        SetStatus("Tinker CPG view.");
    }

    public void ShowG1View()
    {
        uiMode = UiMode.CpgControls;
        activeRobotName = "G1 23DoF";
        SetActiveView(g1ViewObject);
        SetStatus("G1 23DoF CPG view.");
    }

    public void ShowGo2View()
    {
        uiMode = UiMode.CpgControls;
        activeRobotName = "Go2";
        SetActiveView(go2ViewObject);
        SetStatus("Go2 CPG view.");
    }

    public void StartMarathonInference()
    {
        uiMode = UiMode.Running;
        activeRobotName = string.Empty;
        SetPanel(mainPanel, false);
        SetPanel(robotPanel, false);
        SetPanel(controlsPanel, false);
        SetPanel(runPanel, true);
        marathonCameraMoving = false;
        SetAllViews(false);

        SetAllPlayObjects(false);
        DestroyRuntimeInstances();
        activeTinkerPlayObject = InstantiateRuntimeClone(tinkerPlayObject, "Tinker_play_runtime");
        activeG1PlayObject = InstantiateRuntimeClone(g1PlayObject, "g1_23dof_play_runtime");
        activeGo2PlayObject = InstantiateRuntimeClone(go2PlayObject, "go2_play_runtime");
        var activeTinkerPlay = GetComponentInChildrenIncludingInactive<MarathonTinkerCPGTrain>(activeTinkerPlayObject);
        var activeG1Play = GetComponentInChildrenIncludingInactive<MarathonG1CPGTrain>(activeG1PlayObject);
        var activeGo2Play = GetComponentInChildrenIncludingInactive<MarathonGo2CPGTrain>(activeGo2PlayObject);
        PreparePlayAgent(activeTinkerPlayObject, activeTinkerPlay);
        PreparePlayAgent(activeG1PlayObject, activeG1Play);
        PreparePlayAgent(activeGo2PlayObject, activeGo2Play);
        RestoreInitialCameraPose();
        marathonCameraMoving = true;
        StartCoroutine(RestartPlayAgentsNextFrame(activeTinkerPlay, activeG1Play, activeGo2Play));

        SetStatus("Marathon inference running.");
    }

    public void ResetMarathon()
    {
        SetAllPlayObjects(false);
        SetAllViews(false);
        DestroyRuntimeInstances();
        marathonCameraMoving = false;
        RestoreInitialCameraPose();
        ShowMainMenu();
    }

    public void BackToWebGewuMenu()
    {
        var director = FindObjectOfType<SceneDirector>(true);
        if (director != null)
        {
            director.ReturnToMenu();
            return;
        }

        SceneManager.LoadScene("GlobalManager");
    }

    private void ResolveSceneObjects()
    {
        tinkerViewObject = FindSceneObject("Tinker_view");
        g1ViewObject = FindSceneObject("g1_23dof_view");
        go2ViewObject = FindSceneObject("go2_view");
        tinkerPlayObject = FindFirstSceneObject("Tinker_play", "Tinker_train");
        g1PlayObject = FindFirstSceneObject("g1_23dof_play", "g1_23dof_rev_1_0 (1)");
        go2PlayObject = FindFirstSceneObject("go2_play", "go2_train");

        tinkerView = GetComponentInChildrenIncludingInactive<MarathonTinkerCPGView>(tinkerViewObject);
        g1View = GetComponentInChildrenIncludingInactive<MarathonG1CPGView>(g1ViewObject);
        go2View = GetComponentInChildrenIncludingInactive<MarathonGo2CPGView>(go2ViewObject);
        tinkerPlay = GetComponentInChildrenIncludingInactive<MarathonTinkerCPGTrain>(tinkerPlayObject);
        g1Play = GetComponentInChildrenIncludingInactive<MarathonG1CPGTrain>(g1PlayObject);
        go2Play = GetComponentInChildrenIncludingInactive<MarathonGo2CPGTrain>(go2PlayObject);

        AddIfNotNull(viewObjects, tinkerViewObject);
        AddIfNotNull(viewObjects, g1ViewObject);
        AddIfNotNull(viewObjects, go2ViewObject);
        AddIfNotNull(playObjects, tinkerPlayObject);
        AddIfNotNull(playObjects, g1PlayObject);
        AddIfNotNull(playObjects, go2PlayObject);
        SetAllViews(false);
        SetAllPlayObjects(false);
    }

    private void BuildUi()
    {
        if (GameObject.Find(CanvasName) != null)
        {
            return;
        }

        var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        mainPanel = CreatePanel("MainMenuPanel", new Vector2(360f, 320f), new Vector2(30f, -30f));
        CreateText(mainPanel.transform, "Marathon", 26, TextAnchor.MiddleLeft, new Vector2(300f, 40f));
        CreateButton(mainPanel.transform, "CPG View", ShowRobotSelect);
        CreateButton(mainPanel.transform, "Marathon Start", StartMarathonInference);
        CreateButton(mainPanel.transform, "Back To WebGewu", BackToWebGewuMenu);

        robotPanel = CreatePanel("RobotSelectPanel", new Vector2(360f, 360f), new Vector2(30f, -30f));
        CreateText(robotPanel.transform, "CPG View", 24, TextAnchor.MiddleLeft, new Vector2(300f, 40f));
        CreateButton(robotPanel.transform, "Tinker", ShowTinkerView);
        CreateButton(robotPanel.transform, "G1 23DoF", ShowG1View);
        CreateButton(robotPanel.transform, "Go2", ShowGo2View);
        CreateButton(robotPanel.transform, "Back", ShowMainMenu);

        controlsPanel = CreatePanel("CPGControlPanel", new Vector2(460f, 500f), new Vector2(30f, -30f));
        titleText = CreateText(controlsPanel.transform, "CPG", 24, TextAnchor.MiddleLeft, new Vector2(400f, 40f));
        controlRows = CreateVerticalGroup(controlsPanel.transform, "Rows", new Vector2(420f, 300f)).transform;
        CreateButton(controlsPanel.transform, "Reset Phase", ResetActiveViewPhase);
        CreateButton(controlsPanel.transform, "Back", ShowRobotSelect);

        runPanel = CreatePanel("MarathonRunPanel", new Vector2(340f, 180f), new Vector2(30f, -30f));
        CreateText(runPanel.transform, "Marathon Running", 22, TextAnchor.MiddleLeft, new Vector2(280f, 36f));
        CreateButton(runPanel.transform, "Reset", ResetMarathon);
        CreateButton(runPanel.transform, "Back", ShowMainMenu);

        statusText = CreateText(canvasObject.transform, "", 18, TextAnchor.MiddleLeft, new Vector2(640f, 32f));
        var statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(0f, 0f);
        statusRect.pivot = new Vector2(0f, 0f);
        statusRect.anchoredPosition = new Vector2(24f, 20f);
    }

    private void BuildTinkerControls()
    {
        ClearControlRows();
        titleText.text = "Tinker CPG";
        if (tinkerView == null)
        {
            AddMissingTargetText();
            return;
        }

        AddSlider("T1", 5f, 80f, tinkerView.T1, v => tinkerView.SetT1(Mathf.RoundToInt(v)), () => tinkerView.T1);
        AddSlider("d0", -60f, 80f, tinkerView.d0, tinkerView.SetD0, () => tinkerView.d0);
        AddSlider("dh", 0f, 80f, tinkerView.dh, tinkerView.SetDh, () => tinkerView.dh);
    }

    private void BuildG1Controls()
    {
        ClearControlRows();
        titleText.text = "G1 23DoF CPG";
        if (g1View == null)
        {
            AddMissingTargetText();
            return;
        }

        AddSlider("T1", 5f, 100f, g1View.T1, v => g1View.SetT1(Mathf.RoundToInt(v)), () => g1View.T1);
        AddSlider("d0", -60f, 80f, g1View.d0, g1View.SetD0, () => g1View.d0);
        AddSlider("dh", 0f, 80f, g1View.dh, g1View.SetDh, () => g1View.dh);
        AddSlider("vr", 0f, 3f, g1View.vr, g1View.SetVr, () => g1View.vr);
    }

    private void BuildGo2Controls()
    {
        ClearControlRows();
        titleText.text = "Go2 CPG";
        if (go2View == null)
        {
            AddMissingTargetText();
            return;
        }

        AddSlider("T1", 5f, 100f, go2View.T1, v => go2View.SetT1(Mathf.RoundToInt(v)), () => go2View.T1);
        AddSlider("dh", 0f, 80f, go2View.dh, go2View.SetDh, () => go2View.dh);
        AddSlider("k0", 0f, 1.5f, go2View.k0, go2View.SetK0, () => go2View.k0);
        AddSlider("gait", 0f, 1f, go2View.gait, v => go2View.SetGait(Mathf.RoundToInt(v)), () => go2View.gait);
    }

    private void AddSlider(string label, float min, float max, float value, Action<float> setter, Func<float> getter)
    {
        var row = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(controlRows, false);
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childForceExpandWidth = false;

        var labelText = CreateText(row.transform, label, 16, TextAnchor.MiddleLeft, new Vector2(70f, 34f));
        var sliderObject = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        sliderObject.transform.SetParent(row.transform, false);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(230f, 28f);
        var slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.wholeNumbers = (Math.Abs(max - min) > 2f && label == "T1") || label == "gait";
        CreateSliderVisuals(sliderObject.transform, slider);

        var valueText = CreateText(row.transform, "", 16, TextAnchor.MiddleRight, new Vector2(80f, 34f));
        slider.onValueChanged.AddListener(v =>
        {
            setter(v);
            valueText.text = FormatValue(getter());
        });
        valueText.text = FormatValue(getter());
        activeBindings.Add(new SliderBinding(valueText, getter));
        _ = labelText;
    }

    private void CreateSliderVisuals(Transform parent, Slider slider)
    {
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(parent, false);
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.35f);
        bgRect.anchorMax = new Vector2(1f, 0.65f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.2f, 0.95f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(parent, false);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = new Color(0.25f, 0.62f, 0.9f, 1f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(parent, false);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 28f);
        handle.GetComponent<Image>().color = new Color(0.94f, 0.94f, 0.9f, 1f);

        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
    }

    private GameObject CreatePanel(string name, Vector2 size, Vector2 anchoredPosition)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(canvas.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.08f, 0.82f);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        return panel;
    }

    private GameObject CreateVerticalGroup(Transform parent, string name, Vector2 size)
    {
        var group = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        group.transform.SetParent(parent, false);
        group.GetComponent<RectTransform>().sizeDelta = size;
        var layout = group.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        return group;
    }

    private Text CreateText(Transform parent, string text, int size, TextAnchor alignment, Vector2 rectSize)
    {
        var textObject = new GameObject(text + "Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = rectSize;
        var uiText = textObject.GetComponent<Text>();
        uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        uiText.text = text;
        uiText.fontSize = size;
        uiText.alignment = alignment;
        uiText.color = new Color(0.94f, 0.94f, 0.9f, 1f);
        return uiText;
    }

    private void CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction callback)
    {
        var buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 44f);
        buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.21f, 0.25f, 0.95f);
        var button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(callback);
        var label = CreateText(buttonObject.transform, text, 17, TextAnchor.MiddleCenter, new Vector2(280f, 44f));
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void SetActiveView(GameObject target)
    {
        SetPanel(mainPanel, false);
        SetPanel(robotPanel, false);
        SetPanel(controlsPanel, true);
        SetPanel(runPanel, false);
        SetAllPlayObjects(false);
        DestroyRuntimeInstances();
        SetAllViews(false);

        activeViewObject = InstantiateRuntimeClone(target, target != null ? target.name + "_runtime" : "cpg_view_runtime");
        tinkerView = GetComponentInChildrenIncludingInactive<MarathonTinkerCPGView>(activeViewObject);
        g1View = GetComponentInChildrenIncludingInactive<MarathonG1CPGView>(activeViewObject);
        go2View = GetComponentInChildrenIncludingInactive<MarathonGo2CPGView>(activeViewObject);

        FocusCameraOnNextFrame(activeViewObject);
    }

    private void SetAllViews(bool active)
    {
        for (int i = 0; i < viewObjects.Count; i++)
        {
            viewObjects[i].SetActive(active);
        }
    }

    private void SetAllPlayObjects(bool active)
    {
        for (int i = 0; i < playObjects.Count; i++)
        {
            playObjects[i].SetActive(active);
        }
    }

    private void PreparePlayAgent(GameObject agentObject, MarathonTinkerCPGTrain agent)
    {
        if (agentObject == null || agent == null)
        {
            Debug.LogWarning("[Marathon] Tinker play object or agent was not found.");
            return;
        }

        agent.train = false;
        agent.fixbody = false;
        agent.SetInferenceMode();

        var behaviorParameters = agentObject.GetComponentInChildren<BehaviorParameters>(true);
        if (behaviorParameters != null)
        {
            behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
        }
    }

    private void PreparePlayAgent(GameObject agentObject, MarathonG1CPGTrain agent)
    {
        if (agentObject == null || agent == null)
        {
            Debug.LogWarning("[Marathon] G1 play object or agent was not found.");
            return;
        }

        agent.train = false;
        agent.fixbody = false;
        agent.SetInferenceMode();

        var behaviorParameters = agentObject.GetComponentInChildren<BehaviorParameters>(true);
        if (behaviorParameters != null)
        {
            behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
        }
    }

    private void PreparePlayAgent(GameObject agentObject, MarathonGo2CPGTrain agent)
    {
        if (agentObject == null || agent == null)
        {
            Debug.LogWarning("[Marathon] Go2 play object or agent was not found.");
            return;
        }

        agent.train = false;
        agent.fixbody = false;
        agent.SetInferenceMode();

        var behaviorParameters = agentObject.GetComponentInChildren<BehaviorParameters>(true);
        if (behaviorParameters != null)
        {
            behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
        }
    }

    private void ResetActiveViewPhase()
    {
        if (tinkerView != null) tinkerView.ResetPhase();
        if (g1View != null) g1View.ResetPhase();
        if (go2View != null) go2View.ResetPhase();
    }

    private void ClearControlRows()
    {
        activeBindings.Clear();
        for (int i = controlRows.childCount - 1; i >= 0; i--)
        {
            Destroy(controlRows.GetChild(i).gameObject);
        }
    }

    private void AddMissingTargetText()
    {
        CreateText(controlRows, "Target object or component was not found.", 16, TextAnchor.MiddleLeft, new Vector2(380f, 40f));
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    private void SetStatus(string status)
    {
        this.status = status;
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    private void DrawMainMenu()
    {
        if (GUILayout.Button("CPG View", GUILayout.Height(44f)))
        {
            ShowRobotSelect();
        }

        if (GUILayout.Button("Marathon Start", GUILayout.Height(44f)))
        {
            StartMarathonInference();
        }

        if (GUILayout.Button("Back To WebGewu", GUILayout.Height(44f)))
        {
            BackToWebGewuMenu();
        }
    }

    private void DrawRobotSelect()
    {
        GUILayout.Label("CPG View", titleStyle);
        if (GUILayout.Button("Tinker", GUILayout.Height(40f)))
        {
            ShowTinkerView();
        }

        if (GUILayout.Button("G1 23DoF", GUILayout.Height(40f)))
        {
            ShowG1View();
        }

        if (GUILayout.Button("Go2", GUILayout.Height(40f)))
        {
            ShowGo2View();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Back", GUILayout.Height(38f)))
        {
            ShowMainMenu();
        }
    }

    private void DrawCpgControls()
    {
        GUILayout.Label(activeRobotName + " CPG", titleStyle);

        if (activeRobotName == "Tinker" && tinkerView != null)
        {
            tinkerView.SetT1(Mathf.RoundToInt(DrawSlider("T1", tinkerView.T1, 5f, 80f)));
            tinkerView.SetD0(DrawSlider("d0", tinkerView.d0, -60f, 80f));
            tinkerView.SetDh(DrawSlider("dh", tinkerView.dh, 0f, 80f));
        }
        else if (activeRobotName == "G1 23DoF" && g1View != null)
        {
            g1View.SetT1(Mathf.RoundToInt(DrawSlider("T1", g1View.T1, 5f, 100f)));
            g1View.SetD0(DrawSlider("d0", g1View.d0, -60f, 80f));
            g1View.SetDh(DrawSlider("dh", g1View.dh, 0f, 80f));
            g1View.SetVr(DrawSlider("vr", g1View.vr, 0f, 3f));
        }
        else if (activeRobotName == "Go2" && go2View != null)
        {
            go2View.SetT1(Mathf.RoundToInt(DrawSlider("T1", go2View.T1, 5f, 100f)));
            go2View.SetDh(DrawSlider("dh", go2View.dh, 0f, 80f));
            go2View.SetK0(DrawSlider("k0", go2View.k0, 0f, 1.5f));
            go2View.SetGait(Mathf.RoundToInt(DrawSlider("gait", go2View.gait, 0f, 1f)));
        }
        else
        {
            GUILayout.Label("Target object or component was not found.", labelStyle);
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Reset Phase", GUILayout.Height(38f)))
        {
            ResetActiveViewPhase();
        }

        if (GUILayout.Button("Back", GUILayout.Height(38f)))
        {
            ShowRobotSelect();
        }
    }

    private void DrawRunningPanel()
    {
        GUILayout.Label("Marathon Running", titleStyle);
        GUILayout.Label("Three inference robots are active.", labelStyle);

        if (GUILayout.Button("Reset", GUILayout.Height(40f)))
        {
            ResetMarathon();
        }

        if (GUILayout.Button("Back", GUILayout.Height(40f)))
        {
            ShowMainMenu();
        }
    }

    private float DrawSlider(string name, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, labelStyle, GUILayout.Width(52f));
        float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(210f));
        GUILayout.Label(FormatValue(next), labelStyle, GUILayout.Width(64f));
        GUILayout.EndHorizontal();
        return next;
    }

    private void EnsureGuiStyles()
    {
        if (boxStyle != null)
        {
            return;
        }

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(18, 18, 16, 16)
        };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            normal = { textColor = Color.white }
        };
    }

    private static string FormatValue(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) < 0.001f ? Mathf.RoundToInt(value).ToString() : value.ToString("0.00");
    }

    private static void AddIfNotNull(List<GameObject> list, GameObject value)
    {
        if (value != null)
        {
            list.Add(value);
        }
    }

    private static T GetComponentInChildrenIncludingInactive<T>(GameObject root) where T : Component
    {
        return root == null ? null : root.GetComponentInChildren<T>(true);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        var transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            var candidate = transforms[i];
            if (candidate == null || candidate.gameObject.name != objectName)
            {
                continue;
            }

            if (candidate.gameObject.scene.IsValid() && candidate.gameObject.scene == SceneManager.GetActiveScene())
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindFirstSceneObject(params string[] objectNames)
    {
        for (int i = 0; i < objectNames.Length; i++)
        {
            var found = FindSceneObject(objectNames[i]);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void CaptureInitialCameraPose()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            sceneCamera = FindObjectOfType<Camera>(true);
        }

        if (sceneCamera == null)
        {
            return;
        }

        initialCameraPosition = sceneCamera.transform.position;
        initialCameraRotation = sceneCamera.transform.rotation;
        hasInitialCameraPose = true;
    }

    private void RestoreInitialCameraPose()
    {
        if (sceneCamera == null)
        {
            CaptureInitialCameraPose();
        }

        if (sceneCamera == null || !hasInitialCameraPose)
        {
            return;
        }

        sceneCamera.gameObject.SetActive(true);
        sceneCamera.transform.position = initialCameraPosition;
        sceneCamera.transform.rotation = initialCameraRotation;
    }

    private void FocusCameraOnNextFrame(GameObject target)
    {
        if (focusRoutine != null)
        {
            StopCoroutine(focusRoutine);
        }

        focusRoutine = StartCoroutine(FocusCameraRoutine(target));
    }

    private IEnumerator FocusCameraRoutine(GameObject target)
    {
        yield return null;
        FocusCameraOn(target);
        focusRoutine = null;
    }

    private IEnumerator RestartPlayAgentsNextFrame(
        MarathonTinkerCPGTrain activeTinker,
        MarathonG1CPGTrain activeG1,
        MarathonGo2CPGTrain activeGo2)
    {
        yield return null;
        RestartPlayAgent(activeTinker);
        RestartPlayAgent(activeG1);
        RestartPlayAgent(activeGo2);
    }

    private static void RestartPlayAgent(MarathonTinkerCPGTrain agent)
    {
        if (agent == null) return;
        agent.RestartInferenceEpisode();
        agent.RequestDecision();
    }

    private static void RestartPlayAgent(MarathonG1CPGTrain agent)
    {
        if (agent == null) return;
        agent.RestartInferenceEpisode();
        agent.RequestDecision();
    }

    private static void RestartPlayAgent(MarathonGo2CPGTrain agent)
    {
        if (agent == null) return;
        agent.RestartInferenceEpisode();
        agent.RequestDecision();
    }

    private GameObject InstantiateRuntimeClone(GameObject template, string cloneName)
    {
        if (template == null)
        {
            Debug.LogWarning($"[Marathon] Runtime clone template missing for '{cloneName}'.");
            return null;
        }

        template.SetActive(false);
        var clone = Instantiate(template, template.transform.position, template.transform.rotation, template.transform.parent);
        clone.name = cloneName;
        clone.transform.localScale = template.transform.localScale;
        clone.SetActive(true);
        template.SetActive(false);
        return clone;
    }

    private void DestroyRuntimeInstances()
    {
        DestroyRuntimeInstance(ref activeViewObject);
        DestroyRuntimeInstance(ref activeTinkerPlayObject);
        DestroyRuntimeInstance(ref activeG1PlayObject);
        DestroyRuntimeInstance(ref activeGo2PlayObject);
        tinkerView = null;
        g1View = null;
        go2View = null;
    }

    private static void DestroyRuntimeInstance(ref GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Destroy(instance);
        instance = null;
    }

    private static void FocusCameraOn(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        var camera = Camera.main;
        if (camera == null)
        {
            camera = FindObjectOfType<Camera>(true);
        }

        if (camera == null)
        {
            Debug.LogWarning("[Marathon] No camera found for CPG view focus.");
            return;
        }

        var bounds = CalculateBounds(target);
        var center = bounds.center;
        float height = Mathf.Max(1.2f, bounds.size.y);
        float distance = Mathf.Max(2f, bounds.extents.magnitude * 1.4f);
        Vector3 cameraPosition = center + new Vector3(-distance, height * 0.35f, 0f);

        camera.gameObject.SetActive(true);
        camera.transform.position = cameraPosition;
        camera.transform.rotation = Quaternion.LookRotation(center - cameraPosition, Vector3.up);
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.one);
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private sealed class SliderBinding
    {
        private readonly Text label;
        private readonly Func<float> getter;

        public SliderBinding(Text label, Func<float> getter)
        {
            this.label = label;
            this.getter = getter;
        }

        public void RefreshLabel()
        {
            if (label != null && getter != null)
            {
                label.text = FormatValue(getter());
            }
        }
    }
}
