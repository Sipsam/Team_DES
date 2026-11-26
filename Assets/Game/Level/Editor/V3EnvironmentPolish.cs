using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinEdge.Levels.Editor
{
    public static class V3EnvironmentPolish
    {
        const string ContextFolder = "Assets/Game/Level/Data/Generated/V3Environment";
        const string ContextName = "V3 Geological Context";

        [MenuItem("Tools/Twin Edge/V3 World/Refresh Environmental Geology")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath);
            var valley = ValleyGeneratorV3.Config();
            var fortress = FortressGeneratorV3.Config();
            string parent = Path.GetDirectoryName(ContextFolder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(ContextFolder))
                AssetDatabase.CreateFolder(parent, Path.GetFileName(ContextFolder));

            GameObject context = scene.GetRootGameObjects().SingleOrDefault(root => root.name == ContextName);
            if (!context) context = new GameObject(ContextName);
            if (context.GetComponentInChildren<Collider>(true))
                throw new InvalidOperationException("Environmental geology cannot contain colliders.");

            ApplyGorge(context.transform, valley);
            ApplyValleyMargins(context.transform, valley);
            ApplyRiverEnds(context.transform, valley);
            ApplyCastleApron(context.transform, fortress);

            foreach (var section in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ValleySectionV3>()))
            {
                var terrain = section.generated.transform.Find("Terrain").GetComponent<MeshFilter>();
                RefreshValleySurface(section.config, section.part, terrain);
            }

            var surfaces = V3WorldMaterials.PrepareFortressSurfaces();
            var fortressRenderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FortressSectionV3>())
                .SelectMany(section => section.generated.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            foreach (var (original, surface) in surfaces)
            foreach (var renderer in fortressRenderers)
            {
                var materials = renderer.sharedMaterials;
                if (!materials.Contains(original)) continue;
                renderer.sharedMaterials = materials.Select(material => material == original ? surface : material).ToArray();
            }

            var rock = V3WorldMaterials.Rock;
            rock.SetTextureScale("_BaseMap", new Vector2(.067f, .074f));
            rock.SetFloat("_BumpScale", .58f);
            EditorUtility.SetDirty(rock);

            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the V3 environment.");
            AssetDatabase.SaveAssets();
            var meshes = context.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh).ToArray();
            Debug.Log($"V3 ENVIRONMENT {meshes.Length} visual meshes, {meshes.Sum(mesh => (long)mesh.vertexCount)} vertices, " +
                $"{meshes.Sum(mesh => (long)mesh.GetIndexCount(0)/3)} triangles, no colliders");
        }

        internal static void RefreshValleySurface(ValleyConfigV3 config, ValleyPartV3 part, MeshFilter filter)
        {
            if (part is not (ValleyPartV3.CanyonDescent or ValleyPartV3.River))
                return;
            var collision = filter.GetComponent<MeshCollider>().sharedMesh;
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(collision)).Replace('\\', '/');
            string assetPath = folder + "/TerrainSurface.asset";
            var vertices = collision.vertices;
            float minZ = collision.bounds.min.z;
            float maxZ = collision.bounds.max.z;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                bool canyon = part == ValleyPartV3.CanyonDescent;
                var centre = canyon ? config.CanyonStairProfile(p.z) : ValleyGeneratorV3.RiverSample(config, p.z);
                float protectedWidth = canyon ? config.walkWidth * 2.5f : ValleyGeneratorV3.Width(config, p.z) * .5f + 30;
                float verge = Mathf.Abs(p.x - centre.x) - protectedWidth;
                float seam = Ease(0, 32, Mathf.Min(p.z - minZ, maxZ - p.z));
                float relief = Ease(0, 105, verge) * seam;
                if (relief == 0) continue;

                // Shift the rock strata across each bank so both walls soften at different positions.
                float warp = 37 * Mathf.Sin(p.x * .011f + p.z * .002f) +
                    20 * Mathf.Sin(p.x * .029f - p.z * .009f);
                float shiftedZ = Mathf.Clamp(p.z + warp, minZ, maxZ);
                float strata = Mathf.Clamp(ValleyGeneratorV3.Height(config, p.x, shiftedZ) - p.y, -30, 30);
                float broad = Mathf.PerlinNoise(p.x * .015f + p.z * .004f, p.z * .018f - p.x * .003f) - .5f;
                float faults = Mathf.PerlinNoise(p.x * .048f + p.z * .016f, p.z * .011f - p.x * .023f) - .5f;
                p.y += relief * (.74f * strata + 25 * broad + 13 * faults);
                vertices[i] = p;
            }
            filter.sharedMesh = WriteMesh(assetPath, "TerrainSurface", vertices, collision.triangles);
            EditorUtility.SetDirty(filter);
        }

        static void ApplyGorge(Transform context, ValleyConfigV3 valley)
        {
            AddPatch(context, "BridgeGorge", 58, 46, (u,v) =>
            {
                float z = Mathf.Lerp(valley.bridgeStart.z - 64, valley.bridgeEnd.z + 54, v);
                float channel = valley.bridgeStart.x + 38 * Mathf.Sin(v * 5.1f + 1.1f);
                float x = channel + Mathf.Lerp(-360, 345, u);
                float shoulder = Ease(20, 230, Mathf.Abs(x - channel));
                float strike = Mathf.PerlinNoise(x * .011f + z * .003f, z * .013f - x * .006f);
                float fractures = Mathf.PerlinNoise(x * .042f, z * .038f + 7);
                float apron = Mathf.Max(1 - Ease(0, .24f, v), Ease(.76f, 1, v));
                float floor = valley.bridgeStart.y - valley.canyonDepth;
                float y = floor - 61 + shoulder * (68 + 88 * strike) + apron * 42 +
                    21 * (fractures - .5f);
                return new Vector3(x,y,z);
            });
        }

        static void ApplyValleyMargins(Transform context, ValleyConfigV3 valley)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int direction = side;
                AddPatch(context, side < 0 ? "WestRangeFoot" : "EastRangeFoot", 34, 92, (u,v) =>
                {
                    float z = Mathf.Lerp(valley.bridgeEnd.z-60, 2300,v);
                    float cover = direction < 0 ? 1-u : u;
                    float foothill = 16*Mathf.Sin(z*.017f+direction) + 11*Mathf.Sin(z*.037f-direction);
                    float x = direction * (345 + cover * (320 + foothill));
                    float massif = Mathf.Pow(Mathf.PerlinNoise(z*.0051f+18,cover*3.4f+direction*3),1.4f);
                    float rock = 36*Mathf.PerlinNoise(x*.016f,z*.023f+9);
                    float outer = 1 - Ease(.78f, 1, cover);
                    float ends = Ease(0, .16f, v) * (1 - Ease(.86f, 1, v));
                    float y = -112 + outer * ends * Ease(0, .46f, cover) * (42 + massif * 246 + rock);
                    return new Vector3(x,y,z);
                });
            }
        }

        static void ApplyRiverEnds(Transform context, ValleyConfigV3 valley)
        {
            AddRiverHeadwall(context,"LowerRiverBend",valley, valley.downstreamEnd.z, -1);
            AddRiverHeadwall(context,"UpperRiverBend",valley, valley.upstreamEnd.z, 1);
        }

        static void AddRiverHeadwall(Transform context, string name, ValleyConfigV3 valley, float z, int direction)
        {
            var end = ValleyGeneratorV3.RiverSample(valley,z);
            AddPatch(context, name, 20, 17, (u,v) =>
            {
                float x = end.x + Mathf.Lerp(-55,55,u);
                float worldZ = end.z + Mathf.Lerp(-48,48,v);
                float diagonal = (x-end.x)*.21f*direction;
                float dz = (worldZ-z)*direction-9+diagonal;
                float r = new Vector2((x-end.x)/52,dz/39).magnitude;
                float dome = Mathf.Pow(Mathf.Clamp01(1-r),.8f);
                float strata = 19*Mathf.PerlinNoise(x*.036f,worldZ*.047f);
                float y = end.y-11+dome*(40+strata);
                return new Vector3(x,y,worldZ);
            });
        }

        static void ApplyCastleApron(Transform context, FortressConfigV3 fortress)
        {
            float west = fortress.curtain.Min(p => p.x)+fortress.origin.x;
            float east = fortress.curtain.Max(p => p.x)+fortress.origin.x;
            float front = fortress.curtain.Min(p => p.y)+fortress.origin.z;
            float rear = fortress.curtain.Max(p => p.y)+fortress.origin.z;
            for (int side = -1; side <= 1; side += 2)
            {
                int direction = side;
                AddPatch(context, side < 0 ? "CastleWestBedrock" : "CastleEastBedrock", 24, 43, (u,v) =>
                {
                    float z = Mathf.Lerp(front-44,rear+67,v);
                    float slope = direction < 0 ? 1-u : u;
                    float edge = direction < 0 ? west : east;
                    float x = edge+direction*(slope*(136+12*Mathf.Sin(z*.027f))-2);
                    float noise = Mathf.PerlinNoise(x*.022f,z*.019f);
                    float top = fortress.origin.y-31+21*(noise-.5f);
                    float y = Mathf.Lerp(top, -74, Ease(0,1,slope));
                    y -= 26 * (1-Ease(0,.2f,Mathf.Min(v,1-v)));
                    return new Vector3(x,y,z);
                });
            }
        }

        static void AddPatch(Transform parent, string name, int columns, int rows, Func<float,float,Vector3> point)
        {
            var vertices = new List<Vector3>((columns+1)*(rows+1));
            var indices = new List<int>(columns*rows*6);
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                int i = vertices.Count;
                vertices.Add(point(column/(float)columns, row/(float)rows));
                if (row == 0 || column == 0) continue;
                int stride = columns+1;
                indices.Add(i-stride-1);
                indices.Add(i-1);
                indices.Add(i-stride);
                indices.Add(i-stride);
                indices.Add(i-1);
                indices.Add(i);
            }
            var mesh = WriteMesh(ContextFolder+"/"+name+".asset",name,vertices.ToArray(),indices.ToArray());
            var item = parent.Find(name);
            if (!item)
            {
                item = new GameObject(name).transform;
                item.SetParent(parent,false);
                item.gameObject.AddComponent<MeshFilter>();
                var renderer = item.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = V3WorldMaterials.Rock;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            var filter = item.GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            EditorUtility.SetDirty(filter);
        }

        static Mesh WriteMesh(string path, string name, Vector3[] vertices, int[] indices)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh,path);
            }
            mesh.Clear();
            mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static float Ease(float start, float end, float value) =>
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,value));
    }
}
