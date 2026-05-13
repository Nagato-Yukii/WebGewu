using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WebTinkerLocalEditorBridge
{
    [Serializable]
    private class LocalPlayRequest
    {
        public string scenePath = "Assets/WebRL_workspace/WebTinkerRL.unity";
        public bool train = true;
        public string requestedAt = string.Empty;
        public string runId = "webtinkerrl";
        public int playDelaySeconds = 10;
    }

    private const string DefaultScenePath = "Assets/WebRL_workspace/WebTinkerRL.unity";
    private static readonly string SignalPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "webtinker-local-play.json"));
    private static readonly string BridgeLogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "webtinker-local-bridge.log"));
    private static LocalPlayRequest _pendingRequest;
    private static bool _pendingEnterPlay;
    private static double _playNotBeforeTime;

    static WebTinkerLocalEditorBridge()
    {
        WriteBridgeLog("Bridge initialized.");
        EditorApplication.update += OnEditorUpdate;
    }

    private static void WriteBridgeLog(string message)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string directory = Path.GetDirectoryName(BridgeLogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(BridgeLogPath, $"[{timestamp}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void OnEditorUpdate()
    {
        if (_pendingEnterPlay)
        {
            TryEnterPlayMode();
            return;
        }

        if (!File.Exists(SignalPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(SignalPath);
            WriteBridgeLog($"Signal detected. Payload={json}");
            File.Delete(SignalPath);
            _pendingRequest = JsonUtility.FromJson<LocalPlayRequest>(json) ?? new LocalPlayRequest();
            QueueSceneAndPlay(_pendingRequest);
        }
        catch (Exception ex)
        {
            WriteBridgeLog($"Failed to process local start request. {ex}");
            Debug.LogWarning($"[WebTinkerLocalEditorBridge] Failed to process local start request. {ex.Message}");
            _pendingRequest = null;
            _pendingEnterPlay = false;
        }
    }

    private static void QueueSceneAndPlay(LocalPlayRequest request)
    {
        if (request == null)
        {
            request = new LocalPlayRequest();
        }

        WriteBridgeLog($"QueueSceneAndPlay called. scenePath={request.scenePath}, train={request.train}, delay={request.playDelaySeconds}");

        if (EditorApplication.isPlaying)
        {
            _pendingEnterPlay = true;
            _playNotBeforeTime = EditorApplication.timeSinceStartup + Math.Max(0, request.playDelaySeconds);
            WriteBridgeLog($"Editor currently playing. Scheduling stop and delayed restart at {_playNotBeforeTime:0.000}s.");
            EditorApplication.isPlaying = false;
            return;
        }

        string scenePath = string.IsNullOrWhiteSpace(request.scenePath) ? DefaultScenePath : request.scenePath;
        if (!File.Exists(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), scenePath)))
        {
            WriteBridgeLog($"Scene not found: {scenePath}");
            Debug.LogWarning($"[WebTinkerLocalEditorBridge] Scene not found: {scenePath}");
            _pendingRequest = null;
            _pendingEnterPlay = false;
            return;
        }

        _pendingEnterPlay = true;
        _playNotBeforeTime = EditorApplication.timeSinceStartup + Math.Max(0, request.playDelaySeconds);
        WriteBridgeLog($"Play scheduled at {_playNotBeforeTime:0.000}s.");
    }

    private static void TryEnterPlayMode()
    {
        if (EditorApplication.timeSinceStartup < _playNotBeforeTime)
        {
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string scenePath = _pendingRequest == null || string.IsNullOrWhiteSpace(_pendingRequest.scenePath)
            ? DefaultScenePath
            : _pendingRequest.scenePath;
        WriteBridgeLog($"Opening scene {scenePath} and entering play mode.");
        EditorSceneManager.OpenScene(scenePath);

        bool train = _pendingRequest == null || _pendingRequest.train;
        TinkercoinAgent.SetRequestedTrainingMode(train);
        _pendingEnterPlay = false;
        _playNotBeforeTime = 0d;
        _pendingRequest = null;
        WriteBridgeLog($"SetRequestedTrainingMode({train}) and toggling EditorApplication.isPlaying=true.");
        EditorApplication.isPlaying = true;
    }
}
