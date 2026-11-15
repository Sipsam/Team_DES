#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    internal enum LevelViewMode { Top, SideX, SideZ, Perspective }

    internal static class LevelViewCamera
    {
        internal static Quaternion Rotation(LevelViewMode mode, bool opposite)
        {
            float turn = opposite ? 180 : 0;
            switch (mode)
            {
                case LevelViewMode.Top: return Quaternion.Euler(90, turn, 0);
                case LevelViewMode.SideX: return Quaternion.Euler(0, 90 + turn, 0);
                case LevelViewMode.SideZ: return Quaternion.Euler(0, turn, 0);
                default: return Quaternion.Euler(38, -38 + turn, 0);
            }
        }

        internal static SceneView Frame(Bounds bounds, Quaternion rotation, bool orthographic)
        {
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.Show();
            // Fit a bounding sphere, including depth, to the narrower viewport dimension.
            // This also keeps the camera outside all target geometry at every orbit angle.
            float radius = Mathf.Max(2, bounds.extents.magnitude) + 2;
            float aspect = Mathf.Max(.1f, view.camera.aspect);
            float size = radius * 1.15f / Mathf.Min(1, aspect);
            view.in2DMode = false;
            view.LookAt(bounds.center, rotation, size, orthographic, true);
            view.Repaint();
            return view;
        }
    }
}
#endif

