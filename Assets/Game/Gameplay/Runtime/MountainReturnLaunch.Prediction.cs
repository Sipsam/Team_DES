using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    public sealed partial class MountainReturnLaunch
    {
        const float PreviewInterval = .08f;
        const int RingSegments = 48;
        static readonly int PreviewColor = Shader.PropertyToID("_BaseColor");

        internal readonly struct LandingPrediction
        {
            internal readonly bool HasContact;
            internal readonly Vector3 Feet;
            internal readonly RaycastHit Surface;
            internal readonly string Summit;

            internal LandingPrediction(Vector3 feet, RaycastHit surface, string summit)
            {
                HasContact = true;
                Feet = feet;
                Surface = surface;
                Summit = summit;
            }
        }

        internal LandingPrediction Prediction { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal LandingPrediction ReleasedPrediction { get; private set; }
        internal bool PredictionVisible => landingRing != null && landingRing.enabled;
#endif
        bool chargeEndedLocally;
        float nextPreviewAt;
        RaycastHit[] sweepHits;
        MeshRenderer landingRing;
        Mesh previewMesh;
        MaterialPropertyBlock previewProperties;

        void UpdatePrediction()
        {
            if (Time.unscaledTime < nextPreviewAt) return;
            nextPreviewAt = Time.unscaledTime + PreviewInterval;
            Prediction = PredictLanding(config.Velocity(yaw, pitch, Charge));
            if (!Prediction.HasContact)
            {
                if (landingRing != null) landingRing.enabled = false;
                return;
            }

            if (landingRing == null && !CreatePreview()) return;
            var tint = Prediction.Summit != null ? new Color(.18f, 1f, .72f) : new Color(1f, .38f, .16f);
            landingRing.transform.SetPositionAndRotation(Prediction.Surface.point + Prediction.Surface.normal * .18f,
                Quaternion.FromToRotation(Vector3.up, Prediction.Surface.normal));
            previewProperties.SetColor(PreviewColor, tint);
            landingRing.SetPropertyBlock(previewProperties);
            landingRing.enabled = true;
        }

        internal LandingPrediction PredictLanding(Vector3 initial)
        {
            if (!controller.enabled) return default;
            sweepHits ??= new RaycastHit[24];
            var physics = gameObject.scene.GetPhysicsScene();
            var start = transform.position;
            var feet = start;
            var sampleVelocity = initial;
            var centerOffset = transform.TransformPoint(controller.center) - start;
            var scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfSpan = Mathf.Max(0, controller.height * Mathf.Abs(scale.y) * .5f - radius);
            var span = Vector3.up * halfSpan;
            float walkable = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
            int layers = 0;
            for (int layer = 0; layer < 32; layer++)
                if (!Physics.GetIgnoreLayerCollision(gameObject.layer, layer)) layers |= 1 << layer;
            layers = (layers | controller.includeLayers.value) & ~controller.excludeLayers.value;
            float elapsed = 0, remaining = 0;

            while (elapsed < config.maxFlightSeconds)
            {
                if (remaining <= MountainLaunchConfig.SubstepEpsilon) remaining = Time.fixedDeltaTime;
                float dt = Mathf.Min(Mathf.Min(remaining, MountainLaunchConfig.MaxSubstep), config.maxFlightSeconds - elapsed);
                var move = config.Step(ref sampleVelocity, dt);
                remaining -= dt;
                elapsed += dt;
                float distance = move.magnitude;
                if (distance >= controller.minMoveDistance && distance > 0)
                {
                    var center = feet + centerOffset;
                    var direction = move / distance;
                    int hits = physics.CapsuleCast(center - span, center + span, radius, direction, sweepHits,
                        distance, layers, QueryTriggerInteraction.Ignore);
                    if (hits == sweepHits.Length) return default;
                    float nearest = float.PositiveInfinity;
                    RaycastHit impact = default;
                    for (int i = 0; i < hits; i++)
                    {
                        var hit = sweepHits[i];
                        if (hit.distance >= nearest || hit.transform.IsChildOf(transform) ||
                            Physics.GetIgnoreCollision(controller, hit.collider)) continue;
                        nearest = hit.distance;
                        impact = hit;
                    }
                    if (float.IsFinite(nearest))
                    {
                        // A wall, ceiling, or moving character can change the subsequent controller path.
                        if (move.y >= 0 || sampleVelocity.y >= 0 || impact.normal.y < walkable ||
                            impact.collider is CharacterController || impact.rigidbody != null)
                            return default;

                        feet += direction * nearest;
                        // The capsule can touch a lip with no actual ground beneath its center.
                        if (!impact.collider.Raycast(new Ray(feet + Vector3.up * .5f, Vector3.down), out var ground, 1f) ||
                            ground.normal.y < walkable) return default;
                        string summit = Supported(feet, out string id) ? id : null;
                        return new LandingPrediction(feet, ground, summit);
                    }
                    feet += move;
                }
            }
            return default;
        }

        bool CreatePreview()
        {
            var support = CooperativePole.Current?.support;
            if (support == null) return false;
            var marker = new GameObject("Local launch landing ring");
            marker.transform.SetParent(transform, false);
            previewMesh = BuildRing();
            marker.AddComponent<MeshFilter>().sharedMesh = previewMesh;
            landingRing = marker.AddComponent<MeshRenderer>();
            landingRing.sharedMaterial = support.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            landingRing.shadowCastingMode = ShadowCastingMode.Off;
            landingRing.receiveShadows = false;
            previewProperties = new MaterialPropertyBlock();
            return true;
        }

        static Mesh BuildRing()
        {
            var vertices = new Vector3[RingSegments * 2];
            var triangles = new int[RingSegments * 6];
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / RingSegments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                int inner = i * 2;
                int next = (inner + 2) % vertices.Length;
                vertices[inner] = radial * 1.45f;
                vertices[inner + 1] = radial * 1.75f;
                int index = i * 6;
                triangles[index] = inner;
                triangles[index + 1] = next;
                triangles[index + 2] = inner + 1;
                triangles[index + 3] = inner + 1;
                triangles[index + 4] = next;
                triangles[index + 5] = next + 1;
            }
            return new Mesh { name = "Return launch landing ring", vertices = vertices, triangles = triangles };
        }

        void HidePrediction()
        {
            Prediction = default;
            nextPreviewAt = 0;
            if (landingRing != null) landingRing.enabled = false;
        }

        public override void OnDestroy()
        {
            if (previewMesh != null) Destroy(previewMesh);
            base.OnDestroy();
        }
    }
}
