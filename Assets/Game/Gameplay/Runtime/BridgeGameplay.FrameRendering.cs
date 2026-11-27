using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinEdge.Gameplay
{
    public sealed partial class BridgeGameplay
    {
        readonly List<Mesh> frameMeshes = new();

        void BatchFrameRenderers(GameObject template)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            // One local mesh per compatible material in the fixed module. All spans share these meshes.
            var worldToModule = template.transform.worldToLocalMatrix;
            var groups = template.GetComponentsInChildren<MeshRenderer>()
                .Where(part => CanBatchFramePart(part, worldToModule))
                .GroupBy(part => new
                {
                    part.sharedMaterial, part.gameObject.layer, part.renderingLayerMask,
                    part.shadowCastingMode, part.receiveShadows, part.lightProbeUsage, part.reflectionProbeUsage,
                    part.probeAnchor, part.motionVectorGenerationMode, part.allowOcclusionWhenDynamic,
                    part.sortingLayerID, part.sortingOrder
                });

            foreach (var group in groups)
            {
                var parts = group.ToArray();
                if (parts.Length < 2) continue;
                var geometry = new CombineInstance[parts.Length];
                int vertices = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var source = part.GetComponent<MeshFilter>().sharedMesh;
                    geometry[i] = new CombineInstance
                    {
                        mesh = source,
                        transform = worldToModule * part.localToWorldMatrix
                    };
                    vertices += source.vertexCount;
                }

                var key = group.Key;
                var batch = new Mesh { name = $"Bridge frame · {key.sharedMaterial.name}" };
                if (vertices > ushort.MaxValue) batch.indexFormat = IndexFormat.UInt32;
                batch.CombineMeshes(geometry);
                batch.UploadMeshData(true);
                frameMeshes.Add(batch);

                var visual = new GameObject(batch.name);
                visual.transform.SetParent(template.transform, false);
                visual.layer = key.layer;
                visual.AddComponent<MeshFilter>().sharedMesh = batch;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = key.sharedMaterial;
                renderer.renderingLayerMask = key.renderingLayerMask;
                renderer.shadowCastingMode = key.shadowCastingMode;
                renderer.receiveShadows = key.receiveShadows;
                renderer.lightProbeUsage = key.lightProbeUsage;
                renderer.reflectionProbeUsage = key.reflectionProbeUsage;
                renderer.probeAnchor = key.probeAnchor;
                renderer.motionVectorGenerationMode = key.motionVectorGenerationMode;
                renderer.allowOcclusionWhenDynamic = key.allowOcclusionWhenDynamic;
                renderer.sortingLayerID = key.sortingLayerID;
                renderer.sortingOrder = key.sortingOrder;
                foreach (var part in parts) part.enabled = false;
            }
        }

        static bool CanBatchFramePart(MeshRenderer part, Matrix4x4 worldToModule)
        {
            var material = part.sharedMaterial;
            if (!part.enabled || part.forceRenderingOff || part.additionalVertexStreams || part.HasPropertyBlock() ||
                part.lightmapIndex >= 0 || part.realtimeLightmapIndex >= 0 || part.sharedMaterials.Length != 1 ||
                !material || material.shader.name != "Universal Render Pipeline/Lit" ||
                material.renderQueue > (int)RenderQueue.GeometryLast ||
                (worldToModule * part.localToWorldMatrix).determinant <= 0)
                return false;

            return part.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh is { } mesh &&
                mesh.isReadable && mesh.subMeshCount == 1 && mesh.GetTopology(0) == MeshTopology.Triangles;
        }

        void ReleaseFrameMeshes()
        {
            foreach (var mesh in frameMeshes) Destroy(mesh);
            frameMeshes.Clear();
        }

        public override void OnDestroy()
        {
            ReleaseFrameMeshes();
            base.OnDestroy();
        }
    }
}
