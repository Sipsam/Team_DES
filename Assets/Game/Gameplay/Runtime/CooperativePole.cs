using System.Linq;
using TwinEdge.Audio;
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
        GameStateManager game;
        Vector3 homeA, homeB;
        Vector2 intent;
        Vector2 pivotIntent;
        double pivotInputAt;
        bool pivotSprint;
        double inputAt, attachedAt;
        float stable;
        bool orbitAirborne;
        string candidate;
        string pivotSummit;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool debug;
#endif
        float nextSend;
        public bool CanInteract => IsSpawned && snapshot.Value.phase != PolePhase.Failed;
        public bool IsAttached(ulong id) => IsSpawned && snapshot.Value.Has(id);
        internal bool IsPairedPivot(ulong id) => snapshot.Value.Paired && snapshot.Value.Pivot == id;
        bool Outbound => game != null && game.CanPlay && game.CurrentState == GameState.Cooperation &&
            (!game.foundationMode || game.Phase == FoundationPhase.Outbound);
        public void ConfigureHome(Vector3 start)
        {
            homeA = V3GameplayBootstrap.SafeGround(start - Vector3.right * length * .5f);
            homeB = V3GameplayBootstrap.SafeGround(start + Vector3.right * length * .5f);
            // Equal-height placement preserves a rigid length even on the slightly uneven summit.
            float rise = homeB.y - homeA.y;
            homeB = homeA + new Vector3(Mathf.Sqrt(Mathf.Max(0,length*length-rise*rise)), rise, 0);
            var initial = Home(default);
            if (IsSpawned && IsServer) snapshot.Value = initial;
            Render(IsSpawned && !IsServer ? snapshot.Value : initial);
        }
        public override void OnNetworkSpawn()
        {
            Current = this;
            game = FindFirstObjectByType<GameStateManager>();
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
        public void UseRpc(uint revision)
        {
            if (IsSpawned && game != null) RequestPoleUseRpc(revision, game.ActionRevision);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RequestPoleUseRpc(uint revision, uint action, RpcParams rpc = default)
        {
            var s = snapshot.Value;
            ulong sender = rpc.Receive.SenderClientId;
            var player = Player(sender);
            if (!Outbound || !game.AcceptsAction(action) || revision != s.revision || player == null || !player.IsSpawned || player.GetComponent<PoleMotor>() == null) return;
            if (player.GetComponent<GameplayCharacter>()?.BlocksServerInteraction == true) return;
            if (s.Has(sender))
            {
                if (s.phase != PolePhase.Landed || sender != s.Orbit || !SafeLanding(s.OrbitPoint, out var landed) ||
                    landed == pivotSummit || !ClearPath(s.OrbitPoint, s.OrbitPoint, player)) return;
                Player(s.Pivot)?.GetComponent<GameplayCharacter>()?.SetPoleSprintIntentServer(false);
                s.pivotRight = !s.pivotRight;
                orbitAirborne = false;
                pivotSummit = landed; s.surface = landed; s.phase = PolePhase.Ready;
                SetAngles(ref s); s.revision++; snapshot.Value = s; intent = Vector2.zero; pivotIntent = Vector2.zero;
                pivotSprint = false; stable = 0; candidate = null;
                EmitCue(AudioCueId.PoleSwap, (s.a + s.b) * .5f + Vector3.up);
                return;
            }
            if (s.Paired || s.phase == PolePhase.Failed) return;
            bool takeRight = s.left != ulong.MaxValue || (s.right == ulong.MaxValue && Vector3.Distance(player.transform.position, s.b) < Vector3.Distance(player.transform.position, s.a));
            if ((takeRight ? s.right : s.left) != ulong.MaxValue) return;
            var grip = takeRight ? s.b : s.a;
            if (Vector3.Distance(player.transform.position, grip) > grabRange || !SafeLanding(player.transform.position, out _) || !SafeLanding(grip, out _) || !ClearPath(player.transform.position, grip, player)) return;
            if (takeRight) s.right = sender; else s.left = sender;
            if (s.phase == PolePhase.Idle) { s.pivotRight = takeRight; attachedAt = NetworkManager.ServerTime.Time; }
            s.phase = s.Paired ? PolePhase.Ready : PolePhase.OneAttached;
            if (s.Paired) { SafeLanding(s.PivotPoint, out pivotSummit); SetAngles(ref s); }
            player.GetComponent<GameplayCharacter>()?.SetCarryingServer(true);
            s.revision++; snapshot.Value = s;
            EmitCue(AudioCueId.PoleGrab, grip + Vector3.up);
        }

        static void EmitCue(AudioCueId cue, Vector3 at) =>
            V3GameplayBootstrap.Current?.state.EmitWorldCue(cue, at);
        static void SetAngles(ref PoleSnapshot s)
        {
            Vector3 d = (s.OrbitPoint - s.PivotPoint).normalized;
            s.yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            s.pitch = Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg;
        }
        public void SteerRpc(uint revision, Vector2 value, bool sprint = false)
        {
            if (IsSpawned && game != null) RequestPoleSteerRpc(revision, value, sprint, game.ActionRevision);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RequestPoleSteerRpc(uint revision, Vector2 value, bool sprint, uint action, RpcParams rpc = default)
        {
            var s = snapshot.Value;
            if (!Outbound || !game.AcceptsAction(action) || revision != s.revision || !s.Paired || !s.Has(rpc.Receive.SenderClientId) || s.phase == PolePhase.Failed || !float.IsFinite(value.x) || !float.IsFinite(value.y)) return;
            var character = Player(rpc.Receive.SenderClientId)?.GetComponent<GameplayCharacter>();
            if (character == null || character.BlocksServerInteraction) return;
            if (s.Pivot == rpc.Receive.SenderClientId)
            {
                pivotIntent = Vector2.ClampMagnitude(value, 1);
                pivotSprint = sprint && pivotIntent.sqrMagnitude > .001f;
                pivotInputAt = NetworkManager.ServerTime.Time;
                character.SetPoleSprintIntentServer(pivotSprint);
                return;
            }
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
            foreach (ulong id in new[] { s.left, s.right })
            {
                var character = Player(id)?.GetComponent<GameplayCharacter>();
                character?.SetCarryingServer(false);
                character?.SetPoleSprintIntentServer(false);
            }
            s.failure = reason;
            // Release atomically before calling respawn so the hook cannot recurse.
            snapshot.Value = Home(s); orbitAirborne = false; intent = Vector2.zero; pivotIntent = Vector2.zero;
            pivotSprint = false; stable = 0; candidate = null;
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
                if (!Presentation.PresentationRoot.BlocksGameplay && keyboard?.escapeKey.wasPressedThisFrame == true)
                    CancelRpc(s.revision);
                else if (input.TryConsumeInteract()) UseRpc(s.revision);
                if (Time.unscaledTime >= nextSend && s.Paired)
                {
                    nextSend = Time.unscaledTime + .05f;
                    var movement = local.GetComponent<PlayerMovement>();
                    Vector2 move = input.AcceptsInput ? input.Move.ReadValue<Vector2>() : Vector2.zero;
                    SteerRpc(s.revision, CameraSteering(s, local.OwnerClientId, movement, move),
                        input.AcceptsInput && input.Sprint.IsPressed());
                }
            }
        }
        static Vector2 CameraSteering(PoleSnapshot s, ulong owner, PlayerMovement movement, Vector2 move)
        {
            if (s.Pivot == owner)
            {
                Vector3 direction = movement.GetCameraRelativeDirection(move);
                return new Vector2(direction.x, direction.z);
            }

            movement.GetMovementAxes(out _, out var right);
            // Choose the yaw direction whose tangent points toward camera-right.
            // At a radial side view either turn is equally lateral, so default to positive yaw for D.
            Vector3 tangent = Quaternion.Euler(0f, s.yaw, 0f) * Vector3.right;
            if (Vector3.Dot(right, tangent) < -.001f) move.x = -move.x;
            return move; // W/S are the dedicated elevation controls.
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            var s = snapshot.Value;
            if (s.phase == PolePhase.Idle) return;
            if (!Outbound) { Fail("Phase changed", false); return; }
            var pivot = Player(s.Pivot);
            var orbit = Player(s.Orbit);
            if (pivot == null || (s.Paired && orbit == null)) { Fail("Missing participant", false); return; }
            if (!SafeLanding(s.PivotPoint, out _)) { Fail("Pivot lost terrain support", true, s.Pivot); return; }
            if (!s.Paired) { if (NetworkManager.ServerTime.Time - attachedAt > maxIdleSeconds) Fail("Waiting timeout", false); return; }
            bool stoppedOnLanding = false;
            double now = NetworkManager.ServerTime.Time;
            var pivotCharacter = pivot.GetComponent<GameplayCharacter>();
            var orbitCharacter = orbit.GetComponent<GameplayCharacter>();
            Vector2 control = now - inputAt < .25 && !orbitCharacter.BlocksServerInteraction ? intent : Vector2.zero;
            if (now - pivotInputAt < .25 && pivotIntent.sqrMagnitude > .001f && !pivotCharacter.BlocksServerInteraction)
            {
                var movement = pivot.GetComponent<PlayerMovement>();
                float speed = movement.GroundMoveSpeed(pivotSprint && pivotCharacter.SprintAllowed) * movement.GroundSpeedScale;
                Vector3 delta = new Vector3(pivotIntent.x, 0, pivotIntent.y) * (speed * Time.fixedDeltaTime);
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .08f));
                for (int i = 0; i < steps; i++)
                    if (!TryCarry(ref s, delta / steps, pivotSummit, pivot, orbit)) break;
            }
            if (!SafeLanding(s.OrbitPoint, out _)) orbitAirborne = true;
            Vector3 before = s.OrbitPoint;
            float yaw = s.yaw + control.x * angularSpeed * Time.fixedDeltaTime;
            float pitch = Mathf.Clamp(s.pitch + control.y * pitchSpeed * Time.fixedDeltaTime, -maxPitch, maxPitch);
            Vector3 after = PoleGeometry.Endpoint(s.PivotPoint, length, yaw, pitch);
            if ((after - before).sqrMagnitude > .000001f)
            {
                // Subdivide the arc so each capsule sweep is <= 15 cm, including low frame rates.
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(before, after) / .15f));
                Vector3 last = before;
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 point = PoleGeometry.Endpoint(s.PivotPoint, length, Mathf.Lerp(s.yaw, yaw, (float)i / steps), Mathf.Lerp(s.pitch, pitch, (float)i / steps));
                    if (!ClearPath(last, point, orbit)) {
                        // A real walkable contact after flight stops at the previous authoritative sphere pose.
                        // No endpoint is projected/snapped onto terrain, and no rejected pose is applied.
                        if (orbitAirborne && LandingContact(before, point, orbit)) { stoppedOnLanding = true; break; }
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
            if (stable >= landingDwell)
            {
                if (s.phase != PolePhase.Landed) EmitCue(AudioCueId.PoleContact, s.OrbitPoint);
                s.phase = PolePhase.Landed; s.surface = id;
            }
            else if (s.phase == PolePhase.Landed) { s.phase = PolePhase.Ready; s.surface = default; }
            snapshot.Value = s;
        }
        // Translate the rigid span from a supported pivot. The orbit foot may be airborne,
        // but both capsules must clear the step, including any small change in pivot ground height.
        public bool TryCarry(ref PoleSnapshot pose, Vector3 delta, string summit, NetworkObject pivot, NetworkObject orbit)
        {
            Vector3 from = pose.PivotPoint;
            Vector3 to = from + delta;
            if (!SafeLanding(to, out var id) || id != summit || !ClearPath(from, to, pivot))
            {
                if (!TryGround(from, .35f, landingTolerance, out var oldGround) ||
                    !TryGround(to, .35f, landingTolerance, out var newGround)) return false;
                to.y += newGround.point.y - oldGround.point.y;
                if (!SafeLanding(to, out id) || id != summit || !ClearPath(from, to, pivot)) return false;
            }
            Vector3 step = to - from;
            if (!ClearPath(pose.OrbitPoint, pose.OrbitPoint + step, orbit)) return false;
            pose.a += step;
            pose.b += step;
            return true;
        }

        bool Environment(Collider c) => c != supportCollider && !c.transform.IsChildOf(transform) && c.GetComponentInParent<PlayerMovement>() == null;
        static readonly Vector3[] Footprint = { Vector3.zero, Vector3.right * .45f, Vector3.left * .45f, Vector3.forward * .45f, Vector3.back * .45f };

        bool TryGround(Vector3 feet, float rise, float drop, out RaycastHit closest)
        {
            closest = default;
            float distance = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * rise, Vector3.down, rise + drop, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= distance || !Environment(hit.collider)) continue;
                closest = hit;
                distance = hit.distance;
            }
            return distance < float.PositiveInfinity;
        }

        public bool SafeLanding(Vector3 feet, out string id)
        {
            id = PoleGeometry.Summit(feet, mountain, .9f);
            if (id == null) return false;
            foreach (Vector3 offset in Footprint)
            {
                if (!TryGround(feet + offset, .35f, landingTolerance, out var hit) ||
                    hit.normal.y < .85f || Mathf.Abs(hit.point.y - feet.y) > landingTolerance) return false;
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

        public bool ClearPath(Vector3 from, Vector3 to, NetworkObject player)
        {
            var cc = player.GetComponent<CharacterController>();
            Vector3 center = cc.center;
            float radius = cc.radius - .03f;
            float half = cc.height * .5f - radius;
            Vector3 low = from + center - Vector3.up * half + Vector3.up * .04f;
            Vector3 high = from + center + Vector3.up * half;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude > 0 && Physics.CapsuleCastAll(low, high, radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).Any(h => Environment(h.collider))) return false;
            return !Physics.OverlapCapsule(to + center - Vector3.up * half + Vector3.up * .04f, to + center + Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore).Any(Environment);
        }
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DrawDiagnostics()
        {
            if (!debug || !IsSpawned) return;
            var s = snapshot.Value;
            GUILayout.Label($"Pole {s.phase} / rev {s.revision} · {s.yaw:F1}°, {s.pitch:F1}°\nLanding {s.surface} | Candidate {candidate} | Failure {s.failure}");
        }
#endif

        internal string PresentationHint(NetworkObject local)
        {
            if (!IsSpawned || V3GameplayBootstrap.Current?.state.Phase != FoundationPhase.Outbound) return null;
            var s = snapshot.Value;
            var feet = local.transform.position;
            var span = s.b - s.a;
            float along = Mathf.Clamp01(Vector3.Dot(feet - s.a, span) / Mathf.Max(.01f, span.sqrMagnitude));
            if (!s.Has(local.OwnerClientId))
                return (feet - (s.a + span * along)).sqrMagnitude <= 64 ? "막대 끝을 잡으세요." : null;
            return s.Pivot == local.OwnerClientId
                ? "중심 · WASD 이동 · Shift 달리기\n상대가 착지하면 E로 역할 교대 · Esc 놓기"
                : "회전 · A/D 회전 · W/S 높이 조절\n봉우리에 착지한 뒤 E로 역할 교대 · Esc 놓기";
        }
    }
}
