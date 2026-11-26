using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class RiverGameplay
    {
        [SerializeField] Material warningMaterial;
        MaterialPropertyBlock warningColors;
        MaterialPropertyBlock damageColors;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color DamageColor = new(1, .34f, .13f);
        const float DamageFlashSeconds = .45f;
        ParticleSystem rain;
        public event Action<Vector3, string> OnContact;
        Transform NewView(string label, Transform parent)
        {
            var view = new GameObject(label).transform;
            view.SetParent(parent, false);
            return view;
        }

        static Transform Visual(GameObject prefab, Transform parent)
        {
            if (prefab == null) throw new InvalidOperationException("A V3 river source model is unassigned.");
            var child = Instantiate(prefab, parent, false);
            foreach (var preview in child.GetComponentsInChildren<MedievalMotionPreview>(true)) preview.enabled = false;
            foreach (var collider in child.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return child.transform;
        }

        static Bounds VisualBounds(Transform visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("River source model has no visible renderer: " + visual.name);
            var box = renderers[0].bounds;
            foreach (var renderer in renderers) box.Encapsulate(renderer.bounds);
            return box;
        }

        void BuildDock(Collider dock, bool upper)
        {
            var root = NewView(upper ? "Upper river dock" : "Lower river dock", presentation);
            Bounds deck = dock.bounds;
            Transform boards = Visual(dockModel, root);
            var original = VisualBounds(boards);
            boards.localScale = Vector3.Scale(boards.localScale,
                new Vector3(deck.size.x / original.size.x, 1.5f / original.size.y, deck.size.z / original.size.z));
            var resized = VisualBounds(boards);
            boards.position += deck.center - resized.center + Vector3.up * (deck.extents.y - resized.extents.y);
            var blockoutRenderer = dock.GetComponent<Renderer>();
            if (blockoutRenderer != null) blockoutRenderer.enabled = false;

            Transform stairs = Visual(stairsModel, root);
            stairs.rotation = Quaternion.Euler(0, 90, 0);
            var steps = VisualBounds(stairs);
            stairs.position += new Vector3(deck.max.x + steps.extents.x * .35f, deck.max.y - steps.extents.y,
                deck.center.z) - steps.center;
            var boarding = root.gameObject.AddComponent<RiverDockInteraction>();
            boarding.River = this;
            boarding.Upper = upper;
            var trigger = root.gameObject.AddComponent<BoxCollider>();
            trigger.center = deck.center + Vector3.up * 1.4f;
            trigger.size = new Vector3(deck.size.x, 3.5f, deck.size.z);
            trigger.isTrigger = true;
        }

        void BuildHull(int i)
        {
            var hull = hulls[i];
            hull.View = NewView(i == 0 ? "Linked raft" : $"Race raft {i}", presentation);
            var visual = Visual(i == 0 ? sharedModel : soloModel, hull.View);
            damageColors ??= new MaterialPropertyBlock();
            damageColors.SetColor(BaseColor, DamageColor);
            hull.FlashMaterials = visual.GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.enabled)
                .SelectMany(renderer => renderer.sharedMaterials.Select((material, slot) => (renderer, material, slot)))
                .Where(entry => entry.material != null && entry.material.HasProperty(BaseColor))
                .Select(entry => (entry.renderer, entry.slot)).ToArray();
            hull.Flashing = false;
            if (i == 0)
            {
                var pair = visual.GetComponent<RaftPair>();
                hull.Seats = new[] { pair.leftSeat, pair.rightSeat };
            }
            else hull.Seats = new[] { visual.GetComponentsInChildren<Transform>().Single(child => child.name == "Seat") };
            hull.Oars = new Transform[i == 0 ? 2 : 1];
            for (int k = 0; k < hull.Oars.Length; k++)
            {
                var pivot = NewView("Oar pivot", hull.View);
                pivot.localPosition = new Vector3(i == 0 ? (k == 0 ? -1.65f : 1.65f) : -.65f, .45f, .05f);
                Visual(oarModel, pivot);
                hull.Oars[k] = pivot;
            }
            hull.Collider = hull.View.gameObject.AddComponent<BoxCollider>();
            hull.Collider.size = new Vector3(Radius(i) * 2, .7f, Radius(i) * 2);
            hull.Collider.isTrigger = true;
            hull.View.gameObject.SetActive(false);
        }

        void BuildHazards()
        {
            warningColors = new MaterialPropertyBlock();
            warningColors.SetColor("_BaseColor", new Color(1, .23f, .08f, 1));
            foreach (var h in hazards)
            {
                h.View = NewView(h.Kind.ToString(), presentation);
                var model = h.Kind switch
                {
                    HazardKind.Rock => rockModel,
                    HazardKind.Driftwood => driftwoodModel,
                    HazardKind.Fish => fishModel,
                    HazardKind.Crocodile => crocodileModel,
                    _ => whirlpoolModel
                };
                var visual = Visual(model, h.View);
                var bounds = VisualBounds(visual);
                float diameter = Mathf.Max(bounds.size.x, bounds.size.z);
                visual.localScale *= (h.Radius * 2) / diameter;
                if (h.Kind is HazardKind.Whirlpool or HazardKind.Rock or HazardKind.Driftwood)
                {
                    bounds = VisualBounds(visual);
                    if (h.Kind == HazardKind.Whirlpool)
                    {
                        Vector3 scale = visual.localScale;
                        scale.y *= Mathf.Min(1f, .7f / bounds.size.y);
                        visual.localScale = scale;
                        bounds = VisualBounds(visual);
                    }
                    visual.position += h.View.position - bounds.center;
                    if (h.Kind == HazardKind.Whirlpool) visual.localPosition += Vector3.up * (.24f - bounds.extents.y);
                }
                else visual.localPosition -= Vector3.up * .42f;
                h.Jaw = visual.GetComponentInChildren<FantasyJaw>();
                if (h.Jaw != null) h.Jaw.preview = false;
                h.Collider = h.View.gameObject.AddComponent<SphereCollider>();
                h.Collider.radius = h.Radius;
                h.Collider.isTrigger = true;
                if (h.Kind is HazardKind.Fish or HazardKind.Crocodile)
                {
                    var warning = NewView("Attack warning", presentation).gameObject.AddComponent<LineRenderer>();
                    warning.loop = true;
                    warning.useWorldSpace = false;
                    warning.widthMultiplier = .27f;
                    warning.sharedMaterial = warningMaterial;
                    warning.SetPropertyBlock(warningColors);
                    warning.positionCount = 40;
                    for (int i = 0; i < warning.positionCount; i++)
                    {
                        float angle = i * Mathf.PI * 2 / warning.positionCount;
                        warning.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * h.Radius);
                    }
                    warning.gameObject.SetActive(false);
                    h.Warning = warning;
                }
                h.View.gameObject.SetActive(false);
            }
        }

        void HideHazards()
        {
            foreach (var h in hazards)
            {
                h.View.gameObject.SetActive(false);
                if (h.Warning != null) h.Warning.gameObject.SetActive(false);
            }
        }

        void ContactFeedback(int i)
        {
            var s = hulls[i].State;
            ContactRpc(s.Position, s.Contact);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void ContactRpc(Vector3 position, RiverContact cause)
        {
            OnContact?.Invoke(position, cause.ToString());
        }

        void RenderRiver()
        {
            RenderRain();
            float blend = 1 - Mathf.Exp(-18 * Time.deltaTime);
            for (int i = 0; i < hulls.Length; i++)
            {
                var hull = hulls[i];
                var s = Read(i);
                bool show = s.Status != RiverRaftStatus.Hidden && !(i == 0 && s.Status == RiverRaftStatus.Arrived);
                bool flash = show && DamageFlashActive(s);
                if (flash != hull.Flashing)
                {
                    foreach (var (renderer, slot) in hull.FlashMaterials)
                        renderer.SetPropertyBlock(flash ? damageColors : null, slot);
                    hull.Flashing = flash;
                }
                if (!show)
                {
                    hull.View.gameObject.SetActive(false);
                    continue;
                }

                bool snap = hull.RenderedVoyage != s.Voyage || !hull.View.gameObject.activeSelf;
                hull.RenderedVoyage = s.Voyage;
                Vector3 desired = s.Position;
                if (i > 0 && splitAt.Value > 0 && s.Status == RiverRaftStatus.Docked)
                {
                    float t = Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - splitAt.Value) / 1.25f);
                    desired = Vector3.Lerp(Read(0).Position + Vector3.right * (i == 1 ? -1.1f : 1.1f),
                        desired, Mathf.SmoothStep(0, 1, t));
                    desired.y = Height(desired);
                }
                if (s.Status == RiverRaftStatus.Destroyed) desired.y -= .9f;
                float pitch = s.Status == RiverRaftStatus.Destroyed ? 22 : 0;
                var rotation = Quaternion.Euler(pitch, s.Heading, s.Status == RiverRaftStatus.Destroyed ? 32 : 0);
                hull.View.SetPositionAndRotation(snap ? desired : Vector3.Lerp(hull.View.position, desired, blend),
                    snap ? rotation : Quaternion.Slerp(hull.View.rotation, rotation, blend));
                hull.View.gameObject.SetActive(true);
                hull.Collider.enabled = s.Status is RiverRaftStatus.Sailing or RiverRaftStatus.Docked;
                for (int k = 0; k < hull.Oars.Length; k++)
                {
                    float stroke = k == 0 ? s.LeftDrive : s.RightDrive;
                    float wave = Mathf.Sin((float)NetworkManager.ServerTime.Time * 6) * 38 * Mathf.Clamp(stroke, -1, 1);
                    hull.Oars[k].localRotation = Quaternion.Euler(wave, k == 1 ? -38 : 38, k == 1 ? -14 : 14);
                }
            }

            for (int i = 0; i < hazards.Length && i < hazardFeed.Count; i++)
            {
                var h = hazards[i];
                var s = hazardFeed[i];
                bool active = s.Phase != HazardPhase.Hidden;
                bool snap = !h.View.gameObject.activeSelf;
                h.View.gameObject.SetActive(active);
                if (h.Warning != null)
                {
                    bool warn = s.Phase == HazardPhase.Warning;
                    h.Warning.gameObject.SetActive(warn);
                    if (warn)
                    {
                        h.Warning.transform.position = s.Aim + Vector3.up * .16f;
                        h.Warning.widthMultiplier = .27f + Mathf.Abs(Mathf.Sin(Time.time * 9)) * .3f;
                    }
                }
                if (!active) continue;
                h.Collider.enabled = h.Kind is not (HazardKind.Fish or HazardKind.Crocodile) || s.Phase == HazardPhase.Strike;
                var delta = s.Aim - s.Position;
                delta.y = 0;
                float yaw = channel.Sample(s.Position.z).upstream.x * 45;
                if (s.Phase is HazardPhase.Strike or HazardPhase.Warning && delta.sqrMagnitude > .05f)
                    yaw = Quaternion.LookRotation(delta).eulerAngles.y;
                if (h.Kind == HazardKind.Whirlpool) yaw = (float)NetworkManager.ServerTime.Time * 100;
                h.View.SetPositionAndRotation(snap ? s.Position : Vector3.Lerp(h.View.position, s.Position, blend),
                    Quaternion.Euler(0, yaw, 0));
                if (h.Jaw != null) h.Jaw.SetOpen(s.Phase == HazardPhase.Warning ? .72f : s.Phase == HazardPhase.Strike ? 1 : .08f);
            }
        }

        void RenderRain()
        {
            if (!IsClient || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var player = NetworkManager.LocalClient?.PlayerObject;
            bool wet = player != null && Returning &&
                player.transform.position.z >= channel.Start - 18 && player.transform.position.z <= channel.End + 18 &&
                Mathf.Abs(player.transform.position.x - channel.Sample(player.transform.position.z).center.x) < 50;
            if (!wet)
            {
                if (rain != null && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            if (rain == null)
            {
                var emitter = NewView("Return rain", presentation);
                emitter.gameObject.SetActive(false);
                rain = emitter.gameObject.AddComponent<ParticleSystem>();
                var main = rain.main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = .85f;
                main.startSpeed = 25f;
                main.startSize = .028f;
                main.startColor = new Color(.65f, .79f, .86f, 1);
                var shape = rain.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(24, 24, .1f);
                var emission = rain.emission;
                emission.rateOverTime = 400;
                var renderer = emitter.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.maxParticleSize = .03f;
                renderer.lengthScale = 2.7f;
                renderer.velocityScale = .035f;
                renderer.sharedMaterial = warningMaterial;
                emitter.rotation = Quaternion.Euler(90, 0, 0);
                emitter.gameObject.SetActive(true);
            }
            rain.transform.position = player.transform.position + Vector3.up * 11;
            if (!rain.isPlaying) rain.Play();
        }

        bool DamageFlashActive(RiverRaftSnapshot state) => state.Hits > 0 &&
            NetworkManager.ServerTime.Time - state.DamagedAt < DamageFlashSeconds;

        internal string Hud(PlayerMovement player, out bool damaged)
        {
            damaged = false;
            if (!IsSpawned || channel == null || player == null) return null;
            ulong id = player.OwnerClientId;
            if (TrySeat(id, out int i, out int side, out var s))
            {
                damaged = DamageFlashActive(s);
                if (s.Status == RiverRaftStatus.Destroyed) return "Raft destroyed. Recovering at the departure dock.";
                if (s.Status == RiverRaftStatus.Docked) return $"{(side == 0 ? "Left" : "Right")} oar | Waiting for partner | E / Esc: leave";
                float stamina = side == 0 ? s.LeftStamina : s.RightStamina;
                string control = i == 0 ? (side == 0 ? "Left oar" : "Right oar") : "Race raft | A/D steer";
                return $"{control} | W/S row | Shift: strong stroke\nStamina {stamina:P0} | Hits {s.Hits}/{(i == 0 ? SharedLimit : SoloLimit)}";
            }

            Vector3 p = player.transform.position;
            if (Vector3.Distance(p, channel.UpperDock.bounds.center) <= 17 && split.Value)
                return Returning ? "Upper dock | Separate race rafts. E to board at the water edge." :
                    "Upper dock reached. The rafts have split.";
            if (Vector3.Distance(p, channel.LowerDock.bounds.center) <= 17)
            {
                if (Outbound) return "Lower dock | E to board at the water edge.";
                for (int slot = 1; slot < 3; slot++)
                {
                    var result = Read(slot);
                    if (result.FinishPlace > 0 && result.Finisher == id)
                        return $"Lower dock reached | Race place {result.FinishPlace}";
                }
            }
            return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal string DebugHud(PlayerMovement player)
        {
            if (!IsSpawned || channel == null || player == null) return null;
            if (!TrySeat(player.OwnerClientId, out int i, out _, out var s))
            {
                if (Hud(player, out _) == null) return null;
                i = Returning && player.transform.position.z > (channel.Start + channel.End) * .5f ? 1 : 0;
                s = Read(i);
            }
            var p = channel.Sample(s.Position.z);
            string nearby = string.Join(", ", Enumerable.Range(0, hazardFeed.Count).Select(index => (h: hazardFeed[index], index))
                .Where(item => item.h.Phase != HazardPhase.Hidden && Vector3.Distance(item.h.Position, s.Position) < 90)
                .Select(item => $"{hazards[item.index].Kind} {item.h.Phase}"));
            return $"River {p.progress:P0} | {s.Position:F1} | yaw {s.Heading:F0}\n" +
                $"Velocity {s.Velocity:F1} | current {s.Flow:F1} | drive {s.Drive:F2} | turn {s.Steer:F2}\n" +
                $"Oars L {s.LeftDrive:F2} / R {s.RightDrive:F2}\n" +
                $"Raft owner {OwnerClientId} | L {SeatLabel(s.Left)} / R {SeatLabel(s.Right)}\n" +
                $"Contact {s.Contact} | Recovery {s.Recovery}\n{nearby}";
        }

        static string SeatLabel(ulong id) => id == Empty ? "empty" : id.ToString();
#endif
    }
}
