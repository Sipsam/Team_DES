#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinEdge.Levels.Editor
{
    // No scene components, serialized scene state, or generated assets are needed.
    public sealed class LevelVisualizationWindow : EditorWindow
    {
        private static readonly Color Outbound = new Color(.1f, .9f, .85f);
        private static readonly Color Return = new Color(1f, .5f, .12f);
        private static readonly Color Connection = new Color(.85f, .55f, 1f);
        private GameObject root;
        private bool overlay = true, boundaries = true, outbound = true, returning = true;
        private bool landmarks = true, connections = true, labels = true;
        private Vector2 scroll;
        private LevelSection focused;
        private LevelViewMode viewMode;
        private bool opposite, compactLabels = true;
        private float orbitYaw = -38, orbitPitch = 38;
        private readonly ElevationProfileGUI profile = new ElevationProfileGUI();

        [MenuItem("Tools/Twin Edge/Level Visualization")]
        public static void Open() => GetWindow<LevelVisualizationWindow>("Level Visualization");

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawScene;
            EditorApplication.hierarchyChanged += Refresh;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
            Refresh();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScene;
            EditorApplication.hierarchyChanged -= Refresh;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= ActiveSceneChanged;
            SceneView.RepaintAll();
        }

        private void ActiveSceneChanged(UnityEngine.SceneManagement.Scene before, UnityEngine.SceneManagement.Scene after)
        {
            root = null;
            Refresh();
        }

        private void Refresh()
        {
            if (root == null) root = LevelGenerator.FindRoot();
            Repaint();
            SceneView.RepaintAll();
        }

        private LevelSection[] Sections() => root == null ? Array.Empty<LevelSection>() :
            root.GetComponentsInChildren<LevelSection>(true).Where(s => s.owner == LevelSection.Owner).OrderBy(s => s.section).ToArray();

        private void OnGUI()
        {
            if (GUILayout.Button("V3 Castle / Underground review")) FortressViewsV3.Open();
            if (GUILayout.Button("V3 Bridge / Canyon / River framing")) ValleyViewsV3.Open();
            EditorGUILayout.HelpBox("씬을 읽어서 Scene 뷰에만 표시합니다. 창을 닫으면 표시가 해제됩니다.\nScene 뷰의 Gizmos를 켜세요. 카메라 이동은 게임 카메라에 영향을 주지 않습니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var candidate = (GameObject)EditorGUILayout.ObjectField("Blockout Root", root, typeof(GameObject), true);
            if (candidate == null || (candidate.scene.IsValid() && candidate.scene.isLoaded && !EditorUtility.IsPersistent(candidate))) { if (root != candidate) focused = null; root = candidate; }
            if (GUILayout.Button("활성 씬에서 다시 찾기")) { root = LevelGenerator.FindRoot(); focused = null; }
            overlay = EditorGUILayout.Toggle("시각화 표시", overlay);
            boundaries = EditorGUILayout.Toggle("구간 이름 / 경계", boundaries);
            outbound = EditorGUILayout.Toggle("왕로 → 청록색", outbound);
            returning = EditorGUILayout.Toggle("복로 → 주황색", returning);
            landmarks = EditorGUILayout.Toggle("스폰 / 체크포인트 / 반환 / 결승", landmarks);
            connections = EditorGUILayout.Toggle("성 A/B · 하수도 · 구간 연결", connections);
            labels = EditorGUILayout.Toggle("상세 라벨", labels);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

            var sections = Sections();
            if (sections.Length == 0)
            {
                EditorGUILayout.HelpBox("생성된 LevelBlockout 씬을 열거나 해당 씬의 TwinEdge_Blockout 루트를 지정하세요.", MessageType.Warning);
                return;
            }
            EditorGUI.BeginChangeCheck();
            var names = new[] { "전체 맵" }.Concat(sections.Select(s => s.section.ToString())).ToArray();
            int selected = focused == null ? 0 : Array.IndexOf(sections, focused) + 1;
            int next = EditorGUILayout.Popup("구간", selected, names);
            viewMode = (LevelViewMode)EditorGUILayout.EnumPopup("시점", viewMode);
            opposite = EditorGUILayout.Toggle("반대 방향 (Back / Left)", opposite);
            bool changed = EditorGUI.EndChangeCheck();
            focused = next == 0 ? null : sections[next - 1];
            bool frameRequested = GUILayout.Button("선택 Bounds에 카메라 맞추기");
            if (changed || frameRequested) FrameFocused(sections);
            if (GUILayout.Button("전체 맵 Top View")) { focused = null; viewMode = LevelViewMode.Top; FrameFocused(sections); }
            EditorGUI.BeginChangeCheck();
            orbitYaw = EditorGUILayout.Slider("중심 회전 / Yaw", orbitYaw, -180, 180);
            orbitPitch = EditorGUILayout.Slider("내려보기 / Pitch", orbitPitch, 5, 85);
            if (EditorGUI.EndChangeCheck()) {
                viewMode = LevelViewMode.Perspective;
                LevelViewCamera.Frame(FocusedBounds(sections), Quaternion.Euler(orbitPitch, orbitYaw, 0), false);
            }
            compactLabels = EditorGUILayout.Toggle("겹침 축약 / 선택 구간 강조", compactLabels);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            profile.Draw(sections, focused, Outbound, Return);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("보라색 점선: 마커 연결 및 방 순서. 하수도는 복로 지름길입니다.\n복로는 기존 경로를 역순으로 표시하며 산 구간은 결승에서 끝납니다. TEST 우회로는 생략합니다. 지하 점프 구간의 진입·합류선은 진행 안내선입니다.", MessageType.None);
        }

        private static string SectionName(SectionId id)
        {
            switch (id)
            {
                case SectionId.Mountain: return "산악 / Mountain";
                case SectionId.Bridge: return "다리 / Bridge";
                case SectionId.River: return "강 / River";
                case SectionId.Castle: return "성 / Castle";
                default: return "지하 / Underground";
            }
        }

        internal static Bounds SectionBounds(LevelSection section)
        {
            var bounds = new Bounds(section.transform.position, Vector3.zero);
            bool found = false;
            foreach (var renderer in section.GetComponentsInChildren<Renderer>(true)) Include(ref bounds, ref found, renderer.bounds);
            foreach (var terrain in section.GetComponentsInChildren<Terrain>(true))
                if (terrain.terrainData != null)
                    Include(ref bounds, ref found, new Bounds(terrain.transform.position + terrain.terrainData.size / 2, terrain.terrainData.size));
            foreach (var marker in section.GetComponentsInChildren<LevelMarker>(true))
                Include(ref bounds, ref found, new Bounds(marker.transform.position, Vector3.one * 2));
            foreach (var route in section.GetComponentsInChildren<LevelRoute>(true))
                if (route.points != null)
                    foreach (var point in route.points) Include(ref bounds, ref found, new Bounds(point, Vector3.zero));
            if (!found) bounds = new Bounds(section.transform.position, Vector3.one * 20);
            return bounds;
        }

        private static void Include(ref Bounds bounds, ref bool found, Bounds next)
        {
            if (found) bounds.Encapsulate(next);
            else { bounds = next; found = true; }
        }

        private Bounds FocusedBounds(LevelSection[] sections)
        {
            if (focused != null) return SectionBounds(focused);
            var bounds = SectionBounds(sections[0]);
            foreach (var section in sections.Skip(1)) bounds.Encapsulate(SectionBounds(section));
            return bounds;
        }

        private void FrameFocused(LevelSection[] sections) =>
            LevelViewCamera.Frame(FocusedBounds(sections), LevelViewCamera.Rotation(viewMode, opposite), viewMode != LevelViewMode.Perspective);

        private void DrawScene(SceneView view)
        {
            if (!overlay || !view.drawGizmos || Event.current.type != EventType.Repaint) return;
            var sections = Sections();
            using (new Handles.DrawingScope(Matrix4x4.identity))
            {
                var previousDepth = Handles.zTest;
                try
                {
                    Handles.zTest = CompareFunction.Always;
                    foreach (var section in sections)
                    {
                        if (boundaries)
                        {
                            var bounds = SectionBounds(section);
                            Handles.color = section == focused ? Color.white : new Color(.65f, .8f, 1f, .8f);
                            Handles.DrawWireCube(bounds.center, bounds.size);
                            Handles.Label(new Vector3(bounds.min.x, bounds.max.y + 2, bounds.center.z), SectionName(section.section), EditorStyles.whiteLabel);
                        }
                        DrawRoutes(section);
                        if (landmarks) DrawLandmarks(section);
                    }
                    if (connections) DrawConnections(sections);
                }
                finally { Handles.zTest = previousDepth; }
            }
        }

        private void DrawRoutes(LevelSection section)
        {
            foreach (var route in section.GetComponentsInChildren<LevelRoute>(true))
            {
                if (route.points == null || route.points.Length < 2 || IsTemporary(route.transform, section.transform)) continue;
                bool sewer = route.name == "SewerShortcut";
                bool returnOnly = route.kind == RouteKind.Return;
                if (outbound && !returnOnly && !sewer) Path(route.points, Outbound, false);
                if (returning && (returnOnly || (route.name != "Ascent" && route.name != "ARoute" && route.name != "BRoute")))
                    Path(route.points, Return, !returnOnly);
                if (labels && section.section == SectionId.Castle)
                {
                    Handles.color = sewer ? Return : returnOnly ? Return : Outbound;
                    Handles.Label(route.points[route.points.Length / 2] + Vector3.up * 3, route.name);
                }
                if (section.section == SectionId.Underground && route.kind == RouteKind.Jump)
                {
                    var markers = section.GetComponentsInChildren<LevelMarker>(true);
                    var entry = markers.FirstOrDefault(m => m.kind == MarkerKind.SectionEntrance);
                    var turn = markers.FirstOrDefault(m => m.kind == MarkerKind.ReturnStart);
                    if (entry != null && turn != null)
                    {
                        var a = new[] {entry.transform.position, route.points[0]};
                        var b = new[] {route.points[route.points.Length - 1], turn.transform.position};
                        if (outbound) { Path(a, Outbound, false); Path(b, Outbound, false); }
                        if (returning) { Path(a, Return, true); Path(b, Return, true); }
                    }
                }
            }
        }

        private static bool IsTemporary(Transform node, Transform section)
        {
            for (var current = node; current != null && current != section; current = current.parent)
                if (current.name.StartsWith("TEST", StringComparison.Ordinal)) return true;
            return false;
        }

        private static void Path(Vector3[] points, Color color, bool reverse)
        {
            for (int i = 1; i < points.Length; i++)
                if (reverse) Arrow(points[i], points[i - 1], color);
                else Arrow(points[i - 1], points[i], color);
        }

        private static void Arrow(Vector3 from, Vector3 to, Color color)
        {
            var direction = to - from;
            if (direction.sqrMagnitude < .001f) return;
            // Opposite travel directions occupy opposite sides, so shared routes remain readable.
            var side = Vector3.Cross(Vector3.up, direction.normalized).normalized * 1.3f;
            from += side; to += side;
            Handles.color = color;
            Handles.DrawAAPolyLine(3, from, to);
            var middle = (from + to) / 2;
            float length = Mathf.Min(direction.magnitude * .2f, HandleUtility.GetHandleSize(middle) * .12f);
            var right = Vector3.Cross(direction.normalized, Vector3.up).normalized;
            if (right.sqrMagnitude < .01f) right = Vector3.right;
            Handles.DrawAAPolyLine(3, middle - direction.normalized * length + right * length * .5f,
                middle, middle - direction.normalized * length - right * length * .5f);
        }

        private void DrawLandmarks(LevelSection section)
        {
            // Offset labels in screen space so coincident checkpoint/finish markers are both readable.
            var occupied = new List<Rect>();
            foreach (var marker in section.GetComponentsInChildren<LevelMarker>(true))
            {
                string title;
                Color color;
                switch (marker.kind)
                {
                    case MarkerKind.PlayerSpawn: title = "스폰"; color = Color.green; break;
                    case MarkerKind.Checkpoint: title = "체크포인트"; color = Color.yellow; break;
                    case MarkerKind.ReturnStart: title = "반환점"; color = Return; break;
                    case MarkerKind.FinishLine: title = "결승점"; color = Color.magenta; break;
                    default: continue;
                }
                var position = marker.transform.position;
                Handles.color = color;
                Handles.DrawWireDisc(position, Vector3.up, HandleUtility.GetHandleSize(position) * .055f);
                if (!labels) continue;
                if (Vector3.Dot(position - SceneView.currentDrawingSceneView.camera.transform.position, SceneView.currentDrawingSceneView.camera.transform.forward) <= 0) continue;
                var gui = HandleUtility.WorldToGUIPoint(position);
                bool detailed = !compactLabels || section == focused || Selection.activeGameObject == marker.gameObject;
                string text = title + (detailed ? " · " + marker.id : "");
                var style = new GUIStyle(EditorStyles.whiteLabel) { normal = { textColor = color } };
                var size = style.CalcSize(new GUIContent(text));
                var rect = new Rect(gui.x + 10, gui.y, size.x, 20);
                int row = 0;
                while (occupied.Any(r => r.Overlaps(rect)) && row++ < 4) rect.y += 20;
                if (occupied.Any(r => r.Overlaps(rect)) && !detailed) continue;
                occupied.Add(rect);
                Handles.BeginGUI();
                GUI.Label(rect, text, style);
                Handles.EndGUI();
            }
        }

        private void DrawConnections(LevelSection[] sections)
        {
            // IDs are scene-authored; duplicate IDs are not silently connected to arbitrary objects.
            var markers = sections.SelectMany(s => s.GetComponentsInChildren<LevelMarker>(true))
                .Where(m => !string.IsNullOrEmpty(m.id)).GroupBy(m => m.id)
                .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            foreach (var marker in markers.Values)
                foreach (string id in marker.connectedIds ?? Array.Empty<string>())
                    Link(markers, marker.id, id);
            foreach (string lane in new[] {"A", "B"})
            {
                Link(markers, "Castle.Split", "Castle." + lane + "1");
                for (int i = 1; i < 4; i++) Link(markers, "Castle." + lane + i, "Castle." + lane + (i + 1));
                Link(markers, "Castle." + lane + "4", "Castle.Merge");
                for (int i = 1; i <= 4; i++)
                    if (labels && markers.TryGetValue("Castle." + lane + i, out var room))
                    {
                        Handles.color = Connection;
                        Handles.Label(room.transform.position + Vector3.up * 2, "병렬 방 " + lane + i);
                        using (new Handles.DrawingScope(room.transform.localToWorldMatrix))
                            Handles.DrawWireCube(Vector3.up * room.reservedSize.y / 2, room.reservedSize);
                    }
            }
            Link(markers, "Castle.Merge", "Castle.SewerEntry");
            Link(markers, "Castle.SewerExit", "Castle.Split");
        }

        private void Link(Dictionary<string, LevelMarker> markers, string from, string to)
        {
            if (to == null || !markers.TryGetValue(from, out var a) || !markers.TryGetValue(to, out var b)) return;
            Handles.color = Connection;
            Handles.DrawDottedLine(a.transform.position, b.transform.position, 5);
            if (labels && (from.Contains("Sewer") || to.Contains("Sewer")))
                Handles.Label((a.transform.position + b.transform.position) / 2, "하수도 연결 · " + from + " → " + to);
        }
    }
}
#endif
