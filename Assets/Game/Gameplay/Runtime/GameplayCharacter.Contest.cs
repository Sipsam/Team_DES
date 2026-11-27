using TwinEdge.Audio;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter
    {
        const ulong NoPlayer = ulong.MaxValue;
        const float AttackWindup = .18f;
        const double CarryReplicaDelay = .35;
        const float CarryObstructionDistance = 1.55f;

        [Header("V3 stamina (points / seconds)")]
        [SerializeField, Min(1)] float maximumStamina = 100;
        [SerializeField, Min(0)] float jumpCost = 3, sprintPerSecond = 2, strongRowPerSecond = 23, holdPerSecond = 22;
        [SerializeField, Min(.1f)] float staminaRecovery = 16;
        [SerializeField, Min(0)] float emptyRecoveryDelay = 5;

        [Header("V3 contest (metres / seconds)")]
        [SerializeField, Min(.1f)] float hitRange = 3.445f, grabRange = 1.85f;
        [SerializeField, Range(0, 90)] float aimHalfAngle = 60;
        [SerializeField, Min(.01f)] float hitStun = .1f, hitCooldown = 1, grabCooldown = 3;
        [SerializeField, Min(0)] float hitSpeed = 7, hitLift = 1.6f;
        [SerializeField, Min(.1f)] float chargeSeconds = 2, throwMinSpeed = 4, throwMaxSpeed = 11, throwLift = 5;
        [SerializeField, Min(.1f)] float throwControlSeconds = .8f;
        [SerializeField, Min(1)] int resistancePresses = 7;

        readonly NetworkVariable<float> stamina = new();
        readonly NetworkVariable<ulong> holder = new(NoPlayer), held = new(NoPlayer);
        readonly NetworkVariable<int> resistance = new();
        readonly NetworkVariable<double> attackStartedAt = new(), hitReactAt = new();
        readonly NetworkVariable<double> throwPoseUntil = new();
        readonly NetworkVariable<double> attackAt = new(), grabAt = new(), holdBegan = new(), disabledUntil = new(), sprintUntil = new();
        enum ContestMotion : byte { Free, Held, Hit, Thrown }

        GameStateManager contestGame;
        PlayerMovement contestMovement;
        GameplayInput contestInput;
        CharacterController contestController;
        CharacterController carryCollisionPeer;
        readonly Queue<(double Time, Vector3 Feet)> recentLiftTargets = new();
        double recoverAt, spentAt, sprintIntentUntil, lastResistance, nextJumpAt, respawnUntil;
        float nextSprintSend, ownerJumpDeadline, ownerMotionClock, grabObstructedSince;
        bool ownerJumpAccepted, ownerMotionStarted, strikePending;
        int ownerLife;
        uint ownerJumpSequence, lastJumpSequence;
        ulong ownerHolder = NoPlayer;
        ContestMotion ownerMotion;
        double ownerMotionTime;
        Vector3 ownerImpulse;

        double Now => NetworkManager.ServerTime.Time;
        internal float StaminaFraction => stamina.Value / maximumStamina;
        internal int OwnerLife => ownerLife;
        internal bool HasStamina => stamina.Value > 0;
        internal bool SprintAllowed => IsSpawned && sprintUntil.Value > Now && stamina.Value > 0;
        internal Transform CarriedOpponent => IsSpawned && held.Value != NoPlayer ? Peer(held.Value)?.transform : null;
        internal bool BlocksContestImpulse => ownerMotion != ContestMotion.Free;
        internal bool BlocksOwnerInteraction => ownerMotion != ContestMotion.Free || held.Value != NoPlayer;
        internal bool BlocksServerInteraction => holder.Value != NoPlayer || held.Value != NoPlayer || Now < disabledUntil.Value || Now < respawnUntil;
        internal bool CanContestFinish => !BlocksServerInteraction && !SpecialMovement;
        bool SpecialMovement => RiverGameplay.Current?.IsSeated(OwnerClientId) == true ||
            GetComponent<MountainReturnLaunch>()?.ControlsMovement == true;
        bool Attached => CooperativePole.Current?.IsAttached(OwnerClientId) == true;
        bool HasGem => GemOwnership.Current is { State: GemState.Carried } gem && gem.Owner == OwnerClientId;

        void InitializeContest()
        {
            contestGame = FindFirstObjectByType<GameStateManager>();
            contestMovement = GetComponent<PlayerMovement>();
            contestController = GetComponent<CharacterController>();
            contestInput = GetComponent<GameplayInput>();
            ResetContestOwner(RespawnCount.Value);
            nextSprintSend = 0;
            ownerJumpSequence = lastJumpSequence = 0;
            if (!IsServer) return;
            stamina.Value = maximumStamina;
            holder.Value = held.Value = NoPlayer;
            resistance.Value = 0;
            attackAt.Value = throwPoseUntil.Value = grabAt.Value = holdBegan.Value = disabledUntil.Value = sprintUntil.Value = 0;
            attackStartedAt.Value = hitReactAt.Value = 0;
            strikePending = false;
            recoverAt = spentAt = sprintIntentUntil = lastResistance = nextJumpAt = respawnUntil = 0;
        }

        public override void OnNetworkDespawn()
        {
            UnbindAudio();
            capeColor.OnValueChanged -= ApplyCapeColor;
            RestoreContestPose();
            RestoreCarryCollision();
            if (IsServer) BreakCarryServer();
        }

        GameplayCharacter Peer(ulong id)
        {
            if (id == NoPlayer) return null;
            if (IsServer)
                return NetworkManager.ConnectedClients.TryGetValue(id, out var client) && client.PlayerObject != null
                    ? client.PlayerObject.GetComponent<GameplayCharacter>() : null;
            foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
                if (obj.IsPlayerObject && obj.OwnerClientId == id) return obj.GetComponent<GameplayCharacter>();
            return null;
        }

        internal bool IsCurrentLifeServer(int life) => IsSpawned && IsServer && RespawnCount.Value == life && Now >= respawnUntil;
        bool ValidAction(int life, RpcParams rpc) => rpc.Receive.SenderClientId == OwnerClientId &&
            IsCurrentLifeServer(life) && contestGame.CanPlay && NetworkManager.ConnectedClientsList.Count == 2;

        void Update()
        {
            RestoreContestPose();
            if (!IsSpawned || !IsOwner || !contestGame.CanPlay || !contestInput.AcceptsInput) return;
            if (contestInput.Grab.WasPressedThisFrame()) GrabRpc(ownerLife);
            if (contestInput.AttackReady && contestInput.Attack.WasPressedThisFrame()) HitRpc(ownerLife);
        }

        internal bool ReadSprint(bool wantsSprint)
        {
            if (wantsSprint && Time.unscaledTime >= nextSprintSend)
            {
                nextSprintSend = Time.unscaledTime + .1f;
                SprintRpc(ownerLife);
            }
            return wantsSprint && SprintAllowed;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void SprintRpc(int life, RpcParams rpc = default)
        {
            if (ValidAction(life, rpc) && !Attached && CanSprintServer()) sprintIntentUntil = Now + .25;
        }

        bool CanUseMovementAction => !BlocksServerInteraction && !SpecialMovement && !Attached;
        bool CanSprintServer() => holder.Value == NoPlayer && Now >= disabledUntil.Value &&
            Now >= respawnUntil && !SpecialMovement &&
            (!Attached || CooperativePole.Current.IsPairedPivot(OwnerClientId)) &&
            CastleGameplay.Current?.SprintBlocked(contestMovement) != true;

        internal void SetPoleSprintIntentServer(bool wantsSprint)
        {
            sprintIntentUntil = wantsSprint && CanSprintServer() ? Now + .25 : 0;
        }

        internal bool RequestJump(bool sprint, out bool fast)
        {
            fast = sprint && stamina.Value > jumpCost;
            if (BlocksOwnerInteraction || stamina.Value < jumpCost || stamina.Value <= 0) return false;
            ownerJumpAccepted = true;
            ownerJumpDeadline = Time.unscaledTime + .85f;
            JumpRpc(ownerLife, ++ownerJumpSequence, fast);
            return ownerJumpAccepted;
        }

        [Rpc(SendTo.Server)]
        void JumpRpc(int life, uint sequence, bool sprint, RpcParams rpc = default)
        {
            if (!ValidAction(life, rpc) || sequence <= lastJumpSequence) return;
            lastJumpSequence = sequence;
            // Ground contact belongs to the owner capsule. Replicated transforms can already be
            // airborne when the request arrives. The server gates the action and charges the pool.
            bool allowed = CanUseMovementAction && BridgeGameplay.Current?.Holds(OwnerClientId) != true &&
                Now >= nextJumpAt && stamina.Value >= jumpCost && stamina.Value > 0;
            if (!allowed)
            {
                JumpReplyRpc(life, sequence, false, false);
                return;
            }
            bool fast = sprint && CanSprintServer() && stamina.Value > jumpCost;
            if (fast) sprintIntentUntil = Now + .25;
            SpendStamina(jumpCost);
            nextJumpAt = Now + .12;
            JumpReplyRpc(life, sequence, true, fast);
        }

        internal bool HasGround()
        {
            Vector3 origin = transform.position + Vector3.up * .54f;
            if (Physics.SphereCastAll(origin, .39f, Vector3.down, .46f, ~0, QueryTriggerInteraction.Ignore)
                .Any(IsGround)) return true;
            float edge = contestController.radius - .025f;
            foreach (var offset in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                if (Physics.RaycastAll(origin + offset * edge, Vector3.down, .82f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(IsGround)) return true;
            return false;
        }

        static bool IsGround(RaycastHit hit) => hit.normal.y > .4f &&
            hit.collider.GetComponentInParent<PlayerMovement>() == null;

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void JumpReplyRpc(int life, uint sequence, bool approved, bool sprint)
        {
            if (!IsOwner || life != ownerLife || Time.unscaledTime >= ownerJumpDeadline || sequence != ownerJumpSequence) return;
            ownerJumpDeadline = 0;
            ownerJumpAccepted = approved;
            if (!IsServer) contestMovement.ReconcileV3Jump(approved, sprint);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!approved) Debug.Log($"V3_JUMP_REJECTED owner={OwnerClientId} feet={transform.position:F2}");
#endif
        }

        bool SpendStamina(float cost)
        {
            if (stamina.Value <= 0) return false;
            stamina.Value = Mathf.Max(0, stamina.Value - cost);
            spentAt = Now;
            if (stamina.Value <= 0) recoverAt = Now + emptyRecoveryDelay;
            return true;
        }

        internal bool TryStrongRowServer(float dt) => IsServer && contestGame.CanPlay && SpendStamina(strongRowPerSecond * dt);

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (!contestGame.CanPlay || NetworkManager.ConnectedClientsList.Count != 2)
            {
                CancelPendingStrike();
                BreakCarryServer();
                sprintUntil.Value = 0;
                return;
            }

            if (strikePending && Now >= attackStartedAt.Value + AttackWindup)
            {
                strikePending = false;
                if (!BlocksServerInteraction && !SpecialMovement) StrikeServer();
            }

            if (held.Value != NoPlayer)
            {
                var victim = Peer(held.Value);
                bool obstructed = victim != null && CarryObstructed(victim.transform.position);
                if (!obstructed) grabObstructedSince = -1;
                else if (grabObstructedSince < 0) grabObstructedSince = Time.time;
                if (victim == null || HasGem || SpecialMovement || victim.SpecialMovement ||
                    grabObstructedSince >= 0 && Time.time - grabObstructedSince > .45f ||
                    !SpendStamina(holdPerSecond * Time.fixedDeltaTime) || stamina.Value <= 0)
                    EndCarryServer(Vector3.zero);
            }

            if (sprintIntentUntil > Now && CanSprintServer() && SpendStamina(sprintPerSecond * Time.fixedDeltaTime))
            {
                if (sprintUntil.Value - Now < .12) sprintUntil.Value = Now + .22;
            }
            else if (sprintUntil.Value != 0) sprintUntil.Value = 0;

            if (Now >= recoverAt && Now > spentAt + .15 && stamina.Value < maximumStamina)
                stamina.Value = Mathf.Min(maximumStamina, stamina.Value + (stamina.Value == 0 ? 1 : staminaRecovery * Time.fixedDeltaTime));
        }

        GameplayCharacter FindTarget(float range)
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.ClientId == OwnerClientId || client.PlayerObject == null ||
                    !client.PlayerObject.TryGetComponent<GameplayCharacter>(out var other) ||
                    !other.IsSpawned || other.SpecialMovement || Now < other.respawnUntil) continue;
                Vector3 delta = other.transform.position - transform.position;
                if (delta.sqrMagnitude > range * range || Mathf.Abs(delta.y) > 1.45f) continue;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (flat.sqrMagnitude > .001f && Vector3.Angle(transform.forward, flat) > aimHalfAngle) continue;
                Vector3 eye = transform.position + Vector3.up;
                if (Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    .Any(hit => hit.collider.GetComponentInParent<PlayerMovement>() == null)) continue;
                return other;
            }
            return null;
        }

        [Rpc(SendTo.Server)]
        void HitRpc(int life, RpcParams rpc = default)
        {
            if (!ValidAction(life, rpc) || Now < attackAt.Value || BlocksServerInteraction || SpecialMovement) return;
            attackStartedAt.Value = Now;
            attackAt.Value = Now + hitCooldown;
            strikePending = true;
        }

        void CancelPendingStrike()
        {
            if (!strikePending) return;
            strikePending = false;
            attackStartedAt.Value = 0;
        }

        void StrikeServer()
        {
            var victim = FindTarget(hitRange);
            if (victim == null) return;
            victim.InterruptForContest();
            GemOwnership.Current?.TryDropServer(victim.OwnerClientId, victim.transform.position);
            Vector3 away = Vector3.ProjectOnPlane(victim.transform.position - transform.position, Vector3.up).normalized;
            if (away.sqrMagnitude < .01f) away = transform.forward;
            victim.hitReactAt.Value = Now;
            victim.SetMotionServer(ContestMotion.Hit, away * hitSpeed + Vector3.up * hitLift, hitStun);
        }

        [Rpc(SendTo.Server)]
        void GrabRpc(int life, RpcParams rpc = default)
        {
            if (!ValidAction(life, rpc)) return;
            if (holder.Value != NoPlayer)
            {
                if (Now < lastResistance + .13) return;
                lastResistance = Now;
                var carrier = Peer(holder.Value);
                if (carrier != null && ++resistance.Value >= resistancePresses)
                {
                    carrier.EndCarryServer(Vector3.zero);
                    contestGame.EmitWorldCue(AudioCueId.Resist, transform.position + Vector3.up);
                }
                return;
            }
            if (held.Value != NoPlayer)
            {
                float speed = Mathf.Lerp(throwMinSpeed, throwMaxSpeed, Charge);
                throwPoseUntil.Value = Now + ThrowPoseSeconds;
                EndCarryServer(transform.forward * speed + Vector3.up * throwLift);
                return;
            }
            if (Now < grabAt.Value || BlocksServerInteraction || SpecialMovement || HasGem || stamina.Value <= 0) return;
            var victim = FindTarget(grabRange);
            if (victim == null || victim.holder.Value != NoPlayer || Now < victim.disabledUntil.Value ||
                !victim.HasCarryClearance(HoldFeet())) return;

            InterruptForContest();
            victim.InterruptForContest();
            held.Value = victim.OwnerClientId;
            holdBegan.Value = Now;
            recentLiftTargets.Clear();
            grabObstructedSince = -1;
            victim.holder.Value = OwnerClientId;
            victim.resistance.Value = 0;
            victim.lastResistance = Now;
            victim.SetMotionServer(ContestMotion.Held, Vector3.zero, 0, OwnerClientId);
        }

        Vector3 HoldFeet() => transform.position + transform.forward * 1.2f + Vector3.up * .55f;

        bool CarryObstructed(Vector3 victimFeet)
        {
            // The victim follows a replicated carrier, then replicates its own capsule back.
            double now = Now;
            while (recentLiftTargets.TryPeek(out var oldest) && now - oldest.Time > CarryReplicaDelay)
                recentLiftTargets.Dequeue();
            recentLiftTargets.Enqueue((now, HoldFeet()));
            foreach (var target in recentLiftTargets)
                if ((victimFeet - target.Feet).sqrMagnitude <= CarryObstructionDistance * CarryObstructionDistance)
                    return false;
            return true;
        }

        bool HasCarryClearance(Vector3 feet)
        {
            Vector3 center = feet + contestController.center;
            float radius = contestController.radius - contestController.skinWidth;
            Vector3 half = Vector3.up * (contestController.height * .5f - radius);
            return !Physics.OverlapCapsule(center - half, center + half, radius, ~0, QueryTriggerInteraction.Ignore)
                .Any(collider => collider.GetComponentInParent<PlayerMovement>() == null);
        }

        void InterruptForContest()
        {
            CooperativePole.Current?.BeforeRespawnServer(OwnerClientId);
            BridgeGameplay.Current?.BeforeRespawnServer(contestMovement);
            InterruptInteractionServer();
        }

        void InterruptInteractionServer()
        {
            CancelPendingStrike();
            BreakCarryServer();
            CastleGameplay.Current?.InterruptContestServer(OwnerClientId);
            UndergroundGameplay.Current?.InterruptContestServer(OwnerClientId);
            contestGame.ContestInteractionSample?.InterruptServer(OwnerClientId);
        }

        void BreakCarryServer()
        {
            if (held.Value != NoPlayer) EndCarryServer(Vector3.zero);
            if (holder.Value == NoPlayer) return;
            var carrier = Peer(holder.Value);
            holder.Value = NoPlayer;
            resistance.Value = 0;
            if (carrier != null && carrier.IsSpawned) carrier.EndCarryServer(Vector3.zero);
        }

        void EndCarryServer(Vector3 impulse)
        {
            var victim = Peer(held.Value);
            held.Value = NoPlayer;
            recentLiftTargets.Clear();
            grabAt.Value = Now + grabCooldown;
            if (victim == null || !victim.IsSpawned || victim.holder.Value != OwnerClientId) return;
            victim.holder.Value = NoPlayer;
            victim.resistance.Value = 0;
            victim.SetMotionServer(impulse == Vector3.zero ? ContestMotion.Free : ContestMotion.Thrown,
                impulse, impulse == Vector3.zero ? 0 : throwControlSeconds);
        }

        void SetMotionServer(ContestMotion motion, Vector3 impulse, float seconds, ulong carrier = NoPlayer)
        {
            disabledUntil.Value = Now + seconds;
            sprintIntentUntil = 0;
            sprintUntil.Value = 0;
            ApplyContestMotionRpc(RespawnCount.Value, motion, impulse, seconds, carrier);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void ApplyContestMotionRpc(int life, ContestMotion motion, Vector3 impulse, float seconds, ulong carrier)
        {
            if (!IsOwner || life != ownerLife) return;
            ownerMotion = motion;
            ownerImpulse = impulse;
            ownerHolder = carrier;
            ownerMotionClock = seconds;
            ownerMotionStarted = false;
            ownerJumpDeadline = 0;
        }

        internal bool ApplyOwnerContestMovement()
        {
            UpdateCarryCollision();
            if (ownerMotion == ContestMotion.Free) return false;
            if (!ownerMotionStarted)
            {
                contestMovement.ResetVerticalVelocity();
                ownerMotionStarted = true;
                ownerMotionTime = Time.timeAsDouble;
            }
            if (ownerMotion == ContestMotion.Held)
            {
                var carrier = Peer(ownerHolder);
                if (carrier != null)
                {
                    contestController.Move(Vector3.ClampMagnitude(carrier.HoldFeet() - transform.position, 18 * Time.deltaTime));
                    transform.rotation = carrier.transform.rotation;
                }
                return true;
            }

            // Each substep uses the normal capsule, including wall, ceiling and floor contacts.
            float remaining = Time.deltaTime;
            while (remaining > 0)
            {
                float dt = Mathf.Min(remaining, 1f / 120);
                remaining -= dt;
                ownerImpulse.y -= 20 * dt;
                var flags = contestController.Move(ownerImpulse * dt);
                if ((flags & CollisionFlags.Sides) != 0) ownerImpulse = Vector3.up * ownerImpulse.y;
                if ((flags & CollisionFlags.Above) != 0) ownerImpulse.y = Mathf.Min(0, ownerImpulse.y);
                if ((flags & CollisionFlags.Below) != 0) ownerImpulse.y = Mathf.Max(0, ownerImpulse.y);
                if (ownerMotion == ContestMotion.Hit)
                {
                    ownerImpulse.x *= Mathf.Exp(-5 * dt);
                    ownerImpulse.z *= Mathf.Exp(-5 * dt);
                }
            }
            if (Time.timeAsDouble < ownerMotionTime + ownerMotionClock) return true;
            contestMovement.ContinueContestVelocity(ownerImpulse, ownerMotion == ContestMotion.Hit);
            ownerMotion = ContestMotion.Free;
            return true;
        }

        void UpdateCarryCollision()
        {
            ulong partner = ownerMotion == ContestMotion.Held ? ownerHolder :
                holder.Value != NoPlayer ? holder.Value : held.Value;
            var peer = Peer(partner)?.contestController;
            if (peer == carryCollisionPeer) return;
            RestoreCarryCollision();
            if (peer == null || !peer.enabled) return;
            // The owner walks toward a separately replicated capsule while the lift pose catches up.
            Physics.IgnoreCollision(contestController, peer, true);
            carryCollisionPeer = peer;
        }

        void RestoreCarryCollision()
        {
            if (carryCollisionPeer != null)
                Physics.IgnoreCollision(contestController, carryCollisionPeer, false);
            carryCollisionPeer = null;
        }

        internal void BeforeContestRespawnServer()
        {
            InterruptInteractionServer();
            attackStartedAt.Value = hitReactAt.Value = throwPoseUntil.Value = 0;
            respawnUntil = Now + .5;
            sprintIntentUntil = 0;
            sprintUntil.Value = disabledUntil.Value = 0;
            stamina.Value = maximumStamina;
            recoverAt = spentAt = 0;
        }

        internal void ResetContestOwner(int life)
        {
            RestoreCarryCollision();
            ownerLife = life;
            ownerMotion = ContestMotion.Free;
            ownerJumpAccepted = false;
            ownerJumpDeadline = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal string StaminaTuningForTest => $"cap={maximumStamina} jump={jumpCost} sprint={sprintPerSecond}";
        internal void RequestHitForLifeForTest(int life) => HitRpc(life);
        internal ulong HeldForTest => held.Value;
        internal ulong HolderForTest => holder.Value;
        internal double AttackReadyAtForTest => attackAt.Value;
        internal double GrabReadyAtForTest => grabAt.Value;
        internal double ControlledUntilForTest => disabledUntil.Value;
        internal float ChargeForTest => Charge;
        internal byte CapeIndexForTest => capeColor.Value;
        internal MeshRenderer CapeRenderer => capeRenderer;
        internal float HitRangeForTest => hitRange;
        internal double AttackContactAtForTest => attackStartedAt.Value + AttackWindup;
        internal double HitReactAtForTest => hitReactAt.Value;
        internal bool ContestPoseAppliedForTest => Time.frameCount - contestPoseFrame <= 1;
#endif

        float Charge => held.Value == NoPlayer ? 0 : Mathf.Clamp01((float)(Now - holdBegan.Value) / chargeSeconds);

        internal bool HasContestPose => holder.Value != NoPlayer || held.Value != NoPlayer;

        internal string ContestPresentation(GameplayCharacter other)
        {
            if (holder.Value != NoPlayer) return $"F 연타로 저항 ({resistance.Value}/{resistancePresses})";
            if (held.Value != NoPlayer) return $"F 던지기 · 충전 {Charge:P0}";
            if (other == null || SpecialMovement ||
                (other.transform.position - transform.position).sqrMagnitude > 25f) return null;
            float attack = Mathf.Max(0, (float)(attackAt.Value - Now));
            float grab = Mathf.Max(0, (float)(grabAt.Value - Now));
            return attack > 0 || grab > 0 ? $"공격 {attack:F1}초 · 잡기 {grab:F1}초" :
                HasGem ? "좌클릭 공격" : "좌클릭 공격 · F 잡기";
        }
    }
}
