using System.Collections.Generic;
using TwinEdge.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using static TwinEdge.Presentation.CourseMapData;
using static TwinEdge.Presentation.GameplayPresentation;

namespace TwinEdge.Presentation
{
    internal sealed class CourseMapPresentation
    {
        const string Preference = "TwinEdge.MinimapVisible";
        static readonly Unity.Collections.FixedString64Bytes StartingCheckpoint = new("Mountain start");
        static readonly string[] ZoneNames = { "산악", "다리", "협곡", "강", "성채 외부", "성채 내부", "지하 계단", "지하 공동", "결승" };
        static readonly Color32[] Ink =
        {
            new(241, 219, 159, 255), new(145, 221, 226, 255), new(236, 159, 241, 255), new(255, 206, 100, 255),
            new(155, 209, 158, 255), new(218, 185, 133, 255), new(222, 216, 202, 255), new(246, 235, 210, 255)
        };

        readonly PresentationRoot root;
        readonly CourseMapData data;
        readonly RectTransform miniFrame, fullFrame;
        readonly MapView mini, full;
        readonly GameObject modal;
        readonly CourseMapBackdrop backdrop;
        readonly Text zoneLabel, fullTitle;
        readonly Image rim;
        readonly Texture2D symbols, circleTexture;
        readonly Sprite circle;
        readonly List<Point> points = new();
        readonly Pin[] pins;
        readonly UiButton[] zoneButtons = new UiButton[ZoneNames.Length + 1];
        readonly UiButton close;
        static bool? minimapVisible;
        internal static bool MinimapVisible
        {
            get => minimapVisible ??= Mathf.Clamp(PlayerPrefs.GetInt(Preference, 1), 0, 1) == 1;
            set
            {
                minimapVisible = value;
                PlayerPrefs.SetInt(Preference, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMinimapPreference() => minimapVisible = null;
        int fullSelection;
        Zone zone, candidate;
        float acceptZoneAt;
        GameplayCharacter previousLocal;
        bool hasZone;

        internal bool IsOpen => modal && modal.activeSelf;

        internal CourseMapPresentation(PresentationRoot root, UiKit ui, Transform overlay, System.Action closeModal)
        {
            this.root = root;
            data = Resources.Load<CourseMapData>("V3CourseMap");
            if (!data)
            {
                Debug.LogError("V3 course map data missing. Bake the public course atlas.");
                return;
            }
            pins = (Pin[])data.Pins.Clone();
            System.Array.Sort(pins, (a, b) => PinPriority(a.Kind).CompareTo(PinPriority(b.Kind)));
            symbols = CreateSymbols();
            circleTexture = CreateCircle();
            circle = Sprite.Create(circleTexture, new Rect(0, 0, 128, 128), Vector2.one * .5f);
            miniFrame = UiKit.Rect(overlay, "Minimap");
            rim = UiKit.Surface(miniFrame, "Metal rim", PresentationTheme.Accent);
            rim.sprite = circle;
            UiKit.Stretch(rim.rectTransform);
            var clip = UiKit.Surface(miniFrame, "Circular map", PresentationTheme.Background);
            clip.sprite = circle;
            UiKit.Stretch(clip.rectTransform, 3, 3, 3, 3);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            mini = new MapView(clip.rectTransform, ui, symbols, 24);
            Compass(ui, miniFrame, "N", new Vector2(.5f, 1), new Vector2(0, -4));
            Compass(ui, miniFrame, "S", new Vector2(.5f, 0), new Vector2(0, 4));
            Compass(ui, miniFrame, "W", new Vector2(0, .5f), new Vector2(4, 0));
            Compass(ui, miniFrame, "E", new Vector2(1, .5f), new Vector2(-4, 0));
            zoneLabel = ui.Text(miniFrame, "", 19, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            zoneLabel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
            Place(zoneLabel.rectTransform, new Vector2(.5f, 0), new Vector2(0, -30), new Vector2(300, 30));

            var content = ui.Modal(root.Modals);
            modal = content.parent.parent.gameObject;
            backdrop = new CourseMapBackdrop(overlay);
            var panel = (RectTransform)content.parent;
            panel.anchorMin = new Vector2(.08f, .08f);
            panel.anchorMax = new Vector2(.92f, .92f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            fullTitle = ui.Text(content, "", 29, PresentationTheme.PrimaryText);
            fullTitle.rectTransform.anchorMin = new Vector2(0, 1);
            fullTitle.rectTransform.anchorMax = Vector2.one;
            fullTitle.rectTransform.offsetMin = new Vector2(16, -54);
            fullTitle.rectTransform.offsetMax = Vector2.zero;
            var menu = UiKit.Rect(content, "Map sheets");
            menu.anchorMin = new Vector2(0, 0);
            menu.anchorMax = new Vector2(0, 1);
            menu.offsetMin = new Vector2(0, 4);
            menu.offsetMax = new Vector2(196, -64);
            UiKit.Stack(menu, 3);
            for (int i = 0; i < zoneButtons.Length; i++)
            {
                int choice = i - 1;
                zoneButtons[i] = ui.Button(menu, i == 0 ? "전체 월드" : ZoneNames[choice], () => SelectSheet(choice));
                var size = zoneButtons[i].GetComponent<LayoutElement>();
                size.minHeight = 41;
            }
            close = ui.Button(content, "닫기", closeModal);
            Place((RectTransform)close.transform, Vector2.one, new Vector2(-8, 0), new Vector2(115, 49));
            BuildLegend(ui, content);
            fullFrame = UiKit.Rect(content, "Detailed map");
            fullFrame.anchorMin = Vector2.zero;
            fullFrame.anchorMax = Vector2.one;
            fullFrame.offsetMin = new Vector2(210, 43);
            fullFrame.offsetMax = new Vector2(0, -64);
            full = new MapView(fullFrame, ui, symbols, data.Pins.Length + 4);
            modal.SetActive(false);
        }

        void BuildLegend(UiKit ui, Transform content)
        {
            var legend = UiKit.Rect(content, "Marker legend");
            legend.anchorMin = Vector2.zero;
            legend.anchorMax = new Vector2(1, 0);
            legend.offsetMin = new Vector2(210, 2);
            legend.offsetMax = new Vector2(0, 34);
            string[] captions = { "나", "상대", "보석", "목적지", "체크포인트", "기믹", "봉우리", "결승" };
            for (int i = 0; i < captions.Length; i++)
            {
                var entry = UiKit.Rect(legend, captions[i]);
                entry.anchorMin = new Vector2(i / 8f, 0);
                entry.anchorMax = new Vector2((i + 1) / 8f, 1);
                var icon = UiKit.Rect(entry, "Symbol").gameObject.AddComponent<RawImage>();
                icon.texture = symbols;
                icon.uvRect = SymbolUV((Kind)i);
                icon.color = Ink[i];
                icon.raycastTarget = false;
                Place(icon.rectTransform, new Vector2(0, .5f), Vector2.zero, Vector2.one * 22);
                UiKit.Stretch(ui.Text(entry, captions[i], 16, PresentationTheme.SecondaryText).rectTransform, 25);
            }
        }

        static Rect SymbolUV(Kind kind) => new((int)kind / 8f, 0, 1 / 8f, 1);

        void SelectSheet(int index)
        {
            fullSelection = index;
            for (int i = 0; i < zoneButtons.Length; i++) zoneButtons[i].interactable = i != index + 1;
        }

        internal bool Open()
        {
            if (!modal || !root.ShowModal(modal, close.gameObject)) return false;
            SelectSheet(-1);
            return true;
        }

        internal void ToggleMinimap() => MinimapVisible = !MinimapVisible;

        internal void Close() => backdrop?.Release();

        internal void Dispose()
        {
            Close();
            Object.Destroy(symbols);
            Object.Destroy(circle);
            Object.Destroy(circleTexture);
        }

        internal bool NearFinish(Vector3 position) => data && (position - data.Finish).sqrMagnitude < 22 * 22;

        internal void Refresh(V3GameplayBootstrap game, GameplayCharacter local, GameplayCharacter[] peers,
            GemOwnership gem, bool presentingTransition)
        {
            if (!data) return;
            backdrop.Refresh(IsOpen && !presentingTransition ? game.standbyCamera : null);
            Vector3 feet = local.transform.position;
            bool returning = game.state.Phase == FoundationPhase.Return;
            Zone next = data.At(feet);
            if (next == Zone.Finish && !returning) next = Zone.Mountain;
            if (!hasZone || local != previousLocal)
            {
                previousLocal = local;
                zone = candidate = next;
                hasZone = true;
            }
            if (next != candidate)
            {
                candidate = next;
                acceptZoneAt = Time.unscaledTime + .32f;
            }
            if (Time.unscaledTime >= acceptZoneAt) zone = candidate;

            Color tint = returning ? new Color32(244, 174, 137, 252) : PresentationTheme.Accent;
            string name = ZoneNames[(int)zone];
            if (zoneLabel.text != name) zoneLabel.text = name;
            zoneLabel.color = PresentationTheme.PrimaryText;
            rim.color = tint;
            bool miniVisible = MinimapVisible;
            miniFrame.gameObject.SetActive(miniVisible && !IsOpen);
            var rootSize = root.Overlays.rect.size;
            float diameter = Mathf.Min(rootSize.x * .13f, rootSize.y * .32f);
            Place(miniFrame, Vector2.one, new Vector2(-27, -25), Vector2.one * diameter);

            if (!miniVisible && !IsOpen) return;
            Vector3 target = Destination(game, local);
            points.Clear();
            points.Add(new Point(Kind.Self, feet, local.transform.eulerAngles.y));
            foreach (var peer in peers)
            {
                if (!peer || !peer.IsSpawned || peer == local) continue;
                points.Add(new Point(Kind.Partner, peer.transform.position));
            }
            if (gem && gem.IsSpawned)
            {
                if (gem.State == GemState.Carried)
                {
                    foreach (var peer in peers)
                        if (peer && peer.IsSpawned && peer.OwnerClientId == gem.Owner)
                            points.Add(new Point(Kind.Gem, peer.transform.position, offset: true));
                }
                else points.Add(new Point(Kind.Gem, gem.Position));
            }
            points.Add(new Point(Kind.Goal, target));
            foreach (var pin in pins) points.Add(new Point(pin.Kind, pin.Position));
            var detail = data.Sheets[(int)zone];
            if (IsOpen)
            {
                string heading = (returning ? "복로" : "왕로") + " · " + name;
                if (fullTitle.text != heading) fullTitle.text = heading;
                Sheet sheet = fullSelection < 0 ? data.Overview : data.Sheets[fullSelection];
                Rect fit = Fit(sheet.Bounds, fullFrame.rect.width / Mathf.Max(1, fullFrame.rect.height));
                full.Draw(data, sheet, fit, points, false, fullSelection < 0, feet.y);
            }
            else if (miniVisible)
            {
                float radius = detail.Radius;
                Vector2 center = Xz(feet);
                mini.Draw(data, detail, new Rect(center - Vector2.one * radius, Vector2.one * radius * 2),
                    points, true, false, feet.y);
            }
        }

        Vector3 Destination(V3GameplayBootstrap game, GameplayCharacter local)
        {
            var phase = game.state.Phase;
            if (phase == FoundationPhase.SealPending) return data.Seal;
            if (phase == FoundationPhase.Return)
            {
                return zone switch
                {
                    Zone.Cavern => data.CaveStart,
                    Zone.Stair => data.StairTop,
                    Zone.CastleInterior or Zone.CastleExterior => data.UpperDock,
                    Zone.River => data.LowerDock,
                    Zone.Canyon or Zone.Bridge => data.BridgeStart,
                    _ => data.Finish
                };
            }
            return zone switch
            {
                Zone.Finish => data.FirstSummit,
                Zone.Mountain => local.CheckpointName.Value.Equals(StartingCheckpoint) ? data.FirstSummit : data.LastSummit,
                Zone.Bridge => BridgeGameplay.Current is { } bridge && bridge.CompletedDeliveries < bridge.TotalDeliveries
                    ? bridge.SocketPoint : data.BridgeEnd,
                Zone.Canyon => data.LowerDock,
                Zone.River => data.UpperDock,
                Zone.CastleExterior => data.CastleGate,
                Zone.CastleInterior => CastleGameplay.Current?.DoorOpen == true ? data.StairTop :
                    data.CastleGate + new Vector3(0, 0, 130),
                Zone.Stair => data.CaveStart,
                _ => data.Seal
            };
        }

        static int PinPriority(Kind kind) => kind == Kind.Finish ? 0 : (int)kind;

        static Rect Fit(Rect source, float aspect)
        {
            var size = source.size;
            if (size.x / size.y < aspect) size.x = size.y * aspect;
            else size.y = size.x / aspect;
            return new Rect(source.center - size * .5f, size);
        }

        static void Compass(UiKit ui, Transform parent, string letter, Vector2 anchor, Vector2 offset)
        {
            var direction = ui.Text(parent, letter, 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            direction.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
            Place(direction.rectTransform, anchor, offset, new Vector2(26, 24));
        }

        readonly struct Point
        {
            public readonly Kind Kind;
            public readonly Vector3 Position;
            public readonly float Heading;
            public readonly bool Offset;

            public Point(Kind kind, Vector3 position, float heading = 0, bool offset = false)
            {
                Kind = kind;
                Position = position;
                Heading = heading;
                Offset = offset;
            }
        }

        sealed class MapView
        {
            readonly RectTransform parent;
            readonly RawImage terrain;
            readonly RawImage[] icons;
            readonly Text[] floors;
            readonly Vector2[] occupied;

            internal MapView(RectTransform parent, UiKit ui, Texture2D symbols, int capacity)
            {
                this.parent = parent;
                var background = UiKit.Surface(parent, "Map bed", new Color32(26, 31, 30, 210));
                UiKit.Stretch(background.rectTransform);
                terrain = UiKit.Rect(parent, "Public terrain").gameObject.AddComponent<RawImage>();
                terrain.raycastTarget = false;
                terrain.color = new Color(1, 1, 1, .91f);
                UiKit.Stretch(terrain.rectTransform);
                icons = new RawImage[capacity];
                floors = new Text[capacity];
                occupied = new Vector2[capacity];
                for (int i = 0; i < capacity; i++)
                {
                    var icon = UiKit.Rect(parent, "Map marker").gameObject.AddComponent<RawImage>();
                    icon.texture = symbols;
                    icon.raycastTarget = false;
                    icons[i] = icon;
                    var floor = ui.Text(parent, "", 13, Color.white, TextAnchor.MiddleCenter);
                    floor.rectTransform.sizeDelta = new Vector2(28, 22);
                    floors[i] = floor;
                }
            }

            internal void Draw(CourseMapData data, Sheet sheet, Rect window, List<Point> points, bool round, bool overview, float viewerY)
            {
                Vector2 span = parent.rect.size;
                terrain.texture = sheet.Texture;
                Rect bounds = sheet.TextureBounds;
                terrain.uvRect = new Rect((window.xMin - bounds.xMin) / bounds.width, (window.yMin - bounds.yMin) / bounds.height,
                    window.width / bounds.width, window.height / bounds.height);
                int used = 0;
                foreach (var point in points)
                {
                    Zone other = data.At(point.Position);
                    bool different = overview ? IsInteriorFloor(other) : SeparateFloor(sheet.Zone, other);
                    bool dynamic = point.Kind <= Kind.Goal;
                    if (different && !dynamic) continue;
                    Vector2 norm = (Xz(point.Position) - window.center) / (window.size * .5f);
                    bool off = round ? norm.sqrMagnitude > .68f : Mathf.Abs(norm.x) > .94f || Mathf.Abs(norm.y) > .94f;
                    if ((off || different) && !dynamic) continue;
                    if (!round && off) continue;
                    if (point.Kind == Kind.Self && round) { norm = Vector2.zero; different = false; off = false; }
                    if (round && different && norm.sqrMagnitude < .13f)
                    {
                        norm = (norm.sqrMagnitude > .0001f ? norm.normalized : Vector2.up) * .72f;
                        off = true;
                    }
                    if (off && round) norm = norm.normalized * .72f;
                    Vector2 position = norm * span * .5f + (point.Offset ? new Vector2(11, 10) : Vector2.zero);
                    if (!dynamic && TooClose(position, used)) continue;
                    if (off)
                    {
                        Vector2 tangent = new Vector2(-norm.y, norm.x).normalized;
                        if (TooClose(position, used)) position += tangent * 19;
                        if (TooClose(position, used)) position -= tangent * 38;
                    }
                    var icon = icons[used];
                    if (round && (point.Kind is Kind.Partner or Kind.Goal) && position.sqrMagnitude < 18 * 18)
                        position = (position.sqrMagnitude < .1f ? Vector2.right : position.normalized) * 18;
                    float size = point.Kind == Kind.Self ? 24 : dynamic ? 19 : 16;
                    icon.uvRect = SymbolUV(point.Kind);
                    icon.rectTransform.sizeDelta = Vector2.one * size;
                    icon.rectTransform.anchoredPosition = position;
                    icon.rectTransform.localRotation = Quaternion.Euler(0, 0, point.Kind == Kind.Self ? -point.Heading : 0);
                    icon.color = Ink[(int)point.Kind];
                    icon.enabled = true;
                    var floor = floors[used];
                    if (different || off)
                    {
                        string indicator = different && overview ? ZoneNames[(int)other] :
                            different ? (point.Position.y > viewerY ? "↑" : "↓") :
                            Mathf.Abs(norm.x) > Mathf.Abs(norm.y) ? (norm.x > 0 ? "→" : "←") : norm.y > 0 ? "↑" : "↓";
                        floor.rectTransform.sizeDelta = new Vector2(overview ? 96 : 28, 22);
                        if (floor.text != indicator) floor.text = indicator;
                        floor.rectTransform.anchoredPosition = overview ? position + new Vector2(0, 19) :
                            round && off ? position - norm.normalized * 16 : position + new Vector2(13, 9);
                        floor.enabled = true;
                    }
                    else floor.enabled = false;
                    occupied[used++] = position;
                    if (used == icons.Length) break;
                }
                while (used < icons.Length)
                {
                    icons[used].enabled = false;
                    floors[used++].enabled = false;
                }
            }

            bool TooClose(Vector2 point, int count)
            {
                for (int i = 0; i < count; i++)
                    if ((point - occupied[i]).sqrMagnitude < 17 * 17) return true;
                return false;
            }
        }

        static Texture2D CreateCircle()
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var pixels = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float distance = new Vector2(x - 63.5f, y - 63.5f).magnitude;
                pixels[x + y * 128] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(64 - distance)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D CreateSymbols()
        {
            const int cell = 32;
            var texture = new Texture2D(8 * cell, cell, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[8 * cell * cell];
            for (int kind = 0; kind < 8; kind++)
            for (int y = 1; y < cell - 1; y++)
            for (int x = 1; x < cell - 1; x++)
            {
                float a = (x - 15.5f) / 13;
                float b = (y - 15.5f) / 13;
                if (Shape((Kind)kind, a, b)) pixels[kind * cell + x + y * 8 * cell] = Color.white;
                else if (Shape((Kind)kind, a * .82f, b * .82f))
                    pixels[kind * cell + x + y * 8 * cell] = new Color32(27, 24, 23, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static bool Shape(Kind kind, float x, float y)
        {
            float h = Mathf.Abs(x), v = Mathf.Abs(y), r = x * x + y * y;
            return kind switch
            {
                Kind.Self => y < .96f && y > -.73f && h < .56f * (.96f - y) && (y > -.45f || h > -.55f * y),
                Kind.Partner => r < .58f,
                Kind.Gem => h * .85f + v < .88f,
                Kind.Goal => (r < .78f && r > .49f) || r < .12f,
                Kind.Checkpoint => (h < .13f && v < .82f) || (x > 0 && x < .77f && y > .05f && y < .62f),
                Kind.Gimmick => h < .58f && v < .58f && (h > .25f || v > .25f),
                Kind.Peak => y < .84f && y > -.62f && h < .58f * (.84f - y) && (y < -.42f || h > .58f * (.54f - y)),
                _ => h + v < 1 && (h + v > .75f || h + v < .43f)
            };
        }
    }
}
