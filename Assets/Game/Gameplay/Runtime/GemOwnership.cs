using System.Linq;
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
        [SerializeField, Min(.5f)] float dropGroundSearch = 3;
        readonly NetworkVariable<uint> revision = new();
        double pickedUpAt, nextGroundCheck;
        int droppedFrame = -1;
        ulong lastDroppedCarrier = ulong.MaxValue;
        Vector3? lastSafeDrop;
        Vector3 dropFallback;
        BoxCollider finish;
        string finalApproach;
        Vector3 home, caveStart, upperDock, lowerDock, bigSummit, lastCarrierFeet;
        GameStateManager game;
        UndergroundGameplay underground;
        Transform model, pickup;
        bool bound;
        uint presentedRevision;
        float nextGemSpark;

        public static GemOwnership Current { get; private set; }
        public GemState State => status.Value;
        public ulong Owner => owner.Value;
        internal bool IsFirstReturnPickup => game.Transition.Kind == MatchTransition.Return &&
            game.Transition.GemRevision == revision.Value && State == GemState.Carried;
        internal Material VisualMaterial => model ? model.GetComponentInChildren<Renderer>(true)?.sharedMaterial : null;
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
            if (IsSpawned && IsServer && State == GemState.Sealed) position.Value = home;
        }

        internal void BindFinish(BoxCollider collider, TwinEdge.Levels.MountainConfigV3 mountain)
        {
            finish = collider;
            finalApproach = mountain != null ? mountain.poleCanyons.FirstOrDefault(edge =>
                edge.from == mountain.start.id || edge.to == mountain.start.id)?.island : null;
        }

        public override void OnNetworkSpawn()
        {
            presentedRevision = IsServer ? 0 : revision.Value;
            nextGemSpark = 0;
            if (!IsServer) return;
            status.Value = GemState.Sealed;
            owner.Value = ulong.MaxValue;
            position.Value = home;
            revision.Value = 0;
            droppedFrame = -1;
            lastDroppedCarrier = ulong.MaxValue;
            lastSafeDrop = null;
            nextGroundCheck = 0;
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
            revision.Value++;
        }

        internal void RequestAcquire()
        {
            var character = NetworkManager.LocalClient?.PlayerObject?.GetComponent<GameplayCharacter>();
            if (character != null) AcquireRpc(revision.Value, character.OwnerLife, game.ActionRevision);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void AcquireRpc(uint expectedRevision, int life, uint action, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (!bound || !IsServer || !CanAcquire || !game.AcceptsAction(action) || expectedRevision != revision.Value ||
                NetworkManager.ConnectedClientsList.Count != 2 || Time.frameCount == droppedFrame ||
                game.Phase is not (FoundationPhase.SealPending or FoundationPhase.Return) ||
                !NetworkManager.ConnectedClients.TryGetValue(sender, out var peer) || peer.PlayerObject == null ||
                !peer.PlayerObject.TryGetComponent<GameplayCharacter>(out var character) ||
                !character.IsCurrentLifeServer(life) || !character.CanContestFinish) return;
            Vector3 eye = peer.PlayerObject.transform.position + Vector3.up;
            if ((eye - Position).sqrMagnitude > 2.7f * 2.7f ||
                Physics.Linecast(eye, Position, out _, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;

            // Reserve before committing Return. The first accepted request changes the revision.
            GemState previous = State;
            lastCarrierFeet = peer.PlayerObject.transform.position;
            lastSafeDrop = TryDropPoint(lastCarrierFeet, out var safe) ? safe : null;
            owner.Value = sender;
            status.Value = GemState.Carried;
            pickedUpAt = NetworkManager.ServerTime.Time;
            revision.Value++;
            if (game.Phase == FoundationPhase.SealPending && !game.CommitFirstGemReturn(sender, CarrierPoint(character.transform), revision.Value))
            {
                owner.Value = ulong.MaxValue;
                status.Value = previous;
            }
        }

        internal bool TryDropServer(ulong carrier, Vector3 feet)
        {
            if (!IsSpawned || !IsServer || game.TransitionBlocksInput || State != GemState.Carried || Owner != carrier ||
                NetworkManager.ServerTime.Time < pickedUpAt + dropProtectionSeconds) return false;
            lastDroppedCarrier = carrier;
            droppedFrame = Time.frameCount;
            dropFallback = lastSafeDrop ?? RecoveryPoint(feet);
            Release(GemState.Dropped, TryDropPoint(feet, out var safe) ? safe : dropFallback);
            return true;
        }

        bool TryDropPoint(Vector3 feet, out Vector3 point)
        {
            point = default;
            if (underground.InLava(feet) || RiverGameplay.Current?.InWater(feet) == true) return false;
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * .25f, Vector3.down, dropGroundSearch,
                ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
            {
                if (hit.collider.GetComponentInParent<PlayerMovement>() != null) continue;
                if (hit.normal.y < .65f || underground.InLava(hit.point) || RiverGameplay.Current?.InWater(hit.point) == true) return false;
                point = hit.point + Vector3.up * .65f;
                return !Physics.OverlapSphere(point, .23f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(collider => collider.GetComponentInParent<PlayerMovement>() == null);
            }
            return false;
        }

        internal void BeforeRespawnServer(PlayerMovement player, Vector3? bridgeFeet, Vector3? supportedFeet)
        {
            if (!bound || !IsSpawned || !IsServer || game.Phase != FoundationPhase.Return) return;
            ulong id = player.OwnerClientId;
            if (!(State == GemState.Carried && Owner == id) &&
                !(State == GemState.Dropped && lastDroppedCarrier == id && droppedFrame == Time.frameCount)) return;
            Release(GemState.Relocated, RegionalDestination(player.transform.position, bridgeFeet, supportedFeet));
        }

        internal void BeforeRaftDeathServer(ulong rider)
        {
            if (IsSpawned && IsServer && game.Phase == FoundationPhase.Return && State == GemState.Carried && Owner == rider)
                Release(GemState.Relocated, lowerDock);
        }

        void Disconnected(ulong id)
        {
            if (game.Phase == FoundationPhase.Finished || State != GemState.Carried || Owner != id) return;
            Release(GemState.Relocated, RegionalDestination(lastCarrierFeet, null, null));
        }

        Vector3 RegionalDestination(Vector3 feet, Vector3? bridgeFeet, Vector3? supportedFeet) =>
            underground.Contains(feet) ? caveStart :
            CastleGameplay.Current?.Contains(feet) == true ? upperDock :
            bridgeFeet.HasValue ? bigSummit :
            RiverGameplay.Current?.InGemDeathRegion(feet) == true ? lowerDock :
            DeathDestination(feet, supportedFeet);

        Vector3 RecoveryPoint(Vector3 feet)
        {
            if (underground.Contains(feet)) return home;
            if (CastleGameplay.Current?.Contains(feet) == true) return upperDock;
            if (RiverGameplay.Current?.InGemDeathRegion(feet) == true) return lowerDock;
            return DeathDestination(feet, null);
        }

        Vector3 DeathDestination(Vector3 feet, Vector3? supportedFeet)
        {
            var mountain = CooperativePole.Current?.mountain;
            string summit = PoleGeometry.Summit(feet, mountain, 0);
            if (summit == null && supportedFeet.HasValue)
                summit = PoleGeometry.Summit(supportedFeet.Value, mountain, 0);
            if (mountain != null && (summit == mountain.start.id || finalApproach != null && summit == finalApproach))
                return lastSafeDrop ?? (supportedFeet.HasValue ? supportedFeet.Value + Vector3.up * .65f :
                    finish.transform.position + Vector3.up * .65f);
            return bigSummit;
        }

        void Release(GemState next, Vector3 point)
        {
            position.Value = point;
            owner.Value = ulong.MaxValue;
            status.Value = next;
            revision.Value++;
        }

        internal void TickDroppedServer()
        {
            if (State != GemState.Dropped || NetworkManager.ServerTime.Time < nextGroundCheck) return;
            nextGroundCheck = NetworkManager.ServerTime.Time + .25;
            if (!TryDropPoint(Position - Vector3.up * .65f, out Vector3 point) &&
                !TryDropPoint(dropFallback - Vector3.up * .65f, out point))
                point = RecoveryPoint(Position);
            if ((point - Position).sqrMagnitude > .01f) Release(GemState.Dropped, point);
        }

        static Vector3 CarrierPoint(Transform carrier) => carrier.TransformPoint(new Vector3(.46f, 1.32f, -.24f));

        void LateUpdate()
        {
            if (!bound || !underground.IsBound) return;
            Vector3 point = IsSpawned && State != GemState.Sealed ? Position : home;
            bool visible = true;
            bool carried = IsSpawned && State == GemState.Carried;
            Transform carrier = null;
            if (carried)
            {
                if (IsServer && NetworkManager.ConnectedClients.TryGetValue(Owner, out var peer))
                    carrier = peer.PlayerObject != null ? peer.PlayerObject.transform : null;
                else
                    foreach (var client in NetworkManager.SpawnManager.SpawnedObjectsList)
                        if (client.IsPlayerObject && client.OwnerClientId == Owner) { carrier = client.transform; break; }
                visible = carrier != null;
                if (carrier != null)
                {
                    point = CarrierPoint(carrier);
                    if (IsServer)
                    {
                        lastCarrierFeet = carrier.position;
                        if (NetworkManager.ServerTime.Time >= nextGroundCheck)
                        {
                            nextGroundCheck = NetworkManager.ServerTime.Time + .15;
                            if (TryDropPoint(lastCarrierFeet, out var safe)) lastSafeDrop = safe;
                        }
                    }
                }
            }

            model.gameObject.SetActive(visible);
            model.SetPositionAndRotation(point, Quaternion.Euler(0, Time.time * 38, 0));
            pickup.position = point;
            pickup.gameObject.SetActive(IsSpawned && CanAcquire);
            PresentGem(point, carrier, visible);
        }

        void PresentGem(Vector3 point, Transform carrier, bool visible)
        {
            if (!IsSpawned || !IsClient) return;
            if (presentedRevision != revision.Value)
            {
                // A carrier NetworkObject may be delivered one tick after the gem snapshot.
                if (!visible) return;
                presentedRevision = revision.Value;
                nextGemSpark = Time.time + .6f;
                if (State == GemState.Carried)
                {
                    if (carrier.TryGetComponent<GameplayCharacter>(out var character)) character.PresentGemPickup();
                    if (!IsFirstReturnPickup)
                        GameplayEffects.Emit(game, GameplayEffects.Kind.Gem, point, Vector3.up * .55f);
                }
                if (State is GemState.Dropped or GemState.Relocated)
                    GameplayEffects.Emit(game, State == GemState.Dropped ? GameplayEffects.Kind.GemDrop : GameplayEffects.Kind.GemRelocate,
                        point, Vector3.up * .65f);
            }
            if (!visible || State == GemState.Sealed || game.TransitionBlocksInput || Time.time < nextGemSpark) return;
            nextGemSpark = Time.time + (State == GemState.Carried ? 1.10f : .64f);
            GameplayEffects.Emit(game, GameplayEffects.Kind.Gem, point, Vector3.up * .4f, .20f);
        }

    }
}
