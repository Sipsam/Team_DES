using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    [DefaultExecutionOrder(9000)]
    public sealed class CooperativePole : NetworkBehaviour, IInteractable
    {
        public static CooperativePole Current { get; private set; }
        // Optional observational hook; subscribers cannot approve a rejected sweep.
        public event System.Action<Vector3, Vector3, Vector3> CollisionRejected;
        [Header("Unconfirmed tuning hypotheses (metres / degrees / seconds)")]
        [Min(3)] public float length = 32;
        public float angularSpeed = 24, pitchSpeed = 12, maxPitch = 40;
        public float grabRange = 2.5f, landingTolerance = .3f, landingDwell = .35f;
        public float maxIdleSeconds = 90;
        public Transform visual, leftGrip, rightGrip, rotationPivot, support;
        public Collider supportCollider;
        public MountainConfigV3 mountain;
        readonly NetworkVariable<PoleSnapshot> snapshot = new();
        public PoleSnapshot Snapshot => snapshot.Value;
        Vector3 homeA, homeB;
        Vector2 intent;
        Vector2 pivotIntent;
        double pivotInputAt;
        double inputAt, attachedAt;
        float stable;
        bool orbitAirborne;
        string candidate;
        string pivotSummit;
        bool debug;
        float nextSend;
        public bool CanInteract => IsSpawned && snapshot.Value.phase != PolePhase.Failed;
        public bool IsAttached(ulong id) => IsSpawned && snapshot.Value.Has(id);
        bool Outbound
        {
            get
            {
                var game = FindFirstObjectByType<GameStateManager>();
                return game != null && game.CanPlay && game.CurrentState == GameState.Cooperation && (!game.foundationMode || game.FoundationPhaseLabel == "Outbound");
            }
        }
        public void ConfigureHome(Vector3 start)
        {
            homeA = V3GameplayBootstrap.SafeGround(start - Vector3.right * length * .5f);
            homeB = V3GameplayBootstrap.SafeGround(start + Vector3.right * length * .5f);
            // Equal-height placement preserves a rigid length even on the slightly uneven summit.
            float rise = homeB.y - homeA.y;
            homeB = homeA + new Vector3(Mathf.Sqrt(Mathf.Max(0,length*length-rise*rise)), rise, 0);
            Render(new PoleSnapshot { a = homeA, b = homeB, left = ulong.MaxValue, right = ulong.MaxValue });
        }
        public override void OnNetworkSpawn()
        {
            Current = this;
            if (IsServer)
            {
                snapshot.Value = Home(default); orbitAirborne = false;
                NetworkManager.OnClientDisconnectCallback += Disconnected;
            }
        }
        public override void OnNetworkDespawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback -= Disconnected;
            if (Current == this) Current = null;
            support.gameObject.SetActive(false);
        }
        PoleSnapshot Home(PoleSnapshot previous) => new PoleSnapshot { revision = previous.revision + 1, a = homeA, b = homeB, left = ulong.MaxValue, right = ulong.MaxValue, phase = PolePhase.Idle, failure = previous.failure };
        NetworkObject Player(ulong id) => NetworkManager.ConnectedClients.TryGetValue(id, out var c) ? c.PlayerObject : null;
        public void Interact(PlayerInteraction player)
        {
            if (player != null && player.IsOwner) UseRpc(snapshot.Value.revision);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void UseRpc(uint revision, RpcParams rpc = default)
        {
            var s = snapshot.Value;
            ulong sender = rpc.Receive.SenderClientId;
            var player = Player(sender);
            if (!Outbound || revision != s.revision || player == null || !player.IsSpawned || player.GetComponent<PoleMotor>() == null) return;
            if (player.GetComponent<GameplayCharacter>()?.BlocksServerInteraction == true) return;
            if (s.Has(sender))
            {
                if (s.phase != PolePhase.Landed || sender != s.Orbit || !SafeLanding(s.OrbitPoint, out var landed) || landed == pivotSummit) return;
                s.pivotRight = !s.pivotRight;
                orbitAirborne = false;
                pivotSummit = landed; s.surface = landed; s.phase = PolePhase.Ready;
                SetAngles(ref s); s.revision++; snapshot.Value = s; intent = Vector2.zero; pivotIntent = Vector2.zero; stable = 0;
                return;
            }
            if (s.Paired || s.phase == PolePhase.Failed) return;
            bool takeRight = s.left != ulong.MaxValue || (s.right == ulong.MaxValue && Vector3.Distance(player.transform.position, s.b) < Vector3.Distance(player.transform.position, s.a));
            if ((takeRight ? s.right : s.left) != ulong.MaxValue) return;
            var grip = takeRight ? s.b : s.a;
            if (Vector3.Distance(player.transform.position, grip) > grabRange || !SafeLanding(player.transform.position, out _) || !SafeLanding(grip, out _) || !ClearPath(player.transform.position, grip, player) || !ClearRod(s.a,s.b)) return;
            if (takeRight) s.right = sender; else s.left = sender;
            if (s.phase == PolePhase.Idle) { s.pivotRight = takeRight; attachedAt = NetworkManager.ServerTime.Time; }
            s.phase = s.Paired ? PolePhase.Ready : PolePhase.OneAttached;
            if (s.Paired) { SafeLanding(s.PivotPoint, out pivotSummit); SetAngles(ref s); }
            player.GetComponent<GameplayCharacter>()?.SetCarryingServer(true);
            s.revision++; snapshot.Value = s;
        }
        static void SetAngles(ref PoleSnapshot s)
        {
            Vector3 d = (s.OrbitPoint - s.PivotPoint).normalized;
            s.yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            s.pitch = Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SteerRpc(uint revision, Vector2 value, RpcParams rpc = default)
        {
            var s = snapshot.Value;
            if (!Outbound || revision != s.revision || !s.Paired || !s.Has(rpc.Receive.SenderClientId) || s.phase == PolePhase.Failed || !float.IsFinite(value.x) || !float.IsFinite(value.y)) return;
            if (s.Pivot == rpc.Receive.SenderClientId) { pivotIntent = Vector2.ClampMagnitude(value, 1); pivotInputAt = NetworkManager.ServerTime.Time; return; }
            intent = Vector2.ClampMagnitude(value, 1); inputAt = NetworkManager.ServerTime.Time;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CancelRpc(uint revision, RpcParams rpc = default)
        {
            var s = snapshot.Value;
            if (s.revision != revision || !s.Has(rpc.Receive.SenderClientId)) return;
            Fail("Cancelled", false);
        }
        void Disconnected(ulong id) { if (snapshot.Value.Has(id)) Fail("Disconnected", false); }
        public void BeforeRespawnServer(ulong id)
        {
            if (IsServer && snapshot.Value.Has(id)) Fail("Participant respawn", false);
        }
        void Fail(string reason, bool respawnOrbit, ulong failedPlayer = ulong.MaxValue)
        {
            var s = snapshot.Value;
            var falling = Player(failedPlayer == ulong.MaxValue ? s.Orbit : failedPlayer);
            foreach (ulong id in new[] { s.left, s.right }) Player(id)?.GetComponent<GameplayCharacter>()?.SetCarryingServer(false);
            s.failure = reason;
            // Release atomically before calling respawn so the hook cannot recurse.
            snapshot.Value = Home(s); orbitAirborne = false; intent = Vector2.zero; pivotIntent = Vector2.zero; stable = 0; candidate = null;
            if (respawnOrbit && falling != null) FindFirstObjectByType<RespawnManager>()?.RespawnPlayer(falling.GetComponent<PlayerMovement>());
        }
        void Update()
        {
            if (!IsSpawned) return;
            var keyboard = Keyboard.current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame) debug = !debug;
#endif
            var local = NetworkManager.LocalClient?.PlayerObject;
            var s = snapshot.Value;
            if (local != null && s.Has(local.OwnerClientId))
            {
                var input = local.GetComponent<GameplayInput>();
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CancelRpc(s.revision);
                else if (input.TryConsumeInteract()) UseRpc(s.revision);
                if (Time.unscaledTime >= nextSend && s.Paired)
                {
                    nextSend = Time.unscaledTime + .05f;
                    SteerRpc(s.revision, input.AcceptsInput ? input.Move.ReadValue<Vector2>() : Vector2.zero);
                }
            }
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            var s = snapshot.Value;
            if (s.phase == PolePhase.Idle) return;
            if (!Outbound) { Fail("Phase changed", false); return; }
            if (Player(s.Pivot) == null || (s.Paired && Player(s.Orbit) == null)) { Fail("Missing participant", false); return; }
            if (!SafeLanding(s.PivotPoint, out _)) { Fail("Pivot lost terrain support", true, s.Pivot); return; }
            if (!s.Paired) { if (NetworkManager.ServerTime.Time - attachedAt > maxIdleSeconds) Fail("Waiting timeout", false); return; }
            if (!SafeLanding(s.OrbitPoint, out _)) orbitAirborne = true;
            bool stoppedOnLanding = false;
            Vector2 control = NetworkManager.ServerTime.Time - inputAt < .25 ? intent : Vector2.zero;
            // On the same summit, the pivot may carry the rigid pair toward a departure edge.
            // Both real feet must stay supported; once the orbit leaves terrain the pivot is locked.
            if (control.sqrMagnitude < .001f && NetworkManager.ServerTime.Time - pivotInputAt < .25 && pivotIntent.sqrMagnitude > .001f && SafeLanding(s.OrbitPoint, out var carriedSummit) && carriedSummit == pivotSummit)
            {
                Vector3 delta = new Vector3(pivotIntent.x, 0, pivotIntent.y) * (2 * Time.fixedDeltaTime);
                if (SafeLanding(s.a + delta, out _) && SafeLanding(s.b + delta, out _) && ClearPath(s.a, s.a+delta, Player(s.left)) && ClearPath(s.b, s.b+delta, Player(s.right)) && ClearRod(s.a+delta,s.b+delta))
                { s.a += delta; s.b += delta; snapshot.Value = s; return; }
            }
            Vector3 before = s.OrbitPoint;
            float yaw = s.yaw + control.x * angularSpeed * Time.fixedDeltaTime;
            float pitch = Mathf.Clamp(s.pitch + control.y * pitchSpeed * Time.fixedDeltaTime, -maxPitch, maxPitch);
            Vector3 after = PoleGeometry.Endpoint(s.PivotPoint, length, yaw, pitch);
            if ((after - before).sqrMagnitude > .000001f)
            {
                // Subdivide the arc so each capsule sweep is <= 15 cm, including low frame rates.
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(before, after) / .15f));
                var orbit = Player(s.Orbit);
                Vector3 last = before;
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 point = PoleGeometry.Endpoint(s.PivotPoint, length, Mathf.Lerp(s.yaw, yaw, (float)i / steps), Mathf.Lerp(s.pitch, pitch, (float)i / steps));
                    bool clearBody = ClearPath(last, point, orbit), clearRod = ClearRod(s.PivotPoint, point);
                    if (!clearBody || !clearRod) {
                        // A real walkable contact after flight stops at the previous authoritative sphere pose.
                        // No endpoint is projected/snapped onto terrain, and no rejected pose is applied.
                        if (!clearBody && clearRod && orbitAirborne && LandingContact(before, point, orbit)) { stoppedOnLanding = true; break; }
                        try { CollisionRejected?.Invoke(s.PivotPoint,last,point); } catch (System.Exception ex) { Debug.LogWarning("Pole collision trace failed: "+ex.Message); }
                        Fail("Terrain / capsule collision", true); return;
                    }
                    last = point;
                }
                if (!stoppedOnLanding) {
                s.yaw = yaw; s.pitch = pitch;
                if (s.pivotRight) s.a = after; else s.b = after;
                s.phase = PolePhase.Rotating;
                }
            }
            bool landed = SafeLanding(s.OrbitPoint, out string id) && id != pivotSummit && (control.sqrMagnitude < .001f || stoppedOnLanding);
            stable = landed && candidate == id ? stable + Time.fixedDeltaTime : 0;
            candidate = landed ? id : null;
            if (stable >= landingDwell) { s.phase = PolePhase.Landed; s.surface = id; }
            else if (s.phase == PolePhase.Landed) s.phase = PolePhase.Ready;
            snapshot.Value = s;
        }
        bool Environment(Collider c) => c != supportCollider && !c.transform.IsChildOf(transform) && c.GetComponentInParent<PlayerMovement>() == null;
        public bool SafeLanding(Vector3 feet, out string id)
        {
            id = PoleGeometry.Summit(feet, mountain, .9f);
            if (id == null) return false;
            foreach (Vector3 offset in new[] { Vector3.zero, Vector3.right * .45f, Vector3.left * .45f, Vector3.forward * .45f, Vector3.back * .45f })
            {
                var hits = Physics.RaycastAll(feet + offset + Vector3.up * .35f, Vector3.down, .35f + landingTolerance, ~0, QueryTriggerInteraction.Ignore).Where(h => Environment(h.collider)).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0 || hits[0].normal.y < .85f || Mathf.Abs(hits[0].point.y - feet.y) > landingTolerance) return false;
            }
            return true;
        }
        bool LandingContact(Vector3 from, Vector3 to, NetworkObject player)
        {
            if (to.y >= from.y || !SafeLanding(from, out _)) return false;
            var cc = player.GetComponent<CharacterController>();
            float radius = cc.radius - .03f, half = cc.height * .5f - radius;
            Vector3 delta = to - from;
            var hits = Physics.CapsuleCastAll(from + cc.center - Vector3.up * half + Vector3.up * .04f,
                from + cc.center + Vector3.up * half, radius, delta.normalized, delta.magnitude,
                ~0, QueryTriggerInteraction.Ignore).Where(h => Environment(h.collider)).ToArray();
            return hits.Length > 0 && hits.All(h => h.normal.y >= .85f);
        }

        bool ClearPath(Vector3 from, Vector3 to, NetworkObject player)
        {
            var cc = player.GetComponent<CharacterController>();
            Vector3 center = cc.center;
            float radius = cc.radius - .03f;
            float half = cc.height * .5f - radius;
            Vector3 low = from + center - Vector3.up * half + Vector3.up * .04f;
            Vector3 high = from + center + Vector3.up * half;
            Vector3 delta = to - from;
            if (Physics.CapsuleCastAll(low, high, radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).Any(h => Environment(h.collider))) return false;
            return !Physics.OverlapCapsule(to + center - Vector3.up * half + Vector3.up * .04f, to + center + Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore).Any(Environment);
        }
        bool ClearRod(Vector3 a, Vector3 b) => !Physics.CapsuleCastAll(a + Vector3.up * 1.1f, a + Vector3.up * 1.11f, .08f, (b-a).normalized, Vector3.Distance(a,b), ~0, QueryTriggerInteraction.Ignore).Any(h => Environment(h.collider));
        void LateUpdate() { if (IsSpawned) Render(snapshot.Value); }
        void Render(PoleSnapshot s)
        {
            leftGrip.position = s.a + Vector3.up * 1.1f; rightGrip.position = s.b + Vector3.up * 1.1f;
            rotationPivot.position = s.PivotPoint;
            visual.position = (s.a + s.b) * .5f + Vector3.up * 1.1f;
            visual.rotation = Quaternion.FromToRotation(Vector3.right, (s.b-s.a).normalized);
            support.gameObject.SetActive(s.Paired);
            support.position = s.OrbitPoint - Vector3.up * .1f;
            // The platform is private support for its orbital participant, never a public shortcut.
            if (s.Paired)
                foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                    Physics.IgnoreCollision(supportCollider, cc, cc.GetComponent<NetworkObject>()?.OwnerClientId != s.Orbit);
        }
        internal bool DrawPlaytestHud(NetworkObject local)
        {
            if (!Outbound || local == null) return false;
            var s = snapshot.Value;
            bool attached = s.Has(local.OwnerClientId);
            var feet = local.transform.position;
            var span = s.b - s.a;
            float along = Mathf.Clamp01(Vector3.Dot(feet - s.a, span) / Mathf.Max(.01f, span.sqrMagnitude));
            if (!attached && (feet - (s.a + span * along)).sqrMagnitude > 64) return false;
            int count = (s.left != ulong.MaxValue ? 1 : 0) + (s.right != ulong.MaxValue ? 1 : 0);
            GUILayout.Label($"Mountain / Cooperative pole {count}/2");
            if (!attached) GUILayout.Label("Approach one end and press E to grab");
            else
            {
                GUILayout.Label(s.Pivot == local.OwnerClientId
                    ? "Pivot: WASD to carry while both are on the same summit"
                    : "Orbit: A/D turn, W/S raise or lower the pole");
                GUILayout.Label("Land on the next summit, then E to swap roles | Esc to release");
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debug) GUILayout.Label($"Pole {s.phase} / rev {s.revision} | {s.yaw:F1}°, {s.pitch:F1}°\nLanding {s.surface} | Candidate {candidate} | Failure {s.failure}");
#endif
            return true;
        }
    }
}
