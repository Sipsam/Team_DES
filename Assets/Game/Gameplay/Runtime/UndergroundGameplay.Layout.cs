using System;
using System.Collections.Generic;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    public sealed partial class UndergroundGameplay
    {
        [Header("Underground presentation")]
        [SerializeField] GameObject platformModel, leftSwitchModel, rightSwitchModel, altarModel, sealModel, gemModel;
        [SerializeField] GameObject flameModel, arrowModel, spikesModel, movingModel, vanishModel;
        [SerializeField] Material leftMaterial, rightMaterial, warningMaterial;
        [SerializeField] Shader signShader;

        readonly List<Deck>[] groups = Enumerable.Range(0, 8).Select(_ => new List<Deck>()).ToArray();
        readonly UndergroundStation[] stations = new UndergroundStation[12];
        readonly CavernPlatformV3[,] landings = new CavernPlatformV3[2, Stages];
        readonly Collider[,] landingSurfaces = new Collider[2, Stages];
        Collider altarSurface;
        Transform sealView;
        Material signMaterial;
        Font signFont;

        internal readonly struct Deck
        {
            public readonly GameObject Object;
            public readonly Vector3 Top;
            public Deck(GameObject obj, Vector3 top) { Object = obj; Top = top; }
        }

        internal IReadOnlyList<Deck> GroupDecks(int index) => groups[index];
        internal UndergroundStation Station(int index) => stations[index];
        internal Vector3 Landing(int lane, int index) => World(landings[lane, index].position);

        internal void Bind(Scene scene)
        {
            if (!scene.isLoaded || (IsBound && boundScene == scene)) return;
            var roots = scene.GetRootGameObjects();
            var sections = roots.SelectMany(root => root.GetComponentsInChildren<FortressSectionV3>(true));
            var cave = sections.SingleOrDefault(section => section.part == FortressPartV3.UndergroundCavern);
            if (cave == null || cave.generated == null || cave.config == null) return;
            config = cave.config;
            boundScene = scene;
            if (layout != null) Destroy(layout.gameObject);
            layout = Create("UndergroundGameplayLayout", transform, Vector3.zero);
            layout.position = config.origin;

            var generated = cave.generated.transform;
            altarSurface = generated.Find("JointSealLanding").GetComponent<Collider>();
            lavaBounds = generated.Find("Lava").GetComponent<Renderer>().bounds;
            var markers = roots.SelectMany(root => root.GetComponentsInChildren<LevelMarker>(true)).ToArray();
            Vector3 GroundMarker(string id) => V3GameplayBootstrap.SafeGround(markers.Single(marker => marker.id == id).transform.position);
            gem.Bind(
                Display(layout, gemModel, config.seal + Vector3.up * 1.45f, Vector3.one * .85f),
                BuildStation(GemStation, config.seal + Vector3.up * 1.45f, "E: take gem").transform,
                Altar + Vector3.up * 1.45f, Entry + Vector3.up * .8f,
                GroundMarker("Valley.V3.UpperCheckpoint") + Vector3.up * .65f,
                GroundMarker("Valley.V3.LowerCheckpoint") + Vector3.up * .65f,
                GroundMarker("Mountain.V3.Final") + Vector3.up * .65f);

            foreach (var platform in config.platforms.Where(p => p.fixedLanding))
            {
                int lane = platform.id[0] == 'A' ? 0 : 1;
                int index = platform.id[1] - '0';
                landings[lane, index] = platform;
                landingSurfaces[lane, index] = generated.Find($"Fixed_{platform.id}/LandingSurface").GetComponent<Collider>();
                generated.Find($"Fixed_{platform.id}/OppositeSwitch_{platform.id}").gameObject.SetActive(false);
                var target = BuildStation(lane * Stages + index, platform.position + new Vector3(0, 1f, 1.8f),
                    "E: extend partner's platforms");
                Display(target.transform, lane == 0 ? leftSwitchModel : rightSwitchModel, Vector3.down * .44f, new Vector3(1.3f, 1.15f, 1.3f));
                Sign(target.transform, platform.id, Vector3.up * .45f);
            }

            for (int i = 0; i < groups.Length; i++)
            {
                int lane = Receiver(i), stage = Destination(i) - 1;
                groups[i].Clear();
                string id = landings[lane, stage].id;
                var reservations = config.platforms.Where(p => !p.fixedLanding && p.id.StartsWith(id + "-", StringComparison.Ordinal)).ToArray();
                foreach (var reservation in reservations)
                    generated.Find($"Timed_{reservation.id}_by_{reservation.activatedBy}_25s_PROVISIONAL").gameObject.SetActive(false);

                Vector3 start = landings[lane, stage].position;
                Vector3 end = stage + 1 < Stages ? landings[lane, stage + 1].position : config.seal + new Vector3(lane == 0 ? -8 : 8, 0, 5);
                // Smaller steps preserve the elevation profile while fitting the V3 player's jump.
                int spans = Mathf.CeilToInt(Vector3.Distance(start, end) / 3.25f);
                for (int j = 1; j < spans; j++)
                {
                    Vector3 top = Vector3.Lerp(start, end, j / (float)spans);
                    float depth = 1.65f + (j % 2) * .25f;
                    groups[i].Add(BuildDeck($"{id} stone {j}", top, new Vector2(3.35f, depth), platformModel,
                        lane == 0 ? leftMaterial : rightMaterial));
                }
            }

            generated.Find("GemReservation").gameObject.SetActive(false);
            generated.Find("SealInteractionA").gameObject.SetActive(false);
            generated.Find("SealInteractionB").gameObject.SetActive(false);
            Display(layout, altarModel, config.seal + Vector3.up * .52f, new Vector3(2.1f, 1.04f, 2.1f));
            sealView = Display(layout, sealModel, config.seal + Vector3.up * 1.45f, new Vector3(2, 1.8f, 2));
            UndergroundStation final = BuildStation(FinalSwitch, config.seal + new Vector3(8, 1, 3), "E: open the last crossing for A");
            Display(final.transform, rightSwitchModel, Vector3.down * .44f, Vector3.one * 1.1f);
            Sign(final.transform, "OPEN A", Vector3.up * .55f);
            for (int lane = 0; lane < 2; lane++)
            {
                var station = BuildStation(SealStation + lane, config.seal + new Vector3(lane == 0 ? -3.6f : 3.6f, 1, 0), "E: join the shared seal");
                Display(station.transform, lane == 0 ? leftSwitchModel : rightSwitchModel, Vector3.down * .55f, Vector3.one * .83f);
                Sign(station.transform, lane == 0 ? "SEAL A" : "SEAL B", Vector3.up * .4f);
            }
            Sign(layout, "A  <     >  B", config.start + new Vector3(0, 1.55f, -3.5f));
            BuildHazards();
            ApplyViews();
            Physics.SyncTransforms();
        }

        static Transform Create(string name, Transform parent, Vector3 point)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            item.localPosition = point;
            return item;
        }

        Transform Display(Transform parent, GameObject prefab, Vector3 point, Vector3 size, bool fit = false)
        {
            var view = Create(prefab.name, parent, point);
            CastleGameplay.Model(view, prefab, size, fitAxes: fit);
            return view;
        }

        void Sign(Transform parent, string label, Vector3 point)
        {
            if (signMaterial == null)
            {
                signFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                signMaterial = new Material(signShader) { mainTexture = signFont.material.mainTexture };
            }
            var facing = Create(label, parent, point);
            for (int side = 0; side < 2; side++)
            {
                var view = Create("Text", facing, Vector3.forward * (side == 0 ? .015f : -.015f));
                view.localRotation = Quaternion.Euler(0, 180 * side, 0);
                var text = view.gameObject.AddComponent<TextMesh>();
                text.font = signFont;
                text.text = label;
                text.fontSize = 48;
                text.anchor = TextAnchor.MiddleCenter;
                text.characterSize = .175f;
                text.color = Color.white;
                view.GetComponent<MeshRenderer>().sharedMaterial = signMaterial;
            }
        }

        UndergroundStation BuildStation(int id, Vector3 point, string prompt)
        {
            var station = Create($"Station {id}", layout, point);
            var trigger = station.gameObject.AddComponent<SphereCollider>();
            trigger.radius = .3f;
            trigger.isTrigger = true;
            var interactive = station.gameObject.AddComponent<UndergroundStation>();
            interactive.Initialize(this, id, prompt);
            stations[id] = interactive;
            return interactive;
        }

        Deck BuildDeck(string name, Vector3 top, Vector2 size, GameObject model, Material material)
        {
            var root = Create(name, layout, top);
            var support = root.gameObject.AddComponent<BoxCollider>();
            support.size = new Vector3(size.x, .42f, size.y);
            support.center = Vector3.down * .21f;
            Display(root, model, support.center, support.size, fit: true);
            // The coloured edge marks which partner's lane the stone belongs to.
            Stripe(root, new Vector3(0, -.08f, -size.y * .5f), new Vector3(size.x, .15f, .055f), material);
            return new Deck(root.gameObject, World(top));
        }

        GameObject Stripe(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Signal";
            stripe.transform.SetParent(parent, false);
            stripe.transform.localPosition = position;
            stripe.transform.localScale = size;
            stripe.GetComponent<Collider>().enabled = false;
            stripe.GetComponent<Renderer>().sharedMaterial = material;
            return stripe;
        }

        void ApplyViews()
        {
            for (int i = 0; i < groups.Length; i++)
            {
                bool active = IsSpawned && GroupActive(i);
                foreach (var deck in groups[i])
                    if (deck.Object.activeSelf != active) deck.Object.SetActive(active);
            }
            sealView.gameObject.SetActive(!IsSpawned || Seal != UndergroundSeal.Unsealed);
            if (signMaterial.mainTexture != signFont.material.mainTexture) signMaterial.mainTexture = signFont.material.mainTexture;
            DrawHazards();
        }

        internal void DrawHud(NetworkObject player, bool debug)
        {
            if (!IsSpawned || !InCavern(player.transform.position)) return;
            string title = Returning ? "Underground return | follow the permanent stones" :
                !castle.PartitionsOpen ? "Underground | waiting for partner at the castle door" :
                Step < SwitchOrder.Length ? $"Underground crossing {Step + 1}/{SwitchOrder.Length} | switch {(SwitchOrder[Step] == FinalSwitch ? "B altar" : SwitchName(SwitchOrder[Step]))}" :
                "Underground | shared seal";
            GUILayout.Label(title);
            if (!Returning && Step < SwitchOrder.Length && GroupActive(Step))
                GUILayout.Label($"Stones retract in {Math.Max(0, Deadline(Step) - Now):F1}s");
            if (Arrivals != 0 && !Returning)
                GUILayout.Label($"{Seal} | seal participants {((Participants & 1) + (Participants >> 1))}/2" +
                    (Participants == 0 ? "" : " | Esc: cancel"));
            if (RecoveryCount != 0 && !Returning) GUILayout.Label("Lava resets both routes at the underground start");
            if (Returning) GUILayout.Label("Lava respawn: seal terrace");
            if (!debug) return;
            int a = Enumerable.Range(0, Step).Count(i => Receiver(i) == 0);
            GUILayout.Label($"Crossings A {a}/4 · B {Step - a}/4 | arrivals A {(Arrivals & 1) != 0} B {(Arrivals & 2) != 0}");
            GUILayout.Label($"Phase: {game.Phase}; gem: {gem.State} / {gem.Owner} | lava resets: {RecoveryCount} | recent: {recentFailure.Value}");
            for (int first = 0; first < groups.Length; first += 4)
                GUILayout.Label(string.Join("  ", Enumerable.Range(first, 4)
                    .Select(i => $"{i + 1}:{(GroupActive(i) ? '+' : '-')}{Deadline(i):F1}")));
        }

        static string SwitchName(int id) => (id < Stages ? "A" : "B") + (id % Stages);

        public override void OnDestroy()
        {
            if (Current == this) Current = null;
            if (signMaterial != null) Destroy(signMaterial);
            base.OnDestroy();
        }
    }
}
