using System;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class CastleGameplay
    {
        enum TrapKind { Fire, Spikes, Hammer, Arrow, Rock }
        [SerializeField, Min(.1f)] float trapWarning = 1.1f, trapAttack = 1.35f, trapRecovery = 2.95f;
        float TrapCycle => trapWarning + trapAttack + trapRecovery;
        const float SewerX = -32, SewerEntry = 237, SewerExit = 44, SewerHeight = 6, RampLength = 17;
        const float SewerWidth = 5.6f;

        sealed class Trap
        {
            public TrapKind Kind;
            public float Z, X;
            public Transform View;
            public GameObject WarningStripe;
            public BoxCollider Hitbox;
            public int Hit;
            public bool WasActive;
            public Vector3 Previous;
        }

        readonly Trap[] traps = new Trap[5];
        readonly double[] nextTrap = new double[5];
        NetworkList<double> activations, sewerPressTimes;
        readonly Transform[] sewerCaps = new Transform[Glyphs.Length];
        readonly Renderer[] sewerPlates = new Renderer[Glyphs.Length];
        MaterialPropertyBlock sewerPlateProperties;
        static readonly int SewerPlateColor = Shader.PropertyToID("_BaseColor");
        static readonly Color SewerAcceptedColor = new(.28f, 1f, .47f);
        const float SewerPressSeconds = .8f;

        void BuildSewer()
        {
            sewerPlateProperties = new MaterialPropertyBlock();
            var sewer = Group(returnLayout, "Sewer bypass");
            float deckSouth = SewerExit + RampLength, deckNorth = SewerEntry - RampLength;
            SewerSegment(sewer, new Vector3(SewerX, 0, SewerExit), new Vector3(SewerX, SewerHeight, deckSouth), false);
            SewerSegment(sewer, new Vector3(SewerX, SewerHeight, deckSouth), new Vector3(SewerX, SewerHeight, deckNorth), true);
            SewerSegment(sewer, new Vector3(SewerX, SewerHeight, deckNorth), new Vector3(SewerX, 0, SewerEntry), false);
            sewerDoors[0] = Door(sewer, "Sewer entrance", SewerX, SewerEntry + .1f, SewerWidth);
            sewerDoors[1] = Door(sewer, "Sewer exit", SewerX, SewerExit - .1f, SewerWidth);
            float z = SewerEntry + 2.8f;
            for (int glyph = 0; glyph < Glyphs.Length; glyph++)
            {
                var pad = Station(sewer, Action.SewerPad, -1, Stages, glyph,
                    new Vector3(-24 + glyph * 3.5f, .3f, z), "E: sewer " + Glyphs[glyph]);
                Block(pad.transform, "Sewer pad base", Vector3.down * .23f, new Vector3(1.98f, .14f, 1.98f), darkMaterial, false);
                var cap = Group(pad.transform, "Sewer pad cap");
                cap.localPosition = Vector3.up * .26f;
                sewerCaps[glyph] = cap;
                Block(cap, "Sewer plunger", Vector3.down * .35f, new Vector3(.95f, .25f, .95f), darkMaterial, false);
                sewerPlates[glyph] = Block(cap, "Sewer press plate", Vector3.down * .22f,
                    new Vector3(1.75f, .16f, 1.75f), accentMaterial, false).GetComponent<Renderer>();
                Symbol(cap, glyph, 1.45f);
                Label(pad.transform, Glyphs[glyph], Vector3.up * .8f, 0);
            }
            var clear = Station(sewer, Action.ClearSewer, -1, Stages, 0,
                new Vector3(-12, .9f, z), "E: clear your sewer entry");
            Label(clear.transform, "CLEAR", Vector3.up * .72f, 0);
            Label(sewer, "SEWER", new Vector3(SewerX, 3f, SewerEntry + .6f), 0, .26f);
        }

        void DrawSewerPads()
        {
            for (int glyph = 0; glyph < sewerPressTimes.Count; glyph++)
            {
                double elapsed = sewerPressTimes[glyph] < 0 ? SewerPressSeconds : Now - sewerPressTimes[glyph];
                float press = Mathf.Clamp01((float)elapsed / .08f) *
                    Mathf.Clamp01((SewerPressSeconds - (float)elapsed) / .22f);
                sewerCaps[glyph].localPosition = Vector3.up * Mathf.Lerp(.26f, .02f, press);
                sewerPlateProperties.SetColor(SewerPlateColor, Color.Lerp(accentMaterial.color, SewerAcceptedColor, press));
                sewerPlates[glyph].SetPropertyBlock(sewerPlateProperties);
            }
        }

        void SewerSegment(Transform root, Vector3 from, Vector3 to, bool covered)
        {
            var segment = Group(root, covered ? "Covered maintenance run" : "Sewer ramp");
            segment.localPosition = (from + to) * .5f;
            segment.localRotation = Quaternion.LookRotation(to - from, Vector3.up);
            float length = Vector3.Distance(from, to) + .40f;
            Block(segment, "Sewer deck", Vector3.down * .26f, new Vector3(SewerWidth, .52f, length), darkMaterial);
            Material lining = undergroundWallMaterial != null ? undergroundWallMaterial : stoneMaterial;
            foreach (float side in new[] { -1f, 1f })
                Block(segment, "Sewer side", new Vector3(side * (SewerWidth * .5f + .15f), 1.6f, 0),
                    new Vector3(.3f, covered ? 3.2f : 3.8f, length), lining);
            if (covered)
                Block(segment, "Sewer cover", Vector3.up * 3.25f, new Vector3(SewerWidth + .6f, .25f, length), lining);
        }

        bool NearSewer(Vector3 position)
        {
            var p = position - config.origin;
            return p.z >= SewerEntry - 5 && p.x < -7;
        }

        void BuildTraps()
        {
            float first = config.rooms[0].plan.yMin;
            float last = config.rooms[config.rooms.Length - 1].plan.yMax;
            for (int i = 0; i < traps.Length; i++)
            {
                float z = Mathf.Lerp(first, last, (i + .65f) / traps.Length);
                float x = i % 2 == 0 ? 0 : 4;
                var pivot = Group(returnLayout, ((TrapKind)i).ToString());
                pivot.localPosition = new Vector3(x, 0, z);
                var view = Group(pivot, "Moving obstacle");
                var prefab = trapModels != null && i < trapModels.Length ? trapModels[i] : null;
                Vector3 size = TrapSize((TrapKind)i);
                if (prefab != null) Model(view, prefab, size, fitAxes: i < 2);
                else Block(view, "Attack volume", Vector3.zero, size, accentMaterial, false);
                var warning = Block(pivot, "Warning stripe", new Vector3(0, .15f, 0), new Vector3(16, .08f, 2.2f),
                    accentMaterial, false);
                var hitbox = Group(pivot, "Attack trigger").gameObject.AddComponent<BoxCollider>();
                hitbox.isTrigger = true;
                hitbox.size = size;
                hitbox.enabled = false;
                traps[i] = new Trap { Kind = (TrapKind)i, Z = z, X = x, View = view, WarningStripe = warning, Hitbox = hitbox };
                // Low baffles funnel the ground route while the separate sewer passes above.
                TrapBaffle(z, config.keep.xMin, x - 8);
                TrapBaffle(z, x + 8, config.keep.xMax);
            }
        }

        void TrapBaffle(float z, float left, float right)
        {
            Block(returnLayout, "Trap corridor baffle", new Vector3((left + right) * .5f, 2, z),
                new Vector3(right - left, 4, .8f), stoneMaterial);
        }

        static Vector3 TrapSize(TrapKind kind) => kind switch
        {
            TrapKind.Fire => new Vector3(12, 2.1f, 1.6f),
            TrapKind.Spikes => new Vector3(13, 1.3f, 2),
            TrapKind.Hammer => new Vector3(3.2f, 2.8f, 3.2f),
            TrapKind.Arrow => new Vector3(2.6f, 1.25f, 1.55f),
            _ => new Vector3(2.8f, 2.8f, 2.8f)
        };

        void ResetTraps()
        {
            activations.Clear();
            for (int i = 0; i < nextTrap.Length; i++)
            {
                activations.Add(-1);
                nextTrap[i] = 0;
                if (traps[i] == null) continue;
                traps[i].Hit = 0;
                traps[i].WasActive = false;
            }
        }

        static Vector3 AttackPosition(Trap trap, float t)
        {
            float x = trap.X, z = trap.Z;
            return trap.Kind switch
            {
                TrapKind.Fire => new Vector3(x, .95f, z),
                TrapKind.Spikes => new Vector3(x, Mathf.Lerp(-.6f, .63f, Mathf.Sin(t * Mathf.PI)), z),
                TrapKind.Hammer => new Vector3(x + Mathf.Lerp(-7.6f, 7.6f, t), 1.2f + 3f * Mathf.Abs(2 * t - 1), z),
                TrapKind.Arrow => new Vector3(x + Mathf.Lerp(8.7f, -8.7f, t), 1.15f, z),
                _ => new Vector3(x + Mathf.Lerp(-7.2f, 6.9f, t), 1.35f, z + Mathf.Lerp(2.1f, -2.1f, t))
            };
        }

        void DrawTraps()
        {
            for (int i = 0; i < traps.Length && i < activations.Count; i++)
            {
                var trap = traps[i];
                double elapsed = activations[i] < 0 ? -1 : Now - activations[i];
                bool active = elapsed >= trapWarning && elapsed < trapWarning + trapAttack;
                SetActive(trap.WarningStripe, elapsed >= 0 && elapsed < trapWarning);
                SetActive(trap.View.gameObject, active);
                trap.Hitbox.enabled = active;
                if (!active) continue;
                float t = Mathf.Clamp01((float)(elapsed - trapWarning) / trapAttack);
                trap.View.position = config.origin + AttackPosition(trap, t);
                trap.Hitbox.transform.position = trap.View.position;
                if (trap.Kind == TrapKind.Rock) trap.View.localRotation = Quaternion.Euler(0, 0, -720 * t);
                if (trap.Kind == TrapKind.Hammer) trap.View.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(55, -55, t));
            }
        }

        void TickTrapDamage()
        {
            for (int i = 0; i < traps.Length; i++)
            {
                var trap = traps[i];
                if (Now >= nextTrap[i] && PlayerNearTrap(trap))
                {
                    activations[i] = Now;
                    nextTrap[i] = Now + TrapCycle;
                    trap.Hit = 0;
                    trap.WasActive = false;
                }

                double elapsed = activations[i] < 0 ? -1 : Now - activations[i];
                if (elapsed < trapWarning || elapsed >= trapWarning + trapAttack) continue;
                Vector3 point = AttackPosition(trap, Mathf.Clamp01((float)(elapsed - trapWarning) / trapAttack));
                var volume = new Bounds(point, trap.Hitbox.size);
                if (trap.WasActive)
                {
                    var old = new Bounds(trap.Previous, volume.size);
                    volume.Encapsulate(old);
                }
                trap.WasActive = true;
                trap.Previous = point;
                foreach (var peer in NetworkManager.ConnectedClientsList)
                {
                    int slot = Slot(peer.ClientId);
                    if (slot < 0 || peer.PlayerObject == null || (trap.Hit & (1 << slot)) != 0 || Now < graceUntil[slot]) continue;
                    var player = peer.PlayerObject;
                    Vector3 feet = player.transform.position - config.origin;
                    var controller = player.GetComponent<CharacterController>();
                    var body = new Bounds(feet + controller.center, new Vector3(controller.radius * 2,
                        controller.height, controller.radius * 2));
                    if (!volume.Intersects(body)) continue;
                    trap.Hit |= 1 << slot;
                    Damage(player, slot);
                }
            }
        }

        bool PlayerNearTrap(Trap trap)
        {
            foreach (var peer in NetworkManager.ConnectedClientsList)
            {
                if (peer.PlayerObject == null) continue;
                Vector3 p = peer.PlayerObject.transform.position - config.origin;
                if (Mathf.Abs(p.z - trap.Z) < 12 && Mathf.Abs(p.x - trap.X) < 12 && p.y < 3.5f && p.y > -2) return true;
            }
            return false;
        }

        int ActiveTrapCount()
        {
            int count = 0;
            for (int i = 0; i < activations.Count; i++)
                if (activations[i] >= 0 && Now - activations[i] < trapWarning + trapAttack) count++;
            return count;
        }
    }
}
