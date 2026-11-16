using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Levels.Testing.Editor
{
    public static class TraversalSetup
    {
        public const string Folder="Assets/Game/Level/Testing";
        public const string ScenePath=Folder+"/MountainTraversalTest.unity";
        const string Source="Assets/Game/Level/Scenes/LevelBlockoutV3.unity";
        const string Evidence="Docs/Development/Level/V3/TraversalTests";
        [MenuItem("Tools/Twin Edge/V3 Traversal/Open Test Scene")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))Build();
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
        // Only call on a fresh test setup; never overwrites an authored test scene.
        [MenuItem("Tools/Twin Edge/V3 Traversal/Create Independent Test Scene")]
        public static void Build()
        {
            if(File.Exists(ScenePath))throw new InvalidOperationException("Test scene already exists; open it instead.");
            Directory.CreateDirectory(Evidence);
            var source=SceneManager.GetSceneByPath(Source);bool opened=!source.IsValid()||!source.isLoaded;
            if(opened)source=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
            var owner=source.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MountainSectionV3>(true)).Single();
            var filter=owner.generated.GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Mountain_ConnectedGeology");
            var testScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(testScene);
            var root=new GameObject("MountainTraversalTest");var test=root.AddComponent<MountainTraversalTest>();
            test.sourceConfig=owner.config;test.configHash=Hash(AssetDatabase.GetAssetPath(owner.config));test.geometryHash=Hash(AssetDatabase.GetAssetPath(filter.sharedMesh));
            var terrain=new GameObject("TestTerrain");terrain.transform.SetParent(root.transform);
            test.terrainFilter=terrain.AddComponent<MeshFilter>();test.terrainCollider=terrain.AddComponent<MeshCollider>();test.terrainRenderer=terrain.AddComponent<MeshRenderer>();
            // Dense references the existing result. Reduced variants call the same terrain builder
            // with transient config copies; original config, scene, Generated and Manual are untouched.
            test.presets=new TraversalPreset[3];
            int[] counts={15,20,27};string[] names={"Sparse","Balanced","Dense"};
            for(int index=0;index<3;index++) {
                var ids=Choose(owner.config,counts[index]);
                var preset=new TraversalPreset{name=names[index],ids=ids};test.presets[index]=preset;
                if(index==2){preset.mesh=filter.sharedMesh;preset.materials=filter.GetComponent<MeshRenderer>().sharedMaterials;continue;}
                string folder=Folder+"/Data/"+names[index];Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var config=UnityEngine.Object.Instantiate(owner.config);config.smallPeaks=config.smallPeaks.Where(p=>ids.Contains(p.id)).ToList();config.smallPeakCount=counts[index];
                var peaks=new[]{config.start,config.destination}.Concat(config.smallPeaks.Select(p=>TwinEdge.Levels.Editor.MountainGeneratorV3.WorldPeak(config,p))).ToArray();
                var temp=new GameObject("TestVariantBuild");temp.transform.SetParent(root.transform);
                try {
                    var builder=typeof(TwinEdge.Levels.Editor.MountainGeneratorV3).GetMethod("BuildTerrain",BindingFlags.NonPublic|BindingFlags.Static);
                    if(builder==null)throw new MissingMethodException("V3 terrain builder changed; update traversal adapter.");
                    builder.Invoke(null,new object[]{config,peaks,temp.transform,folder});
                    var result=temp.GetComponentInChildren<MeshFilter>();preset.mesh=result.sharedMesh;preset.materials=result.GetComponent<MeshRenderer>().sharedMaterials;
                } finally {UnityEngine.Object.DestroyImmediate(temp);UnityEngine.Object.DestroyImmediate(config);}
            }
            // Preserve bridge stub as geometry only; no gameplay/ownership components are copied.
            var stub=owner.generated.GetComponentsInChildren<Transform>().FirstOrDefault(x=>x.name=="Bridge_StartStub_Only");
            if(stub!=null)UnityEngine.Object.Instantiate(stub.gameObject,root.transform,true);
            var light=new GameObject("Test Sun");light.transform.SetParent(root.transform);light.AddComponent<Light>().type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);
            test.SetPreset(2);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(testScene,ScenePath);
            if(opened)EditorSceneManager.CloseScene(source,true);
            WriteAnalysis(test);Debug.Log("Independent traversal scene saved: "+ScenePath);
        }
        // Spatial farthest-point coverage seeded by both endpoints. The ID tie-break makes
        // presets deterministic; this is NOT list truncation and does not guarantee reachability.
        public static string[] Choose(MountainConfigV3 c,int count)
        {
            var selected=new List<MountainPeakV3>();var remaining=c.smallPeaks.OrderBy(p=>p.id,StringComparer.Ordinal).ToList();
            var anchors=new List<Vector2>{c.start.position,c.destination.position};
            while(selected.Count<count) {
                var next=remaining.OrderByDescending(p=>anchors.Min(a=>Vector2.SqrMagnitude(TwinEdge.Levels.Editor.MountainGeneratorV3.WorldPeak(c,p).position-a))).ThenBy(p=>p.id,StringComparer.Ordinal).First();
                selected.Add(next);remaining.Remove(next);anchors.Add(TwinEdge.Levels.Editor.MountainGeneratorV3.WorldPeak(c,next).position);
            }
            return selected.Select(p=>p.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static void WriteAnalysis(MountainTraversalTest test)
        {
            Directory.CreateDirectory(Evidence);test.comparisons.Clear();
            for(int i=0;i<3;i++){test.SetPreset(i);if(test.nodes.Count!=new[]{17,22,29}[i])throw new Exception("Wrong node count");if(test.nodes.Select(n=>n.id).Distinct().Count()!=test.nodes.Count)throw new Exception("Duplicate node IDs");if(!test.nodes[0].hasLanding||!test.nodes[1].hasLanding)throw new Exception("Start/final missing collider-supported landing");}
            File.WriteAllText(Evidence+"/StaticColliderAnalysis.json",JsonUtility.ToJson(new Analysis{evidence="Unity Editor collider samples and static graph ONLY; no human play",snapshots=test.comparisons},true));
            test.SetPreset(2);
        }
        [Serializable] class Analysis {public string evidence;public List<TraversalSnapshot> snapshots;}
        public static void Batch()
        {
            try {
                EditorSceneManager.OpenScene(Source,OpenSceneMode.Single); Build();
                Debug.Log("TRAVERSAL_SETUP_PASS");
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
    [CustomEditor(typeof(MountainTraversalTest))]
    public class TraversalInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var test=(MountainTraversalTest)target;
            EditorGUILayout.HelpBox("Parabola and graph are hypotheses. Surface samples and two capsule occupancy are estimates, not human or multiplayer approval.",MessageType.Info);
            if(GUILayout.Button("Recompute actual-collider samples / graph")){test.Reanalyze();EditorUtility.SetDirty(test);}
            for(int i=0;i<3;i++){int next=i;if(GUILayout.Button("Preview "+test.presets[i].name)){test.SetPreset(next);EditorUtility.SetDirty(test);}}
        }
    }
}

