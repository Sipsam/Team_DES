using TwinEdge.Audio;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    [RequireComponent(typeof(NetworkObject), typeof(GameStateManager), typeof(RespawnManager))]
    [DefaultExecutionOrder(-30)]
    public sealed partial class RiverGameplay : NetworkBehaviour
    {
        const ulong Empty = RiverRaftSnapshot.Empty;
        const float InputTimeout = .35f;
        const int SharedLimit = 5, SoloLimit = 3;
        [Header("Movement")]
        [SerializeField, Min(0)] float outboundFlow = 5.5f, returnFlow = 10f;
        [SerializeField, Min(.1f)] float weakSpeed = 11.5f, strongMultiplier = 1.65f;
        [SerializeField, Min(.1f)] float response = 2.4f, turnSpeed = 72f, courseCorrection = 1.45f;
        [SerializeField, Min(.5f)] float sharedRadius = 2.35f, soloRadius = 1.45f;
        [Header("Recovery")]
        [SerializeField, Min(.1f)] float hitInterval = .85f, rebuildDelay = 2f;
        [SerializeField, Min(.5f)] float boardingRange = 7f;
        [Header("Source models")]
        [SerializeField] GameObject sharedModel, soloModel, oarModel, dockModel, stairsModel;
        public static RiverGameplay Current { get; private set; }
        [SerializeField] Material waterMaterial;
        public string Prompt => Returning ? "E: board your race raft" : "E: board the linked raft";
        bool CanInteract => CanBoardAt(false) || CanBoardAt(true);
        internal bool CanBoardAt(bool upper) => IsSpawned && channel != null && (upper ?
            RaceActive && (Read(1).Status == RiverRaftStatus.Docked || Read(2).Status == RiverRaftStatus.Docked) :
            Outbound && Read(0).Status == RiverRaftStatus.Docked);

        readonly NetworkVariable<RiverRaftSnapshot> shared = new(), first = new(), second = new();
        readonly NetworkVariable<bool> split = new();
        readonly NetworkVariable<double> splitAt = new();
        readonly Hull[] hulls = { new(), new(), new() };
        readonly RaycastHit[] terrainHits = new RaycastHit[24];
        readonly HashSet<ulong> seenPlayers = new();
        readonly ulong[] soloOwners = { Empty, Empty };
        readonly uint[] suppressedVoyage = new uint[3];
        RiverChannel channel;
        GameStateManager game;
        RespawnManager respawns;
        Transform presentation;
        byte finishCount;
        bool recoveringShared, returningStarted, dockReconnect;
        float nextSend;
        uint sequence;
        bool Outbound => game.Phase == FoundationPhase.Outbound;
        bool Returning => game.Phase == FoundationPhase.Return;
        bool RaceActive => Returning && (split.Value || DevelopmentReturn);
        bool DevelopmentReturn
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return game.DevelopmentReturnRequested;
#else
                return false;
#endif
            }
        }

        sealed class Paddle
        {
            public uint Sequence;
            public float LastAt = float.NegativeInfinity;
            public float Drive, Steer;
            public bool Sprint;
            public void Clear()
            {
                Sequence = 0;
                LastAt = float.NegativeInfinity;
                Drive = Steer = 0;
                Sprint = false;
            }
        }

        sealed class Hull
        {
            public RiverRaftSnapshot State;
            public Transform View;
            public Transform[] Oars, Seats;
            public BoxCollider Collider;
            public (Renderer renderer, int slot)[] FlashMaterials;
            public bool Flashing;
            public uint RenderedVoyage;
            public readonly Paddle Left = new(), Right = new();
            public float NextHit, RebuildAt;
            public bool BankContact;
            public readonly HashSet<int> Contacts = new();
        }

        void Awake()
        {
            game = GetComponent<GameStateManager>();
            respawns = GetComponent<RespawnManager>();
            InitializeHazardNetwork();
        }

        public void Bind(Scene scene)
        {
            if (channel != null && presentation != null && presentation.gameObject.scene == scene) return;
            channel = new RiverChannel(scene);
            if (waterMaterial != null) channel.WaterRenderer.sharedMaterial = waterMaterial;
            if (presentation != null) Destroy(presentation.gameObject);
            presentation = new GameObject("V3 River Gameplay Wrappers").transform;
            SceneManager.MoveGameObjectToScene(presentation.gameObject, scene);
            BuildDock(channel.LowerDock, false);
            BuildDock(channel.UpperDock, true);
            for (int i = 0; i < hulls.Length; i++) BuildHull(i);
            BuildHazards();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            Array.Clear(suppressedVoyage, 0, suppressedVoyage.Length);
            nextSend = 0;
            sequence = 0;
            shared.OnValueChanged += OnHullAudio;
            first.OnValueChanged += OnHullAudio;
            second.OnValueChanged += OnHullAudio;
            if (!IsServer) return;
            split.Value = false;
            splitAt.Value = 0;
            finishCount = 0;
            dockReconnect = returningStarted = recoveringShared = false;
            soloOwners[0] = soloOwners[1] = Empty;
            seenPlayers.Clear();
            if (channel != null) ResetServer();
            NetworkManager.OnClientDisconnectCallback += Disconnect;
        }

        public override void OnNetworkDespawn()
        {
            shared.OnValueChanged -= OnHullAudio;
            first.OnValueChanged -= OnHullAudio;
            second.OnValueChanged -= OnHullAudio;
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= Disconnect;
            if (Current == this) Current = null;
            if (presentation == null) return;
            foreach (var hull in hulls) hull.View.gameObject.SetActive(false);
            HideHazards();
            if (rain != null) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnHullAudio(RiverRaftSnapshot before, RiverRaftSnapshot after)
        {
            if (before.DamagedAt != after.DamagedAt && after.DamagedAt > 0 &&
                NetworkManager.ServerTime.Time - after.DamagedAt < .65)
                game.PlayLiveCue(AudioCueId.RaftHit, after.Position);
            if (before.Status != RiverRaftStatus.Destroyed && after.Status == RiverRaftStatus.Destroyed &&
                after.Recovery is not (RiverContact.Disconnect or RiverContact.PhaseChanged))
                game.PlayLiveCue(AudioCueId.RaftBreak, after.Position);
            if (before.Status == RiverRaftStatus.Destroyed && after.Status == RiverRaftStatus.Docked)
                game.PlayLiveCue(AudioCueId.RaftRestore, after.Position);
        }

        void ResetServer()
        {
            for (int i = 0; i < hulls.Length; i++)
            {
                hulls[i].State = default;
                ResetHull(i, RiverRaftStatus.Hidden);
            }
            ResetHazards();
        }

        NetworkVariable<RiverRaftSnapshot> Stream(int i) => i == 0 ? shared : i == 1 ? first : second;
        internal RiverRaftSnapshot Read(int i) => Stream(i).Value;
        float Radius(int i) => i == 0 ? sharedRadius : soloRadius;
        float Height(Vector3 point) => channel.Sample(point.z).center.y + .05f;

        internal Vector3 Berth(int i)
        {
            var dock = i == 0 ? channel.LowerDock.bounds : channel.UpperDock.bounds;
            float z = dock.center.z + (i == 1 ? -3.1f : i == 2 ? 3.1f : 0);
            Vector3 p = new(dock.min.x - Radius(i) - .9f, 0, z);
            p.y = Height(p);
            return p;
        }

        internal Pose Landing(bool upper, int side)
        {
            var dock = upper ? channel.UpperDock.bounds : channel.LowerDock.bounds;
            Vector3 point = new(dock.min.x + 2.2f, dock.max.y + .12f, dock.center.z + (side == 0 ? -3.1f : 3.1f));
            return new Pose(point, Quaternion.LookRotation(Vector3.left));
        }

        internal void Board(PlayerInteraction player, bool upper)
        {
            if (!player.IsOwner || !CanBoardAt(upper)) return;
            BoardRpc(!upper, Read(upper ? 1 : 0).Voyage, upper ? Read(2).Voyage : 0);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void BoardRpc(bool lower, uint firstVoyage, uint secondVoyage, RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!CanInteract || !game.CanPlay || !TryPlayer(id, out var player) ||
                player.GetComponent<GameplayCharacter>()?.BlocksServerInteraction == true || TrySeat(id, out _, out _, out _) ||
                CooperativePole.Current?.IsAttached(id) == true || BridgeGameplay.Current?.Holds(id) == true ||
                player.GetComponent<MountainReturnLaunch>()?.ControlsMovement == true) return;

            if (lower)
            {
                var s = Read(0);
                if (!Outbound || s.Status != RiverRaftStatus.Docked || s.Voyage != firstVoyage) return;
                int side = PreferredSeat(player.transform.position.z, s);
                if (side < 0 || !NearLanding(player.transform.position, false, side)) return;
                Seat(0, side, id);
                return;
            }

            if (!RaceActive) return;
            int slot = Array.IndexOf(soloOwners, id);
            if (slot == -1)
            {
                int near = player.transform.position.z < channel.UpperDock.bounds.center.z ? 0 : 1;
                slot = soloOwners[near] == Empty && Read(near + 1).Status == RiverRaftStatus.Docked ? near : 1 - near;
                if (soloOwners[slot] != Empty) return;
            }
            var candidate = Read(slot + 1);
            if (candidate.Status != RiverRaftStatus.Docked || candidate.Voyage != (slot == 0 ? firstVoyage : secondVoyage) ||
                !NearLanding(player.transform.position, true, slot)) return;
            soloOwners[slot] = id;
            Seat(slot + 1, 0, id);
        }

        int PreferredSeat(float z, RiverRaftSnapshot state)
        {
            int near = z < channel.LowerDock.bounds.center.z ? 0 : 1;
            if (near == 0 && state.Left == Empty || near == 1 && state.Right == Empty) return near;
            if (near == 0 && state.Right == Empty || near == 1 && state.Left == Empty) return 1 - near;
            return -1;
        }

        bool NearLanding(Vector3 point, bool upper, int side)
        {
            Vector3 landing = Landing(upper, side).position;
            return Mathf.Abs(point.y - landing.y) < 1.7f && Vector3.Distance(point, landing) <= boardingRange;
        }

        void Seat(int index, int side, ulong id)
        {
            var hull = hulls[index];
            if (side == 0) { hull.State.Left = id; hull.Left.Clear(); }
            else { hull.State.Right = id; hull.Right.Clear(); }
            if (index != 0 || hull.State.Left != Empty && hull.State.Right != Empty)
                hull.State.Status = RiverRaftStatus.Sailing;
            Publish(index);
        }

        bool TryPlayer(ulong id, out PlayerMovement player)
        {
            player = null;
            if (!NetworkManager.ConnectedClients.TryGetValue(id, out var client) ||
                client.PlayerObject == null || !client.PlayerObject.IsSpawned || client.PlayerObject.OwnerClientId != id) return false;
            return client.PlayerObject.TryGetComponent(out player);
        }

        internal bool TrySeat(ulong id, out int index, out int side, out RiverRaftSnapshot state)
        {
            for (index = 0; index < hulls.Length; index++)
            {
                state = Read(index);
                if (state.Voyage == 0 || !state.Has(id)) continue;
                side = state.Left == id ? 0 : 1;
                return true;
            }
            side = -1;
            state = default;
            return false;
        }

        public bool IsSeated(ulong id) => IsSpawned && TrySeat(id, out _, out _, out _);

        public bool ApplyOwnerSeat(PlayerMovement player)
        {
            if (!TrySeatPose(player.OwnerClientId, out var pose, out _, out _)) return false;
            player.transform.SetPositionAndRotation(pose.position, pose.rotation);
            return true;
        }

        internal bool TrySeatPose(ulong id, out Pose pose, out float stroke, out int side)
        {
            pose = default;
            stroke = 0;
            side = 0;
            if (!IsSpawned || channel == null || !TrySeat(id, out int index, out side, out var state) ||
                suppressedVoyage[index] == state.Voyage) return false;
            var hull = hulls[index];
            pose = new Pose(hull.Seats[side].position, hull.View.rotation);
            if (state.Status == RiverRaftStatus.Sailing) stroke = side == 0 ? state.LeftDrive : state.RightDrive;
            return true;
        }

        public bool HandleOwnerInteraction(PlayerInteraction player, GameplayInput input)
        {
            if (!IsSpawned || !TrySeat(player.OwnerClientId, out int index, out _, out var state)) return false;
            if (input?.LaunchCancel?.WasPressedThisFrame() == true || input?.TryConsumeInteract() == true)
                CancelRpc(index, state.Voyage);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void CancelRpc(int index, uint voyage, RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!TrySeat(id, out int actual, out int side, out var state) || index != actual ||
                voyage != state.Voyage || !TryPlayer(id, out var player) ||
                state.Status is not (RiverRaftStatus.Docked or RiverRaftStatus.Sailing) ||
                Vector3.Distance(state.Position, Berth(index)) > 4) return;

            if (index == 0 && state.Status == RiverRaftStatus.Sailing)
                Recover(0, RiverContact.RiderLost);
            else if (index == 0)
            {
                ResetHull(0, RiverRaftStatus.Docked);
                LeaveSeat(player, false, side, index, voyage);
            }
            else
            {
                ResetHull(index, RiverRaftStatus.Docked);
                LeaveSeat(player, true, index - 1, index, voyage);
            }
        }

        void LeaveSeat(PlayerMovement player, bool upper, int side, int index, uint voyage)
        {
            var landing = Landing(upper, side);
            respawns.SetCheckpoint(player, landing.position, landing.rotation, upper ? "Upper dock" : "Lower dock");
            LeaveSeatRpc(index, voyage, landing.position, landing.rotation, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void LeaveSeatRpc(int index, uint voyage, Vector3 position, Quaternion rotation, RpcParams rpc = default)
        {
            suppressedVoyage[index] = voyage;
            NetworkManager.LocalClient?.PlayerObject?.GetComponent<PlayerMovement>()?.PlaceAtOwner(position, rotation);
        }

        public void SuspendOwnerSeat(ulong id)
        {
            if (TrySeat(id, out int index, out _, out var s)) suppressedVoyage[index] = s.Voyage;
        }

        void Update()
        {
            if (!IsSpawned || channel == null) return;
            RenderRiver();
            if (!IsClient || Time.unscaledTime < nextSend ||
                !TrySeat(NetworkManager.LocalClientId, out int index, out _, out var state) ||
                state.Status != RiverRaftStatus.Sailing || suppressedVoyage[index] == state.Voyage) return;
            var input = NetworkManager.LocalClient.PlayerObject?.GetComponent<GameplayInput>();
            Vector2 move = input?.AcceptsInput == true ? input.Move.ReadValue<Vector2>() : Vector2.zero;
            nextSend = Time.unscaledTime + .05f;
            PaddleRpc(index, state.Voyage, ++sequence, (sbyte)Mathf.RoundToInt(move.y * 100),
                (sbyte)Mathf.RoundToInt(move.x * 100), input?.AcceptsInput == true && input.Sprint.IsPressed());
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Unreliable)]
        void PaddleRpc(int index, uint voyage, uint serial, sbyte drive, sbyte steer, bool sprint, RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (drive is < -100 or > 100 || steer is < -100 or > 100 ||
                !TrySeat(id, out int actual, out int side, out var state) || actual != index || state.Voyage != voyage ||
                state.Status != RiverRaftStatus.Sailing || (index == 0 ? !Outbound : !RaceActive) ||
                !TryPlayer(id, out _)) return;

            var paddle = side == 0 ? hulls[index].Left : hulls[index].Right;
            if (serial <= paddle.Sequence || Time.time - paddle.LastAt < .015f) return;
            paddle.Sequence = serial;
            paddle.LastAt = Time.time;
            paddle.Drive = drive / 100f;
            paddle.Steer = index == 0 ? 0 : steer / 100f;
            paddle.Sprint = sprint;
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || channel == null) return;
            if (hulls[0].State.Voyage == 0) ResetServer();
            RegisterNewPlayers();
            if (Outbound && Read(0).Status == RiverRaftStatus.Hidden && !split.Value) ResetHull(0, RiverRaftStatus.Docked);
            if (RaceActive && !returningStarted)
            {
                returningStarted = true;
                if (!split.Value)
                {
                    ReleaseSharedForPhase();
                    split.Value = true;
                }
                for (int i = 1; i < 3; i++)
                    if (Read(i).Status == RiverRaftStatus.Hidden) ResetHull(i, RiverRaftStatus.Docked);
            }
            if (!Outbound && !RaceActive && game.Phase == FoundationPhase.Finished)
            {
                for (int i = 0; i < hulls.Length; i++)
                    if (Read(i).Status is RiverRaftStatus.Docked or RiverRaftStatus.Sailing) Recover(i, RiverContact.PhaseChanged);
            }

            TickHazards();
            for (int i = 0; i < hulls.Length; i++)
            {
                var hull = hulls[i];
                if (hull.State.Status == RiverRaftStatus.Destroyed && Time.time >= hull.RebuildAt)
                    Rebuild(i);
                else if (hull.State.Status == RiverRaftStatus.Sailing && (i == 0 ? Outbound : RaceActive))
                {
                    if (!TryPlayer(hull.State.Left, out _) || i == 0 && !TryPlayer(hull.State.Right, out _))
                        Recover(i, RiverContact.Disconnect);
                    else
                        Sail(i);
                }
            }
        }

        void ResetHull(int i, RiverRaftStatus status)
        {
            var hull = hulls[i];
            Vector3 position = Berth(i);
            var tangent = channel.Sample(position.z).upstream * (i == 0 ? 1 : -1);
            hull.State = new RiverRaftSnapshot
            {
                Position = position, Heading = Quaternion.LookRotation(tangent).eulerAngles.y,
                Left = Empty, Right = Empty, Voyage = hull.State.Voyage + 1,
                LeftStamina = 1, RightStamina = 1, Status = status, Recovery = hull.State.Recovery
            };
            hull.Left.Clear();
            hull.Right.Clear();
            hull.Contacts.Clear();
            hull.BankContact = false;
            hull.NextHit = hull.RebuildAt = 0;
            Publish(i);
        }

        void Publish(int i) => Stream(i).Value = hulls[i].State;

        float Thrust(ulong rider, Paddle paddle, ref float stamina, float dt)
        {
            bool fresh = Time.time - paddle.LastAt < InputTimeout;
            float drive = fresh ? paddle.Drive : 0;
            if (!TryPlayer(rider, out var player) || !player.TryGetComponent<GameplayCharacter>(out var character))
                return drive;
            bool strong = paddle.Sprint && Mathf.Abs(drive) > .01f && character.TryStrongRowServer(dt);
            stamina = character.StaminaFraction;
            return strong ? drive * strongMultiplier : drive;
        }

        internal bool InWater(Vector3 point)
        {
            if (channel == null || point.z < channel.WaterStart || point.z > channel.WaterEnd) return false;
            var sample = channel.Sample(point.z);
            return Mathf.Abs(point.x - sample.center.x) < sample.halfWidth && point.y <= sample.center.y + .15f;
        }

        internal bool InGemDeathRegion(Vector3 point) => channel != null &&
            point.z >= channel.LowerDock.bounds.min.z && point.z <= channel.UpperDock.bounds.max.z;

        void Sail(int i)
        {
            var hull = hulls[i];
            ref var s = ref hull.State;
            float dt = Time.fixedDeltaTime;
            float left = Thrust(s.Left, hull.Left, ref s.LeftStamina, dt);
            float right = i == 0 ? Thrust(s.Right, hull.Right, ref s.RightStamina, dt) : 0;
            s.LeftDrive = left;
            s.RightDrive = right;
            s.Drive = i == 0 ? (left + right) * .5f : left;
            s.Steer = i == 0 ? Mathf.Clamp((left - right) * .5f, -1, 1) :
                Time.time - hull.Left.LastAt < InputTimeout ? hull.Left.Steer : 0;
            var sample = channel.Sample(s.Position.z);
            Vector3 direction = i == 0 ? sample.upstream : -sample.upstream;
            float current = i == 0 ? outboundFlow : returnFlow;
            float course = Quaternion.LookRotation(direction).eulerAngles.y;
            float turn = Mathf.DeltaAngle(s.Heading, course) * courseCorrection + turnSpeed * s.Steer;
            s.TurnRate = Mathf.Lerp(s.TurnRate, turn, 1 - Mathf.Exp(-response * dt));
            s.Heading = Mathf.Repeat(s.Heading + s.TurnRate * dt, 360);
            Vector3 heading = Quaternion.Euler(0, s.Heading, 0) * Vector3.forward;
            Vector3 flow = -sample.upstream * current;
            s.Flow = current;
            Vector3 desired = heading * (s.Drive * weakSpeed) + flow + WhirlpoolForce(s.Position);
            s.Velocity = Vector3.Lerp(s.Velocity, desired, 1 - Mathf.Exp(-response * dt));
            Vector3 from = s.Position;
            Vector3 next = from + s.Velocity * dt;
            float radius = Radius(i);
            next.z = Mathf.Clamp(next.z, channel.Start + radius, channel.End - radius);
            if (next.z == channel.Start + radius || next.z == channel.End - radius) s.Velocity.z = 0;
            var future = channel.Sample(next.z);
            float padding = radius / Mathf.Max(.45f, future.upstream.z) + .25f;
            float half = Mathf.Max(0, future.halfWidth - padding);
            float edge = Mathf.Clamp(next.x, future.center.x - half, future.center.x + half);
            bool bank = edge != next.x;
            if (bank) { next.x = edge; s.Velocity.x *= -.25f; }
            next.y = Height(next);
            int terrain = HitTerrain(from, ref next, radius);
            if (terrain != 0) s.Velocity *= -.25f;
            var obstacle = ContactHazards(i, from, ref next, radius, out bool blocked);
            if (blocked) s.Velocity *= -.3f;
            s.Position = next;
            if (next.z <= channel.Start + radius + .01f && s.Velocity.z < 0 ||
                next.z >= channel.End - radius - .01f && s.Velocity.z > 0) s.Velocity.z = 0;
            bool shore = terrain == 1 || bank;
            if (!shore && Mathf.Abs(next.x - future.center.x) < half - 1) hull.BankContact = false;
            if (obstacle != RiverContact.None) Damage(i, obstacle);
            else if (shore && !hull.BankContact && Time.time >= hull.NextHit)
            {
                hull.BankContact = true;
                Damage(i, RiverContact.Bank);
            }
            else if (terrain == 2 && s.Contact != RiverContact.Dock) Damage(i, RiverContact.Dock);

            if (s.Status == RiverRaftStatus.Sailing)
            {
                bool upper = i == 0;
                var dock = upper ? channel.UpperDock.bounds : channel.LowerDock.bounds;
                if (next.z >= dock.min.z && next.z <= dock.max.z &&
                    next.x >= dock.min.x - radius - 3 && next.x <= dock.min.x)
                    Arrive(i);
            }
            Publish(i);
        }

        int HitTerrain(Vector3 from, ref Vector3 to, float radius)
        {
            Vector3 displacement = to - from;
            float length = displacement.magnitude;
            if (length < .001f) return 0;
            int count = Physics.SphereCastNonAlloc(from + Vector3.up * .45f, radius, displacement / length,
                terrainHits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = length;
            int type = 0;
            for (int i = 0; i < count; i++)
            {
                var hit = terrainHits[i];
                if (hit.distance > nearest || !hit.collider.transform.IsChildOf(channel.Section)) continue;
                nearest = hit.distance;
                type = hit.collider == channel.LowerDock || hit.collider == channel.UpperDock ? 2 : 1;
            }
            if (type != 0)
            {
                to = from + displacement / length * Mathf.Max(0, nearest - .05f);
                to.y = Height(to);
            }
            return type;
        }

        void Damage(int i, RiverContact contact)
        {
            var hull = hulls[i];
            if (Time.time < hull.NextHit) return;
            hull.NextHit = Time.time + hitInterval;
            hull.State.Contact = contact;
            ContactFeedback(i);
            if (contact == RiverContact.Dock) return;
            hull.State.DamagedAt = NetworkManager.ServerTime.Time;
            if (++hull.State.Hits >= (i == 0 ? SharedLimit : SoloLimit))
                Recover(i, contact);
        }

        void Arrive(int i)
        {
            var hull = hulls[i];
            var s = hull.State;
            hull.State.Status = RiverRaftStatus.Arrived;
            hull.State.Velocity = Vector3.zero;
            hull.State.Left = hull.State.Right = Empty;
            if (i == 0)
            {
                splitAt.Value = NetworkManager.ServerTime.Time;
                split.Value = true;
                ResetHull(1, RiverRaftStatus.Docked);
                ResetHull(2, RiverRaftStatus.Docked);
                soloOwners[0] = s.Left;
                soloOwners[1] = s.Right;
            }
            else
            {
                hull.State.FinishPlace = ++finishCount;
                hull.State.Finisher = s.Left;
            }
            Publish(i);
            if (TryPlayer(s.Left, out var left)) LeaveSeat(left, i == 0, i == 0 ? 0 : i - 1, i, s.Voyage);
            if (i == 0 && TryPlayer(s.Right, out var right)) LeaveSeat(right, true, 1, i, s.Voyage);
        }

        void ReleaseSharedForPhase()
        {
            if (Read(0).Status is RiverRaftStatus.Sailing or RiverRaftStatus.Docked)
            {
                var s = Read(0);
                ResetHull(0, RiverRaftStatus.Hidden);
                if (TryPlayer(s.Left, out var left)) LeaveSeat(left, false, 0, 0, s.Voyage);
                if (TryPlayer(s.Right, out var right)) LeaveSeat(right, false, 1, 0, s.Voyage);
            }
        }

        public bool TryRecoverRiderServer(PlayerMovement player)
        {
            if (!IsSpawned || !IsServer || !TrySeat(player.OwnerClientId, out int index, out _, out _)) return false;
            Recover(index, RiverContact.RiderLost);
            return true;
        }

        void Recover(int i, RiverContact reason)
        {
            var hull = hulls[i];
            if (hull.State.Status == RiverRaftStatus.Destroyed) return;
            if (reason is not (RiverContact.PhaseChanged or RiverContact.Disconnect))
            {
                GemOwnership.Current?.BeforeRaftDeathServer(hull.State.Left);
                GemOwnership.Current?.BeforeRaftDeathServer(hull.State.Right);
            }
            hull.State.Status = RiverRaftStatus.Destroyed;
            hull.State.Recovery = reason;
            hull.State.Velocity = Vector3.zero;
            hull.State.Drive = hull.State.LeftDrive = hull.State.RightDrive = hull.State.Steer = 0;
            hull.RebuildAt = Time.time + rebuildDelay;
            Publish(i);
        }

        void Rebuild(int i)
        {
            var state = Read(i);
            var next = (i == 0 ? Outbound : RaceActive) ? RiverRaftStatus.Docked : RiverRaftStatus.Hidden;
            ResetHull(i, next);
            if (game.Phase == FoundationPhase.Finished) return;

            if (i == 0)
            {
                recoveringShared = true;
                respawns.SetSharedCheckpointServer(channel.LowerCheckpoint, Quaternion.identity, "Lower dock", true);
                respawns.RespawnGroupServer(true);
                recoveringShared = false;
            }
            else if (TryPlayer(state.Left, out var player))
            {
                var pose = Landing(true, i - 1);
                respawns.SetCheckpoint(player, pose.position, pose.rotation, "Upper dock");
                respawns.RespawnPlayer(player, true);
            }
        }

        void Disconnect(ulong id)
        {
            seenPlayers.Remove(id);
            if (TrySeat(id, out int index, out _, out _))
            {
                dockReconnect = true;
                if (index == 0)
                {
                    if (Read(index).Status == RiverRaftStatus.Docked) ResetHull(index, RiverRaftStatus.Docked);
                    else Recover(index, RiverContact.Disconnect);
                }
                else
                {
                    soloOwners[index - 1] = Empty;
                    Recover(index, RiverContact.Disconnect);
                }
            }
            int slot = Array.IndexOf(soloOwners, id);
            if (slot >= 0)
            {
                if (Read(slot + 1).Status != RiverRaftStatus.Arrived) dockReconnect = true;
                soloOwners[slot] = Empty;
            }
        }

        void RegisterNewPlayers()
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (!TryPlayer(client.ClientId, out var player) || !seenPlayers.Add(client.ClientId) ||
                    !dockReconnect || recoveringShared) continue;
                int slot = Read(1).Status == RiverRaftStatus.Arrived || soloOwners[0] != Empty ? 1 : 0;
                var pose = Landing(RaceActive || split.Value, slot);
                respawns.SetCheckpoint(player, pose.position, pose.rotation, RaceActive || split.Value ? "Upper dock" : "Lower dock");
                respawns.RespawnPlayer(player, true);
                dockReconnect = false;
            }
        }
    }
}
