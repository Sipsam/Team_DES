using System;
using System.IO;
using System.Linq;
using TwinEdge.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay.Editor
{
    public static class UndergroundGameplaySetup
    {
        const string GameplayScene = "Assets/Game/Gameplay/Scenes/V3Gameplay.unity";
        const string LevelScene = "Assets/Game/Level/Scenes/LevelBlockoutV3.unity";
        const string Art = "Assets/05_Gimmick_Working_Assets/04_Jump_Map_Treasure";
        static readonly (string field, string model)[] Models =
        {
            ("platformModel", "MOV_JUMP_PLATFORM_01"), ("leftSwitchModel", "MOV_JUMP_SWITCH_01"),
            ("rightSwitchModel", "MOV_JUMP_SWITCH_02"), ("altarModel", "FIX_GEM_ALTAR_01"),
            ("sealModel", "MOV_GEM_SEAL_01"), ("gemModel", "ITEM_GEM_01"),
            ("flameModel", "FIX_JUMP_FIRE_NOZZLE_01"), ("arrowModel", "MOV_TRAP_ARROW_01"),
            ("spikesModel", "MOV_JUMP_SPIKES_01"),
            ("vanishModel", "MOV_JUMP_VANISH_PLATFORM_01")
        };

        [MenuItem("Tools/Twin Edge/Gameplay/Install Underground Gameplay")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScene);
            var game = UnityEngine.Object.FindFirstObjectByType<GameStateManager>();
            var underground = game.GetComponent<UndergroundGameplay>() ?? game.gameObject.AddComponent<UndergroundGameplay>();
            var data = new SerializedObject(underground);
            foreach (var (field, model) in Models)
            {
                string path = AssetDatabase.FindAssets($"{model} t:Prefab", new[] { Art })
                    .Select(AssetDatabase.GUIDToAssetPath).Single(candidate => Path.GetFileNameWithoutExtension(candidate) == model);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null && prefab.GetComponentInChildren<Renderer>(true) != null, "Missing renderable " + model);
                data.FindProperty(field).objectReferenceValue = prefab;
                Debug.Log($"UNDERGROUND_MODEL {model}: {path}");
            }
            foreach (var (field, material) in new[] { ("leftMaterial", "RouteA"), ("rightMaterial", "RouteB"), ("warningMaterial", "Lava") })
                data.FindProperty(field).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>($"Assets/Game/Level/Data/FortressMaterials/{material}.mat");
            data.FindProperty("signShader").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Gameplay/Runtime/CastleWorldLabel.shader");
            data.ApplyModifiedPropertiesWithoutUndo();
            Require(EditorSceneManager.SaveScene(scene), "Failed to save gameplay scene");
            Validate();
        }

        [MenuItem("Tools/Twin Edge/Gameplay/Validate Saved Underground Gameplay")]
        public static void Validate()
        {
            EditorSceneManager.OpenScene(GameplayScene);
            var level = EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Additive);
            var underground = UnityEngine.Object.FindFirstObjectByType<UndergroundGameplay>();
            Require(underground != null && underground.GetComponent<GemOwnership>() != null, "Underground network scene components missing");
            var data = new SerializedObject(underground);
            foreach (var (field, model) in Models)
                Require(data.FindProperty(field).objectReferenceValue?.name == model, "Missing saved " + model);
            var sections = level.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FortressSectionV3>());
            var cave = sections.Single(s => s.part == FortressPartV3.UndergroundCavern);
            var access = sections.Single(s => s.part == FortressPartV3.UndergroundAccess);
            var keep = sections.Single(s => s.part == FortressPartV3.CastleInterior);
            foreach (var (section, name) in new[] { (cave, "FacetedCavernWalls"), (cave, "CavernFloor"),
                     (cave, "JointSealLanding"), (access, "U0Landing"), (keep, "HallToStairDoor") })
                Require(section.generated.transform.Find(name)?.GetComponent<Collider>() != null, $"Missing saved collider {name}");
            Require(cave.generated.transform.Find("Lava")?.GetComponent<Renderer>() != null, "Missing lava surface");
            Require(cave.config.platforms.Count(p => p.fixedLanding) == 8 && cave.config.platforms.Count(p => !p.fixedLanding) == 56,
                "Expected saved landings and provisional route reservations");
            Debug.Log("UNDERGROUND_STRUCTURE_PASS");
        }

        public static void Build()
        {
            string directory = Environment.GetEnvironmentVariable("TWIN_EDGE_UNDERGROUND_BUILD") ?? "Builds/V3Underground";
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameplayScene, LevelScene },
                locationPathName = Path.Combine(directory, "V3Gameplay.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Require(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded, "Underground Windows development build failed");
            Debug.Log("UNDERGROUND_BUILD_PASS");
        }

        public static void InstallAndBuild()
        {
            Install();
            Build();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
