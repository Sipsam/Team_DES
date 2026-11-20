using System;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class BridgeGameplay : IInteractable
    {
        internal struct LoadState : INetworkSerializable, IEquatable<LoadState>
        {
            public const ulong Empty = ulong.MaxValue;
            public uint revision;
            public ulong left, right;
            public Vector3 center;
            public float yaw;
            public bool supplied;
            public byte approvals;
            public bool Paired => left != Empty && right != Empty;
            public bool Has(ulong id) => supplied && (left == id || right == id);
            public Vector3 Grip(bool isRight, float halfWidth) =>
                center + Quaternion.Euler(0, yaw, 0) * Vector3.right * (isRight ? halfWidth : -halfWidth);
            public bool Equals(LoadState other) =>
                revision == other.revision && left == other.left && right == other.right &&
                center.Equals(other.center) && yaw.Equals(other.yaw) && supplied == other.supplied && approvals == other.approvals;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref revision);
                serializer.SerializeValue(ref left);
                serializer.SerializeValue(ref right);
                serializer.SerializeValue(ref center);
                serializer.SerializeValue(ref yaw);
                serializer.SerializeValue(ref supplied);
                serializer.SerializeValue(ref approvals);
            }
        }

        [Header("Bridge construction tuning")]
        [SerializeField, Min(.5f)] float gripHalfWidth = 1.25f;
        [SerializeField, Min(.25f)] float gripRange = 1.25f;
        [SerializeField, Min(1)] float supplySetback = 7f;
        [SerializeField, Min(.5f)] float socketSetback = 1.4f;
        [SerializeField, Min(.1f)] float socketRadius = .75f;
        [SerializeField, Range(1, 45)] float socketAngle = 18f;
        [SerializeField, Min(.1f)] float carrySpeed = 2.5f;
        [SerializeField, Min(1)] float turnSpeed = 85f;
        [SerializeField, Min(1)] float looseLoadTimeout = 45f;
        [SerializeField] GameObject crateModel;
        [SerializeField] GameObject plankModel;
        [SerializeField] GameObject frameModel;
        [SerializeField, Min(1)] float nominalFrameLength = 12f;

        readonly NetworkVariable<LoadState> load = new();
        readonly Vector3[] directions = new Vector3[2];
        readonly float[] efforts = new float[2];
        readonly double[] inputAt = new double[2];
        readonly RaycastHit[] groundHits = new RaycastHit[12];
        readonly Collider[] carryHits = new Collider[16];
        GameStateManager game;
        GameObject bridgeVisuals, crateVisual, cargoVisual, socketVisual;
        BoxCollider crateInteraction, leftInteraction, rightInteraction;
        double lastActivity;
        bool constructionReached;
        float nextSend;

        bool Outbound => IsSpawned && planks != null && levelScene.isLoaded && game != null &&
            game.CanPlay && game.FoundationPhaseLabel == nameof(FoundationPhase.Outbound);
        public bool CanInteract => Outbound && CompletedDeliveries < TotalDeliveries;
        public bool Holds(ulong id) => IsSpawned && load.Value.Has(id);
        internal LoadState Load => load.Value;
        internal Vector3 SupplyPoint => StartPoint + Forward * (InstalledCount * Pitch - supplySetback);
        internal Vector3 SocketPoint => StartPoint + Forward * (InstalledCount * Pitch - socketSetback);
        internal float GripHalfWidth => gripHalfWidth;
        internal Vector3 CratePoint => SupplyPoint - BridgeRight * 2.65f;
        Vector3 BridgeRight => Vector3.Cross(Vector3.up, Forward);
        float BridgeYaw => Mathf.Atan2(Forward.x, Forward.z) * Mathf.Rad2Deg;

        public string Prompt
        {
            get
            {
                var current = load.Value;
                if (CompletedDeliveries == TotalDeliveries) return "Bridge complete. Cross carefully.";
                if (!current.supplied) return "E: take timber from the crate";
                if (!current.Paired) return "E: grab an end · Esc: cancel";
                if (!AtSocket(current)) return "Both: move timber to the socket · Esc: cancel";
                return current.approvals == 0 ? "Both: E to install" : "Waiting for the other player to press E";
            }
        }

        partial void OnDeckBound()
        {
            if (!Application.isPlaying) return;
            game = GetComponent<GameStateManager>();
            BindReturn(levelScene);
            if (bridgeVisuals != null) Destroy(bridgeVisuals);
            bridgeVisuals = new GameObject("Bridge Gameplay Visuals");
            bridgeVisuals.transform.SetParent(transform, false);
            if (frameModel != null)
            {
                int modules = Mathf.CeilToInt(Length / nominalFrameLength);
                float moduleLength = Length / modules;
                // The source asset also contains review props. Build one visual-only frame from its timber and rail children.
                var template = new GameObject("Bridge frame");
                template.transform.SetParent(bridgeVisuals.transform, false);
                foreach (Transform child in frameModel.transform)
                {
                    if (child.name is not ("\uC544\uCE58\uBAA9\uC7AC" or "\uB09C\uAC04\uAE30\uB465" or "\uBC27\uC904\uB09C\uAC04")) continue;
                    var part = CloneVisual(child.gameObject, template.transform);
                    var position = child.localPosition;
                    // Preserve the individual beams/posts. Move their two sides out to the saved deck edges.
                    position.x += Mathf.Sign(position.x) * Mathf.Max(0, layout.bridgeWidth * .5f - 1.1f);
                    part.transform.localPosition = position;
                }
                template.SetActive(false);
                for (int i = 0; i < modules; i++)
                {
                    var frame = Instantiate(template, bridgeVisuals.transform);
                    frame.transform.SetPositionAndRotation(StartPoint + Forward * ((i + .5f) * moduleLength),
                        Quaternion.LookRotation(Forward));
                    frame.transform.localScale = new Vector3(1, 1, moduleLength / nominalFrameLength);
                    frame.SetActive(true);
                }
                Destroy(template);
            }

            crateVisual = CloneVisual(crateModel, bridgeVisuals.transform);
            cargoVisual = new GameObject("Cooperative timber bundle");
            cargoVisual.transform.SetParent(bridgeVisuals.transform, false);
            if (plankModel != null)
            {
                for (int i = -1; i <= 1; i++)
                {
                    var timber = CloneVisual(plankModel, cargoVisual.transform);
                    timber.transform.localPosition = new Vector3(0, Mathf.Abs(i) * -.09f, i * .19f);
                }
            }

            socketVisual = new GameObject("Installation socket");
            socketVisual.transform.SetParent(bridgeVisuals.transform, false);
            if (plankModel != null)
            {
                var socket = CloneVisual(plankModel, socketVisual.transform);
                socket.transform.localScale = new Vector3(1, .35f, .6f);
            }

            crateInteraction = MakeInteraction("Timber crate interaction", new Vector3(1.3f, 1, 1.3f));
            leftInteraction = MakeInteraction("Left timber grip", Vector3.one * .45f);
            rightInteraction = MakeInteraction("Right timber grip", Vector3.one * .45f);
            RenderLoad();
        }

        static GameObject CloneVisual(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                var empty = new GameObject("Missing bridge visual");
                empty.transform.SetParent(parent, false);
                return empty;
            }
            var visual = Instantiate(prefab, parent);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return visual;
        }

        BoxCollider MakeInteraction(string label, Vector3 size)
        {
            var trigger = new GameObject(label);
            trigger.transform.SetParent(bridgeVisuals.transform, false);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return box;
        }

        partial void StartBridgeSession()
        {
            if (IsServer)
            {
                hazard = null;
                constructionReached = false;
                ResetLoad();
                NetworkManager.OnClientDisconnectCallback += OnBridgeDisconnect;
            }
            load.OnValueChanged += LoadChanged;
            RenderLoad();
        }

        partial void EndBridgeSession()
        {
            load.OnValueChanged -= LoadChanged;
            if (IsServer) NetworkManager.OnClientDisconnectCallback -= OnBridgeDisconnect;
            if (bridgeVisuals != null)
            {
                crateVisual.SetActive(false);
                cargoVisual.SetActive(false);
                socketVisual.SetActive(false);
                crateInteraction.enabled = leftInteraction.enabled = rightInteraction.enabled = false;
            }
        }

        void LoadChanged(LoadState previous, LoadState next) => RenderLoad();
        partial void OnDeliveryChanged() => RenderLoad();

        void RenderLoad()
        {
            if (bridgeVisuals == null || !levelScene.isLoaded) return;
            var current = load.Value;
            bool active = CanInteract;
            var yaw = Quaternion.Euler(0, BridgeYaw, 0);
            var crate = CratePoint;
            if (Ground(crate, out var height)) crate.y = height;
            crateVisual.transform.SetPositionAndRotation(crate, yaw);
            crateVisual.SetActive(active);
            crateInteraction.transform.position = crate + Vector3.up * .55f;
            crateInteraction.enabled = active && !current.supplied;
            cargoVisual.transform.SetPositionAndRotation(current.center + Vector3.up * .95f, Quaternion.Euler(0, current.yaw, 0));
            cargoVisual.SetActive(active && current.supplied);
            leftInteraction.transform.position = current.Grip(false, gripHalfWidth) + Vector3.up * .95f;
            rightInteraction.transform.position = current.Grip(true, gripHalfWidth) + Vector3.up * .95f;
            leftInteraction.enabled = active && current.supplied && current.left == LoadState.Empty;
            rightInteraction.enabled = active && current.supplied && current.right == LoadState.Empty;
            socketVisual.transform.SetPositionAndRotation(SocketPoint + Vector3.up * .07f, yaw);
            socketVisual.SetActive(active);
        }

        void ResetLoad()
        {
            var old = load.Value;
            SetCarry(old.left, false);
            SetCarry(old.right, false);
            Array.Clear(directions, 0, directions.Length);
            Array.Clear(efforts, 0, efforts.Length);
            Array.Clear(inputAt, 0, inputAt.Length);
            load.Value = new LoadState { revision = old.revision + 1, left = LoadState.Empty, right = LoadState.Empty };
        }

        void SetCarry(ulong id, bool value) => Player(id)?.GetComponent<GameplayCharacter>()?.SetCarryingServer(value);

        NetworkObject Player(ulong id) =>
            id != LoadState.Empty && NetworkManager.ConnectedClients.TryGetValue(id, out var client) ? client.PlayerObject : null;

        public void Interact(PlayerInteraction player)
        {
            if (player != null && player.IsOwner && IsSpawned) UseRpc(load.Value.revision);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void UseRpc(uint revision, RpcParams rpc = default)
        {
            var current = load.Value;
            ulong id = rpc.Receive.SenderClientId;
            if (!CanInteract || current.revision != revision) return;
            var player = Player(id);
            if (player == null || player.GetComponent<GameplayCharacter>()?.BlocksServerInteraction == true || CooperativePole.Current?.IsAttached(id) == true ||
                player.GetComponent<MountainReturnLaunch>()?.ControlsMovement == true) return;
            var position = player.transform.position;
            if (!current.supplied)
            {
                if (Vector3.Distance(position, CratePoint) > 2.3f || !Ground(SupplyPoint, out float height)) return;
                current.center = SupplyPoint;
                current.center.y = height;
                current.yaw = BridgeYaw;
                current.supplied = true;
                constructionReached = true;
                lastActivity = NetworkManager.ServerTime.Time;
                load.Value = current;
                return;
            }

            if (current.Has(id))
            {
                if (!current.Paired || !AtSocket(current)) return;
                var left = Player(current.left);
                var right = Player(current.right);
                if (!NearGrip(left, current, false) || !NearGrip(right, current, true)) return;
                current.approvals |= id == current.left ? (byte)1 : (byte)2;
                if (current.approvals == 3)
                {
                    ResetLoad();
                    InstallNextServer();
                }
                else load.Value = current;
                return;
            }

            float leftDistance = Vector3.Distance(position, current.Grip(false, gripHalfWidth));
            float rightDistance = Vector3.Distance(position, current.Grip(true, gripHalfWidth));
            bool takeRight = rightDistance < leftDistance;
            if ((takeRight ? rightDistance : leftDistance) > gripRange ||
                (takeRight ? current.right : current.left) != LoadState.Empty ||
                !Ground(position, out float floor) || Mathf.Abs(floor - position.y) > .32f) return;
            if (takeRight) current.right = id;
            else current.left = id;
            SetCarry(id, true);
            lastActivity = NetworkManager.ServerTime.Time;
            load.Value = current;
        }

        bool NearGrip(NetworkObject player, LoadState current, bool right) =>
            player != null && Vector3.Distance(player.transform.position, current.Grip(right, gripHalfWidth)) < .85f;

        bool AtSocket(LoadState current) =>
            Vector3.ProjectOnPlane(current.center - SocketPoint, Vector3.up).sqrMagnitude <= socketRadius * socketRadius &&
            Mathf.Abs(Mathf.DeltaAngle(current.yaw, BridgeYaw)) <= socketAngle;

        public void Cancel(PlayerInteraction player)
        {
            if (player != null && player.IsOwner && Holds(player.OwnerClientId)) CancelRpc(load.Value.revision);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void CancelRpc(uint revision, RpcParams rpc = default)
        {
            if (load.Value.revision == revision && Holds(rpc.Receive.SenderClientId)) ResetLoad();
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Everyone)]
        void SteerRpc(uint revision, Vector3 direction, bool sprint, RpcParams rpc = default)
        {
            var current = load.Value;
            ulong id = rpc.Receive.SenderClientId;
            if (!CanInteract || !current.Paired || current.revision != revision || !current.Has(id) ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.z) ||
                Mathf.Abs(direction.x) > 1 || Mathf.Abs(direction.z) > 1) return;
            direction.y = 0;
            int side = id == current.left ? 0 : 1;
            directions[side] = Vector3.ClampMagnitude(direction, 1);
            efforts[side] = sprint && Player(id)?.GetComponent<GameplayCharacter>()?.SprintAllowed == true ? 1.25f : 1f;
            inputAt[side] = NetworkManager.ServerTime.Time;
        }

        public void MoveHolder(PlayerMovement movement, CharacterController controller, Vector3 direction, bool sprint)
        {
            var current = load.Value;
            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + .05f;
                SteerRpc(current.revision, direction, sprint);
            }
            bool right = movement.OwnerClientId == current.right;
            var grip = current.Grip(right, gripHalfWidth);
            controller.Move(Vector3.ClampMagnitude(grip - movement.transform.position, 7f * Time.deltaTime));
            movement.transform.rotation = Quaternion.LookRotation(current.Grip(!right, gripHalfWidth) - grip);
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || planks == null || !levelScene.isLoaded) return;
            TickCarry();
            TickReturn();
        }

        void Update()
        {
            if (IsSpawned && planks != null && levelScene.isLoaded)
            {
                bool active = CanInteract;
                if (crateVisual.activeSelf != active) RenderLoad();
            }
        }

        void TickCarry()
        {
            var current = load.Value;
            if (!current.supplied) return;
            double now = NetworkManager.ServerTime.Time;
            if (!CanInteract || now - lastActivity > looseLoadTimeout ||
                (current.left != LoadState.Empty && !HolderValid(current, false)) ||
                (current.right != LoadState.Empty && !HolderValid(current, true)))
            {
                ResetLoad();
                return;
            }
            if (!current.Paired || now - inputAt[0] > .3 || now - inputAt[1] > .3 ||
                directions[0].sqrMagnitude < .01f || directions[1].sqrMagnitude < .01f) return;

            var left = directions[0] * efforts[0];
            var right = directions[1] * efforts[1];
            var next = current;
            next.center += (left + right) * (.5f * carrySpeed * Time.fixedDeltaTime);
            var loadForward = Quaternion.Euler(0, current.yaw, 0) * Vector3.forward;
            next.yaw += Vector3.Dot(left - right, loadForward) * .5f * turnSpeed * Time.fixedDeltaTime;
            // Ground both hands and the center. Small gaps between original planks remain traversable.
            if (!Ground(next.center, out float height) || Mathf.Abs(height - current.center.y) > .25f) return;
            next.center.y = height;
            if (!ClearCarry(next)) return;
            if (!AtSocket(next)) next.approvals = 0;
            lastActivity = now;
            load.Value = next;
        }

        bool HolderValid(LoadState current, bool right)
        {
            var player = Player(right ? current.right : current.left);
            return player != null && player.IsSpawned &&
                Vector3.Distance(player.transform.position, current.Grip(right, gripHalfWidth)) < 2.5f;
        }

        bool ClearCarry(LoadState next)
        {
            var rotation = Quaternion.Euler(0, next.yaw, 0);
            int count = Physics.OverlapBoxNonAlloc(next.center + Vector3.up,
                new Vector3(gripHalfWidth, .24f, .38f), carryHits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (Blocked(count)) return false;
            for (int i = 0; i < 2; i++)
            {
                var grip = next.Grip(i == 1, gripHalfWidth);
                if (!Ground(grip, out float ground) || Mathf.Abs(ground - grip.y) > .25f) return false;
                count = Physics.OverlapCapsuleNonAlloc(grip + Vector3.up * .5f, grip + Vector3.up * 1.5f,
                    .39f, carryHits, ~0, QueryTriggerInteraction.Ignore);
                if (Blocked(count)) return false;
            }
            return true;
        }

        bool Blocked(int count)
        {
            if (count == carryHits.Length) return true;
            for (int i = 0; i < count; i++)
            {
                var player = carryHits[i].GetComponentInParent<PlayerMovement>();
                if (player == null || !load.Value.Has(player.OwnerClientId)) return true;
            }
            return false;
        }

        bool Ground(Vector3 position, out float height)
        {
            float nearest = float.PositiveInfinity;
            height = position.y;
            for (int sample = 0; sample < 3; sample++)
            {
                var origin = position + Forward * ((sample - 1) * .13f) + Vector3.up * .6f;
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 1.15f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = groundHits[i];
                    if (hit.distance >= nearest || hit.normal.y < .7f || hit.collider.GetComponentInParent<PlayerMovement>() != null) continue;
                    nearest = hit.distance;
                    height = hit.point.y;
                }
                if (float.IsFinite(nearest)) return true;
            }
            return false;
        }

        void OnBridgeDisconnect(ulong id)
        {
            if (Holds(id)) ResetLoad();
        }

        public void BeforeRespawnServer(PlayerMovement player)
        {
            if (IsServer && Holds(player.OwnerClientId)) ResetLoad();
        }
    }
}
