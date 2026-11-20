using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public enum GemState : byte { Sealed, Available, Carried, Dropped, Relocated }

    // Server-owned objective. The model follows the replicated carrier on each peer.
    [RequireComponent(typeof(GameStateManager))]
    public sealed class GemOwnership : NetworkBehaviour
    {
        [SerializeField, Range(1, 2)] float dropProtectionSeconds = 1.5f;
        readonly NetworkVariable<GemState> status = new();
        readonly NetworkVariable<ulong> owner = new(ulong.MaxValue);
        readonly NetworkVariable<Vector3> position = new();
        double pickedUpAt;
        Vector3 home, caveStart, upperDock, lowerDock, bigSummit, lastCarrierFeet;
        GameStateManager game;
        UndergroundGameplay underground;
        Transform model, pickup;
        bool bound;

        public static GemOwnership Current { get; private set; }
        public GemState State => status.Value;
        public ulong Owner => owner.Value;
        internal Vector3 Position => position.Value;
        internal bool CanAcquire => State is GemState.Available or GemState.Dropped or GemState.Relocated;

        void Awake()
        {
            Current = this;
            game = GetComponent<GameStateManager>();
            underground = GetComponent<UndergroundGameplay>();
        }

        public override void OnDestroy()
        {
            if (Current == this) Current = null;
            base.OnDestroy();
        }

        internal void Bind(Transform view, Transform target, Vector3 altar, Vector3 entry, Vector3 upper, Vector3 lower, Vector3 summit)
        {
            model = view;
            pickup = target;
            home = altar;
            caveStart = entry;
            upperDock = upper;
            lowerDock = lower;
            bigSummit = summit;
            bound = true;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            status.Value = GemState.Sealed;
            owner.Value = ulong.MaxValue;
            position.Value = home;
            NetworkManager.OnClientDisconnectCallback += Disconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback -= Disconnected;
        }

        internal void UnlockServer(Vector3 point)
        {
            if (!IsSpawned || !IsServer || State != GemState.Sealed) return;
            position.Value = point;
            status.Value = GemState.Available;
        }

        internal void RequestAcquire() => AcquireRpc();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void AcquireRpc(RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (!bound || !IsServer || !CanAcquire || !game.CanPlay ||
                game.Phase is not (FoundationPhase.SealPending or FoundationPhase.Return) ||
                !NetworkManager.ConnectedClients.TryGetValue(sender, out var peer) || peer.PlayerObject == null) return;
            Vector3 eye = peer.PlayerObject.transform.position + Vector3.up;
            if ((eye - Position).sqrMagnitude > 2.7f * 2.7f ||
                Physics.Linecast(eye, Position, out _, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;

            // Reserve the item before changing the phase. RPCs are serialized on the server.
            GemState previous = State;
            lastCarrierFeet = peer.PlayerObject.transform.position;
            owner.Value = sender;
            status.Value = GemState.Carried;
            pickedUpAt = NetworkManager.ServerTime.Time;
            if (game.Phase == FoundationPhase.SealPending && !game.AdvanceFoundation(FoundationPhase.Return))
            {
                owner.Value = ulong.MaxValue;
                status.Value = previous;
            }
        }

        public bool TryDropServer(ulong carrier, Vector3 feet)
        {
            if (!IsSpawned || !IsServer || State != GemState.Carried || Owner != carrier ||
                NetworkManager.ServerTime.Time < pickedUpAt + dropProtectionSeconds) return false;
            Release(GemState.Dropped, feet + Vector3.up * .55f);
            return true;
        }

        internal void BeforeRespawnServer(PlayerMovement player)
        {
            if (!bound || !IsSpawned || !IsServer || State != GemState.Carried || Owner != player.OwnerClientId) return;
            Relocate(player.transform.position);
        }

        void Disconnected(ulong id)
        {
            if (State == GemState.Carried && Owner == id) Relocate(lastCarrierFeet);
        }

        void Relocate(Vector3 feet)
        {
            Vector3 destination = underground.Contains(feet) ? caveStart :
                feet.z >= upperDock.z - 12 ? upperDock :
                feet.z > lowerDock.z ? lowerDock : bigSummit;
            Release(GemState.Relocated, destination);
        }

        void Release(GemState next, Vector3 point)
        {
            position.Value = point;
            owner.Value = ulong.MaxValue;
            status.Value = next;
        }

        void LateUpdate()
        {
            if (!bound) return;
            Vector3 point = IsSpawned && State != GemState.Sealed ? Position : home;
            bool visible = true;
            bool carried = IsSpawned && State == GemState.Carried;
            if (carried)
            {
                Transform carrier = null;
                if (IsServer && NetworkManager.ConnectedClients.TryGetValue(Owner, out var peer))
                    carrier = peer.PlayerObject != null ? peer.PlayerObject.transform : null;
                else
                    foreach (var client in NetworkManager.SpawnManager.SpawnedObjectsList)
                        if (client.IsPlayerObject && client.OwnerClientId == Owner) { carrier = client.transform; break; }
                visible = carrier != null;
                if (carrier != null)
                {
                    point = carrier.TransformPoint(new Vector3(.46f, 1.32f, -.24f));
                    if (IsServer) lastCarrierFeet = carrier.position;
                }
            }

            model.gameObject.SetActive(visible);
            model.SetPositionAndRotation(point, Quaternion.Euler(0, Time.time * 38, 0));
            pickup.position = point;
            pickup.gameObject.SetActive(IsSpawned && CanAcquire);
        }

        internal void DrawHud()
        {
            if (!IsSpawned || State == GemState.Sealed) return;
            GUILayout.Label(State == GemState.Carried
                ? Owner == NetworkManager.LocalClientId ? "Gem: you carry it" : "Gem: partner carries it"
                : State == GemState.Relocated ? "Gem relocated to the next return checkpoint" : "Gem available");
        }
    }
}
