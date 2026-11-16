using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TwinEdge.Gameplay.Editor
{
    public static class CooperativePoleSetup
    {
        const string PrefabPath = "Assets/Game/Gameplay/Prefabs/V3CooperativePole.prefab";
        const string Evidence = "Docs/Development/Gameplay/V1/CooperativePole";
        [MenuItem("Tools/Twin Edge/Gameplay/Install Cooperative Pole")]
        public static void Install()
        {
            Directory.CreateDirectory(Evidence);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(GameplayFoundationSetup.ScenePath);
            if (!File.Exists(PrefabPath))
            {
                var sourcePath = AssetDatabase.FindAssets("MOV_BRIDGE_POLE_01 t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Single();
                var root = new GameObject("PoleGameplayRoot", typeof(NetworkObject));
                var pole = root.AddComponent<CooperativePole>();
                pole.mountain = AssetDatabase.LoadAssetAtPath<MountainConfigV3>("Assets/Game/Level/Data/MountainConfigV3.asset");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
                model.transform.SetParent(root.transform, false);
                // Imported cylinder is 3 m along local X, centered at (0,.65,0).
                var bounds = new Bounds(); bool first = true;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
                File.WriteAllText(Evidence + "/ModelBounds.txt", $"Source {sourcePath}\nWorld identity bounds center={bounds.center:F4} size={bounds.size:F4}\nWrapper normalizes X length only; original asset unchanged.\n");
                var visual = new GameObject("PoleVisual").transform; visual.SetParent(root.transform, false); pole.visual = visual;
                model.transform.SetParent(visual, true); model.transform.position -= bounds.center;
                visual.localScale = new Vector3(pole.length / bounds.size.x, 1, 1);
                foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
                foreach (var script in model.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
                pole.leftGrip = Child(root, "LeftGrip"); pole.rightGrip = Child(root, "RightGrip"); pole.rotationPivot = Child(root, "RotationPivot");
                foreach (var grip in new[] { pole.leftGrip, pole.rightGrip }) { var col = grip.gameObject.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = .6f; }
                var support = GameObject.CreatePrimitive(PrimitiveType.Cylinder); support.name = "PrivateOrbitalSupport"; support.transform.SetParent(root.transform, false);
                support.transform.localScale = new Vector3(1.8f, .08f, 1.8f); pole.support = support.transform; pole.supportCollider = support.GetComponent<Collider>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetFloat("_Surface", 1); mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0); mat.SetColor("_BaseColor", new Color(.2f,.9f,1,.12f)); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
                AssetDatabase.CreateAsset(mat, "Assets/Game/Gameplay/Prefabs/PoleSupport.mat"); support.GetComponent<Renderer>().sharedMaterial = mat; support.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); Object.DestroyImmediate(root);
            }
            var bootstrap = Object.FindFirstObjectByType<V3GameplayBootstrap>();
            if (bootstrap.cooperativePole == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                instance.transform.SetParent(bootstrap.transform);
                bootstrap.cooperativePole = instance.GetComponent<CooperativePole>();
                EditorUtility.SetDirty(bootstrap);
                EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            }
            var player = PrefabUtility.LoadPrefabContents(GameplayFoundationSetup.PlayerPath);
            if (player.GetComponent<PoleMotor>() == null) player.AddComponent<PoleMotor>();
            PrefabUtility.SaveAsPrefabAsset(player, GameplayFoundationSetup.PlayerPath); PrefabUtility.UnloadPrefabContents(player);
            AssetDatabase.SaveAssets();
            Validate();
        }
        static Transform Child(GameObject root, string name) { var t = new GameObject(name).transform; t.SetParent(root.transform, false); return t; }
        [MenuItem("Tools/Twin Edge/Gameplay/Validate Cooperative Pole")]
        public static void Validate()
        {
            Directory.CreateDirectory(Evidence);
            EditorSceneManager.OpenScene(GameplayFoundationSetup.ScenePath);
            EditorSceneManager.OpenScene("Assets/Game/Level/Scenes/LevelBlockoutV3.unity", OpenSceneMode.Additive);
            Physics.SyncTransforms();
            var pole = Object.FindFirstObjectByType<CooperativePole>();
            if (pole == null || pole.mountain == null || pole.GetComponent<NetworkObject>() == null || pole.supportCollider == null) throw new Exception("Pole wrapper incomplete");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameplayFoundationSetup.PlayerPath).GetComponent<PoleMotor>() == null) throw new Exception("Player motor missing");
            var start = Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).Single(m => m.id == "Mountain.V3.Start");
            pole.ConfigureHome(start.transform.position);
            if (pole.support.gameObject.activeSelf) throw new Exception("Orbital support active without two participants");
            Vector3 homeA = pole.leftGrip.position - Vector3.up * 1.1f;
            Vector3 homeB = pole.rightGrip.position - Vector3.up * 1.1f;
            if (!pole.SafeLanding(homeA, out _) || !pole.SafeLanding(homeB, out _)) throw new Exception("Initial grips lack actual summit support");
            var sweep = typeof(CooperativePole).GetMethod("ClearPath",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GameplayFoundationSetup.PlayerPath));
            var identity = character.GetComponent<NetworkObject>();
            bool clear = (bool)sweep.Invoke(pole,new object[] { homeB, homeB + Vector3.forward * .1f, identity });
            bool blocked = !(bool)sweep.Invoke(pole,new object[] { homeB, homeB + Vector3.down * 3, identity });
            Object.DestroyImmediate(character);
            if (!clear || !blocked) throw new Exception($"Collider sweep checks failed: clear={clear}, blocked={blocked}");
            int samples = 0;
            for (int yaw = -180; yaw <= 180; yaw += 3)
                for (int pitch = -40; pitch <= 40; pitch += 2)
                {
                    var pivot = new Vector3(12,132,-225);
                    var end = PoleGeometry.Endpoint(pivot, pole.length, yaw, pitch);
                    if (Mathf.Abs(Vector3.Distance(pivot,end)-pole.length) > .0001f) throw new Exception("Rigid radius violated");
                    samples++;
                }
            var c = pole.mountain;
            var peaks = new[] { c.start }.Concat(c.smallPeaks).Concat(new[] { c.destination }).ToArray();
            Vector3 Position(MountainPeakV3 p) { Vector2 xz = p == c.start || p == c.destination ? p.position : c.clusterCenter + Vector2.Scale(p.position,c.clusterSize); return new Vector3(xz.x,p.height,xz.y); }
            float Radius(MountainPeakV3 p) => Mathf.Max(p.summitSize.x,p.summitSize.y)*.5f;
            var reached = new HashSet<int> { 0 }; bool changed;
            var lines = new List<string> { $"PASS wrapper/player/scene references; rigid radius samples={samples}.", "PASS actual V3 Collider queries: five-point support under both initial grips; clear horizontal capsule step; downward terrain penetration rejected.", "Optimistic graph only: circumscribed summit discs, +/-6 m surface variation. Edges do NOT prove a collision-free route." };
            do
            {
                changed = false;
                foreach (int i in reached.ToArray())
                    for (int j = 0; j < peaks.Length; j++)
                    {
                        var delta = Position(peaks[j])-Position(peaks[i]);
                        float horizontalGap = Mathf.Max(0, Vector3.ProjectOnPlane(delta,Vector3.up).magnitude - Radius(peaks[i]) - Radius(peaks[j]));
                        float dy = Mathf.Max(0,Mathf.Abs(delta.y)-12);
                        if (dy <= pole.length*Mathf.Sin(pole.maxPitch*Mathf.Deg2Rad) && horizontalGap*horizontalGap+dy*dy <= pole.length*pole.length && reached.Add(j)) changed = true;
                    }
            } while (changed);
            lines.Add("Optimistically reachable from Start: " + string.Join(", ",reached.Select(i=>peaks[i].id)));
            lines.Add("Final optimistic reachability: " + reached.Contains(peaks.Length-1));
            foreach (var p in peaks.Skip(1).OrderBy(p=>Vector3.Distance(Position(c.start),Position(p))).Take(5))
                lines.Add($"Start -> {p.id}: center {Position(p):F3}; center distance {Vector3.Distance(Position(c.start),Position(p)):F3}m; height delta {p.height-c.start.height:F3}m; optimistic horizontal gap {Vector3.ProjectOnPlane(Position(p)-Position(c.start),Vector3.up).magnitude-Radius(c.start)-Radius(p):F3}m");
            lines.Add("NOT PROVEN by this editor check: runtime gap traversal, two-player Relay pole traversal, human input feel. See separate runtime evidence if produced.");
            File.WriteAllLines(Evidence + "/Validation.txt",lines);
            EditorSceneManager.OpenScene(GameplayFoundationSetup.ScenePath);
            Debug.Log("COOPERATIVE_POLE_STRUCTURE_MATH_PASS");
        }
        public static void ValidateAndBuild()
        {
            Validate();
            GameplayFoundationSetup.BuildPlayer();
        }
    }
}
