using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Gameplay.Editor
{
    public static class MountainReturnSetup
    {
        public const string ConfigPath = "Assets/Game/Gameplay/Prefabs/MountainReturnLaunchConfig.asset";
        [MenuItem("Tools/Twin Edge/Gameplay/Install Mountain Return Launch")]
        public static void Install()
        {
            var config = AssetDatabase.LoadAssetAtPath<MountainLaunchConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<MountainLaunchConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            var root = PrefabUtility.LoadPrefabContents(GameplayFoundationSetup.PlayerPath);
            try
            {
                var launch = root.GetComponent<MountainReturnLaunch>() ?? root.AddComponent<MountainReturnLaunch>(); launch.config = config;
                PrefabUtility.SaveAsPrefabAsset(root, GameplayFoundationSetup.PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            GameplayFoundationSetup.Validate();
            Debug.Log("MOUNTAIN_RETURN_INSTALLED");
        }
        public static void ValidateAndBuild()
        {
            GameplayFoundationSetup.Validate();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { GameplayFoundationSetup.ScenePath, "Assets/Game/Level/Scenes/LevelBlockoutV3.unity" },
                locationPathName = "Builds/MountainReturnValidation/V3Gameplay.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | (Environment.GetCommandLineArgs().Contains("-returnScriptsOnly") ? BuildOptions.BuildScriptsOnly : BuildOptions.None) });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Mountain return build failed");
            Debug.Log("MOUNTAIN_RETURN_BUILD_PASS");
        }
    }
}
