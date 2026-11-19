using System;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class RiverGameplay
    {
        enum HazardKind : byte { Rock, Driftwood, Fish, Crocodile, Whirlpool }
        enum HazardPhase : byte { Hidden, Active, Warning, Strike }

        struct HazardState : INetworkSerializable, IEquatable<HazardState>
        {
            public Vector3 Position, Aim;
            public HazardPhase Phase;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Aim);
                serializer.SerializeValue(ref Phase);
            }
            public bool Equals(HazardState other) => Position.Equals(other.Position) && Aim.Equals(other.Aim) && Phase == other.Phase;
        }

        sealed class Hazard
        {
            public readonly HazardKind Kind;
            public readonly float CourseFraction, Offset, Radius;
            public HazardState State;
            public float Since, NextAttack;
            public Vector3 Origin;
            public int HitMask;
            public Transform View;
            public SphereCollider Collider;
            public LineRenderer Warning;
            public FantasyJaw Jaw;
            public Hazard(HazardKind kind, float fraction, float offset, float radius)
            {
                Kind = kind; CourseFraction = fraction; Offset = offset; Radius = radius;
            }
        }

        [Header("Hazards")]
        [SerializeField] GameObject rockModel, driftwoodModel, fishModel, crocodileModel, whirlpoolModel;
        [SerializeField, Min(.1f)] float driftSpeed = 3.8f, driftPeriod = 32f;
        [SerializeField, Min(.1f)] float warningTime = 1.4f, strikeTime = .65f, predatorCooldown = 9f;
        [SerializeField, Min(.1f)] float predatorNoticeRange = 17f, predatorLunge = 12f;
        [SerializeField, Min(.1f)] float whirlpoolPeriod = 25f, whirlpoolActive = 16f, whirlpoolPull = 5.5f;

        readonly Hazard[] hazards =
        {
            new(HazardKind.Rock, .12f, 10, 2.4f),
            new(HazardKind.Driftwood, .22f, -8, 2.6f),
            new(HazardKind.Fish, .31f, -13, 3.2f),
            new(HazardKind.Whirlpool, .39f, 7, 10),
            new(HazardKind.Rock, .48f, -8, 2.8f),
            new(HazardKind.Crocodile, .57f, 13, 3.5f),
            new(HazardKind.Driftwood, .67f, 6, 2.6f),
            new(HazardKind.Whirlpool, .74f, -8, 10),
            new(HazardKind.Fish, .82f, 11, 3.2f),
            new(HazardKind.Rock, .90f, -6, 2.5f)
        };
        NetworkList<HazardState> hazardFeed;
        float hazardTime, nextHazardPublish;
        void InitializeHazardNetwork() => hazardFeed = new NetworkList<HazardState>();

        void ResetHazards()
        {
            hazardTime = Time.time;
            nextHazardPublish = 0;
            hazardFeed.Clear();
            foreach (var hazard in hazards)
            {
                hazard.State = default;
                hazard.Since = hazard.NextAttack = 0;
                hazard.HitMask = 0;
                hazardFeed.Add(hazard.State);
            }
        }

        float HazardZ(Hazard h) => Mathf.Lerp(channel.Start, channel.End, h.CourseFraction);

        void TickHazards()
        {
            float elapsed = Time.time - hazardTime;
            bool send = Time.time >= nextHazardPublish;
            if (send) nextHazardPublish = Time.time + .07f;
            for (int i = 0; i < hazards.Length; i++)
            {
                var h = hazards[i];
                float z = HazardZ(h);
                switch (h.Kind)
                {
                    case HazardKind.Rock:
                        h.State.Position = channel.Point(z, h.Offset);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (recoveryFixture && i == 0) h.State.Position = TickRecoveryFixture();
#endif
                        h.State.Phase = HazardPhase.Active;
                        break;
                    case HazardKind.Driftwood:
                        float travel = Mathf.Repeat(elapsed + i * 3.1f, driftPeriod);
                        h.State.Position = channel.Point(z + 28 - travel * driftSpeed, h.Offset + Mathf.Sin(travel * .32f) * 5);
                        h.State.Phase = travel < Mathf.Min(driftPeriod * .72f, 78f / driftSpeed) ? HazardPhase.Active : HazardPhase.Hidden;
                        break;
                    case HazardKind.Whirlpool:
                        h.State.Position = channel.Point(z, h.Offset);
                        h.State.Phase = Mathf.Repeat(elapsed + i * 2f, whirlpoolPeriod) < whirlpoolActive ? HazardPhase.Active : HazardPhase.Hidden;
                        break;
                    default:
                        TickPredator(h, z);
                        break;
                }
                if (send) hazardFeed[i] = h.State;
            }
        }

        void TickPredator(Hazard h, float z)
        {
            float elapsed = Time.time - h.Since;
            if (h.State.Phase == HazardPhase.Warning)
            {
                if (elapsed < warningTime) return;
                h.Since = Time.time;
                h.State.Phase = HazardPhase.Strike;
                return;
            }
            if (h.State.Phase == HazardPhase.Strike)
            {
                h.State.Position = Vector3.Lerp(h.Origin, h.State.Aim, elapsed / strikeTime);
                h.State.Position.y = Height(h.State.Position);
                if (elapsed < strikeTime + .18f) return;
                h.NextAttack = Time.time + predatorCooldown;
            }

            h.State.Phase = HazardPhase.Active;
            h.State.Position = channel.Point(z + Mathf.Sin(Time.time * .54f) * 5,
                h.Offset + Mathf.Cos(Time.time * .68f) * 2.5f);
            if (Time.time < h.NextAttack) return;
            float nearest = predatorNoticeRange;
            foreach (var hull in hulls)
            {
                var s = hull.State;
                if (s.Status != RiverRaftStatus.Sailing) continue;
                Vector3 delta = s.Position - h.State.Position;
                delta.y = 0;
                if (delta.magnitude > nearest) continue;
                nearest = delta.magnitude;
                Vector3 lead = s.Position + s.Velocity * warningTime * .6f - h.State.Position;
                lead.y = 0;
                Vector3 target = h.State.Position + Vector3.ClampMagnitude(lead, predatorLunge);
                var sample = channel.Sample(target.z);
                h.State.Aim = channel.Point(target.z, target.x - sample.center.x);
                h.State.Phase = HazardPhase.Warning;
                h.Origin = h.State.Position;
                h.Since = Time.time;
                h.HitMask = 0;
            }
        }

        Vector3 WhirlpoolForce(Vector3 position)
        {
            Vector3 force = Vector3.zero;
            foreach (var h in hazards)
            {
                if (h.Kind != HazardKind.Whirlpool || h.State.Phase != HazardPhase.Active) continue;
                Vector3 delta = h.State.Position - position;
                delta.y = 0;
                float weight = 1 - delta.magnitude / h.Radius;
                if (weight <= 0) continue;
                force += (delta.normalized + Vector3.Cross(Vector3.up, delta.normalized) * .65f) * (weight * whirlpoolPull);
            }
            return force;
        }

        RiverContact ContactHazards(int index, Vector3 from, ref Vector3 next, float radius, out bool blocked)
        {
            blocked = false;
            RiverContact cause = RiverContact.None;
            var hull = hulls[index];
            for (int i = 0; i < hazards.Length; i++)
            {
                var h = hazards[i];
                bool predator = h.Kind is HazardKind.Fish or HazardKind.Crocodile;
                if (h.Kind == HazardKind.Whirlpool || h.State.Phase != (predator ? HazardPhase.Strike : HazardPhase.Active))
                {
                    hull.Contacts.Remove(i);
                    continue;
                }

                Vector2 center = Flat(h.State.Position);
                Vector2 start = Flat(from);
                Vector2 delta = Flat(next) - start;
                float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(center - start, delta) / delta.sqrMagnitude) : 0;
                float separation = Vector2.Distance(center, start + t * delta);
                if (separation > radius + h.Radius)
                {
                    if (separation > radius + h.Radius + .9f) hull.Contacts.Remove(i);
                    continue;
                }

                if (!predator)
                {
                    blocked = true;
                    Vector2 normal = start + t * delta - center;
                    if (normal.sqrMagnitude < .001f) normal = start - center;
                    if (normal.sqrMagnitude < .001f) normal = Vector2.right;
                    Vector2 corrected = center + normal.normalized * (radius + h.Radius + .05f);
                    float z = Mathf.Clamp(corrected.y, channel.Start + radius, channel.End - radius);
                    var sample = channel.Sample(z);
                    float limit = Mathf.Max(0, sample.halfWidth - radius / Mathf.Max(.45f, sample.upstream.z) - .25f);
                    next = new Vector3(Mathf.Clamp(corrected.x, sample.center.x - limit, sample.center.x + limit), sample.center.y + .05f, z);
                }
                if (Time.time < hull.NextHit) continue;
                if (predator)
                {
                    if ((h.HitMask & (1 << index)) != 0) continue;
                    h.HitMask |= 1 << index;
                }
                else if (!hull.Contacts.Add(i)) continue;
                cause = h.Kind switch
                {
                    HazardKind.Rock => RiverContact.Rock,
                    HazardKind.Driftwood => RiverContact.Driftwood,
                    HazardKind.Fish => RiverContact.Fish,
                    _ => RiverContact.Crocodile
                };
                break;
            }
            return cause;
        }

        static Vector2 Flat(Vector3 p) => new(p.x, p.z);
    }
}
