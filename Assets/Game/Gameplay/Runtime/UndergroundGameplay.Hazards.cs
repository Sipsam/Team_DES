using System;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class UndergroundGameplay
    {
        enum TrapKind { Fire, Arrow, Spikes }

        [Header("Underground hazards")]
        [SerializeField, Min(.2f)] float trapWarning = 1.2f, trapAttack = 1.1f, trapRecovery = 6;
        [SerializeField, Min(.1f)] float slowSeconds = 1.3f;
        [SerializeField, Range(.2f, 1f)] float trapSpeed = .78f;
        [SerializeField, Range(0, 10)] float knockback = 1.65f;
        [SerializeField, Min(3)] float movingPeriod = 8, vanishPeriod = 8;
        [SerializeField, Min(.25f)] float vanishWarning = 1.25f, vanishAbsent = 2;

        sealed class Trap
        {
            public TrapKind Kind;
            public Vector3 Origin, Size;
            public float Offset;
            public Transform Attack;
            public GameObject Warning;
            public long PreviousCycle = -1;
            public int Hits;
            public Vector3 PreviousPosition;
            public bool WasActive;
        }

        readonly Trap[] traps = new Trap[6];
        readonly UndergroundVanishPlatform[] vanishing = new UndergroundVanishPlatform[2];
        readonly BoxCollider[] moving = new BoxCollider[2];
        readonly Vector3[] movingStarts = new Vector3[2];
        readonly Vector3[] movingTravel = new Vector3[2];

        float AttackPeriod => trapWarning + trapAttack + trapRecovery;

        void BuildHazards()
        {
            for (int lane = 0; lane < 2; lane++)
            {
                float inward = lane == 0 ? 1 : -1;
                for (int i = 0; i < 3; i++)
                {
                    var landing = landings[lane, i + 1];
                    var center = landing.position + new Vector3(inward * 2, 0, -.7f);
                    var kind = (TrapKind)((i + lane) % 3);
                    GameObject model = kind == TrapKind.Fire ? flameModel : kind == TrapKind.Arrow ? arrowModel : spikesModel;
                    Vector3 size = kind == TrapKind.Arrow ? new Vector3(1.2f, .7f, .75f) :
                        new Vector3(kind == TrapKind.Fire ? 3.1f : 2.7f, 1.35f, 1.5f);
                    var attack = Display(layout, model, center, size, fit: true);
                    var warning = Stripe(layout, center + Vector3.up * .045f,
                        new Vector3(kind == TrapKind.Arrow ? 8 : size.x, .09f, 1.7f), warningMaterial);
                    traps[lane * 3 + i] = new Trap
                    {
                        Kind = kind, Origin = center, Size = size, Offset = 1.3f * i + lane * 2.2f,
                        Attack = attack, Warning = warning
                    };
                }

                // These optional footholds extend the fixed shelves. Their cycles run in both directions.
                var moveShelf = landings[lane, 1].position;
                float wall = WallX(moveShelf.z + 5, lane);
                Vector3 from = new(wall - inward, moveShelf.y - .12f, moveShelf.z + 5);
                movingStarts[lane] = World(from);
                movingTravel[lane] = Vector3.right * (moveShelf.x - inward * 2 - from.x);
                moving[lane] = BuildDeck("Wall slide", from, new Vector2(3.2f, 2.6f), movingModel, warningMaterial)
                    .Object.GetComponent<BoxCollider>();

                var vanishPoint = landings[lane, 2].position + new Vector3(inward * 2.7f, -.1f, -5.25f);
                var deck = BuildDeck("Retracting trap stone", vanishPoint, new Vector2(2.5f, 2.3f), vanishModel, warningMaterial);
                var warningView = Stripe(layout, vanishPoint + Vector3.up * .04f, new Vector3(2.5f, .08f, 2.3f), warningMaterial);
                vanishing[lane] = Create("Retracting stone cycle", layout, vanishPoint).gameObject.AddComponent<UndergroundVanishPlatform>();
                vanishing[lane].Configure(deck.Object, warningView);
            }
        }

        float WallX(float z, int lane)
        {
            float x = lane == 0 ? float.PositiveInfinity : float.NegativeInfinity;
            for (int i = 0; i < config.cave.Length; i++)
            {
                Vector2 a = config.cave[i], b = config.cave[(i + 1) % config.cave.Length];
                if ((a.y > z) == (b.y > z)) continue;
                float edge = Mathf.Lerp(a.x, b.x, (z - a.y) / (b.y - a.y));
                x = lane == 0 ? Mathf.Min(edge, x) : Mathf.Max(edge, x);
            }
            return x;
        }

        Vector3 AttackPosition(Trap trap, double phase)
        {
            float t = Mathf.Clamp01((float)(phase - trapWarning) / trapAttack);
            return trap.Origin + (trap.Kind == TrapKind.Arrow
                ? new Vector3(Mathf.Lerp(-4.4f, 4.4f, t), .85f, 0)
                : Vector3.up * (trap.Kind == TrapKind.Fire ? .7f : Mathf.Lerp(.3f, .72f, Mathf.Sin(t * Mathf.PI))));
        }

        void DrawHazards()
        {
            bool active = IsSpawned && (Cooperative || Returning) && hazardEpoch.Value >= 0;
            double elapsed = active ? Math.Max(0, Now - hazardEpoch.Value) : 0;
            foreach (var trap in traps)
            {
                double phase = (elapsed + trap.Offset) % AttackPeriod;
                trap.Warning.SetActive(active && phase < trapWarning);
                trap.Attack.gameObject.SetActive(active && phase >= trapWarning && phase < trapWarning + trapAttack);
                trap.Attack.localPosition = AttackPosition(trap, phase);
            }

            var local = IsSpawned ? NetworkManager.LocalClient?.PlayerObject : null;
            for (int i = 0; i < moving.Length; i++)
            {
                float period = Mathf.Max(vanishPeriod, vanishWarning + vanishAbsent + .5f);
                vanishing[i].Present(active, elapsed + 2 * i, period, vanishWarning, vanishAbsent);
                var support = moving[i];
                var target = movingStarts[i] + movingTravel[i] * Mathf.PingPong((float)(elapsed / movingPeriod) * 2, 1);
                Vector3 delta = target - support.transform.position;
                bool wasActive = support.gameObject.activeSelf;
                support.gameObject.SetActive(active);
                var movement = local != null ? local.GetComponent<PlayerMovement>() : null;
                bool aboard = active && wasActive && movement != null && movement.IsGrounded &&
                    support.Raycast(new Ray(local.transform.position + Vector3.up * .22f, Vector3.down), out _, .55f);
                support.transform.position = target;
                if (!aboard || delta.sqrMagnitude <= .000001f) continue;
                Physics.SyncTransforms();
                movement.CarryUndergroundPlatform(delta);
            }
        }

        void TickTrapDamage()
        {
            double elapsed = Math.Max(0, Now - hazardEpoch.Value);
            foreach (var trap in traps)
            {
                double clock = elapsed + trap.Offset;
                long cycle = (long)(clock / AttackPeriod);
                double phase = clock % AttackPeriod;
                bool attacking = phase >= trapWarning && phase < trapWarning + trapAttack;
                if (cycle != trap.PreviousCycle)
                {
                    trap.PreviousCycle = cycle;
                    trap.Hits = 0;
                    trap.WasActive = false;
                }
                if (!attacking) { trap.WasActive = false; continue; }
                var center = World(AttackPosition(trap, phase));
                var volume = new Bounds(center, trap.Size);
                if (trap.WasActive) volume.Encapsulate(new Bounds(trap.PreviousPosition, trap.Size));
                trap.PreviousPosition = center;
                trap.WasActive = true;
                foreach (var peer in NetworkManager.ConnectedClientsList)
                {
                    var player = peer.PlayerObject;
                    if (player == null || !spawns.TryGetSlot(peer.ClientId, out int slot) ||
                        slot >= 2 || (trap.Hits & (1 << slot)) != 0 || Now < recoveryGraceUntil) continue;
                    var capsule = player.GetComponent<CharacterController>();
                    var body = new Bounds(player.transform.position + capsule.center,
                        new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2));
                    if (!volume.Intersects(body)) continue;
                    trap.Hits |= 1 << slot;
                    slowedUntil[slot] = Now + slowSeconds;
                    Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - center, Vector3.up).normalized;
                    player.GetComponent<PlayerMovement>().ApplyExternalVelocityRpc(direction * knockback);
                }
            }
        }
    }
}
