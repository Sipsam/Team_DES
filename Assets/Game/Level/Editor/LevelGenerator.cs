using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Levels.Editor
{
    public static class LevelGenerator
    {
        public const string BasePath="Assets/Game/Level";
        public static GameObject FindRoot() => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x=>x.name=="TwinEdge_Blockout" && x.GetComponentsInChildren<LevelSection>(true).Any(s=>s.owner==LevelSection.Owner));
        public static LevelLayoutConfig DefaultConfig()
        {
            string path=BasePath+"/Data/LevelLayoutConfig.asset";
            var c=AssetDatabase.LoadAssetAtPath<LevelLayoutConfig>(path);
            if(c!=null) return c;
            c=ScriptableObject.CreateInstance<LevelLayoutConfig>(); AssetDatabase.CreateAsset(c,path);return c;
        }
        public static GameObject Generate(LevelLayoutConfig c, SectionId? only=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required");
            var errors=LevelValidation.ConfigErrors(c); if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Generate level blockout");
            try
            {
                var root=FindRoot();
                if(root==null) { root=new GameObject("TwinEdge_Blockout"); Undo.RegisterCreatedObjectUndo(root,"Create level"); }
                string folder=BasePath+"/Data/Generated/Run_"+Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                foreach(SectionId id in Enum.GetValues(typeof(SectionId)))
                {
                    if(only.HasValue && only.Value!=id) continue;
                    var matches=root.GetComponentsInChildren<LevelSection>(true).Where(x=>x.owner==LevelSection.Owner && x.section==id).ToArray();
                    if(matches.Length>1) throw new InvalidOperationException("Duplicate owned section "+id);
                    LevelSection section=matches.FirstOrDefault();
                    if(section!=null && section.config!=c) throw new InvalidOperationException("Section uses another config. Create a new scene to switch layouts safely.");
                    if(section==null)
                    {
                        var g=new GameObject(id.ToString()); g.transform.SetParent(root.transform); section=g.AddComponent<LevelSection>();
                        section.section=id;section.config=c;Undo.RegisterCreatedObjectUndo(g,"Create section");
                        var manual=new GameObject("Manual");manual.transform.SetParent(g.transform);Undo.RegisterCreatedObjectUndo(manual,"Create manual area");
                    }
                    // Only the explicit Generated child is replaced. Manual siblings survive.
                    var old=section.transform.Find("Generated"); if(old!=null) Undo.DestroyObjectImmediate(old.gameObject);
                    var generated=new GameObject("Generated"); generated.transform.SetParent(section.transform);
                    Undo.RegisterCreatedObjectUndo(generated,"Create generated geometry");
                    var b=new BlockoutBuild(c,id,generated.transform,folder);
                    if(c.spatialV2) SpatialBlockoutV2.Build(b);
                    else switch(id)
                    {
                        case SectionId.Mountain: TerrainBuilder.Mountain(b);break;
                        case SectionId.Bridge: TerrainBuilder.Bridge(b);break;
                        case SectionId.River: TerrainBuilder.River(b);break;
                        case SectionId.Castle: StructureBuilder.Castle(b);break;
                        case SectionId.Underground: StructureBuilder.Underground(b);break;
                    }
                    if(!c.spatialV2) AddConnections(b);
                    Undo.RecordObject(section,"Update generation ID");section.generationId=Path.GetFileName(folder);
                }
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(root.scene);AssetDatabase.SaveAssets();return root;
            }
            catch { Undo.RevertAllDownToGroup(undo);throw; }
        }
        private static void AddConnections(BlockoutBuild b)
        {
            var c=b.c; Vector3 entry=Vector3.zero,exit=Vector3.zero; string link="";
            switch(b.section)
            {
                case SectionId.Mountain: entry=c.mountainPath[0];exit=c.mountainPath[c.mountainPath.Length-1];link="Bridge.Entry";break;
                case SectionId.Bridge: exit=Quaternion.Inverse(Quaternion.Euler(0,c.sections.Find(s=>s.id==b.section).yaw,0))*(c.World(SectionId.River,c.riverPath[0])-b.W(Vector3.zero));link="River.Entry";break;
                case SectionId.River: entry=c.riverPath[0];exit=c.riverPath[c.riverPath.Length-1];link="Castle.Entry";break;
                case SectionId.Castle: exit=new Vector3(0,0,StructureBuilder.CastleEnd(c));link="Underground.Entry";break;
                case SectionId.Underground: exit=new Vector3(0,0,c.sections.Find(s=>s.id==b.section).size.z-5);break;
            }
            b.Mark("Entry",MarkerKind.SectionEntrance,entry,new Vector3(c.pathWidth,3,2));
            b.Mark("Exit",MarkerKind.SectionExit,exit,new Vector3(c.pathWidth,3,2),links:string.IsNullOrEmpty(link)?null:new[]{link});
        }
        public static void CreateScene(LevelLayoutConfig c)
        {
            // Additive creation leaves the current scene and its unsaved changes intact.
            var previous=SceneManager.GetActiveScene();
            if(!Application.isBatchMode && string.IsNullOrEmpty(previous.path))
                throw new InvalidOperationException("Unity requires the current untitled scene to be saved before additive creation. Save it first, or open an existing saved scene. No scene was changed.");
            var keep=c.hideFlags;
            c.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            Scene scene;
            try { scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive); }
            finally { if(c!=null) c.hideFlags=keep; }
            SceneManager.SetActiveScene(scene);
            try
            {
                var root=Generate(c);
                var light=new GameObject("PreviewDirectionalLight").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);
                var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.transform.position=c.origin+new Vector3(240,340,330);camera.transform.LookAt(c.origin+new Vector3(0,30,550));camera.farClipPlane=3000;
                string path=AssetDatabase.GenerateUniqueAssetPath(BasePath+(c.spatialV2?"/Scenes/LevelBlockoutV2.unity":"/Scenes/LevelBlockout.unity"));
                if(!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Could not save "+path);
                var report=c.spatialV2?SpatialReviewV2.Inspect(root,c):LevelValidation.Inspect(root,c);
                Directory.CreateDirectory(c.spatialV2?"Docs/Development/Level/V2":"Docs/Development/Level/V1");File.WriteAllLines(c.spatialV2?"Docs/Development/Level/V2/Validation.txt":"Docs/Development/Level/V1/Validation.txt",report);
                Debug.Log("BLOCKOUT SAVED "+path+"\n"+string.Join("\n",report));
                Selection.activeGameObject=root;
            }
            catch { if(!Application.isBatchMode) { EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous); } throw; }
        }
        public static void CreateForBatch()
        {
            CreateScene(DefaultConfig());
            // Validate deserialized assets too, rather than only the builder's in-memory objects.
            string savedPath=SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(savedPath,OpenSceneMode.Single);
            var restored=FindRoot();
            if(restored==null) throw new InvalidOperationException("Saved scene missing owned root");
            var restoredConfig=restored.GetComponentInChildren<LevelSection>().config;
            var persisted=LevelValidation.Inspect(restored,restoredConfig);
            File.WriteAllLines("Docs/Development/Level/V1/Validation.txt",persisted);
            File.AppendAllText("Docs/Development/Level/V1/Validation.txt","\nPASS: saved scene reopened and revalidated in Unity 6000.3.27f1.\n");
            var report=File.ReadAllText("Docs/Development/Level/V1/Validation.txt");
            if(report.Contains("ERROR:")) throw new InvalidOperationException(report);
            RegressionChecks.Run();
        }
    }
}
