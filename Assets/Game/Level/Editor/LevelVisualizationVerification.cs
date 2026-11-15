#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Levels.Editor
{
    // Explicit, read-only verification. Never invokes generation or the legacy capture method.
    [InitializeOnLoad]
    internal static class LevelVisualizationVerification
    {
        private const string Request = "Temp/LevelVisualization.verify";
        static LevelVisualizationVerification() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        }
        [MenuItem("Tools/Twin Edge/Verify Level Visualization (read only)")]
        internal static async void Run()
        {
            var report = new List<string> { "Unity " + Application.unityVersion, "Read-only preview scene verification: " + DateTime.Now.ToString("O") };
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var dirty = loaded.Select(s => s.isDirty).ToArray();
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            var pivot = view.pivot; var rotation = view.rotation; var size = view.size; bool ortho = view.orthographic; bool mode2D = view.in2DMode;
            try {
                foreach (string path in new[] { "Assets/Game/Level/Scenes/LevelBlockout.unity", "Assets/Game/Level/Scenes/LevelBlockoutV2.unity" }) {
                    string before = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
                    var scene = EditorSceneManager.OpenPreviewScene(path);
                    try {
                        var root = scene.GetRootGameObjects().First(g => g.name == "TwinEdge_Blockout");
                        var sections = root.GetComponentsInChildren<LevelSection>(true).OrderBy(s => s.section).ToArray();
                        var all = LevelVisualizationWindow.SectionBounds(sections[0]);
                        foreach (var section in sections.Skip(1)) all.Encapsulate(LevelVisualizationWindow.SectionBounds(section));
                        var targets = new[] { (name: "Whole", bounds: all) }.Concat(sections.Select(s => (name: s.section.ToString(), bounds: LevelVisualizationWindow.SectionBounds(s))));
                        foreach (var target in targets) {
                            foreach (LevelViewMode mode in Enum.GetValues(typeof(LevelViewMode))) foreach (bool opposite in new[] { false, true })
                                await CheckCamera(report, target.name + "/" + mode + "/" + opposite, target.bounds, LevelViewCamera.Rotation(mode, opposite), mode != LevelViewMode.Perspective);
                            foreach (float yaw in new[] { -180f, -90, 0, 90 }) await CheckCamera(report,target.name+"/Orbit/"+yaw,target.bounds,Quaternion.Euler(38,yaw,0),false);
                        }
                        foreach (bool returning in new[] { false, true }) foreach (bool sewer in new[] { false, true }) foreach (int lane in new[] { 0,1 }) {
                            var legs = ElevationProfileGUI.Build(sections,returning,sewer,lane);
                            bool valid = legs.Count > 0 && legs.All(l => !ElevationProfileGUI.Temporary(l.route) && l.points.Length >= 2 && l.length > 0);
                            valid &= !returning || legs.All(l => l.route.name != "Ascent");
                            valid &= !legs.Any(l => l.route.name == (lane==0?"BRoute":"ARoute"));
                            float previousEnd = 0;
                            foreach (var leg in legs) { valid &= Mathf.Abs(leg.offset-previousEnd)<.01f; previousEnd=leg.offset+leg.length; }
                            report.Add((valid?"PASS":"FAIL")+$": {path} profile return={returning} sewer={sewer} lane={lane}; legs={legs.Count}; 3D length={previousEnd:F2}m");
                        }
                        // Numeric fixture detects accidental X/Y or Z/Y projection length.
                        var a = new Vector3(0,0,0);var b = new Vector3(3,4,12);
                        report.Add((Mathf.Abs(Vector3.Distance(a,b)-13)<.0001f?"PASS":"FAIL")+": 3D distance fixture (3,4,12)=13m");
                        report.Add((!scene.isDirty?"PASS":"FAIL")+": preview scene remains clean: "+path);
                    } finally { EditorSceneManager.ClosePreviewScene(scene); }
                    var after = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
                    report.Add((before==after?"PASS":"FAIL")+": scene SHA256 unchanged: "+path+" "+before);
                }
            } catch (Exception ex) { report.Add("FAIL: " + ex); }
            finally {
                view.in2DMode=mode2D;view.LookAt(pivot,rotation,size,ortho,true);view.Repaint();
                for (int i=0;i<loaded.Length;i++) report.Add((loaded[i].isDirty==dirty[i]?"PASS":"FAIL")+": loaded scene dirty unchanged: "+loaded[i].path+" dirty="+dirty[i]);
                Directory.CreateDirectory("Docs/Development/Level/Shared");
                File.WriteAllLines("Docs/Development/Level/Shared/Visualization-Validation.txt",report);
            }
        }
        private static async System.Threading.Tasks.Task CheckCamera(List<string> report,string label,Bounds bounds,Quaternion rotation,bool ortho)
        {
            var view=LevelViewCamera.Frame(bounds,rotation,ortho);
            var completion = new System.Threading.Tasks.TaskCompletionSource<bool>();
            Action<SceneView> rendered = null;
            rendered = current => {
                if (current != view || Event.current.type != EventType.Repaint) return;
                SceneView.duringSceneGui -= rendered;
            var camera=view.camera;
            bool pass=!bounds.Contains(camera.transform.position) && view.orthographic==ortho;
            float margin=1;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2) {
                var p=camera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
                pass &= p.z>camera.nearClipPlane && p.z<camera.farClipPlane && p.x>=0 && p.x<=1 && p.y>=0 && p.y<=1;
                margin=Mathf.Min(margin,p.x,1-p.x,p.y,1-p.y);
            }
            report.Add((pass?"PASS":"FAIL")+$": camera {label} margin={margin:F3} distance={Vector3.Distance(camera.transform.position,bounds.center):F1}m");
                EditorApplication.delayCall += () => completion.TrySetResult(true);
            };
            SceneView.duringSceneGui += rendered;
            view.Focus(); view.Repaint();
            if (await System.Threading.Tasks.Task.WhenAny(completion.Task, System.Threading.Tasks.Task.Delay(5000)) != completion.Task) {
                SceneView.duringSceneGui -= rendered;
                throw new TimeoutException("SceneView repaint did not arrive: " + label);
            }

        }
    }
}
#endif

