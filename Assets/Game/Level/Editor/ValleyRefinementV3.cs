using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Levels.Editor
{
    [InitializeOnLoad]
    public static class ValleyRefinementV3
    {
        const string Root = "Docs/Development/Level/V3/";
        static ValleyRefinementV3() { Directory.CreateDirectory(Root); File.WriteAllText(Root+"RefinementReady.txt",DateTime.UtcNow.ToString("O")); EditorApplication.update += Poll; }
        static void Poll()
        {
            var path = Root + "Refinement.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var command = File.ReadAllText(path).Trim(); File.Delete(path);
            try
            {
                var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
                File.WriteAllLines(Root + "RefinementSession.txt", scenes.Select(s => s.path + " dirty=" + s.isDirty).Concat(new[]{"Undo group=" + Undo.GetCurrentGroup(), "Command=" + command}));
                if (command == "status") return;
                if (command == "save-exit") { EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); return; }
                if (scenes.Any(s => s.isDirty && (command != "run" || s.path == MountainGeneratorV3.ScenePath))) throw new Exception("Unsaved target scene present; operation stopped to preserve manual work.");
                if (command == "run") { if(File.Exists(Root+"RefinementError.txt"))File.Delete(Root+"RefinementError.txt"); ValleyReviewV3.Run(); File.WriteAllText(Root + "RefinementComplete.txt", DateTime.UtcNow.ToString("O")); }
                if (command == "exit") { AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
            }
            catch (Exception ex) { File.WriteAllText(Root + "RefinementError.txt", ex.ToString()); Debug.LogException(ex); }
        }
    }
}
