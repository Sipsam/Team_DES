#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    // Authored world-space route polylines, never a projection or inferred walkable surface.
    internal sealed class ElevationProfileGUI
    {
        internal sealed class Leg
        {
            internal LevelSection section;
            internal LevelRoute route;
            internal Vector3[] points;
            internal float offset, length;
        }
        private bool returnTrip, sewer;
        private int lane;
        private float slope = 45, step = 3;
        internal static bool Temporary(LevelRoute route) => route.GetComponentsInParent<Transform>(true)
            .Any(t => t.name.StartsWith("TEST", StringComparison.Ordinal));

        internal static List<Leg> Build(LevelSection[] sections, bool returning, bool shortcut, int lane)
        {
            string branch = lane == 0 ? "A" : "B";
            var legs = new List<Leg>();
            foreach (var section in sections.OrderBy(s => s.section))
            {
                string[] order;
                switch (section.section)
                {
                    case SectionId.Mountain: order = returning ? new[] { "PoleAndElasticRoute" } : new[] { "Ascent", "PoleAndElasticRoute" }; break;
                    case SectionId.Bridge: order = new[] { "BridgeReservation", "DescentToDock" }; break;
                    case SectionId.River: order = new[] { "SharedWaterway", "ApproachCastle" }; break;
                    case SectionId.Castle:
                        var corridor = section.GetComponentsInChildren<LevelRoute>(true).Any(r => r.name == "Return" + branch)
                            ? "Return" + branch : "ReturnTrapCorridorFootprint";
                        order = new[] { returning ? (shortcut ? "SewerShortcut" : corridor) : branch + "Route", "UndergroundDescent" }; break;
                    default: order = new[] { "JumpLane" + branch }; break;
                }
                foreach (string name in order)
                {
                    var route = section.GetComponentsInChildren<LevelRoute>(true).FirstOrDefault(r => r.name == name && !Temporary(r));
                    if (route == null || route.points == null || route.points.Length < 2) continue;
                    var points = (Vector3[])route.points.Clone();
                    // Return routes and sewer already encode return direction.
                    if (returning && route.kind != RouteKind.Return && name != "SewerShortcut") Array.Reverse(points);
                    legs.Add(new Leg { section = section, route = route, points = points });
                }
            }
            if (returning) {
                // Reverse section/leg order, preserving each leg's travel direction.
                legs = legs.OrderByDescending(l => l.section.section)
                    .ThenBy(l => l.route.name == "UndergroundDescent" ? 0 : l.route.name == "ApproachCastle" || l.route.name == "DescentToDock" ? 0 : 1).ToList();
            }
            float distance = 0;
            foreach (var leg in legs) {
                leg.offset = distance;
                for (int i = 1; i < leg.points.Length; i++) leg.length += Vector3.Distance(leg.points[i - 1], leg.points[i]);
                distance += leg.length;
            }
            return legs;
        }

        internal void Draw(LevelSection[] sections, LevelSection focused, Color outbound, Color returning)
        {
            EditorGUILayout.LabelField("Elevation Profile · 실제 경로 폴리라인", EditorStyles.boldLabel);
            returnTrip = GUILayout.Toolbar(returnTrip ? 1 : 0, new[] { "왕로", "복로" }) == 1;
            lane = GUILayout.Toolbar(lane, new[] { "A 경로", "B 경로" });
            if (returnTrip) sewer = EditorGUILayout.Toggle("하수도 지름길", sewer);
            slope = EditorGUILayout.Slider("경사 경고 (도)", slope, 5, 80);
            step = EditorGUILayout.FloatField("높이 변화 경고 (m/선분)", Mathf.Max(.1f, step));
            var legs = Build(focused == null ? sections : new[] { focused }, returnTrip, sewer, lane);
            if (legs.Count == 0) { EditorGUILayout.HelpBox("선택 구간에 경로 데이터가 없습니다.", MessageType.Info); return; }
            float total = Mathf.Max(1, legs.Sum(l => l.length));
            float min = legs.Min(l => l.points.Min(p => p.y)) - 3, max = legs.Max(l => l.points.Max(p => p.y)) + 3;
            var rect = GUILayoutUtility.GetRect(240, 240, GUILayout.ExpandWidth(true));
            var plot = new Rect(rect.x + 48, rect.y + 20, Mathf.Max(1, rect.width - 65), rect.height - 55);
            GUI.Box(rect, GUIContent.none);
            Vector3 Map(float x, float y) => new Vector3(plot.x + x / total * plot.width, plot.yMax - (y - min) / (max - min) * plot.height);
            Handles.BeginGUI();
            for (int tick = 0; tick <= 4; tick++) {
                float f = tick / 4f;
                Handles.color = new Color(.5f,.5f,.5f,.35f);
                Handles.DrawLine(new Vector3(plot.x, plot.y + f * plot.height), new Vector3(plot.xMax, plot.y + f * plot.height));
                GUI.Label(new Rect(rect.x, plot.y + f * plot.height - 8, 48, 20), Mathf.Lerp(max,min,f).ToString("F0"));
                GUI.Label(new Rect(plot.x + f * plot.width - 12, plot.yMax + 2, 65, 20), (total*f).ToString("F0"));
            }
            int warnings = 0, gaps = 0;
            var labelRects = new List<Rect>();
            foreach (var leg in legs) {
                float x = leg.offset;
                Handles.color = Color.gray;
                Handles.DrawLine(Map(x,min),Map(x,max));
                var lr = new Rect(Map(x,max).x,rect.y,110,20);
                GUI.Label(lr,leg.section.section.ToString(), EditorStyles.miniLabel);
                for (int i = 1; i < leg.points.Length; i++) {
                    var a = leg.points[i-1]; var b = leg.points[i]; var delta = b-a;
                    float horizontal = new Vector2(delta.x,delta.z).magnitude;
                    bool risk = Mathf.Atan2(Mathf.Abs(delta.y),horizontal)*Mathf.Rad2Deg > slope || Mathf.Abs(delta.y) >= step;
                    if (risk) warnings++;
                    Handles.color = risk ? Color.red : returnTrip ? returning : outbound;
                    Handles.DrawAAPolyLine(2,Map(x,a.y),Map(x+delta.magnitude,b.y));
                    // Mark checkpoints by nearest projection onto the actual route segment.
                    foreach (var marker in leg.section.GetComponentsInChildren<LevelMarker>(true).Where(m => m.kind == MarkerKind.Checkpoint || m.kind == MarkerKind.ReturnStart || m.kind == MarkerKind.FinishLine || m.kind == MarkerKind.PlayerSpawn)) {
                        float t = Mathf.Clamp01(Vector3.Dot(marker.transform.position-a,delta)/Mathf.Max(.0001f,delta.sqrMagnitude));
                        if (Vector3.Distance(a+delta*t,marker.transform.position)>4) continue;
                        var pos = Map(x+delta.magnitude*t,(a+delta*t).y);
                        var labelRect = new Rect(pos.x+3,pos.y-18,125,18);
                        Handles.color = Color.yellow; Handles.DrawSolidDisc(pos,Vector3.forward,3);
                        if (!labelRects.Any(r => r.Overlaps(labelRect))) {
                            GUI.Label(labelRect,new GUIContent(marker.id,marker.id),EditorStyles.miniLabel); labelRects.Add(labelRect);
                        }
                    }
                    var pa = Map(x,a.y); var pb = Map(x+delta.magnitude,b.y);
                    var hit = Rect.MinMaxRect(Mathf.Min(pa.x,pb.x)-4,Mathf.Min(pa.y,pb.y)-4,Mathf.Max(pa.x,pb.x)+4,Mathf.Max(pa.y,pb.y)+4);
                    GUI.Label(hit,new GUIContent("",$"{leg.section.section}/{leg.route.name} ({leg.route.kind})\n거리 {x:F1}–{x+delta.magnitude:F1}m; 고도 {a.y:F1}→{b.y:F1}m; 경사 {Mathf.Atan2(Mathf.Abs(delta.y),horizontal)*Mathf.Rad2Deg:F1}°"));
                    x += delta.magnitude;
                }
            }
            for (int i = 1; i < legs.Count; i++) if (Vector3.Distance(legs[i-1].points.Last(),legs[i].points[0]) > .5f) {
                gaps++; Handles.color = Color.magenta;
                var p = Map(legs[i].offset,legs[i].points[0].y); Handles.DrawSolidDisc(p,Vector3.forward,5);
            }
            Handles.EndGUI();
            EditorGUILayout.LabelField($"누적 진행 거리 (3D m): {total:F1} | Y: 월드 고도 (m) | 경고 선분 {warnings} | 미연결 {gaps}");
            EditorGUILayout.HelpBox("X/Y·Z/Y 투영이 아닙니다. 저장된 LevelRoute의 3D 선분 길이를 누적합니다. A/B는 대안 경로이며 합산하지 않습니다. 분홍 점은 미연결: 빈 구간 거리는 제외하며 직선으로 연결하지 않습니다. 점프·다리·특수 이동은 예약 중심선으로 실제 비행 궤적/바닥 고도가 아닙니다. 빨강은 검토 후보이며 이동 불가 판정이 아닙니다. 점/선 위에 마우스를 올려 세부 수치를 확인하세요.", MessageType.Info);
        }
    }
}
#endif
