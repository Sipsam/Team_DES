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
            readonly float phase, rimVariation;

            internal Shelf(MountainPeakV3 peak, int index, Vector3[] crests)
            {
                this.peak = peak;
                float angle = peak.rotation * Mathf.Deg2Rad;
                major = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                minor = new Vector2(-major.y, major.x);
                phase = index * 2.39996f;
                // The upper shelves already have supported carry lines near their edges.
                rimVariation = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(48, 90, peak.position.y));
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
                // Keep the authored support ellipse below an irregular, jointed rock rim.
                float joints = Mathf.Sin(u * .19f + v * .11f + phase)
                    + .55f * Mathf.Sin(v * .31f - u * .08f - phase);
                const float supportRim = 1.95f;
                float rim = supportRim + rimVariation * Mathf.Max(0, 1.4f + joints - supportRim);
                float edge = Mathf.Min(face, safe - rim);
                float along = Vector2.Dot(d, spurAxis);
                float across = Mathf.Abs(spurAxis.x * d.y - spurAxis.y * d.x + 3.6f * Mathf.Sin(along * .088f));
                float reach = 33 + 17 * (.5f + .5f * Mathf.Sin(phase));
                float tailWidth = Mathf.Lerp(20, 5, Mathf.InverseLerp(10, reach, along));
                float tailEdge = Mathf.Max(across - tailWidth, Mathf.Max(-along, along - reach));
                float tailDrop = Mathf.Max(0, along - 15) * (.21f + .13f * Mathf.Cos(phase));
                float shoulder = peak.height - tailDrop - Mathf.Max(0, tailEdge) * 1.4f;
                // Leave room for the triangles and contact probes around the authored support ellipse.
                float clearOfSupport = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.6f, 6f, safe));
                float drop = Mathf.Lerp(Mathf.Max(0, edge), RoundPositiveEdge(edge, 4f), clearOfSupport);
                float apron = Mathf.Max(peak.height - drop * 1.65f, shoulder);

                // Keep the landing window low enough for the rod to clear taller neighbouring rock.
                float clearanceEdge = Mathf.Lerp(safe - 2, Mathf.Min(face + .8f, safe - rim), rimVariation);
                float limit = peak.height + Mathf.Max(0, clearanceEdge) * 1.25f;
                // Ease the apron into the existing rock without raising the valley floor.
                float relief = apron - substrate;
                float baseWeight = clearOfSupport
                    * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8, 16, peak.height - substrate));
                float merged = Mathf.Max(substrate, apron) - (Mathf.Max(0, relief) - RoundPositiveEdge(relief, 5f)) * baseWeight;
                return Mathf.Lerp(Mathf.Min(merged, limit), merged, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(36, 65, radius)));
            }
        }

        // Match both the height and tangent of the original profile at the ends of the fillet.
        internal static float RoundPositiveEdge(float distance, float width)
        {
            if (distance <= 0) return 0;
            if (distance >= width) return distance;
            float t = distance / width;
            return distance * t * (2 - t);
        }

        readonly Flank[] flanks;
        readonly Shelf[] shelves;
        readonly Vector2 westernRavineEnd;
        readonly float westernRavineDepth;
        readonly Vector3[] foothill;
        readonly float[] lateralTangent, gradeTangent;
        // These projecting shoulders keep adjacent but disconnected crowns separate in the pole graph.
        static readonly Vector2[] InteriorShoulders =
        {
            new(-40, -23), // Peak_08 / Peak_10
            new(82, 8),    // Peak_12 / Peak_15
            new(-14, 181)  // Peak_20 / Peak_23
        };

        internal MountainRidgelineV3(MountainConfigV3 config, MountainPeakV3[] terraces)
        {
            flanks = config.ridges.SelectMany(ridge => ridge.crest.Zip(ridge.crest.Skip(1),
                (a, b) => new Flank(a, b, ridge.halfWidth, ridge.crestHalfWidth, ridge.flankBias))).ToArray();
            var crests = config.ridges.SelectMany(ridge => ridge.crest).ToArray();
            shelves = terraces.Select((peak, index) => new Shelf(peak, index, crests)).ToArray();
            westernRavineEnd = config.westernRavineEnd;
            westernRavineDepth = config.westernRavineDepth;
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
            float surface = Mathf.Min(apron, roof);
            return ErodeWesternSaddle(p, SeparateRockMasses(p, surface, fracture));
        }

        float SeparateRockMasses(Vector2 p, float surface, float fracture)
        {
            // Keep the foothill, western ravine and outer skyline outside the interior erosion.
            float interior = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(132, 166, Mathf.Abs(p.x))))
                * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-196, -168, p.y))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(183, 224, p.y)));
            if (interior <= 0) return surface;

            foreach (var shoulder in InteriorShoulders)
                interior *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22, 31, Vector2.Distance(p, shoulder)));

            float first = float.PositiveInfinity, second = float.PositiveInfinity;
            float support = 1;
            foreach (var shelf in shelves)
            {
                var peak = shelf.peak;
                float radius = Vector2.Distance(p, peak.position);
                if (radius > 80) continue;

                // Offset and rotate the lower buttress relative to the crown. The seams
                // between adjacent buttresses then wander instead of tracing the rims.
                float angle = peak.rotation * Mathf.Deg2Rad;
                var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var offset = p - peak.position - axis * (5 + 4 * peak.asymmetry);
                float along = Vector2.Dot(offset, axis);
                float across = axis.x * offset.y - axis.y * offset.x;
                float distance = Mathf.Sqrt(along * along * .65f + across * across);
                if (distance < first)
                {
                    second = first;
                    first = distance;
                }
                else second = Mathf.Min(second, distance);

                float outsideCrown = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(
                    Mathf.Max(peak.summitSize.x, peak.summitSize.y) * .5f + 2, 34, radius));
                float belowCrown = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5, 17, peak.height - surface));
                support = Mathf.Min(support, Mathf.Max(outsideCrown, belowCrown));
            }

            if (float.IsPositiveInfinity(second)) return surface;
            float apron = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17, 37, first));
            float seam = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 13, second - first + fracture * .32f));
            float erosion = interior * support * apron;
            float floor = surface - erosion * (7 + 14 * seam);
            float selected = Mathf.Max(
                Mathf.Max(GullyBand(p.y, -170, -124), GullyBand(p.y, -61, 2)),
                Mathf.Max(GullyBand(p.y, 34, 115), GullyBand(p.y, 150, 207)));
            float valleyFloor = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.68f, .96f, seam))
                * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.62f, .9f, erosion));
            return floor - selected * valleyFloor * Mathf.Lerp(4, 8, Mathf.InverseLerp(-5, 5, fracture));
        }

        static float GullyBand(float z, float from, float to) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from - 9, from, z))
            * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(to, to + 9, z)));

        float ErodeWesternSaddle(Vector2 p, float rock)
        {
            float west = p.x - westernRavineEnd.x;
            if (westernRavineDepth <= 0 || west >= 0) return rock;

            // The ravine runs down the flank and fades out in the low basin and at the inner shoulder.
            float channelZ = westernRavineEnd.y + .16f * (west + 45) + 5 * Mathf.Sin((west + 55) * .041f);
            float channel = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5, 15, Mathf.Abs(p.y - channelZ)));
            float shoulder = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-24, 0, west));
            float relief = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(25, 85, rock));
            return rock - westernRavineDepth * channel * shoulder * relief;
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
            float lowerShoulder = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(
                foothill[1].z + 28, foothill[^3].z, p.y));
            float bench = profile.y + tilt * lateral - Mathf.Lerp(29, 43, lowerShoulder) * offset * offset;
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
