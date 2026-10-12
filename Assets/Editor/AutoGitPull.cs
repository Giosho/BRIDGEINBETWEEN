using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Pulls new commits from GitHub automatically: once when the project opens, then every few minutes.
/// Uses "git pull --ff-only", so it never merges or overwrites your work. If a pull can't be done
/// safely it just logs a message and tries again later.
/// Toggle it or pull on demand from the Tools > Auto Git Pull menu.
/// </summary>
[InitializeOnLoad]
public static class AutoGitPull
{
    const string EnabledKey = "AutoGitPull.Enabled";
    const double IntervalSeconds = 300; // 5 minutes
    const int TimeoutMs = 60000;
    const string MenuEnabled = "Tools/Auto Git Pull/Enabled";
    const string MenuNow = "Tools/Auto Git Pull/Pull Now";

    static double nextCheck;
    static bool running;
    static volatile string pendingResult; // set by the background thread, handled on the main thread

    static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledKey, true);
        set => EditorPrefs.SetBool(EnabledKey, value);
    }

    static AutoGitPull()
    {
        nextCheck = EditorApplication.timeSinceStartup + 5; // first pull shortly after the editor opens
        EditorApplication.update += Update;
    }

    [MenuItem(MenuEnabled)]
    static void ToggleEnabled() => Enabled = !Enabled;

    [MenuItem(MenuEnabled, true)]
    static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(MenuEnabled, Enabled);
        return true;
    }

    [MenuItem(MenuNow)]
    static void PullNow() => StartPull(true);

    static void Update()
    {
        if (pendingResult != null)
        {
            HandleResult(pendingResult);
            pendingResult = null;
        }

        if (!Enabled || running || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + IntervalSeconds;
        StartPull(false);
    }

    static bool SafeToPull()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return false;

        // Don't pull under an unsaved scene: the incoming change could clash with it.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) return false;

        return true;
    }

    static void StartPull(bool manual)
    {
        if (running) return;
        if (!SafeToPull())
        {
            if (manual) Debug.Log("[AutoGitPull] Not pulling now (Play mode, compiling, or a scene has unsaved changes).");
            return;
        }

        running = true;
        string projectPath = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        Task.Run(() => pendingResult = RunGit(projectPath));
    }

    // Runs on a background thread. Never touches the Unity API.
    static string RunGit(string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "pull --ff-only")
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0"; // fail instead of hanging on a login prompt

            using (var p = Process.Start(psi))
            {
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(TimeoutMs))
                {
                    try { p.Kill(); } catch { }
                    return "ERROR: git pull timed out.";
                }
                string text = (outTask.Result + "\n" + errTask.Result).Trim();
                return (p.ExitCode == 0 ? "OK: " : "ERROR: ") + text;
            }
        }
        catch (Exception e)
        {
            return "ERROR: could not run git (" + e.Message + "). Is git installed and on PATH?";
        }
    }

    static void HandleResult(string result)
    {
        running = false;

        if (result.StartsWith("ERROR"))
        {
            Debug.LogWarning("[AutoGitPull] " + result);
            return;
        }

        if (result.Contains("Already up to date")) return; // nothing to do, stay quiet

        Debug.Log("[AutoGitPull] Pulled new changes:\n" + result);
        AssetDatabase.Refresh();
    }
}
