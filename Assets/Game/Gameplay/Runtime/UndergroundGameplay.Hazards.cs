using TwinEdge.Audio;
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
        [SerializeField, Min(3)] float vanishPeriod = 8;
        [SerializeField, Min(.25f)] float vanishWarning = 1.25f, vanishAbsent = 2;

        sealed class Trap
        {
            public TrapKind Kind;
            public Transform Frame;
            public Vector3 Size;
            public float Offset;
            public Transform Attack;
            public GameObject Warning;
            public long PreviousCycle = -1;
            public int Hits;
            public Vector3 PreviousPosition;
            public bool WasActive;
            public TimedTrapAudio Audio;
        }

        readonly Trap[] traps = new Trap[6];
        readonly UndergroundVanishPlatform[] vanishing = new UndergroundVanishPlatform[2];

        float AttackPeriod => trapWarning + trapAttack + trapRecovery;

        void BuildHazards(MeshCollider cavernWall)
        {
            for (int lane = 0; lane < 2; lane++)
            {
                float inward = lane == 0 ? 1 : -1;
                for (int i = 0; i < 3; i++)
                {
                    var landing = landings[lane, i + 1];
                    var shelf = (MeshCollider)landingSurfaces[lane, i + 1];
                    var frame = Create($"Trap {landing.id}", layout, landing.position);
                    frame.rotation = shelf.transform.rotation;
                    frame.position -= frame.forward * .7f;
                    float width = FitShelfSpan(frame, shelf, cavernWall);
                    var kind = (TrapKind)((i + lane) % 3);
                    GameObject model = kind == TrapKind.Fire ? flameModel : kind == TrapKind.Arrow ? arrowModel : spikesModel;
                    Vector3 size = kind == TrapKind.Arrow ? new Vector3(width, .7f, .75f) :
                        new Vector3(width, 1.35f, 1.5f);
                    var attack = Create(model.name, frame, Vector3.zero);
                    int sections = Mathf.CeilToInt(width / 3.4f);
                    var sectionSize = new Vector3(size.x / sections, size.y, size.z);
                    for (int section = 0; section < sections; section++)
                        Display(attack, model, Vector3.right * ((section + .5f) * sectionSize.x - width * .5f),
                            sectionSize, fit: true, meshBoundsOnly: true);
                    var warning = Stripe(frame, Vector3.up * .045f, new Vector3(width, .09f, 1.7f), warningMaterial);
                    traps[lane * 3 + i] = new Trap
                    {
                        Kind = kind, Frame = frame, Size = size, Offset = 1.3f * i + lane * 2.2f,
                        Attack = attack, Warning = warning
                    };
                }

                var vanishPoint = landings[lane, 2].position + new Vector3(inward * 2.7f, -.1f, -5.25f);
                var deck = BuildDeck("Retracting trap stone", vanishPoint, new Vector2(2.5f, 2.3f), vanishModel, warningMaterial);
                var warningView = Stripe(layout, vanishPoint + Vector3.up * .04f, new Vector3(2.5f, .08f, 2.3f), warningMaterial);
                vanishing[lane] = Create("Retracting stone cycle", layout, vanishPoint).gameObject.AddComponent<UndergroundVanishPlatform>();
                vanishing[lane].Configure(deck.Object, warningView);
            }
        }

        static float FitShelfSpan(Transform frame, MeshCollider shelf, MeshCollider cavernWall)
        {
            var mesh = shelf.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float z = shelf.transform.InverseTransformPoint(frame.position).z;
            float top = mesh.bounds.max.y;
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            // Cut the top surface at the attack depth so bevelled edges are measured at the trap.
            for (int i = 0; i < triangles.Length; i += 3)
            for (int edge = 0; edge < 3; edge++)
            {
                Vector3 a = vertices[triangles[i + edge]], b = vertices[triangles[i + (edge + 1) % 3]];
                if (a.y != top || b.y != top || (a.z > z) == (b.z > z)) continue;
                float x = Mathf.Lerp(a.x, b.x, (z - a.z) / (b.z - a.z));
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
            }

            float scale = Mathf.Abs(shelf.transform.lossyScale.x);
            float xAtCenter = shelf.transform.InverseTransformPoint(frame.position).x;
            left = (left - xAtCenter) * scale;
            right = (right - xAtCenter) * scale;
            // The saved shelf extends into the rock. Stop at the inner face of the cavern wall.
            Vector3 rayStart = frame.position + frame.up * .25f;
            if (cavernWall.Raycast(new Ray(rayStart, -frame.right), out var wallLeft, -left)) left = -wallLeft.distance;
            if (cavernWall.Raycast(new Ray(rayStart, frame.right), out var wallRight, right)) right = wallRight.distance;
            frame.position += frame.right * ((left + right) * .5f);
            return right - left;
        }

        Vector3 AttackPosition(Trap trap, double phase)
        {
            float t = Mathf.Clamp01((float)(phase - trapWarning) / trapAttack);
            float lift = Mathf.Sin(t * Mathf.PI);
            return Vector3.up * (trap.Kind switch
            {
                TrapKind.Fire => .7f,
                // The full-width volley rises within the shelf footprint.
                TrapKind.Arrow => .85f + .15f * lift,
                _ => Mathf.Lerp(.3f, .72f, lift)
            });
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
                var attack = trap.Kind switch
                {
                    TrapKind.Fire => AudioCueId.TrapFire,
                    TrapKind.Arrow => AudioCueId.TrapArrow,
                    _ => AudioCueId.TrapSpikes
                };
                double now = IsSpawned ? Now : 0;
                double start = active
                    ? hazardEpoch.Value + System.Math.Floor((elapsed + trap.Offset) / AttackPeriod) * AttackPeriod - trap.Offset
                    : double.NegativeInfinity;
                trap.Audio.Observe(game, now, start, trapWarning, trap.Frame.position, attack, trap.Attack.position);
            }

            float period = Mathf.Max(vanishPeriod, vanishWarning + vanishAbsent + .5f);
            for (int i = 0; i < vanishing.Length; i++)
                vanishing[i].Present(active, elapsed + 2 * i, period, vanishWarning, vanishAbsent);
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
                var center = AttackPosition(trap, phase);
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
                    var body = new Bounds(trap.Frame.InverseTransformPoint(player.transform.TransformPoint(capsule.center)),
                        new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2));
                    if (!volume.Intersects(body)) continue;
                    trap.Hits |= 1 << slot;
                    game.EmitWorldCue(AudioCueId.TrapHit, player.transform.position + Vector3.up);
                    slowedUntil[slot] = Now + slowSeconds;
                    Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - trap.Frame.TransformPoint(center), Vector3.up).normalized;
                    player.GetComponent<PlayerMovement>().ApplyExternalVelocityRpc(direction * knockback);
                }
            }
        }
    }
}
