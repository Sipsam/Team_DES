using System;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    [RequireComponent(typeof(GameStateManager), typeof(RespawnManager))]
    public sealed partial class CastleGameplay : NetworkBehaviour
    {
        internal enum Action : byte { Pad, ReadHint, Signal, ClearSignal, Collect, Keypad, SewerPad, ClearSewer }

        internal struct PlayerProgress : INetworkSerializable, IEquatable<PlayerProgress>
        {
            public int Revision, Steps, SewerLength, Feedback;
            public bool Injured;
            public double RetryAt;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Revision);
                serializer.SerializeValue(ref Steps);
                serializer.SerializeValue(ref SewerLength);
                serializer.SerializeValue(ref Feedback);
                serializer.SerializeValue(ref Injured);
                serializer.SerializeValue(ref RetryAt);
            }

            public bool Equals(PlayerProgress other) => Revision == other.Revision && Steps == other.Steps &&
                SewerLength == other.SewerLength && Feedback == other.Feedback && Injured == other.Injured && RetryAt == other.RetryAt;
        }

        const int Stages = 4, Both = 3;
        internal static readonly string[] Glyphs = { "Rock", "Spike", "Water" };

        [SerializeField] bool randomizeNumbers = true;
        [SerializeField, Range(0, 9999)] int configuredA = 2718, configuredB = 6043;
        [SerializeField, Range(.1f, 1f)] float injuredSpeed = .55f;
        [SerializeField, Min(.1f)] float retryDelay = 3f, respawnGrace = 2f;
        [SerializeField] bool clearInjuryOnRespawn = true, clearInjuryOnExit = true;

        readonly NetworkVariable<int> stage = new(), signal = new(), collected = new(), accepted = new(), crossed = new();
        readonly NetworkVariable<bool> sewerOpen = new();
        readonly NetworkVariable<double> returnStart = new();
        readonly NetworkVariable<ulong> clientA = new(ulong.MaxValue), clientB = new(ulong.MaxValue);
        readonly byte[] answer = new byte[Stages * 3];
        readonly int[] numbers = new int[2], sewerCodes = new int[2];
        readonly double[] graceUntil = new double[2];
        readonly bool[] resumeAtCastle = new bool[2];
        readonly Crossing[] crossings = new Crossing[2];
        readonly ulong[] occupants = { ulong.MaxValue, ulong.MaxValue };
        NetworkList<PlayerProgress> progress;

        struct Crossing
        {
            public bool Armed;
            public Vector3 Last;
        }

        GameStateManager game;
        RespawnManager respawns;
        NetworkSpawnCoordinator spawns;
        FortressConfigV3 config;
        readonly Pose[] dockPoses = new Pose[2];
        Scene boundScene;
        int hintStage = -1, hintCode, localNumber = -1, pendingKeypad = -1;
        internal bool IsKeypadOpen => pendingKeypad >= 0;
        string numberEntry = "";
        bool ready;

        public static CastleGameplay Current { get; private set; }
        internal int Stage => stage.Value;
        internal bool DoorOpen => accepted.Value == Both;
        internal bool PartitionsOpen => crossed.Value == Both;
        internal bool Returning => returnStart.Value > 0;
        internal bool SewerOpen => sewerOpen.Value;
        internal bool IsReady => ready;
        internal int Crossed => crossed.Value;
        internal int Signal => signal.Value;
        internal Vector3 World(float x, float y, float z) => config.origin + new Vector3(x, y, z);
        internal double Now => NetworkManager.ServerTime.Time;
        internal int Slot(ulong client) => client == clientA.Value ? 0 : client == clientB.Value ? 1 : -1;
        internal PlayerProgress Progress(int slot) => IsSpawned && slot >= 0 && slot < progress.Count ? progress[slot] : default;
        public float SpeedScale(PlayerMovement player) =>
            Returning && Contains(player.transform.position) && Progress(Slot(player.OwnerClientId)).Injured ? injuredSpeed : 1;
        public bool SprintBlocked(PlayerMovement player) =>
            Returning && Contains(player.transform.position) && Progress(Slot(player.OwnerClientId)).Injured;

        void Awake()
        {
            Current = this;
            game = GetComponent<GameStateManager>();
            respawns = GetComponent<RespawnManager>();
            progress = new NetworkList<PlayerProgress>();
            activations = new NetworkList<double>();
        }

        public override void OnDestroy()
        {
            if (Current == this) Current = null;
            if (labelMaterial != null) Destroy(labelMaterial);
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            hintStage = localNumber = pendingKeypad = -1;
            if (!IsServer) return;
            spawns = NetworkManager.GetComponent<NetworkSpawnCoordinator>();
            stage.Value = signal.Value = collected.Value = accepted.Value = crossed.Value = 0;
            returnStart.Value = 0;
            sewerOpen.Value = false;
            clientA.Value = clientB.Value = ulong.MaxValue;
            progress.Clear();
            for (int slot = 0; slot < 2; slot++)
            {
                progress.Add(default);
                occupants[slot] = ulong.MaxValue;
                crossings[slot] = default;
                sewerCodes[slot] = 0;
                graceUntil[slot] = 0;
                resumeAtCastle[slot] = false;
                numbers[slot] = randomizeNumbers ? UnityEngine.Random.Range(0, 10000) : slot == 0 ? configuredA : configuredB;
            }
            if (randomizeNumbers)
                while (numbers[1] == numbers[0]) numbers[1] = UnityEngine.Random.Range(0, 10000);
            for (int offset = 0; offset < answer.Length; offset += 3)
            {
                for (byte i = 0; i < 3; i++) answer[offset + i] = i;
                for (int i = 2; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (answer[offset + i], answer[offset + j]) = (answer[offset + j], answer[offset + i]);
                }
            }
            ResetTraps();
        }

        internal bool Contains(Vector3 position)
        {
            if (!ready) return false;
            Vector3 p = position - config.origin;
            return p.y >= -3 && p.z >= config.keep.yMin - 5 && p.z <= config.keep.yMax + 5 &&
                p.x >= config.keep.xMin - 1 && p.x <= config.keep.xMax + 1 && p.y <= 13;
        }

        internal bool TryResume(int slot, out Pose pose, out string label)
        {
            pose = default;
            label = null;
            if (!ready || !IsServer || slot < 0 || slot >= resumeAtCastle.Length || !resumeAtCastle[slot]) return false;
            pose = Returning ? ReturnPose(slot) : DockPose(slot);
            label = Returning ? "Castle basement door" : "Upper dock / castle rooms";
            return true;
        }

        Pose DockPose(int slot) => dockPoses[slot];

        Pose ReturnPose(int slot) => new(World(slot == 0 ? 2.3f : -2.3f, .15f, config.undergroundDoor.z + 4),
            Quaternion.Euler(0, 180, 0));

        internal bool AssignDeathCheckpoint(PlayerMovement player)
        {
            int slot = Slot(player.OwnerClientId);
            if (!IsSpawned || !IsServer || slot < 0 || !Contains(player.transform.position)) return false;
            Pose pose = Returning ? ReturnPose(slot) : DockPose(slot);
            respawns.SetCheckpoint(player, pose.position, pose.rotation,
                Returning ? "Castle basement door" : "Upper dock / castle rooms");
            var p = progress[slot];
            if (clearInjuryOnRespawn) p.Injured = false;
            p.SewerLength = 0;
            p.Revision++;
            progress[slot] = p;
            sewerCodes[slot] = 0;
            crossings[slot] = default;
            if (!PartitionsOpen) crossed.Value &= ~(1 << slot);
            graceUntil[slot] = Now + respawnGrace;
            return true;
        }

        void Update()
        {
            if (!ready) return;
            if (pendingKeypad >= 0 && (!game.CanPlay || Returning ||
                UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true))
                pendingKeypad = -1;
            if (IsSpawned && IsServer) TickServer();
            ApplyViews();
        }

        void TickServer()
        {
            for (int slot = 0; slot < 2; slot++)
            {
                var binding = slot == 0 ? clientA : clientB;
                if (binding.Value == ulong.MaxValue) continue;
                if (NetworkManager.ConnectedClients.ContainsKey(binding.Value)) continue;
                binding.Value = ulong.MaxValue;
                var p = progress[slot];
                p.Revision++;
                p.SewerLength = 0;
                progress[slot] = p;
                sewerCodes[slot] = 0;
                occupants[slot] = ulong.MaxValue;
                crossings[slot] = default;
                if (!PartitionsOpen) crossed.Value &= ~(1 << slot);
            }

            foreach (var peer in NetworkManager.ConnectedClientsList)
            {
                if (peer.PlayerObject == null || !spawns.TryGetSlot(peer.ClientId, out int slot) || slot >= 2) continue;
                var binding = slot == 0 ? clientA : clientB;
                binding.Value = peer.ClientId;
                Vector3 position = peer.PlayerObject.transform.position;
                bool inside = Contains(position);
                var p = progress[slot];
                if (game.CanPlay) resumeAtCastle[slot] = inside;
                if (clearInjuryOnExit && Returning && !inside && position.z < config.origin.z + config.keep.yMin - 5 && p.Injured)
                {
                    p.Injured = false;
                    progress[slot] = p;
                }

                if (occupants[slot] == peer.ClientId && (!game.CanPlay || !Near(peer.PlayerObject, keypads[slot])))
                    occupants[slot] = ulong.MaxValue;
                if (!game.CanPlay) continue;
                if (game.Phase == FoundationPhase.Outbound && DoorOpen && !Returning)
                    TrackCrossing(slot, position - config.origin);
            }

            if (!Returning && PartitionsOpen && game.Phase == FoundationPhase.Return)
                returnStart.Value = Now;
            if (Returning && game.CanPlay) TickTrapDamage();
        }

        void TrackCrossing(int slot, Vector3 position)
        {
            if (PartitionsOpen) return;
            ref var track = ref crossings[slot];
            Vector3 delta = position - config.undergroundDoor;
            bool inOpening = Mathf.Abs(delta.x) < 3.5f && delta.y >= -.3f && delta.y < 4;
            if (inOpening && delta.z >= -3 && delta.z < -.6f)
            {
                track.Armed = true;
                crossed.Value &= ~(1 << slot);
            }
            if (!inOpening || Mathf.Abs(delta.z) > 4 || Vector3.Distance(track.Last, position) > 3f)
                track.Armed = inOpening && delta.z >= -3 && delta.z < -.6f;
            else if (track.Armed && delta.z >= .65f)
                crossed.Value |= 1 << slot;
            track.Last = position;
        }

        internal void InterruptContestServer(ulong client)
        {
            int slot = Slot(client);
            if (!IsSpawned || !IsServer || slot < 0 || occupants[slot] != client) return;
            occupants[slot] = ulong.MaxValue;
            CloseKeypadRpc(RpcTarget.Single(client, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void CloseKeypadRpc(RpcParams rpc = default) => pendingKeypad = -1;

        bool TryPlayer(RpcParams rpc, out NetworkObject player, out int slot)
        {
            slot = Slot(rpc.Receive.SenderClientId);
            player = null;
            if (!ready || !IsServer || !game.CanPlay || slot < 0 ||
                !NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out var peer)) return false;
            player = peer.PlayerObject;
            return player != null && player.GetComponent<GameplayCharacter>()?.BlocksServerInteraction != true && Contains(player.transform.position);
        }

        static bool Near(NetworkObject player, CastleStation station)
        {
            Vector3 feet = player.transform.position;
            Vector3 target = station.transform.position;
            return Mathf.Abs(feet.y - target.y) <= 2 && (feet + Vector3.up - target).sqrMagnitude <= 3.25f * 3.25f;
        }

        internal void Interact(CastleStation station, PlayerInteraction player)
        {
            int slot = Slot(player.OwnerClientId);
            if (!player.IsOwner || slot < 0 || !CanUse(station, slot)) return;
            UseRpc(station.Id, Stage, Progress(slot).Revision);
        }

        internal bool CanUse(CastleStation station, int slot)
        {
            if (!IsSpawned || !game.CanPlay || slot < 0 || station.Slot >= 0 && station.Slot != slot) return false;
            if (station.Kind is Action.SewerPad or Action.ClearSewer)
                return Returning && !SewerOpen && Now >= Progress(slot).RetryAt;
            if (Returning) return false;
            if (game.Phase != FoundationPhase.Outbound) return false;
            if (station.Kind == Action.Collect) return Stage == Stages;
            if (station.Kind == Action.Keypad) return Stage == Stages && !DoorOpen && (accepted.Value & (1 << slot)) == 0;
            return station.Stage == Stage && (station.Kind != Action.Pad || Progress(slot).Steps < 3);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        internal void UseRpc(int id, int expectedStage, int revision, RpcParams rpc = default)
        {
            if (id < 0 || id >= stations.Count || Stage != expectedStage || !TryPlayer(rpc, out var player, out int slot)) return;
            CastleStation station = stations[id];
            var p = progress[slot];
            if (p.Revision != revision || !CanUse(station, slot) || !Near(player, station)) return;
            switch (station.Kind)
            {
                case Action.Pad:
                    bool correct = station.Value == answer[Stage * 3 + p.Steps];
                    p.Steps = correct ? p.Steps + 1 : 0;
                    p.Feedback = correct ? 1 : -1;
                    break;
                case Action.ReadHint:
                    ShowHintRpc(Stage, CodeForStage(Stage), RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
                    return;
                case Action.Signal:
                    int count = SignalLength(signal.Value);
                    if (count == 3) count = 0;
                    int existing = count == 0 ? 0 : signal.Value;
                    signal.Value = existing | ((station.Value + 1) << (count * 2));
                    break;
                case Action.ClearSignal:
                    signal.Value = 0;
                    break;
                case Action.Collect:
                    collected.Value |= 1 << slot;
                    ShowNumberRpc(numbers[slot], RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
                    return;
                case Action.Keypad:
                    occupants[slot] = player.OwnerClientId;
                    OpenKeypadRpc(slot, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
                    return;
                case Action.SewerPad:
                    sewerCodes[slot] = sewerCodes[slot] * 3 + station.Value;
                    p.SewerLength++;
                    p.Feedback = 1;
                    if (p.SewerLength == answer.Length)
                    {
                        bool matches = sewerCodes[slot] == SewerAnswer();
                        p.SewerLength = sewerCodes[slot] = 0;
                        p.Feedback = matches ? 1 : -1;
                        if (matches) sewerOpen.Value = true;
                        else p.RetryAt = Now + retryDelay;
                        p.Revision++;
                        progress[slot] = p;
                        if (!matches) Damage(player, slot);
                        return;
                    }
                    break;
                case Action.ClearSewer:
                    p.SewerLength = sewerCodes[slot] = 0;
                    p.Feedback = 0;
                    break;
            }
            p.Revision++;
            progress[slot] = p;
            if (Stage < Stages && progress[0].Steps == 3 && progress[1].Steps == 3)
            {
                signal.Value = 0;
                for (int i = 0; i < 2; i++)
                {
                    var next = progress[i];
                    next.Steps = next.Feedback = 0;
                    next.Revision++;
                    progress[i] = next;
                }
                stage.Value++;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        internal void SubmitNumberRpc(int value, int revision, RpcParams rpc = default)
        {
            if (value < 0 || value > 9999 || !TryPlayer(rpc, out var player, out int slot) ||
                occupants[slot] != player.OwnerClientId || !CanUse(keypads[slot], slot) || !Near(player, keypads[slot])) return;
            var p = progress[slot];
            if (p.Revision != revision) return;
            bool valid = (collected.Value & (1 << slot)) != 0 && value == numbers[slot];
            p.Feedback = valid ? 1 : -1;
            p.Revision++;
            progress[slot] = p;
            if (!valid) return;
            accepted.Value |= 1 << slot;
            occupants[slot] = ulong.MaxValue;
        }

        void Damage(NetworkObject player, int slot)
        {
            var p = progress[slot];
            if (p.Injured)
            {
                var movement = player.GetComponent<PlayerMovement>();
                AssignDeathCheckpoint(movement);
                respawns.RespawnPlayer(movement, useAssignedCheckpoint: true);
            }
            else
            {
                p.Injured = true;
                progress[slot] = p;
            }
        }

        int CodeForStage(int index) => (answer[index * 3] + 1) | ((answer[index * 3 + 1] + 1) << 2) |
            ((answer[index * 3 + 2] + 1) << 4);
        internal static int SignalLength(int code) => code >= 16 ? 3 : code >= 4 ? 2 : code > 0 ? 1 : 0;
        int SewerAnswer()
        {
            int code = 0;
            foreach (byte glyph in answer) code = code * 3 + glyph;
            return code;
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void ShowHintRpc(int index, int code, RpcParams rpc = default)
        {
            hintStage = index;
            hintCode = code;
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void ShowNumberRpc(int number, RpcParams rpc = default) => localNumber = number;

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void OpenKeypadRpc(int slot, RpcParams rpc = default)
        {
            pendingKeypad = slot;
            numberEntry = "";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
