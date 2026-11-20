using System;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    public enum UndergroundSeal : byte { Sealed, ReadyForCooperation, Unsealing, Unsealed }

    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(GameStateManager), typeof(RespawnManager), typeof(GemOwnership))]
    public sealed partial class UndergroundGameplay : NetworkBehaviour
    {
        enum UndergroundFailure : byte { None, Expired, Lava, SealCancelled, PartnerDisconnected }
        const int Both = 3;
        const int Stages = 4;
        const int FinalSwitch = 8;
        const int SealStation = 9;
        const int GemStation = 11;
        // B opens A's last crossing from the altar, where the switch remains reachable.
        static readonly int[] SwitchOrder = { 0, 5, 1, 6, 2, 7, 3, FinalSwitch };
        [SerializeField, Range(20, 30)] float platformSeconds = 25;
        [SerializeField, Min(.25f)] float sealSeconds = 2.5f;
        [SerializeField, Min(1)] float participationSeconds = 8;
        readonly NetworkVariable<int> step = new(), arrivals = new(), participants = new(), recoveryCount = new();
        readonly NetworkVariable<UndergroundSeal> seal = new();
        readonly NetworkVariable<UndergroundFailure> recentFailure = new();
        readonly NetworkVariable<double> unsealAt = new(), firstParticipationEnds = new(), hazardEpoch = new(-1);
        NetworkList<double> deadlines;
        NetworkList<double> slowedUntil;
        readonly int[] safeStages = new int[2];
        readonly bool[] resumeHere = new bool[2];
        readonly double[] lastReturnRespawn = new double[2];
        readonly ulong[] clientIds = { ulong.MaxValue, ulong.MaxValue };

        GameStateManager game;
        RespawnManager respawns;
        NetworkSpawnCoordinator spawns;
        CastleGameplay castle;
        GemOwnership gem;
        FortressConfigV3 config;
        Scene boundScene;
        Transform layout;
        Bounds lavaBounds;
        bool resetting;
        double recoveryGraceUntil;

        public static UndergroundGameplay Current { get; private set; }
        internal bool IsBound => layout != null;
        internal bool Returning => IsSpawned && game.Phase == FoundationPhase.Return;
        internal int Step => step.Value;
        internal UndergroundSeal Seal => seal.Value;
        internal int Arrivals => arrivals.Value;
        internal int Participants => participants.Value;
        internal int RecoveryCount => recoveryCount.Value;
        internal Vector3 World(Vector3 local) => config.origin + local;
        internal Vector3 Entry => World(config.start);
        internal Vector3 Altar => World(config.seal);
        internal double Now => NetworkManager.ServerTime.Time;
        bool Cooperative => IsSpawned && castle.PartitionsOpen &&
            game.Phase is FoundationPhase.Outbound or FoundationPhase.SealPending;

        void Awake()
        {
            Current = this;
            game = GetComponent<GameStateManager>();
            respawns = GetComponent<RespawnManager>();
            castle = GetComponent<CastleGameplay>();
            gem = GetComponent<GemOwnership>();
            deadlines = new NetworkList<double>();
            slowedUntil = new NetworkList<double>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            spawns = NetworkManager.GetComponent<NetworkSpawnCoordinator>();
            NetworkManager.OnClientDisconnectCallback += OnDisconnect;
            deadlines.Clear();
            slowedUntil.Clear();
            for (int i = 0; i < SwitchOrder.Length; i++) deadlines.Add(0);
            for (int i = 0; i < 2; i++)
            {
                slowedUntil.Add(0);
                safeStages[i] = 0;
                resumeHere[i] = false;
                lastReturnRespawn[i] = -1;
                clientIds[i] = ulong.MaxValue;
            }
            step.Value = arrivals.Value = recoveryCount.Value = 0;
            ClearParticipation();
            seal.Value = UndergroundSeal.Sealed;
            recentFailure.Value = UndergroundFailure.None;
            hazardEpoch.Value = -1;
            foreach (var trap in traps)
                if (trap != null) trap.PreviousCycle = -1;
            recoveryGraceUntil = 0;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback -= OnDisconnect;
        }

        void OnDisconnect(ulong id)
        {
            int slot = Array.IndexOf(clientIds, id);
            if (slot < 0) return;
            clientIds[slot] = ulong.MaxValue;
            if (resumeHere[slot]) recentFailure.Value = UndergroundFailure.PartnerDisconnected;
            arrivals.Value &= ~(1 << slot);
            slowedUntil[slot] = 0;
            lastReturnRespawn[slot] = -1;
            ClearParticipation();
            if (Seal != UndergroundSeal.Unsealed) seal.Value = UndergroundSeal.Sealed;
        }

        bool InCavern(Vector3 position)
        {
            if (!IsBound || position.y > config.origin.y + config.ceiling || position.z > lavaBounds.max.z + 5) return false;
            return position.x >= lavaBounds.min.x - 3 && position.x <= lavaBounds.max.x + 3 &&
                position.z >= lavaBounds.min.z - 3;
        }

        internal bool Contains(Vector3 position) => InCavern(position);

        internal bool TryResume(int slot, out Pose pose, out string label)
        {
            pose = default;
            label = null;
            if (!IsBound || !IsSpawned || !IsServer || slot < 0 || slot >= 2 || !resumeHere[slot]) return false;
            pose = Returning ? AltarPose(slot) : safeStages[slot] == 0 ? EntryPose(slot) : LandingPose(slot, safeStages[slot]);
            label = Returning ? "Underground seal" : safeStages[slot] == 0 ? "Underground start" : "Underground landing";
            return true;
        }

        Pose EntryPose(int lane) => new(Entry + new Vector3(lane == 0 ? -2 : 2, .15f, 0), Quaternion.Euler(0, 180, 0));
        Pose AltarPose(int lane) => new(Altar + new Vector3(lane == 0 ? -4 : 4, .15f, -2), Quaternion.identity);
        Pose LandingPose(int lane, int index) => index == Stages ? AltarPose(lane) :
            new(World(landings[lane, index].position) + new Vector3(0, .15f, 0), Quaternion.Euler(0, 180, 0));

        internal bool HandleFall(PlayerMovement player)
        {
            if (!IsBound || !IsSpawned || !IsServer || !InCavern(player.transform.position)) return false;
            if (player.transform.position.y <= lavaBounds.max.y + .1f && Now >= recoveryGraceUntil &&
                spawns.TryGetSlot(player.OwnerClientId, out int slot) && slot < 2 && Now >= lastReturnRespawn[slot])
                respawns.RespawnPlayer(player);
            // The global support-height fallback would kill players well above the lava.
            return true;
        }

        internal bool AssignDeathCheckpoint(PlayerMovement player)
        {
            if (!InCavern(player.transform.position) || !spawns.TryGetSlot(player.OwnerClientId, out int slot)) return false;
            slowedUntil[slot] = 0;
            ClearParticipation();
            recentFailure.Value = UndergroundFailure.Lava;
            if (Returning)
            {
                arrivals.Value &= ~(1 << slot);
                lastReturnRespawn[slot] = Now + 1;
                SetDeathCheckpoint(player, AltarPose(slot), "Underground seal");
            }
            else if (NetworkManager.ConnectedClientsList.Count < 2)
            {
                Pose safe = safeStages[slot] == 0 ? EntryPose(slot) : LandingPose(slot, safeStages[slot]);
                SetDeathCheckpoint(player, safe, safeStages[slot] == 0 ? "Underground start" : "Underground landing");
            }
            else if (!resetting)
            {
                resetting = true;
                recoveryGraceUntil = Now + 1;
                recoveryCount.Value++;
                step.Value = arrivals.Value = 0;
                if (Seal != UndergroundSeal.Unsealed) seal.Value = UndergroundSeal.Sealed;
                for (int i = 0; i < deadlines.Count; i++) deadlines[i] = 0;
                Array.Clear(safeStages, 0, safeStages.Length);
                SetDeathCheckpoint(player, EntryPose(slot), "Underground start");

                // Reset the alternating sequence and return both participants to its reachable start.
                foreach (var peer in NetworkManager.ConnectedClientsList)
                {
                    if (peer.ClientId == player.OwnerClientId || peer.PlayerObject == null ||
                        !spawns.TryGetSlot(peer.ClientId, out int other) || !InCavern(peer.PlayerObject.transform.position)) continue;
                    var partner = peer.PlayerObject.GetComponent<PlayerMovement>();
                    slowedUntil[other] = 0;
                    SetDeathCheckpoint(partner, EntryPose(other), "Underground start");
                    respawns.RespawnPlayer(partner, useAssignedCheckpoint: true);
                }
                resetting = false;
            }
            return true;
        }

        void SetDeathCheckpoint(PlayerMovement player, Pose pose, string label) =>
            respawns.SetCheckpoint(player, pose.position, pose.rotation, label);

        bool AtAltar(Vector3 feet) =>
            Mathf.Abs(feet.x - Altar.x) < 11.5f && Mathf.Abs(feet.z - Altar.z) < 9.5f && OnSurface(altarSurface, feet);

        static bool OnSurface(Collider support, Vector3 feet) =>
            support.Raycast(new Ray(feet + Vector3.up * .2f, Vector3.down), out _, .6f);

        void ClearParticipation()
        {
            participants.Value = 0;
            firstParticipationEnds.Value = unsealAt.Value = 0;
        }

        internal bool GroupActive(int index) => Returning || Cooperative && index < deadlines.Count && deadlines[index] > Now;
        internal double Deadline(int index) => index < deadlines.Count ? deadlines[index] : 0;

        static int SwitchLane(int id) => id == FinalSwitch ? 1 : id / Stages;
        static int Receiver(int index) => 1 - SwitchLane(SwitchOrder[index]);
        static int Destination(int index) => index / 2 + 1;

        bool TrySender(ulong id, out NetworkObject player, out int slot)
        {
            player = null;
            slot = -1;
            if (!IsBound || !IsSpawned || !IsServer || !game.CanPlay || !spawns.TryGetSlot(id, out slot) ||
                slot < 0 || slot >= 2 || !NetworkManager.ConnectedClients.TryGetValue(id, out var peer)) return false;
            player = peer.PlayerObject;
            return player != null && InCavern(player.transform.position);
        }

        bool Near(NetworkObject player, UndergroundStation target)
        {
            Vector3 delta = player.transform.position + Vector3.up - target.transform.position;
            return Mathf.Abs(delta.y) < 1.5f && delta.sqrMagnitude <= 2.7f * 2.7f &&
                !Physics.Linecast(player.transform.position + Vector3.up, target.transform.position,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        internal bool CanUse(int id, ulong client)
        {
            int lane = castle.Slot(client);
            if (!IsBound || !IsSpawned || !game.CanPlay || lane < 0) return false;
            if (id == GemStation) return gem.CanAcquire;
            if (!Cooperative) return false;
            if (id >= SealStation) return id == SealStation + lane && Step == SwitchOrder.Length && arrivals.Value == Both &&
                Seal != UndergroundSeal.Unsealed && (participants.Value & (1 << lane)) == 0;
            return Step < SwitchOrder.Length && SwitchLane(id) == lane && id == SwitchOrder[Step] && !GroupActive(Step);
        }

        internal void Interact(int id, PlayerInteraction player)
        {
            if (!player.IsOwner || !CanUse(id, player.OwnerClientId)) return;
            if (id == GemStation) gem.RequestAcquire();
            else UseRpc(id, Step, Deadline(Step));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void UseRpc(int id, int expectedStep, double previousDeadline, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (id < 0 || id >= GemStation || !TrySender(sender, out var player, out int slot) ||
                !CanUse(id, sender) || !Near(player, stations[id])) return;
            if (id < SealStation)
            {
                if (Step != expectedStep || Deadline(Step) != previousDeadline) return;
                deadlines[Step] = Now + platformSeconds;
                recentFailure.Value = UndergroundFailure.None;
                return;
            }

            if (!AtAltar(player.transform.position) || game.Phase != FoundationPhase.SealPending) return;
            participants.Value |= 1 << slot;
            if (participants.Value == Both)
            {
                seal.Value = UndergroundSeal.Unsealing;
                unsealAt.Value = Now + sealSeconds;
            }
            else firstParticipationEnds.Value = Now + participationSeconds;
        }

        internal void CancelOwner(PlayerInteraction player)
        {
            int lane = castle.Slot(player.OwnerClientId);
            if (IsSpawned && player.IsOwner && lane >= 0 && (participants.Value & (1 << lane)) != 0) CancelRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void CancelRpc(RpcParams rpc = default)
        {
            if (!TrySender(rpc.Receive.SenderClientId, out _, out int slot) ||
                (participants.Value & (1 << slot)) == 0) return;
            ClearParticipation();
            recentFailure.Value = UndergroundFailure.SealCancelled;
            seal.Value = arrivals.Value == Both ? UndergroundSeal.ReadyForCooperation : UndergroundSeal.Sealed;
        }

        void Update()
        {
            if (!IsBound) return;
            if (IsSpawned && IsServer) TickServer();
            ApplyViews();
        }

        void TickServer()
        {
            bool active = Cooperative || Returning;
            if (active && hazardEpoch.Value < 0) hazardEpoch.Value = Now;
            if (Cooperative && Step < deadlines.Count && deadlines[Step] > 0 && deadlines[Step] <= Now &&
                recentFailure.Value == UndergroundFailure.None)
                recentFailure.Value = UndergroundFailure.Expired;
            int actualArrivals = 0, nearbyParticipants = 0;
            foreach (var peer in NetworkManager.ConnectedClientsList)
            {
                if (peer.PlayerObject == null || !spawns.TryGetSlot(peer.ClientId, out int slot) || slot >= 2) continue;
                clientIds[slot] = peer.ClientId;
                Vector3 feet = peer.PlayerObject.transform.position;
                resumeHere[slot] = active && InCavern(feet);
                if (!game.CanPlay || !resumeHere[slot]) continue;
                if (AtAltar(feet))
                {
                    actualArrivals |= 1 << slot;
                    if (Near(peer.PlayerObject, stations[SealStation + slot])) nearbyParticipants |= 1 << slot;
                }

                if (Cooperative && Step < SwitchOrder.Length && Receiver(Step) == slot && GroupActive(Step) &&
                    ReachedLanding(slot, Destination(Step), feet))
                {
                    safeStages[slot] = Destination(Step);
                    step.Value++;
                }
            }

            arrivals.Value = actualArrivals;
            if (Cooperative && Step == SwitchOrder.Length && actualArrivals == Both && game.Phase == FoundationPhase.Outbound)
                game.AdvanceFoundation(FoundationPhase.SealPending);

            if (Cooperative && Seal != UndergroundSeal.Unsealed)
            {
                bool ready = game.CanPlay && Step == SwitchOrder.Length && actualArrivals == Both;
                if (!ready || (participants.Value & nearbyParticipants) != participants.Value ||
                    (participants.Value != Both && Now >= firstParticipationEnds.Value))
                    ClearParticipation();

                if (ready && participants.Value == Both && Now >= unsealAt.Value)
                {
                    seal.Value = UndergroundSeal.Unsealed;
                    ClearParticipation();
                    gem.UnlockServer(Altar + Vector3.up * 1.45f);
                }
                else seal.Value = !ready ? UndergroundSeal.Sealed : participants.Value == Both ?
                    UndergroundSeal.Unsealing : UndergroundSeal.ReadyForCooperation;
            }
            if (active && game.CanPlay) TickTrapDamage();
        }

        bool ReachedLanding(int lane, int index, Vector3 feet)
        {
            if (index == Stages) return AtAltar(feet);
            var target = landings[lane, index];
            Vector3 delta = feet - World(target.position);
            return Mathf.Abs(delta.x) < target.size.x * .5f && Mathf.Abs(delta.z) < target.size.y * .5f &&
                OnSurface(landingSurfaces[lane, index], feet);
        }

        internal float SpeedScale(PlayerMovement player)
        {
            int slot = castle.Slot(player.OwnerClientId);
            return IsSpawned && slot >= 0 && slot < slowedUntil.Count && slowedUntil[slot] > Now ? trapSpeed : 1f;
        }
    }
}
