using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace TwinEdge.Levels.Editor
{
    public static class MountainGeneratorV3
    {
        public const string ScenePath = "Assets/Game/Level/Scenes/LevelBlockoutV3.unity";
        public const string Notes = "Docs/Development/Level/V3";
        const string ConfigPath = "Assets/Game/Level/Data/MountainConfigV3.asset";

        public static MountainConfigV3 Config()
        {
            var c = AssetDatabase.LoadAssetAtPath<MountainConfigV3>(ConfigPath);
            if (c != null) return c;
            c = ScriptableObject.CreateInstance<MountainConfigV3>();
            Populate(c);
            AssetDatabase.CreateAsset(c, ConfigPath);
            return c;
        }

        public static void Populate(MountainConfigV3 c)
        {
            var random = new System.Random(c.seed);
            c.smallPeaks.Clear();
            // An unordered, staggered field: no route indices or single prescribed chain.
            int rows = Mathf.CeilToInt(Mathf.Sqrt(c.smallPeakCount * c.clusterSize.y / c.clusterSize.x));
            int cols = Mathf.CeilToInt(c.smallPeakCount / (float)rows);
            for (int i = 0; i < c.smallPeakCount; i++)
            {
                int row = i / cols, col = i % cols;
                float u = (col + .5f + (row % 2 == 0 ? -.13f : .13f)) / cols - .5f;
                float v = (row + .5f) / rows - .5f;
                c.smallPeaks.Add(new MountainPeakV3 {
                    id = "Peak_" + (i+1).ToString("00"),
                    position = new Vector2(u + ((float)random.NextDouble()-.5f)*.035f, v + ((float)random.NextDouble()-.5f)*.045f),
                    height = Mathf.Lerp(137,174,row/(float)Mathf.Max(1,rows-1)) + random.Next(-10,11),
                    summitSize = new Vector2(random.Next(18,25),random.Next(18,28)),
                    rotation = random.Next(0,180), silhouette = .1f + (float)random.NextDouble()*.14f,
                    shoulderWidth = random.Next(4,7)
                });
            }
            if (c.boundary.Count == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int j = 0; j < 8; j++) c.boundary.Add(new MountainPeakV3 {
                        id = "Range_" + side + "_" + j,
                        position = new Vector2(side * (340 + random.Next(-12,28)), -290+j*95),
                        height = random.Next(215,290), summitSize = new Vector2(random.Next(30,70),random.Next(45,80)),
                        shoulderWidth = 24, rotation = random.Next(180), silhouette = .3f });
                foreach (int side in new[] {-1,1}) c.boundary.Add(new MountainPeakV3 {
                    id = "Rear_" + side, position = new Vector2(side*140,-330), height = 220,
                    summitSize = new Vector2(60,50), shoulderWidth = 25, silhouette = .3f });
            }
        }

        public static MountainPeakV3 WorldPeak(MountainConfigV3 c, MountainPeakV3 p) => new MountainPeakV3 {
            id=p.id, position=c.clusterCenter+Vector2.Scale(p.position,c.clusterSize), height=p.height,
            summitSize=p.summitSize, rotation=p.rotation, silhouette=p.silhouette, shoulderWidth=p.shoulderWidth };

        static float RockHeight(MountainPeakV3 p, float x, float z, float floor)
        {
            var d = new Vector2(x,z)-p.position;
            float a = -p.rotation*Mathf.Deg2Rad;
            float u = Mathf.Cos(a)*d.x-Mathf.Sin(a)*d.y, v = Mathf.Sin(a)*d.x+Mathf.Cos(a)*d.y;
            float angle = Mathf.Atan2(v,u);
            float shape = 1 + p.silhouette*(.65f*Mathf.Sin(angle*3+1.2f)+.35f*Mathf.Cos(angle*5+.8f));
            float rx=p.summitSize.x*.5f, rz=p.summitSize.y*.5f;
            float q = Mathf.Sqrt(u*u/(rx*rx)+v*v/(rz*rz))/shape;
            float distance = (q-1)*Mathf.Min(rx,rz);
            if (distance <= 0) {
                if(p.id=="Start")return p.height;
                if(p.id.StartsWith("Range") || p.id.StartsWith("Rear"))
                    return p.height+38*(1-q);
                // Keep central landing space flat; one off-centre crest makes a wedge / broken summit.
                return p.height+Mathf.Max(0,q-.42f)*Mathf.Max(0,Mathf.Sin(angle+p.rotation))*(p.id=="Final"?42:28);
            }
            // Narrow fractured shoulder, then near-vertical canyon, then a flared scree foot.
            float shoulder = p.shoulderWidth;
            if (distance < shoulder) return p.height-distance*.75f;
            float pitch=3.1f+1.1f*Mathf.Sin(angle*2+p.rotation)+.45f*Mathf.Cos(angle*5+distance*.05f);
            float cliff = p.height-shoulder*.75f-(distance-shoulder)*pitch;
            float scree = floor+Mathf.Max(0,(p.height-floor)*.72f-distance*(.68f+.12f*Mathf.Sin(angle*3+p.rotation)));
            return Mathf.Max(cliff,scree);
        }

        public static void CheckConfig(MountainConfigV3 c)
        {
            if (c == null || c.smallPeaks.Count < 12 || c.smallPeaks.Count != c.smallPeakCount)
                throw new InvalidOperationException("Field count must match Small Peak Count (at least 12). Use Reseed Field after count changes.");
            if (c.meshSpacing < 2 || c.meshSpacing > 8 || c.clusterSize.x < 200 || c.clusterSize.y < 200 || c.minimumGap < 4 || c.bridgeStubLength <= 0 || c.bridgeStubLength > 12 || c.boundary.Count < 8)
                throw new InvalidOperationException("Invalid terrain dimensions, gap, boundary count or exit stub (max 12m).");
            var peaks = new[] {c.start,c.destination}.Concat(c.smallPeaks.Select(p=>WorldPeak(c,p))).ToArray();
            foreach (var p in peaks.Concat(c.boundary))
                if (p == null || p.summitSize.x < 12 || p.summitSize.y < 12 || p.shoulderWidth < 0 || p.height < 80 || p.height > 400 || !float.IsFinite(p.position.x) || !float.IsFinite(p.position.y))
                    throw new InvalidOperationException("Invalid peak data.");
            for (int i=0;i<peaks.Length;i++) for (int j=i+1;j<peaks.Length;j++)
            {
                float r1=SupportRadius(peaks[i],peaks[j].position-peaks[i].position);
                float r2=SupportRadius(peaks[j],peaks[i].position-peaks[j].position);
                // Bounding circles are conservative: reject overlap before replacing any generation.
                if (Vector2.Distance(peaks[i].position,peaks[j].position)-r1-r2 < c.minimumGap)
                    throw new InvalidOperationException("Insufficient canyon clearance: "+peaks[i].id+" / "+peaks[j].id);
            }
            if (Vector2.Distance(new Vector2(c.bridgeAnchor.x,c.bridgeAnchor.z),c.destination.position) > c.destination.summitSize.y*.6f || Mathf.Abs(c.bridgeAnchor.y-c.destination.height)>.5f)
                throw new InvalidOperationException("Bridge anchor must start on the destination summit at landing height.");
        }

        static float SupportRadius(MountainPeakV3 p,Vector2 direction)
        {
            float angle=Mathf.Atan2(direction.y,direction.x)-p.rotation*Mathf.Deg2Rad;
            return .5f*Mathf.Sqrt(Mathf.Pow(p.summitSize.x*Mathf.Cos(angle),2)+Mathf.Pow(p.summitSize.y*Mathf.Sin(angle),2))*(1+p.silhouette)+p.shoulderWidth;
        }

        [MenuItem("Tools/Twin Edge/V3 Mountain/Create or Open Mountain Scene")]
        public static void CreateOrOpen()
        {
            if (File.Exists(ScenePath)) {
                var loaded=SceneManager.GetSceneByPath(ScenePath);
                if(loaded.IsValid() && loaded.isLoaded){SceneManager.SetActiveScene(loaded);return;}
                EditorSceneManager.OpenScene(ScenePath,Application.isBatchMode?OpenSceneMode.Single:OpenSceneMode.Additive);return;
            }
            if(!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                throw new InvalidOperationException("Save the current untitled scene before additive creation; no scene was changed.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root=new GameObject("TwinEdge_Blockout");
            var section=new GameObject("Mountain"); section.transform.SetParent(root.transform,false);
            section.AddComponent<LevelSection>().section=SectionId.Mountain;
            var owner=section.AddComponent<MountainSectionV3>(); owner.config=Config();
            new GameObject("Manual").transform.SetParent(section.transform,false);
            Generate(owner);
            var light=new GameObject("MountainPreviewSun").AddComponent<Light>(); light.type=LightType.Directional;
            light.intensity=1.8f; light.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.55f,.6f,.68f);
            var cam=new GameObject("MountainPreviewCamera").AddComponent<Camera>();
            cam.transform.position=new Vector3(620,650,-800); cam.transform.LookAt(new Vector3(0,70,30)); cam.farClipPlane=3000;
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.13f,.18f,.23f);
            if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save V3 scene");
            Selection.activeGameObject=owner.gameObject;
        }

        public static void Generate(MountainSectionV3 owner)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required");
            CheckConfig(owner.config);
            if(owner.transform.lossyScale!=Vector3.one || owner.transform.rotation!=Quaternion.identity || owner.transform.position!=Vector3.zero)
                throw new InvalidOperationException("Mountain section must remain at world identity; edit configuration positions instead.");
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Regenerate V3 Mountain");
            try
            {
                // Build completely before retiring the old owned tree. Versioned assets keep Undo valid.
                var c=owner.config;
                string folder="Assets/Game/Level/Data/Generated/MountainV3_"+Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                var generated=new GameObject("Generated"); generated.transform.SetParent(owner.transform,false);
                Undo.RegisterCreatedObjectUndo(generated,"Generate mountain");
                var peaks=new[] {c.start,c.destination}.Concat(c.smallPeaks.Select(p=>WorldPeak(c,p))).ToArray();
                BuildTerrain(c,peaks,generated.transform,folder);
                foreach(var p in peaks) Marker(generated.transform,p.id,new Vector3(p.position.x,p.height,p.position.y),
                    p==c.start?MarkerKind.SectionEntrance:p==c.destination?MarkerKind.SectionExit:MarkerKind.PlatformPlacement);
                Marker(generated.transform,"BridgeAnchor",c.bridgeAnchor,MarkerKind.BridgeAnchor);
                var mat=Material(folder,"ExitAnchor",new Color(.72f,.47f,.19f));
                var stub=GameObject.CreatePrimitive(PrimitiveType.Cube); stub.name="Bridge_StartStub_Only";
                stub.transform.SetParent(generated.transform,false);
                stub.transform.position=c.bridgeAnchor+new Vector3(0,-.6f,c.bridgeStubLength*.5f);
                stub.transform.localScale=new Vector3(c.bridgeWidth,1.2f,c.bridgeStubLength);
                stub.GetComponent<Renderer>().sharedMaterial=mat;
                Undo.RegisterCompleteObjectUndo(owner,"Replace mountain generation");
                if(owner.generated!=null) Undo.DestroyObjectImmediate(owner.generated);
                owner.generated=generated; owner.generationAssets=folder;
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(owner.gameObject.scene); AssetDatabase.SaveAssets();
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static void Marker(Transform parent,string id,Vector3 point,MarkerKind kind)
        {
            var g=new GameObject(id);g.transform.SetParent(parent,false);g.transform.position=point;
            var m=g.AddComponent<LevelMarker>();m.id="Mountain.V3."+id;m.section=SectionId.Mountain;m.kind=kind;
            m.reservedSize=new Vector3(6,2,6);m.notes="Spatial hypothesis; pole traversal unverified.";
        }

        static Material Material(string folder,string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);
            AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;
        }

        static void BuildTerrain(MountainConfigV3 c,MountainPeakV3[] peaks,Transform parent,string folder)
        {
            // A jittered triangulation clipped to an irregular geological perimeter. One shared surface.
            var vertices=new List<Vector3>();var triangles=new List<int>();
            int nx=Mathf.CeilToInt(1060/c.meshSpacing)+1,nz=Mathf.CeilToInt(1250/c.meshSpacing)+1;
            var inside=new bool[nx*nz];
            Func<float,float,float> height=(x,z)=> {
                float floor=c.basinHeight+c.terrainHeightVariation*(Mathf.Sin(x*.028f+z*.012f)+Mathf.Cos(z*.039f-x*.009f))*.5f;
                float y=floor;
                foreach(var p in peaks) y=Mathf.Max(y,RockHeight(p,x,z,floor));
                foreach(var p in c.boundary) y=Mathf.Max(y,RockHeight(p,x,z,floor));
                // Carve a short socket through the destination crest, so the exit stub is exposed.
                // This stops at the stub: it never connects across the next section.
                if(Mathf.Abs(x-c.bridgeAnchor.x)<c.bridgeWidth*.9f && z>c.bridgeAnchor.z-18 && z<c.bridgeAnchor.z+c.bridgeStubLength+3)
                    y=Mathf.Min(y,c.bridgeAnchor.y-1.2f);
                return y;
            };
            for(int j=0;j<nz;j++) for(int i=0;i<nx;i++) {
                float x=-530+i*c.meshSpacing+.18f*c.meshSpacing*Mathf.Sin(i*17.13f+j*37.77f+c.seed);
                float z=-595+j*c.meshSpacing+.18f*c.meshSpacing*Mathf.Cos(i*27.43f-j*13.19f+c.seed);
                float a=Mathf.Atan2((z-30)/540,x/460);
                float edge=1+.09f*Mathf.Sin(5*a)+.045f*Mathf.Cos(9*a);
                float radius=Mathf.Sqrt(x*x/(460*460)+(z-30)*(z-30)/(540*540))/edge;
                inside[j*nx+i]=radius<=1;
                float y=height(x,z);
                if(radius>.92f)y=Mathf.Lerp(y,c.basinHeight-70+18*Mathf.Sin(a*7),Mathf.Clamp01((radius-.92f)/.08f));
                vertices.Add(new Vector3(x,y,z));
            }
            for(int j=0;j<nz-1;j++) for(int i=0;i<nx-1;i++) {
                int a=j*nx+i,b=a+1,d=a+nx,e=d+1;
                if(inside[a]&&inside[d]&&inside[b])triangles.AddRange(new[]{a,d,b});
                if(inside[b]&&inside[d]&&inside[e])triangles.AddRange(new[]{b,d,e});
            }
            var flat=triangles.Select(i=>vertices[i]).ToArray();
            var mesh=new Mesh {name="Mountain_ConnectedGeology",indexFormat=IndexFormat.UInt32};
            mesh.vertices=flat;mesh.triangles=Enumerable.Range(0,flat.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,folder+"/ConnectedGeology.asset");
            var g=new GameObject("Mountain_ConnectedGeology");g.transform.SetParent(parent,false);
            g.AddComponent<MeshFilter>().sharedMesh=mesh;
            // URP Lit does not consume vertex colors: slope-based submeshes preserve readable terrain colors.
            var tops=new List<int>();var walls=new List<int>();var low=new List<int>();
            for(int i=0;i<flat.Length;i+=3) {
                var n=Vector3.Cross(flat[i+1]-flat[i],flat[i+2]-flat[i]).normalized;
                float y=(flat[i].y+flat[i+1].y+flat[i+2].y)/3;
                var list=y<80?low:n.y>.94f && y>110?tops:walls;
                list.AddRange(new[]{i,i+1,i+2});
            }
            mesh.subMeshCount=3;mesh.SetTriangles(tops,0);mesh.SetTriangles(walls,1);mesh.SetTriangles(low,2);
            g.AddComponent<MeshRenderer>().sharedMaterials=new[]{Material(folder,"Summit",new Color(.64f,.68f,.59f)),Material(folder,"Cliff",new Color(.4f,.46f,.48f)),Material(folder,"DeepRock",new Color(.23f,.29f,.32f))};
            g.AddComponent<MeshCollider>().sharedMesh=mesh;
            EditorUtility.SetDirty(mesh);
        }
    }
}
