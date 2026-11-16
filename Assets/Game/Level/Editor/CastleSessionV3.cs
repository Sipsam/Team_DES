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
    public static class CastleSessionV3
    {
        const string Notes = "Docs/Development/Level/V3/";
        static CastleSessionV3() { EditorApplication.update += Poll; }
        static void Poll()
        {
            string path = Notes + "CastleSession.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string command = File.ReadAllText(path).Trim(); File.Delete(path);
            Directory.CreateDirectory(Notes);
            try {
                var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
                File.WriteAllLines(Notes + "CastleSession.txt", scenes.Select(s => s.path + " dirty=" + s.isDirty));
                if (command != "discard-test-exit") return;
                const string test = "Assets/Game/Level/Testing/MountainTraversalTest.unity";
                if (scenes.Any(s => s.isDirty && s.path != test)) throw new InvalidOperationException("Other unsaved scenes preserved; shutdown stopped.");
                foreach (var s in scenes.Where(s => s.path == test)) EditorSceneManager.CloseScene(s, true);
                File.AppendAllText(Notes + "CastleSession.txt", "\nAuthorized test changes discarded without saving; normal EditorApplication.Exit requested.\n");
                EditorApplication.Exit(0);
            } catch (Exception e) { File.WriteAllText(Notes + "CastleSessionError.txt", e.ToString()); Debug.LogException(e); }
        }
    }
}
