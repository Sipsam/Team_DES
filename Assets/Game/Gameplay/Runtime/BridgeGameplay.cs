using TwinEdge.Audio;
using System;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class BridgeGameplay : NetworkBehaviour
    {
        public static BridgeGameplay Current { get; private set; }

        [SerializeField, Min(1)] int deliveryCount = 13;
        readonly NetworkVariable<int> deliveries = new();
        NetworkList<int> collapsed;

        BridgePlankV3[] planks;
        Renderer[][] plankRenderers;
        Collider[][] plankSupports;
        bool[] removed;
        Scene levelScene;
        ValleyConfigV3 layout;

        public int CompletedDeliveries => deliveries.Value;
        public int TotalDeliveries => deliveryCount;
        public int PlankCount => planks?.Length ?? 0;
        public bool IsCollapsed(int index) => removed[index];
        public int InstalledCount => Frontier(deliveries.Value);
        public Vector3 StartPoint => layout.bridgeStart;
        public Vector3 EndPoint => layout.bridgeEnd;
        public Vector3 Forward => (EndPoint - StartPoint).normalized;
        public float Length => Vector3.Distance(StartPoint, EndPoint);
        public float Pitch => Length / PlankCount;
        internal int Frontier(int count) => PlankCount * count / deliveryCount;

        void Awake()
        {
            collapsed = new NetworkList<int>();
            warning = new NetworkList<int>();
            warningProperties = new MaterialPropertyBlock();
        }

        public void Bind(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || (scene == levelScene && planks != null)) return;
            var section = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ValleySectionV3>())
                .Single(owner => owner.part == ValleyPartV3.Bridge);
            layout = section.config;
            var sorted = section.GetComponentsInChildren<BridgePlankV3>(true).OrderBy(plank => plank.plankId).ToArray();
            if (sorted.Length != layout.plankCount || deliveryCount < 1 || deliveryCount > sorted.Length ||
                sorted.Where((plank, index) => plank.plankId != index).Any())
                throw new InvalidOperationException("V3 bridge layout does not match its ordered plank identifiers.");
            planks = sorted;
            plankRenderers = sorted.Select(plank => plank.GetComponentsInChildren<Renderer>(true)).ToArray();
            plankSupports = sorted.Select(plank => plank.GetComponentsInChildren<Collider>(true)).ToArray();
            if (plankRenderers.Any(items => items.Length == 0) ||
                plankSupports.Any(items => !items.Any(collider => !collider.isTrigger)))
                throw new InvalidOperationException("A V3 plank is missing its saved visual or support collider.");

            levelScene = scene;
            removed = new bool[PlankCount];
            RefreshDeck();
            OnDeckBound();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            if (IsServer)
            {
                deliveries.Value = 0;
                collapsed.Clear();
                warning.Clear();
            }
            deliveries.OnValueChanged += OnDelivery;
            collapsed.OnListChanged += OnCollapse;
            warning.OnListChanged += OnWarning;
            RefreshDeck();
            StartBridgeSession();
        }

        public override void OnNetworkDespawn()
        {
            EndBridgeSession();
            deliveries.OnValueChanged -= OnDelivery;
            collapsed.OnListChanged -= OnCollapse;
            warning.OnListChanged -= OnWarning;
            if (Current == this) Current = null;
            if (planks != null && levelScene.isLoaded)
                for (int i = 0; i < PlankCount; i++) SetSupport(i, false);
        }

        void OnDelivery(int previous, int next)
        {
            RefreshDeck();
            OnDeliveryChanged();
        }

        void OnCollapse(NetworkListEvent<int> change)
        {
            if (planks == null || !levelScene.isLoaded) return;
            if (change.Type == NetworkListEvent<int>.EventType.Add)
            {
                removed[change.Value] = true;
                SetSupport(change.Value, false);
                game.PlayLiveCue(AudioCueId.BridgeCollapse, planks[change.Value].transform.position);
            }
            else RefreshDeck();
        }

        internal void RefreshDeck()
        {
            if (planks == null || !levelScene.isLoaded) return;
            Array.Clear(removed, 0, removed.Length);
            if (collapsed != null)
                foreach (int index in collapsed) removed[index] = true;
            for (int i = 0; i < PlankCount; i++)
            {
                SetSupport(i, i < InstalledCount && !removed[i]);
                SetWarning(i, warning != null && warning.Contains(i));
            }
        }

        void SetSupport(int index, bool enabled)
        {
            foreach (var renderer in plankRenderers[index]) renderer.enabled = enabled;
            foreach (var collider in plankSupports[index]) collider.enabled = enabled;
        }

        internal bool InstallNextServer()
        {
            if (!IsServer || deliveries.Value >= deliveryCount) return false;
            deliveries.Value++;
            return true;
        }

        partial void OnDeckBound();
        partial void StartBridgeSession();
        partial void EndBridgeSession();
        partial void OnDeliveryChanged();
    }
}
