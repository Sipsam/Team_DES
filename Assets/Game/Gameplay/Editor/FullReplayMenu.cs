using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TwinEdge.Levels.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TwinEdge.Gameplay.Editor
{
    internal static class FullReplayMenu
    {
        const string MenuPath = "Tools/Twin Edge/Run Full Replay";
        const string SoloMenuPath = "Tools/Twin Edge/Play Solo";
        const string BuildDirectory = "Builds/V3DevelopmentLocal";
        const string PlayerPath = BuildDirectory + "/V3Gameplay.exe";
        const string EvidenceRoot = "Docs/Development/Gameplay/V1/RelayRuns";
        static Process replay;
        static Task<string> output;
        static Task<string> errors;

        [MenuItem(MenuPath, true)]
        [MenuItem(SoloMenuPath, true)]
        static bool CanRun() => Application.platform == RuntimePlatform.WindowsEditor &&
            !EditorApplication.isPlayingOrWillChangePlaymode && replay == null;

        [MenuItem(MenuPath)]
        static void Run() => Launch(false);

        [MenuItem(SoloMenuPath)]
        static void PlaySolo() => Launch(true);

        static void Launch(bool solo)
        {
            if (!CanRun() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if (!solo && !File.Exists(Path.Combine(MountainPoleReachability.Output, "ContinuousPlan.json")))
                    MountainPoleTuning.VerifyCurrent();
                if (!solo && !File.Exists(Path.Combine(MountainReturnValidation.Output, "Route.json")))
                    MountainReturnValidation.Run();
                var assemblyTime = File.GetLastWriteTimeUtc(BuildDirectory + "/V3Gameplay_Data/Managed/Assembly-CSharp.dll");
                if (!File.Exists(PlayerPath) ||
                    File.GetLastWriteTimeUtc("Assets/Game/Gameplay/Runtime/LocalMatch.cs") > assemblyTime ||
                    File.GetLastWriteTimeUtc("Assets/Game/Gameplay/Testing/ReplayOrbitCamera.cs") > assemblyTime)
                    GameplayFoundationSetup.BuildWindowsDevelopment(BuildDirectory);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return;
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
            }

            string mode = solo ? "Solo" : "FullReview";
            string name = $"{mode}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            string arguments = solo ? "-Case solo" : "-Case full -Screenshots -Spectate";
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass " +
                    $"-File \"Tools/Playtest/Run-Relay.ps1\" -Build \"{PlayerPath}\" " +
                    $"-Run {name} {arguments}",
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                replay = Process.Start(start);
                output = replay.StandardOutput.ReadToEndAsync();
                errors = replay.StandardError.ReadToEndAsync();
                EditorApplication.update += CheckReplay;
                Debug.Log($"{mode} started. Results: {EvidenceRoot}/{name}");
            }
            catch (Exception error)
            {
                replay?.Dispose();
                replay = null;
                Debug.LogException(error);
            }
        }

        static void CheckReplay()
        {
            if (!replay.HasExited || !output.IsCompleted || !errors.IsCompleted)
                return;

            EditorApplication.update -= CheckReplay;
            int exitCode = replay.ExitCode;
            replay.Dispose();
            replay = null;
            if (exitCode == 0)
                Debug.Log(output.Result);
            else
                Debug.LogError($"Local play runner failed ({exitCode}).\n{output.Result}\n{errors.Result}");
        }
    }
}
