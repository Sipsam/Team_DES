using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TwinEdge.Gameplay.Editor
{
    public static class CastleGameplaySetup
    {
        const string GameplayScene = "Assets/Game/Gameplay/Scenes/V3Gameplay.unity";
        const string LevelScene = "Assets/Game/Level/Scenes/LevelBlockoutV3.unity";

        [MenuItem("Tools/Twin Edge/Gameplay/Install Castle Gameplay")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScene);
            var game = Object.FindFirstObjectByType<GameStateManager>();
            var castle = game.GetComponent<CastleGameplay>() ?? game.gameObject.AddComponent<CastleGameplay>();
            var data = new SerializedObject(castle);
            SetModels(data, "symbolModels", "MOV_PRESS_ROCK_02", "MOV_PRESS_SPIKE_02", "MOV_PRESS_WATER_02");
            SetModels(data, "trapModels", "MOV_TRAP_FIRE_02", "MOV_TRAP_SPIKES_02", "MOV_TRAP_HAMMER_01",
                "MOV_TRAP_ARROW_01", "MOV_TRAP_ROCK_02");
            SetModel(data, "doorModel", "MOV_CASTLE_GATE_LEAF_01");
            SetModel(data, "wallModel", "MOV_DOOR_WALL_MESSAGE_02");
            SetModel(data, "transferModel", "MOV_DOOR_TRANSFER_STONE_01");
            SetModel(data, "windowModel", "FIX_DOOR_INFO_WINDOW_01");
            SetModel(data, "hintModel", "FIX_DOOR_HINT_STONE_01");
            SetModel(data, "numberModel", "ITEM_DOOR_KEY_01");
            SetModel(data, "sequenceModel", "MOV_DOOR_SEQUENCE_3_02");
            foreach (var (field, name) in new[] { ("stoneMaterial", "Limestone"), ("darkMaterial", "Slate"), ("accentMaterial", "RouteB") })
                data.FindProperty(field).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>($"Assets/Game/Level/Data/FortressMaterials/{name}.mat");
            data.FindProperty("labelShader").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Gameplay/Runtime/CastleWorldLabel.shader");
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save V3Gameplay.");
            Validate();
        }

        public static void Build()
        {
            Directory.CreateDirectory("Builds/V3Castle");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameplayScene, LevelScene },
                locationPathName = "Builds/V3Castle/V3Gameplay.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("V3 Castle Windows development build failed.");
            Debug.Log("CASTLE_BUILD_PASS");
        }

        static GameObject FindModel(string name)
        {
            var path = AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/05_Gimmick_Working_Assets", "Assets/07_Revised_Gimmick_Models" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Single(candidate => Path.GetFileNameWithoutExtension(candidate) == name);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void SetModel(SerializedObject data, string field, string name) =>
            data.FindProperty(field).objectReferenceValue = FindModel(name);

        static void SetModels(SerializedObject data, string field, params string[] names)
        {
            var list = data.FindProperty(field);
            list.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = FindModel(names[i]);
        }

        [MenuItem("Tools/Twin Edge/Gameplay/Validate Saved Castle Gameplay")]
        public static void Validate()
        {
            EditorSceneManager.OpenScene(GameplayScene);
            Scene level = EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Additive);
            var castle = Object.FindFirstObjectByType<CastleGameplay>();
            if (castle == null) throw new InvalidOperationException("CastleGameplay missing in the saved gameplay scene.");
            var data = new SerializedObject(castle);
            foreach (string field in new[] { "wallModel", "doorModel", "transferModel", "windowModel", "hintModel",
                         "numberModel", "sequenceModel", "stoneMaterial", "darkMaterial", "accentMaterial", "labelShader" })
                if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing " + field);
            foreach (string field in new[] { "symbolModels", "trapModels" })
            {
                var list = data.FindProperty(field);
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == null) throw new InvalidOperationException("Missing " + field + " model " + i);
            }

            var keep = level.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TwinEdge.Levels.FortressSectionV3>())
                .Single(section => section.part == TwinEdge.Levels.FortressPartV3.CastleInterior);
            var walls = keep.generated.transform.Find("RemovablePartitions");
            if (keep.config.rooms.Length != 8 || walls == null || walls.Find("CentralSharedWall")?.GetComponent<Collider>() == null)
                throw new InvalidOperationException("Saved A/B room and partition layout is incomplete.");
            foreach (var room in keep.config.rooms)
                if (walls.Find(room.id + "/StageArch_Left")?.GetComponent<Collider>() == null)
                    throw new InvalidOperationException("Missing room portal collider for " + room.id);
            Debug.Log("CASTLE_STRUCTURE_PASS: saved scene reload, prefab references, eight generated rooms and shared-wall Collider.");
        }
    }
}
