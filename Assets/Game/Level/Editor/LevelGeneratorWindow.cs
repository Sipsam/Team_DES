using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    public sealed class LevelGeneratorWindow : EditorWindow
    {
        private LevelLayoutConfig config;
        private SectionId section;
        private bool all=true,markers=true;
        private Vector2 scroll;
        private string result="";
        [MenuItem("Tools/Twin Edge/Level Blockout")]
        public static void Open() => GetWindow<LevelGeneratorWindow>("Level Blockout");
        private void OnGUI()
        {
            config=(LevelLayoutConfig)EditorGUILayout.ObjectField("Layout",config,typeof(LevelLayoutConfig),false);
            if(GUILayout.Button("Select / create default layout")) config=LevelGenerator.DefaultConfig();
            if(GUILayout.Button("Select / create V2 spatial layout")) config=SpatialBlockoutV2.Config();
            if(config==null) return;
            all=EditorGUILayout.Toggle("All sections",all);
            if(!all) section=(SectionId)EditorGUILayout.EnumPopup("Section",section);
            var root=LevelGenerator.FindRoot();
            int count=root==null?0:root.GetComponentsInChildren<LevelSection>(true).Where(s=>all || s.section==section).Sum(s=>s.transform.Find("Generated")?.GetComponentsInChildren<Transform>(true).Length ?? 0);
            EditorGUILayout.HelpBox("Target: active scene / "+(all?"all sections":section.ToString())+"\nReplace "+count+" Generated transforms. Manual children survive. Edits inside Generated are replaced. Assets are versioned for Undo.\nAll dimensions are provisional. Neighbor connectors may need regeneration after layout changes.",MessageType.Info);
            var errors=LevelValidation.ConfigErrors(config);
            foreach(var e in errors) EditorGUILayout.HelpBox(e,MessageType.Error);
            using(new EditorGUI.DisabledScope(errors.Count>0 || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if(GUILayout.Button("Create NEW saved blockout scene (additive)")) Try(()=>LevelGenerator.CreateScene(config));
                if(GUILayout.Button("Generate / replace target in active scene")) Try(()=>LevelGenerator.Generate(config,all?(SectionId?)null:section));
            }
            bool visible=EditorGUILayout.Toggle("Show marker gizmos",markers);
            if(visible!=markers)
            {
                markers=visible;
                if(root!=null) foreach(var m in root.GetComponentsInChildren<LevelMarker>(true)) { Undo.RecordObject(m,"Marker visibility");m.show=visible;EditorUtility.SetDirty(m); }
                SceneView.RepaintAll();
            }
            if(GUILayout.Button("Validate active blockout") && root!=null) result=string.Join("\n",config.spatialV2?SpatialReviewV2.Inspect(root,config):LevelValidation.Inspect(root,config));
            if(GUILayout.Button("Frame target") && root!=null) { Selection.activeGameObject=root;SceneView.lastActiveSceneView?.FrameSelected(); }
            scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.TextArea(result);EditorGUILayout.EndScrollView();
        }
        private void Try(Action action) { try { action();result="Completed. Use Validate for structural checks."; } catch(Exception e) { result=e.ToString();Debug.LogException(e); } }
    }
}
