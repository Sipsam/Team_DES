using System;
using System.IO;
using System.Linq;
using TwinEdge.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Gameplay.Editor
{
    public static class RiverValidation
    {
        const string LevelPath = "Assets/Game/Level/Scenes/LevelBlockoutV3.unity";
        const string Output = "Docs/Development/Gameplay/V1/RiverValidation";

        [MenuItem("Tools/Twin Edge/Gameplay/Validate Saved River")]
        public static void Validate()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(GameplayFoundationSetup.ScenePath);
            var scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);
            var river = UnityEngine.Object.FindFirstObjectByType<RiverGameplay>();
            if (river == null) throw new InvalidOperationException("Missing river NetworkBehaviour.");
            ValidateSourceAssets(river);
            var config = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ValleySectionV3>())
                .Single(section => section.part == ValleyPartV3.River).config;
            river.ValidateSavedChannel(scene, config, out string report);
            File.WriteAllText(Path.Combine(Output, "StaticGeometry.txt"), report);
            Debug.Log(report);
            EditorSceneManager.OpenScene(GameplayFoundationSetup.ScenePath);
        }

        static void ValidateSourceAssets(RiverGameplay river)
        {
            var serialized = new SerializedObject(river);
            foreach (var (field, id) in new[]
            {
                ("sharedModel", "MOV_RAFT_SHARED_01"), ("soloModel", "MOV_RAFT_SOLO_01"), ("oarModel", "MOV_RAFT_OAR_01"),
                ("dockModel", "FIX_RIVER_DOCK_01"), ("stairsModel", "FIX_RIVER_DOCK_STAIRS_01"),
                ("rockModel", "FIX_RIVER_ROCK_01"), ("driftwoodModel", "MOV_RIVER_DRIFTWOOD_01"),
                ("fishModel", "MOV_MONSTER_FISH_JAW_02"), ("crocodileModel", "MOV_RIVER_CROCODILE_01"),
                ("whirlpoolModel", "VFX_WHIRLPOOL_FUNNEL_02")
            })
            {
                var prefab = FindPrefab(id);
                if (serialized.FindProperty(field).objectReferenceValue != prefab)
                    throw new InvalidOperationException($"The V3 river scene reference {field} does not point to {id}.");
            }
            var water = FindPrefab("ENV_WATER_FACET_02").GetComponentInChildren<Renderer>().sharedMaterial;
            if (serialized.FindProperty("waterMaterial").objectReferenceValue != water)
                throw new InvalidOperationException("The river water material differs from the revised source.");
        }

        static GameObject FindPrefab(string id)
        {
            string path = AssetDatabase.FindAssets($"{id} t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .Single(candidate => Path.GetFileNameWithoutExtension(candidate) == id);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static void ValidateAndBuild()
        {
            Validate();
            GameplayFoundationSetup.BuildPlayer();
        }
    }
}
