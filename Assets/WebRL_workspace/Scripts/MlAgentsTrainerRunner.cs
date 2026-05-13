using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class MlAgentsTrainerRunner : MonoBehaviour
{
    [Header("Trainer Command")]
    [SerializeField] private string shellExecutable = "cmd.exe";
    [SerializeField] private string shellArgumentsPrefix = "/c";
    [SerializeField] private string activationCommand = string.Empty;
    [SerializeField] private string condaEnvironmentName = "gewu";
    [SerializeField] private string trainerExecutable = "mlagents-learn";
    [SerializeField] private string configPath = "config.yaml";
    [SerializeField] private string runId = "webtinkerrl";
    [SerializeField] private string extraArguments = string.Empty;
    [SerializeField] private bool useForceOnFirstTrainingInSession = true;
    [SerializeField] private bool appendResumeAfterFirstTraining = true;
    [SerializeField] private bool autoStopOnDestroy = true;

    private Process _trainerProcess;
    private bool _hasStartedTrainingInSession;

    public bool IsTrainingRunning => _trainerProcess != null && !_trainerProcess.HasExited;

    public bool StartTraining()
    {
        if (IsTrainingRunning)
        {
            UnityEngine.Debug.Log($"[MlAgentsTrainerRunner] Training is already running. PID={_trainerProcess.Id}");
            return true;
        }

        string workingDirectory = Application.dataPath;
        string resolvedConfigPath = ResolveConfigPath(workingDirectory);
        string trainerArgs = BuildTrainerArguments(resolvedConfigPath);

        var startInfo = BuildStartInfo(workingDirectory, trainerArgs);

        _trainerProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        _trainerProcess.OutputDataReceived += HandleOutputDataReceived;
        _trainerProcess.ErrorDataReceived += HandleErrorDataReceived;
        _trainerProcess.Exited += HandleTrainerExited;

        try
        {
            _trainerProcess.Start();
            _trainerProcess.BeginOutputReadLine();
            _trainerProcess.BeginErrorReadLine();
            _hasStartedTrainingInSession = true;
            UnityEngine.Debug.Log($"[MlAgentsTrainerRunner] Started trainer with PID={_trainerProcess.Id}. Command: {trainerExecutable} {trainerArgs}");
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[MlAgentsTrainerRunner] Failed to start training process. {ex.Message}");
            CleanupProcessSubscriptions();
            _trainerProcess = null;
            return false;
        }
    }

    public void StopTraining()
    {
        if (!IsTrainingRunning)
        {
            return;
        }

        try
        {
            int pid = _trainerProcess.Id;
            if (Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer)
            {
                Process killProcess = new Process();
                killProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pid} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                killProcess.Start();
                killProcess.WaitForExit(5000);
                killProcess.Dispose();
            }
            else
            {
                _trainerProcess.Kill();
            }

            UnityEngine.Debug.Log($"[MlAgentsTrainerRunner] Stopped trainer process PID={pid}.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[MlAgentsTrainerRunner] Failed to stop trainer process cleanly. {ex.Message}");
        }
        finally
        {
            CleanupProcessSubscriptions();
            _trainerProcess = null;
        }
    }

    private void OnDestroy()
    {
        if (autoStopOnDestroy)
        {
            StopTraining();
        }
    }

    private string ResolveConfigPath(string workingDirectory)
    {
        if (Path.IsPathRooted(configPath))
        {
            return configPath;
        }

        return Path.Combine(workingDirectory, configPath);
    }

    private string BuildTrainerArguments(string resolvedConfigPath)
    {
        string quotedConfigPath = $"\"{resolvedConfigPath}\"";
        string arguments = $"{quotedConfigPath} --run-id={runId}";
        if (useForceOnFirstTrainingInSession && !_hasStartedTrainingInSession)
        {
            arguments += " --force";
        }
        else if (appendResumeAfterFirstTraining)
        {
            arguments += " --resume";
        }

        if (!string.IsNullOrWhiteSpace(extraArguments))
        {
            arguments += $" {extraArguments.Trim()}";
        }

        return arguments;
    }

    private ProcessStartInfo BuildStartInfo(string workingDirectory, string trainerArgs)
    {
        string condaExecutable = ResolveCondaExecutablePath();
        if (!string.IsNullOrWhiteSpace(condaExecutable))
        {
            if (condaExecutable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessStartInfo
                {
                    FileName = shellExecutable,
                    Arguments = $"{shellArgumentsPrefix} call \"{condaExecutable}\" run -n {condaEnvironmentName} --no-capture-output {trainerExecutable} {trainerArgs}",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

            return new ProcessStartInfo
            {
                FileName = condaExecutable,
                Arguments = $"run -n {condaEnvironmentName} --no-capture-output {trainerExecutable} {trainerArgs}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        string shellCommand = $"{ResolveActivationCommand()} && cd /d \"{workingDirectory}\" && {trainerExecutable} {trainerArgs}";
        return new ProcessStartInfo
        {
            FileName = shellExecutable,
            Arguments = $"{shellArgumentsPrefix} {shellCommand}",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }

    private string ResolveCondaExecutablePath()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidatePaths =
        {
            Path.Combine(userProfile, "anaconda3", "Scripts", "conda.exe"),
            Path.Combine(userProfile, "anaconda3", "condabin", "conda.bat"),
            Path.Combine(userProfile, "miniconda3", "condabin", "conda.bat"),
            Path.Combine(userProfile, "miniconda3", "Scripts", "conda.exe"),
        };

        for (int i = 0; i < candidatePaths.Length; i++)
        {
            if (File.Exists(candidatePaths[i]))
            {
                return candidatePaths[i];
            }
        }

        return string.Empty;
    }

    private string ResolveActivationCommand()
    {
        if (!string.IsNullOrWhiteSpace(activationCommand))
        {
            return activationCommand.Trim();
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidatePaths =
        {
            Path.Combine(userProfile, "anaconda3", "Scripts", "activate.bat"),
            Path.Combine(userProfile, "miniconda3", "Scripts", "activate.bat"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "anaconda3", "Scripts", "activate.bat"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "miniconda3", "Scripts", "activate.bat"),
        };

        for (int i = 0; i < candidatePaths.Length; i++)
        {
            if (File.Exists(candidatePaths[i]))
            {
                return $"call \"{candidatePaths[i]}\" {condaEnvironmentName}";
            }
        }

        return $"call conda activate {condaEnvironmentName}";
    }

    private void HandleOutputDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            UnityEngine.Debug.Log($"[MlAgentsTrainerRunner] {eventArgs.Data}");
        }
    }

    private void HandleErrorDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            UnityEngine.Debug.LogWarning($"[MlAgentsTrainerRunner] {eventArgs.Data}");
        }
    }

    private void HandleTrainerExited(object sender, EventArgs eventArgs)
    {
        int exitCode = 0;
        try
        {
            if (_trainerProcess != null)
            {
                exitCode = _trainerProcess.ExitCode;
            }
        }
        catch
        {
            exitCode = 0;
        }

        UnityEngine.Debug.Log($"[MlAgentsTrainerRunner] Trainer process exited with code {exitCode}.");
        CleanupProcessSubscriptions();
        _trainerProcess = null;
    }

    private void CleanupProcessSubscriptions()
    {
        if (_trainerProcess == null)
        {
            return;
        }

        _trainerProcess.OutputDataReceived -= HandleOutputDataReceived;
        _trainerProcess.ErrorDataReceived -= HandleErrorDataReceived;
        _trainerProcess.Exited -= HandleTrainerExited;
        _trainerProcess.Dispose();
    }
}
