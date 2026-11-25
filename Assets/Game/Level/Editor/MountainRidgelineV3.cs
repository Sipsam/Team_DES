using System.Linq;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    internal sealed class MountainRidgelineV3
    {
        readonly struct Flank
        {
            readonly Vector2 origin, direction;
            readonly float length, fromHeight, toHeight, width, crestWidth, bias;

            internal Flank(Vector3 from, Vector3 to, float width, float crestWidth, float bias)
            {
                origin = new Vector2(from.x, from.z);
                var delta = new Vector2(to.x, to.z) - origin;
                length = delta.magnitude;
                direction = delta / length;
                fromHeight = from.y;
                toHeight = to.y;
                this.width = width;
                this.crestWidth = crestWidth;
                this.bias = bias;
            }

            internal float Height(Vector2 position, float displacement, float floor)
            {
                var offset = position - origin;
                float along = Mathf.Clamp(Vector2.Dot(offset, direction), 0, length);
                var across = offset - direction * along;
                float cross = direction.x * across.y - direction.y * across.x;
                float side = Mathf.Clamp(cross / (width * .47f), -1, 1);
                float crest = Mathf.Lerp(fromHeight, toHeight, along / length);
                float span = width * (1 + bias * side);
                float ledge = crestWidth * (1 + .28f * Mathf.Sin(along * .068f));
                float offsetFromCrest = across.magnitude + displacement;
                float distance = Mathf.Max(0, offsetFromCrest - ledge) / Mathf.Max(1, span - ledge);
                // Terrace ridges share broad shoulders with the adjacent rock. Sharp spines have no ledge.
                float falloff = distance * (.66f + .34f * Mathf.Clamp01(distance));
                float lip = crest - Mathf.Clamp(offsetFromCrest, 0, ledge) * .16f;
                return Mathf.Lerp(floor, lip, Mathf.Clamp01(1 - falloff));
            }
        }

        readonly struct Shelf
        {
            internal readonly MountainPeakV3 peak;
            readonly Vector2 major, minor, spurAxis;
            readonly float phase;

            internal Shelf(MountainPeakV3 peak, int index, Vector3[] crests)
            {
                this.peak = peak;
                float angle = peak.rotation * Mathf.Deg2Rad;
                major = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                minor = new Vector2(-major.y, major.x);
                phase = index * 2.39996f;
                var anchor = crests.Select(p => new Vector2(p.x, p.z) - peak.position)
                    .Where(d => d.sqrMagnitude > 34 * 34)
                    .OrderBy(d => d.sqrMagnitude).FirstOrDefault();
                spurAxis = (anchor.normalized + .45f * new Vector2(Mathf.Cos(phase), Mathf.Sin(phase))).normalized;
            }

            internal float Height(Vector2 position, float substrate)
            {
                var d = position - peak.position;
                if (d.sqrMagnitude > 65 * 65) return substrate;

                float u = Vector2.Dot(d, major);
                float v = Vector2.Dot(d, minor);
                float radius = d.magnitude;
                float rx = peak.summitSize.x * .5f;
                float rz = peak.summitSize.y * .5f;
                float extentX = rx + 1.8f + 2.2f * Mathf.Sin(v * .11f + phase);
                float extentZ = rz + 1.5f + 2.1f * Mathf.Cos(u * .17f - phase);
                float corner = Mathf.Abs(u + .15f * v) + Mathf.Abs(v) - (rx + rz) * .78f;
                float face = Mathf.Max(Mathf.Max(Mathf.Abs(u + .12f * v) - extentX, Mathf.Abs(v) - extentZ), corner * .707f);
                float safe = (Mathf.Sqrt(u * u / (rx * rx) + v * v / (rz * rz)) - 1) * Mathf.Min(rx, rz);
                float edge = Mathf.Min(face, safe - 1.95f);
                float along = Vector2.Dot(d, spurAxis);
                float across = Mathf.Abs(spurAxis.x * d.y - spurAxis.y * d.x + 3.6f * Mathf.Sin(along * .088f));
                float reach = 33 + 17 * (.5f + .5f * Mathf.Sin(phase));
                float tailWidth = Mathf.Lerp(20, 5, Mathf.InverseLerp(10, reach, along));
                float tailEdge = Mathf.Max(across - tailWidth, Mathf.Max(-along, along - reach));
                float tailDrop = Mathf.Max(0, along - 15) * (.21f + .13f * Mathf.Cos(phase));
                float shoulder = peak.height - tailDrop - Mathf.Max(0, tailEdge) * 1.4f;
                float apron = Mathf.Max(peak.height - Mathf.Max(0, edge) * 1.65f, shoulder);

                // Keep the landing window low enough for the rod to clear taller neighbouring rock.
                float limit = peak.height + Mathf.Max(0, safe - 2) * 1.25f;
                float merged = Mathf.Max(substrate, apron);
                return Mathf.Lerp(Mathf.Min(merged, limit), merged, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(36, 65, radius)));
            }
        }

        readonly Flank[] flanks;
        readonly Shelf[] shelves;
        readonly Vector3[] foothill;
        readonly float[] lateralTangent, gradeTangent;

        internal MountainRidgelineV3(MountainConfigV3 config, MountainPeakV3[] terraces)
        {
            flanks = config.ridges.SelectMany(ridge => ridge.crest.Zip(ridge.crest.Skip(1),
                (a, b) => new Flank(a, b, ridge.halfWidth, ridge.crestHalfWidth, ridge.flankBias))).ToArray();
            var crests = config.ridges.SelectMany(ridge => ridge.crest).ToArray();
            shelves = terraces.Select((peak, index) => new Shelf(peak, index, crests)).ToArray();
            foothill = config.entryTrail;
            lateralTangent = Tangents(foothill.Select(p => p.x).ToArray());
            gradeTangent = Tangents(foothill.Select(p => p.y).ToArray());
        }

        float[] Tangents(float[] samples)
        {
            var tangents = new float[samples.Length];
            for (int i = 1; i < samples.Length - 1; i++)
            {
                float left = (samples[i] - samples[i - 1]) / (foothill[i].z - foothill[i - 1].z);
                float right = (samples[i + 1] - samples[i]) / (foothill[i + 1].z - foothill[i].z);
                if (left * right > 0) tangents[i] = 2 * left * right / (left + right);
            }
            return tangents;
        }

        internal static float StartCrownWeight(float relativeZ) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-54, -29, relativeZ));

        internal float Height(Vector2 p, float floor)
        {
            float weathering = Mathf.PerlinNoise(p.x * .027f, p.y * .021f) - .5f;
            float wallTexture = Mathf.PerlinNoise(p.x * .085f + 19, p.y * .059f + 23) - .5f;
            float fracture = 17 * weathering + 5 * wallTexture;
            float band = Mathf.InverseLerp(-174, 230, p.y);
            float interior = Mathf.Lerp(93, 134, band) + 11 * weathering;
            float bedWidth = 205 + 27 * Mathf.Sin(p.y * .014f);
            float bed = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(bedWidth, bedWidth + 79, Mathf.Abs(p.x)))) *
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-395, -241, p.y)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(260, 377, p.y)));
            float substrate = Mathf.Lerp(floor, interior, bed);
            float rock = substrate;
            foreach (var flank in flanks)
                rock = Mathf.Max(rock, flank.Height(p, fracture, substrate));
            rock += (weathering * 8 + wallTexture * 4) * Mathf.InverseLerp(floor, floor + 45, rock);

            // Assemble shelf surfaces against the same substrate. Peak order cannot change overlaps.
            float apron = rock;
            float roof = float.PositiveInfinity;
            foreach (var shelf in shelves)
            {
                float sample = shelf.Height(p, rock);
                apron = Mathf.Max(apron, sample);
                if (sample < rock) roof = Mathf.Min(roof, sample);
                if ((shelf.peak.position - p).sqrMagnitude < 27 * 27)
                    roof = Mathf.Min(roof, sample + Mathf.Max(0, Vector2.Distance(shelf.peak.position, p) - 20) * 1.35f);
            }
            return Mathf.Min(apron, roof);
        }

        internal float Foothill(Vector2 p, float rock)
        {
            if (foothill.Length == 0 || p.y < foothill[0].z - 69 || p.y > foothill[^1].z + 4) return rock;
            var profile = Approach(p.y);
            float lateral = p.x - profile.x;
            float ascent = Mathf.InverseLerp(foothill[1].z, foothill[^2].z, p.y);
            float width = Mathf.Lerp(89, 65, ascent) * (1 + .23f * Mathf.Sign(lateral));
            float offset = Mathf.Abs(lateral) / width;
            float tilt = .14f * Mathf.Sin((p.y - foothill[1].z - 12) * .023f);
            tilt *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(foothill[^2].z - 20, foothill[^2].z - 2, p.y));
            float bench = profile.y + tilt * lateral - 29 * offset * offset;
            float toe = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(foothill[0].z - 69, foothill[0].z, p.y));
            float crown = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(foothill[^2].z, foothill[^1].z + 4, p.y));
            float influence = toe * crown * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.37f, 1, offset)));
            return Mathf.Lerp(rock, bench, influence);
        }

        internal Vector3 Approach(float z)
        {
            int i = 0;
            while (i < foothill.Length - 2 && z > foothill[i + 1].z) i++;
            var from = foothill[i];
            var to = foothill[i + 1];
            float dz = to.z - from.z;
            float t = Mathf.InverseLerp(from.z, to.z, z);
            float blend = t * t * (3 - 2 * t);
            float left = t * (1 - t) * (1 - t) * dz;
            float right = -t * t * (1 - t) * dz;
            float center = Mathf.Lerp(from.x, to.x, blend) + left * lateralTangent[i] + right * lateralTangent[i + 1];
            float grade = Mathf.Lerp(from.y, to.y, blend) + left * gradeTangent[i] + right * gradeTangent[i + 1];
            return new Vector3(center, grade, z);
        }
    }
}
