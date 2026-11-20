using System;
using System.Collections.Generic;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    public sealed partial class CastleGameplay
    {
        [Header("Castle presentation")]
        [SerializeField] GameObject[] symbolModels, trapModels;
        [SerializeField] GameObject doorModel, wallModel, transferModel, windowModel, hintModel, numberModel, sequenceModel;
        [SerializeField] Material stoneMaterial, darkMaterial, accentMaterial;
        [SerializeField] Shader labelShader;

        Material labelMaterial;
        Font labelFont;

        internal readonly List<CastleStation> stations = new();
        readonly CastleStation[] keypads = new CastleStation[2];
        readonly GameObject[] unlockedGroups = new GameObject[Stages];
        readonly GameObject[] sewerDoors = new GameObject[2];
        readonly Transform[,,,] stones = new Transform[Stages, 2, 3, 3];
        readonly TextMesh[] hintText = new TextMesh[Stages];
        readonly GameObject[] digits = new GameObject[2];
        readonly List<GameObject> reservations = new();
        Transform layout, partitions, chambers, returnLayout;
        GameObject basementGate;

        internal void Bind(Scene scene)
        {
            if (ready && boundScene == scene) return;
            if (!scene.isLoaded) return;
            var sections = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FortressSectionV3>(true));
            var keep = sections.SingleOrDefault(section => section.part == FortressPartV3.CastleInterior);
            var dock = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelMarker>(true))
                .SingleOrDefault(marker => marker.id == "Valley.V3.UpperCheckpoint");
            if (keep == null || keep.config == null || keep.generated == null || dock == null) return;
            config = keep.config;
            partitions = keep.generated.transform.Find("RemovablePartitions");
            if (partitions == null)
            {
                Debug.LogError("Castle gameplay requires the saved RemovablePartitions group.", keep);
                return;
            }

            var rooms = new FortressRoomV3[Stages, 2];
            for (int i = 0; i < Stages; i++)
            for (int lane = 0; lane < 2; lane++)
            {
                string id = $"{Lane(lane)}{i + 1}";
                var room = config.rooms.SingleOrDefault(candidate => candidate.id == id);
                if (room == null || room.hint != (lane == i % 2) || partitions.Find(id) == null)
                {
                    Debug.LogError($"Castle gameplay binding mismatch in {id}.", keep);
                    return;
                }
                rooms[i, lane] = room;
            }

            Physics.SyncTransforms();
            for (int slot = 0; slot < dockPoses.Length; slot++)
            {
                var anchor = dock.transform;
                Vector3 offset = anchor.right * (slot == 0 ? -1.5f : 1.5f);
                dockPoses[slot] = new Pose(V3GameplayBootstrap.SafeGround(anchor.position + offset), anchor.rotation);
            }
            boundScene = scene;
            if (layout != null) Destroy(layout.gameObject);
            stations.Clear();
            reservations.Clear();
            layout = Group(transform, "CastleGameplayLayout");
            layout.position = config.origin;
            chambers = Group(layout, "Cooperation");
            returnLayout = Group(layout, "Return");
            var parkedDoors = partitions.Find("OpenDoors");
            if (parkedDoors != null) reservations.Add(parkedDoors.gameObject);
            AddReservation(keep.generated.transform, "TrapReservations");
            AddReservation(keep.generated.transform, "H_TwoFourDigitInputReservation");

            for (int i = 0; i < Stages; i++)
            {
                float z = rooms[i, 0].plan.yMax;
                var a = rooms[Mathf.Min(i + 1, Stages - 1), 0].plan;
                var b = rooms[Mathf.Min(i + 1, Stages - 1), 1].plan;
                var gates = Group(chambers, $"Stage {i + 1} door leaves");
                unlockedGroups[i] = gates.gameObject;
                Door(gates, "A door", a.center.x, z, 6);
                Door(gates, "B door", b.center.x, z, 6);
                Divider(chambers, z, b.center.x, a.center.x);
                for (int lane = 0; lane < 2; lane++)
                {
                    var room = rooms[i, lane];
                    AddReservation(keep.generated.transform, $"{room.id}/DigitReservation");
                    AddReservation(keep.generated.transform, $"{room.id}/OppositeHint_{(room.hint ? "Source" : "Receiver")}");
                    BuildRoom(i, lane, room, rooms[i, 0].plan.center.y);
                }
            }

            Divider(chambers, rooms[0, 0].plan.yMin, rooms[0, 1].plan.center.x, rooms[0, 0].plan.center.x);
            // Existing shared masonry starts at Z=58. Close the seam at the first arch.
            var h = config.undergroundDoor;
            Panel(layout, "Basement west wall", config.keep.xMin, h.x - 3.6f, h.z);
            Panel(layout, "Basement east wall", h.x + 3.6f, config.keep.xMax, h.z);
            basementGate = Door(layout, "Basement gate", h.x, h.z, 7.2f);
            for (int lane = 0; lane < 2; lane++)
            {
                float side = Side(lane);
                var badge = Station(chambers, Action.Collect, lane, Stages, 0,
                    new Vector3(side * 9, 1.25f, rooms[3, lane].plan.yMax + 6), "E: read your four-digit stone");
                Model(badge.transform, numberModel != null ? numberModel : hintModel, new Vector3(1.3f, 1.8f, 1.1f));
                Label(badge.transform, "NUMBER " + Lane(lane), Vector3.up * 1.1f, 180);
                digits[lane] = badge.gameObject;
                keypads[lane] = Station(chambers, Action.Keypad, lane, Stages, 0,
                    new Vector3(h.x + side * 5.2f, 1.1f, h.z - 2), "E: enter number " + Lane(lane));
                Block(keypads[lane].transform, "Keypad", Vector3.zero, new Vector3(.9f, 1.1f, .55f), accentMaterial, false);
                Label(keypads[lane].transform, "LOCK " + Lane(lane), Vector3.up, 180);
            }
            BuildSewer();
            BuildTraps();
            ready = true;
            ApplyViews();
            Physics.SyncTransforms();
        }

        void AddReservation(Transform root, string path)
        {
            var reservation = root.Find(path);
            if (reservation != null) reservations.Add(reservation.gameObject);
        }

        static string Lane(int slot) => slot == 0 ? "A" : "B";
        static float Side(int slot) => slot == 0 ? 1 : -1;
        static Transform Group(Transform parent, string label)
        {
            var group = new GameObject(label).transform;
            group.SetParent(parent, false);
            return group;
        }

        void Divider(Transform root, float z, float b, float a)
        {
            Panel(root, "West stage boundary", config.keep.xMin, b - 3, z);
            Panel(root, "Middle stage boundary", b + 3, a - 3, z);
            Panel(root, "East stage boundary", a + 3, config.keep.xMax, z);
        }

        void Panel(Transform parent, string name, float left, float right, float z)
        {
            Block(parent, name, new Vector3((left + right) / 2, 4, z),
                new Vector3(right - left, 8, .65f), stoneMaterial);
        }

        GameObject Door(Transform parent, string name, float x, float z, float width)
        {
            var gate = Group(parent, name);
            gate.localPosition = new Vector3(x, 2.6f, z);
            gate.gameObject.AddComponent<BoxCollider>().size = new Vector3(width, 5.2f, .55f);
            if (doorModel != null) Model(gate, doorModel, new Vector3(width, 5.2f, .65f), fitAxes: true);
            else Block(gate, name, Vector3.zero, new Vector3(width, 5.2f, .55f), darkMaterial, false);
            return gate.gameObject;
        }

        void BuildRoom(int index, int lane, FortressRoomV3 room, float signalZ)
        {
            var root = Group(chambers, room.id);
            float side = Side(lane);
            float x = room.plan.center.x;
            var panel = Group(root, "Sequence panel");
            panel.localPosition = new Vector3(x, .2f, room.plan.yMax - 9);
            Model(panel, sequenceModel, new Vector3(4.5f, .6f, 1.15f), fitAxes: true);
            Label(root, room.id, new Vector3(x, 2.6f, room.plan.yMin + 2), 180, .25f);
            for (int glyph = 0; glyph < Glyphs.Length; glyph++)
            {
                var pad = Station(root, Action.Pad, lane, index, glyph,
                    new Vector3(x + (glyph - 1) * 4.8f, .24f, room.plan.yMax - 5), "E: input " + Glyphs[glyph]);
                Symbol(pad.transform, glyph, 1.45f);
                Label(pad.transform, Glyphs[glyph], Vector3.up * .86f, 180, .13f);
            }

            var wall = Group(root, "Shared wall message");
            wall.localPosition = new Vector3(config.sharedWallX + side * .95f, 1.65f, signalZ);
            wall.localRotation = Quaternion.Euler(0, side * 90, 0);
            Model(wall, room.hint ? wallModel : windowModel, new Vector3(7.4f, 2.1f, .45f), fitAxes: true);
            for (int column = 0; column < 3; column++)
            {
                var socket = Group(wall, $"Message socket {column + 1}");
                socket.localPosition = new Vector3((column - 1) * -2.2f * side, 0, .38f);
                Block(socket, "Recess", Vector3.zero, new Vector3(1.8f, 1.5f, .15f), darkMaterial, false);
                Label(socket, (column + 1).ToString(), new Vector3(0, .86f, .2f), 0);
                for (int glyph = 0; glyph < 3; glyph++)
                {
                    var stone = Group(socket, Glyphs[glyph]);
                    Model(stone, transferModel, new Vector3(.9f, .55f, .65f), fitAxes: true);
                    var symbol = Group(stone, "Glyph");
                    symbol.localPosition = new Vector3(0, 0, .43f);
                    symbol.localRotation = Quaternion.Euler(90, 0, 0);
                    Symbol(symbol, glyph, .85f);
                    Label(stone, Glyphs[glyph], new Vector3(0, -.53f, .47f), 0, .11f);
                    stone.gameObject.SetActive(false);
                    stones[index, lane, column, glyph] = stone;
                }
            }

            if (!room.hint) return;
            var hint = Station(root, Action.ReadHint, lane, index, 0,
                new Vector3(side * 4, 1f, signalZ - 6), "E: inspect your hint stone");
            Model(hint.transform, hintModel, new Vector3(1.4f, 1.4f, 1.4f));
            hintText[index] = Label(hint.transform, "HINT", Vector3.up * 1.15f, 180);
            for (int glyph = 0; glyph < 3; glyph++)
            {
                var sender = Station(root, Action.Signal, lane, index, glyph,
                    new Vector3(side * 2.2f, .32f, signalZ + (glyph - 1) * 2.2f), "E: send " + Glyphs[glyph]);
                Symbol(sender.transform, glyph, .95f);
            }
            var clear = Station(root, Action.ClearSignal, lane, index, 0,
                new Vector3(side * 2.2f, .45f, signalZ + 4.8f), "E: clear the message stones");
            Label(clear.transform, "CLEAR", Vector3.up * .7f, side * 90);
        }

        CastleStation Station(Transform parent, Action kind, int slot, int index, int value, Vector3 position, string prompt)
        {
            var root = Group(parent, kind + " " + (kind is Action.Pad or Action.Signal or Action.SewerPad ? Glyphs[value] : prompt));
            root.localPosition = position;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(.9f, .65f, .9f);
            var station = root.gameObject.AddComponent<CastleStation>();
            station.Castle = this;
            station.Id = stations.Count;
            station.Kind = kind;
            station.Slot = slot;
            station.Stage = index;
            station.Value = value;
            station.Prompt = prompt;
            stations.Add(station);
            return station;
        }

        void Symbol(Transform parent, int glyph, float size)
        {
            GameObject prefab = symbolModels != null && glyph < symbolModels.Length ? symbolModels[glyph] : null;
            if (prefab != null) Model(parent, prefab, new Vector3(size, size * .32f, size), fitAxes: true);
            else Block(parent, Glyphs[glyph], Vector3.zero, new Vector3(size, .25f, size), accentMaterial, false);
        }

        TextMesh Label(Transform parent, string message, Vector3 position, float yaw, float size = .17f)
        {
            var label = Group(parent, message);
            label.localPosition = position;
            // TextMesh glyphs face local -Z.
            label.localRotation = Quaternion.Euler(0, yaw + 180, 0);
            var text = label.gameObject.AddComponent<TextMesh>();
            text.text = message;
            text.fontSize = 48;
            text.characterSize = size;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.white;
            if (labelShader != null)
            {
                if (labelMaterial == null)
                {
                    labelFont = text.font;
                    labelMaterial = new Material(labelShader) { mainTexture = labelFont.material.mainTexture };
                }
                label.GetComponent<MeshRenderer>().sharedMaterial = labelMaterial;
            }
            return text;
        }

        static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            if (material != null) block.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) block.GetComponent<Collider>().enabled = false;
            return block;
        }

        internal static void Model(Transform parent, GameObject prefab, Vector3 size, bool fitAxes = false)
        {
            if (prefab == null) return;
            var frame = Group(parent, prefab.name + " display");
            var clone = Instantiate(prefab, frame.transform);
            foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var animator in clone.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            var renderers = clone.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            // Measure in the display's basis, including rotations stored by the source prefab.
            var bounds = new Bounds(frame.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var renderer in renderers)
            {
                var b = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(frame.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            }

            Vector3 scale = new(size.x / Mathf.Max(.001f, bounds.size.x), size.y / Mathf.Max(.001f, bounds.size.y),
                size.z / Mathf.Max(.001f, bounds.size.z));
            if (!fitAxes) scale = Vector3.one * Mathf.Min(scale.x, scale.y, scale.z);
            clone.transform.localPosition -= bounds.center;
            frame.localScale = scale;
        }

        void ApplyViews()
        {
            if (labelMaterial != null && labelMaterial.mainTexture != labelFont.material.mainTexture)
                labelMaterial.mainTexture = labelFont.material.mainTexture;
            bool returning = IsSpawned && Returning;
            foreach (var reservation in reservations) SetActive(reservation, false);
            SetActive(partitions.gameObject, !IsSpawned || !PartitionsOpen);
            SetActive(chambers.gameObject, !IsSpawned || !PartitionsOpen);
            SetActive(returnLayout.gameObject, returning);
            SetActive(basementGate, !IsSpawned || !DoorOpen);
            for (int lane = 0; lane < 2; lane++)
            {
                SetActive(digits[lane], Stage == Stages);
                SetActive(sewerDoors[lane], !SewerOpen);
            }
            for (int i = 0; i < Stages; i++)
            {
                SetActive(unlockedGroups[i], Stage <= i);
                string nextHint = i == Stage && i == hintStage ? ReadGlyphs(hintCode) : "HINT";
                if (hintText[i].text != nextHint) hintText[i].text = nextHint;
                for (int lane = 0; lane < 2; lane++)
                for (int column = 0; column < 3; column++)
                for (int glyph = 0; glyph < 3; glyph++)
                {
                    var stone = stones[i, lane, column, glyph];
                    bool show = Stage == i && ((Signal >> (column * 2)) & 3) == glyph + 1;
                    SetActive(stone.gameObject, show);
                    if (show)
                        stone.localPosition = Vector3.MoveTowards(stone.localPosition, Vector3.forward * .5f, Time.deltaTime * 2.5f);
                    else stone.localPosition = Vector3.back * .3f;
                }
            }
            if (returning) DrawTraps();
        }

        static void SetActive(GameObject value, bool active)
        {
            if (value.activeSelf != active) value.SetActive(active);
        }

        static string ReadGlyphs(int code) => string.Join("  ", Enumerable.Range(0, SignalLength(code))
            .Select(i => Glyphs[((code >> (i * 2)) & 3) - 1]));

        internal void DrawHud(NetworkObject player)
        {
            if (!ready) return;
            int slot = Slot(player.OwnerClientId);
            if (slot < 0 || !Contains(player.transform.position))
            {
                pendingKeypad = -1;
                return;
            }

            var p = Progress(slot);
            if (Returning)
            {
                GUILayout.Label(p.Injured ? "Castle / Injured · slowed · sprint locked" : "Castle / Return corridor");
                if (SewerOpen) GUILayout.Label("Sewer open");
                else if (NearSewer(player.transform.position))
                {
                    double wait = p.RetryAt - Now;
                    GUILayout.Label(wait > 0 ? $"Sewer locked · retry in {wait:F1}s" : $"Sewer symbols {p.SewerLength}/12");
                }
            }
            else
            {
                string feedback = p.Feedback < 0 ? " · reset" : p.Steps == 3 ? " · partner pending" : "";
                if (Stage < Stages)
                {
                    Vector3 feet = player.transform.position - config.origin;
                    var room = config.rooms.FirstOrDefault(candidate => candidate.plan.Contains(new Vector2(feet.x, feet.z)));
                    GUILayout.Label($"{room?.id ?? "Keep hall"} / Path {Lane(slot)} / Stage {Stage + 1} / {p.Steps}/3{feedback}");
                }
                else GUILayout.Label(DoorOpen ? "Basement open: both cross the threshold" :
                    (accepted.Value & (1 << slot)) != 0 ? "Number accepted, waiting for partner" : "Castle / Submit both numbers");
                if (localNumber >= 0) GUILayout.Label($"Your number {localNumber:D4}");
                if (pendingKeypad == slot && !DoorOpen && (accepted.Value & (1 << slot)) == 0 &&
                    Near(player, keypads[slot]) && Keyboard.current?.escapeKey.wasPressedThisFrame != true &&
                    Cursor.lockState != CursorLockMode.Locked)
                {
                    GUILayout.Label(p.Feedback < 0 ? "Number rejected" : "Enter four digits");
                    numberEntry = new string(GUILayout.TextField(numberEntry, 4).Where(c => c >= '0' && c <= '9').ToArray());
                    if (GUILayout.Button("Submit") && numberEntry.Length == 4 && int.TryParse(numberEntry, out int value))
                        SubmitNumberRpc(value, p.Revision);
                }
                else pendingKeypad = -1;
            }

        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DrawDiagnostics(NetworkObject player)
        {
            if (!ready || Slot(player.OwnerClientId) < 0 || !Contains(player.transform.position)) return;
            GUILayout.Label($"Castle / Stage {Stage} / Crossed {Crossed} / Traps {ActiveTrapCount()}");
            GUILayout.Label(IsServer ? string.Join(" | ", Enumerable.Range(0, Stages).Select(i => ReadGlyphs(CodeForStage(i)))) +
                $"\nNumbers {numbers[0]:D4} / {numbers[1]:D4}" : "Authoritative answers available on Host");
        }
#endif
    }
}
