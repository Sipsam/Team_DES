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
        public static event Action<MountainSectionV3> Regenerated;
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
            var field = new List<MountainPeakV3>();
            // Authored group anchors, unequal group sizes and constrained dart sampling.
            var anchors = new[] {new Vector2(-.18f,-.37f),new Vector2(.27f,-.28f),
                new Vector2(-.29f,-.04f),new Vector2(.16f,.12f),new Vector2(-.12f,.37f)};
            for (int i = 0; i < c.smallPeakCount; i++)
            {
                var p = new MountainPeakV3 { id="Peak_"+(i+1).ToString("00"),
                    height=125+random.Next(0,49), summitSize=new Vector2(random.Next(18,29),random.Next(20,35)),
                    rotation=random.Next(180), shoulderWidth=4, silhouette=.12f };
                bool placed=false;
                for(int attempt=0;attempt<12000;attempt++) {
                    var anchor=anchors[(i+attempt/160)%anchors.Length];
                    p.position=anchor+new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.40f;
                    if(Mathf.Abs(p.position.x)>.48f || Mathf.Abs(p.position.y)>.48f)continue;
                    var wp=WorldPeak(c,p);
                    var others=new[]{c.start,c.destination}.Concat(field.Select(q=>WorldPeak(c,q)));
                    if(others.All(q=>Vector2.Distance(q.position,wp.position)>=SupportRadius(q,wp.position-q.position)+SupportRadius(wp,q.position-wp.position)+c.minimumGap)) {
                        field.Add(p);placed=true;break;
                    }
                }
                if(!placed)throw new InvalidOperationException("Cannot fit field: increase cluster dimensions or reduce count.");
            }
            c.smallPeaks = field;
            // Explicit reseeding creates a different layout; old authored canyon links no longer apply.
            c.poleCanyons = new List<MountainPoleCanyonV3>();
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
            landingTerrace=p.landingTerrace, landingApproachWidth=p.landingApproachWidth, landingApproachDirection=p.landingApproachDirection, shape=p.shape, asymmetry=p.asymmetry, id=p.id, position=c.clusterCenter+Vector2.Scale(p.position,c.clusterSize), height=p.height,
            summitSize=p.summitSize, rotation=p.rotation, silhouette=p.silhouette, shoulderWidth=p.shoulderWidth,
            crestOffset=p.crestOffset, crestRise=p.crestRise };

        static float RockHeight(MountainPeakV3 p, float x, float z, float floor, bool roundCrown = false)
        {
            var d = new Vector2(x,z)-p.position;
            float a=-p.rotation*Mathf.Deg2Rad;
            float u=Mathf.Cos(a)*d.x-Mathf.Sin(a)*d.y, v=Mathf.Sin(a)*d.x+Mathf.Cos(a)*d.y;
            float rx=p.summitSize.x*.5f, rz=p.summitSize.y*.5f;
            float angle=Mathf.Atan2(v,u);
            int hash=0;foreach(char ch in p.id)hash=unchecked(hash*31+ch);
            int family=p.shape==MountainShapeV3.Automatic?(hash & 0x7fffffff)%6:(int)p.shape-1;
            float U=u/rx,V=v/rz;
            if (p.landingTerrace) {
                // The guaranteed rock core covers the landing ellipse and the surrounding mesh cells.
                // Directional lobes grow outward from it, independently of the permitted landing area.
                const float rimMargin = 2.15f;
                float ellipse = Mathf.Sqrt(u*u/((rx+rimMargin)*(rx+rimMargin))+v*v/((rz+rimMargin)*(rz+rimMargin)));
                float distanceFromCore = (ellipse-1)*Mathf.Min(rx,rz);
                float phase = family * 1.19f + p.rotation * Mathf.Deg2Rad;
                float ribs = .56f * Mathf.Sin(2*angle + phase) + .30f * Mathf.Cos(3*angle - phase)
                    + .14f * Mathf.Sin(5*angle + phase);
                float outside = Mathf.Max(0, distanceFromCore);
                float lip = ribs * p.silhouette * Mathf.Min(outside, 14);
                float terracePitch = Mathf.Lerp(2.3f, 3.8f, .5f+.5f*ribs*p.asymmetry);
                float terraceCliff = p.height - outside*1.94f -
                    Mathf.Max(0,outside-lip-p.shoulderWidth)*(terracePitch-1.94f);

                float spurAngle = angle-phase-.27f*Mathf.Sin(angle*3+phase);
                float spur = Mathf.Pow(Mathf.Clamp01(.5f+.5f*Mathf.Cos(spurAngle)), 3+family*.43f);
                float spurShoulder = p.height-Mathf.Lerp(48,12,spur) -
                    Mathf.Max(0,distanceFromCore-6-p.shoulderWidth)*Mathf.Lerp(2.5f,1.12f,spur*p.asymmetry);
                float footRoughness = 7*Mathf.PerlinNoise(x*.046f+phase,z*.033f) +
                    4*Mathf.PerlinNoise(x*.137f,z*.101f+phase)-5;
                float talus = p.height - 57 - Mathf.Max(0, distanceFromCore-ribs*15) * 1.5f + footRoughness;
                float shelf = Mathf.Max(floor, Mathf.Max(terraceCliff,Mathf.Max(spurShoulder,talus)));
                if (p.crestRise <= 0 || distanceFromCore < 0) return shelf;

                var axis = p.crestOffset.normalized;
                var crestDelta = d-p.crestOffset;
                float longitudinal = Vector2.Dot(crestDelta,axis)*.82f;
                float transverse = (crestDelta.x*axis.y-crestDelta.y*axis.x)*1.44f;
                float crestDistance = Mathf.Sqrt(longitudinal*longitudinal+transverse*transverse);
                float rockCrest = p.height+p.crestRise-crestDistance*2.05f;
                float fractures = 4*Mathf.PerlinNoise(x*.067f+phase,z*.108f);
                rockCrest -= fractures*Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,9,crestDistance));
                float join = Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,5,distanceFromCore)) *
                    Mathf.SmoothStep(0,1,Mathf.InverseLerp(p.height-48,p.height-28,rockCrest));
                return Mathf.Lerp(shelf,Mathf.Max(shelf,rockCrest),join);
            }
            // Large-scale directional footprints; no angular cliff/crest height discontinuity.
            float q;
            switch(family) {
                case 0:q=Mathf.Pow(Mathf.Pow(Mathf.Abs(U),4)+Mathf.Pow(Mathf.Abs(V),4),.25f);break;
                case 1:q=Mathf.Sqrt(U*U*2.4f+V*V*.8f);break;
                case 2:q=Mathf.Sqrt(U*U+V*V)*(1+.16f*p.asymmetry*U/Mathf.Max(1,Mathf.Abs(U)));break;
                case 3:q=Mathf.Min(new Vector2(U-.28f,V).magnitude,new Vector2(U+.28f,V).magnitude)*1.28f;break;
                case 4:q=Mathf.Max(Mathf.Abs(U),Mathf.Abs(V))*.94f;break;
                default:q=Mathf.Max(Mathf.Abs(U)*.85f+Mathf.Abs(V)*.45f,Mathf.Abs(V)*.9f);break;
            }
            q/=1+p.silhouette*(.65f*Mathf.Sin(angle*3+1.2f)+.35f*Mathf.Cos(angle*5+.8f));
            if (p.id == "Start")
            {
                float north = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 27, d.y));
                q = Mathf.Lerp(q, Mathf.Sqrt(U*U+V*V), north);
            }
            if(p.landingApproachWidth>0) {
                var direction=p.landingApproachDirection.normalized;
                float lateral=Mathf.Abs(d.x*direction.y-d.y*direction.x);
                if(Vector2.Dot(d,direction)>0) q=Mathf.Lerp(Mathf.Sqrt(U*U+V*V),q,Mathf.SmoothStep(0,1,Mathf.InverseLerp(p.landingApproachWidth,p.landingApproachWidth+5,lateral)));
            }
            float distance=Mathf.Max(0,q-1)*Mathf.Min(rx,rz);
            bool range=p.id.StartsWith("Range") || p.id.StartsWith("Rear");
            float crest=0;
            // Flat core with continuous perimeter rock crests, differing in section and silhouette.
            float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,1,q));
            if(p.id!="Start") {
                float profile=family==1?Mathf.Max(0,1-Mathf.Abs(U)):
                    family==3?Mathf.Abs(Mathf.Sin(U*3)):
                    family==4?.08f:Mathf.Max(0,Mathf.Sin(angle+family));
                crest=rim*profile*(range?24:p.id=="Final"?48:family==4?5:22);
            }
            if(p.id=="Final")crest+=65*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.9f,V))*Mathf.Exp(-U*U*2)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(10,24,Mathf.Abs(u)));
            if(p.landingApproachWidth>0) {
                var approach=p.landingApproachDirection.normalized;
                float along=Vector2.Dot(d,approach), lateral=Mathf.Abs(d.x*approach.y-d.y*approach.x);
                if(along>0) crest*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(p.landingApproachWidth,p.landingApproachWidth+5,lateral));
            }
            float top=p.height+crest;
            if(q<=1)return top;
            float shoulder=Mathf.Max(1,p.shoulderWidth);
            float pitch=family==2?Mathf.Lerp(1.6f,4.2f,(Mathf.Sin(angle)+1)*.5f):family==1?2.3f:family==4?2.8f:2.0f;
            float lipPitch = p.id is "Start" or "Final" ? 2.25f : .65f;
            float rimDistance = distance;
            if (roundCrown)
            {
                float outsideSupport = (Mathf.Sqrt(U * U + V * V) - 1) * Mathf.Min(rx, rz);
                float clear = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.5f, 5f, outsideSupport));
                rimDistance = Mathf.Lerp(distance, MountainRidgelineV3.RoundPositiveEdge(distance, 4f), clear);
            }
            float drop=rimDistance*lipPitch+Mathf.Max(0,distance-shoulder)*Mathf.Max(0,pitch-lipPitch);
            // Fade perimeter crest continuously into the shoulder; flared geological foot below.
            float cliff=p.height+crest*Mathf.Exp(-distance/shoulder)-drop;
            float scree=floor+Mathf.Max(0,(p.height-floor)*.52f-distance*.7f);
            if (roundCrown && distance > shoulder && cliff > scree && cliff < scree + 5f)
                cliff = scree + MountainRidgelineV3.RoundPositiveEdge(cliff - scree, 5f);
            return Mathf.Max(floor,Mathf.Max(cliff,scree));
        }

        static MountainPeakV3 RangePeak(MountainConfigV3 c, MountainPeakV3 p, int index)
        {
            return new MountainPeakV3 { id=p.id, shape=MountainShapeV3.Ridge,
                position=p.position+new Vector2(Mathf.Sign(p.position.x)*c.outerRangeSetback,0),
                height=p.height*c.outerRangeHeightScale*(.85f+.15f*Mathf.Cos(index*1.7f)),
                summitSize=p.summitSize, rotation=p.rotation, silhouette=p.silhouette, shoulderWidth=p.shoulderWidth };
        }

        public static void CheckConfig(MountainConfigV3 c)
        {
            if (c == null || c.smallPeaks.Count < 12 || c.smallPeaks.Count != c.smallPeakCount)
                throw new InvalidOperationException("Field count must match Small Peak Count (at least 12). Use Reseed Field after count changes.");
            if (c.meshSpacing < 2 || c.meshSpacing > 8 || c.clusterSize.x < 200 || c.clusterSize.y < 200 || c.minimumGap < 4 || c.bridgeStubLength <= 0 || c.bridgeStubLength > 12 || (c.ridges.Count == 0 && c.boundary.Count < 8))
                throw new InvalidOperationException("Invalid terrain dimensions, gap, boundary count or exit stub (max 12m).");
            if (c.entryTrail.Length > 0)
            {
                if (c.entryTrail.Length < 4)
                    throw new InvalidOperationException("The approach requires a trailhead, spawn, finish and summit entrance.");
                for (int i = 1; i < c.entryTrail.Length; i++)
                    if (c.entryTrail[i].z <= c.entryTrail[i - 1].z)
                        throw new InvalidOperationException("Approach control points must increase Z.");
            }
            foreach (var ridge in c.ridges)
                if (ridge.crest.Length < 2 || !float.IsFinite(ridge.halfWidth) || ridge.halfWidth <= 0 ||
                    !float.IsFinite(ridge.crestHalfWidth) || ridge.crestHalfWidth < 0 || ridge.crestHalfWidth >= ridge.halfWidth ||
                    !float.IsFinite(ridge.flankBias) || Mathf.Abs(ridge.flankBias) >= 1 ||
                    ridge.crest.Any(p => !float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) ||
                    ridge.crest.Zip(ridge.crest.Skip(1), (a, b) => a.x == b.x && a.z == b.z).Any(equal => equal))
                    throw new InvalidOperationException("A mountain ridge needs finite, distinct crest points and positive flank widths.");
            var peaks = new[] {c.start,c.destination}.Concat(c.smallPeaks.Select(p=>WorldPeak(c,p))).ToArray();
            foreach (var p in peaks.Concat(c.boundary))
                if (p == null || p.summitSize.x < 12 || p.summitSize.y < 12 || p.shoulderWidth < 0 || p.height < 80 || p.height > 400 || !float.IsFinite(p.position.x) || !float.IsFinite(p.position.y))
                    throw new InvalidOperationException("Invalid peak data.");
                else if (p.crestRise > 0 && (!p.landingTerrace || p.crestOffset.magnitude < Mathf.Max(p.summitSize.x,p.summitSize.y)*.5f+4))
                    throw new InvalidOperationException("A shelf crest must begin outside its landing area: "+p.id);
            for (int i=0;i<peaks.Length;i++) for (int j=i+1;j<peaks.Length;j++)
            {
                bool terracePair=peaks[i].landingTerrace || peaks[j].landingTerrace;
                float r1=SupportRadius(peaks[i],peaks[j].position-peaks[i].position,terracePair);
                float r2=SupportRadius(peaks[j],peaks[i].position-peaks[j].position,terracePair);
                // Terrace pairs require a real canyon between summit rims; lower geological shoulders may merge.
                if (Vector2.Distance(peaks[i].position,peaks[j].position)-r1-r2 < c.minimumGap)
                    throw new InvalidOperationException("Insufficient canyon clearance: "+peaks[i].id+" / "+peaks[j].id);
            }
            foreach(var cut in c.poleCanyons) {
                var a=peaks.FirstOrDefault(p=>p.id==cut.from);var b=peaks.FirstOrDefault(p=>p.id==cut.to);
                if((!string.IsNullOrEmpty(cut.island)&&cut.island!=cut.from&&cut.island!=cut.to)||a==null||b==null||a==b||!float.IsFinite(cut.width)||!float.IsFinite(cut.span)||!float.IsFinite(cut.depth)||cut.width<4||cut.span<20||cut.depth<10)throw new InvalidOperationException("Invalid pole canyon data");
                var direction=(b.position-a.position).normalized;
                float gap=Vector2.Distance(a.position,b.position)-SupportRadius(a,direction,true)-SupportRadius(b,-direction,true)-(a.landingTerrace?2:0)-(b.landingTerrace?2:0);
                if(cut.width>gap-2)throw new InvalidOperationException("Pole canyon must stay outside both summit landing rims: "+cut.from+" / "+cut.to);
            }
            if (Vector2.Distance(new Vector2(c.bridgeAnchor.x,c.bridgeAnchor.z),c.destination.position) > c.destination.summitSize.y*.6f || Mathf.Abs(c.bridgeAnchor.y-c.destination.height)>.5f)
                throw new InvalidOperationException("Bridge anchor must start on the destination summit at landing height.");
        }

        static float SupportRadius(MountainPeakV3 p,Vector2 direction,bool summitOnly=false)
        {
            float angle=Mathf.Atan2(direction.y,direction.x)-p.rotation*Mathf.Deg2Rad;
            return .5f*Mathf.Sqrt(Mathf.Pow(p.summitSize.x*Mathf.Cos(angle),2)+Mathf.Pow(p.summitSize.y*Mathf.Sin(angle),2))*(summitOnly?1:1+p.silhouette)+(summitOnly?0:p.shoulderWidth);
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
                AddTrailMarkers(c, generated.transform);
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
            Regenerated?.Invoke(owner);
        }

        internal static void AddTrailMarkers(MountainConfigV3 config, Transform parent)
        {
            var trail = config.entryTrail;
            if (trail.Length == 0) return;
            Marker(parent, "InitialSpawn", trail[1], MarkerKind.SectionEntrance);
            AlignTrailSpawn(trail, parent.Find("InitialSpawn"));
            Marker(parent, "TrailEntrance", trail[^1], MarkerKind.SectionEntrance);
            Marker(parent, "ReturnFinish", trail[^2], MarkerKind.SectionExit);
        }

        internal static void AlignTrailSpawn(Vector3[] trail, Transform spawn)
        {
            Vector3 ahead = trail[2] - trail[1];
            ahead.y = 0;
            spawn.rotation = Quaternion.LookRotation(ahead, Vector3.up);
        }

        internal static float TrailHeight(MountainConfigV3 config, float x, float z, float rock)
        {
            var trail = config.entryTrail;
            if (trail.Length == 0 || z < trail[0].z - 67 || z > trail[^1].z + 8) return rock;
            var path = SampleTrail(trail, z);
            float lateral = x - path.x;
            float offset = Mathf.Abs(lateral);
            float toe = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(trail[0].z - 67, trail[0].z, z));
            float crest = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(trail[^1].z, trail[^1].z + 8, z));
            float ridgeSide = Mathf.Clamp01(.5f + lateral*.015f);
            float spread = Mathf.Lerp(94, 57, ridgeSide) + 9 * Mathf.Sin(z*.026f);
            float foothill = 1-Mathf.SmoothStep(0, 1, Mathf.InverseLerp(trail[1].z+14, trail[1].z+65, z));
            float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(15+foothill*15, spread, offset));
            float trailWidth = Mathf.Lerp(7.5f, 10, .5f+.5f*Mathf.Sin(z*.071f)) + foothill*14;
            float shoulder = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(trailWidth, 32+foothill*12, offset));
            float relief = Mathf.Lerp(22, -19, ridgeSide) + 8*Mathf.Sin(x*.071f + z*.046f);
            float grade = path.y + shoulder * relief;
            return Mathf.Lerp(rock, grade, toe * (1-crest) * edge);
        }

        internal static Vector3 SampleTrail(Vector3[] trail, float z)
        {
            int segment = 0;
            while (segment < trail.Length - 2 && z > trail[segment + 1].z) segment++;
            var from = trail[segment];
            var to = trail[segment + 1];
            float t = Mathf.InverseLerp(from.z, to.z, z);
            return new Vector3(Mathf.SmoothStep(from.x, to.x, t), Mathf.Lerp(from.y, to.y, t), z);
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
            var mesh = BuildTerrainMesh(c, peaks);
            AssetDatabase.CreateAsset(mesh, folder + "/" + (c.ridges.Count > 0 ? RidgelineMeshFile : "ConnectedGeology.asset"));
            var g = new GameObject("Mountain_ConnectedGeology");
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = V3WorldMaterials.MountainBlend();
            g.AddComponent<MeshCollider>().sharedMesh = mesh;
            EditorUtility.SetDirty(mesh);
        }

        internal const string RidgelineMeshFile = "ConnectedRidgeline.asset";

        internal static Mesh BuildTerrainMesh(MountainConfigV3 c, MountainPeakV3[] peaks)
        {
            // A jittered triangulation clipped to an irregular geological perimeter. One shared surface.
            var vertices=new List<Vector3>();var triangles=new List<int>();
            int nx=Mathf.CeilToInt(1060/c.meshSpacing)+1,nz=Mathf.CeilToInt(1250/c.meshSpacing)+1;
            var inside=new bool[nx*nz];
            var terraces=peaks.Where(p=>p.landingTerrace).ToArray();
            var ridgeline = c.ridges.Count > 0 ? new MountainRidgelineV3(c, terraces) : null;
            var ranges = ridgeline == null ? c.boundary.Select((p,i)=>RangePeak(c,p,i)).ToArray() : Array.Empty<MountainPeakV3>();
            var cuts = c.poleCanyons.Select(cut =>
            {
                var from = peaks.First(p => p.id == cut.from);
                var to = peaks.First(p => p.id == cut.to);
                var axis = (to.position-from.position).normalized;
                float ra = SupportRadius(from,axis,true)+(from.landingTerrace?2:0);
                float rb = SupportRadius(to,-axis,true)+(to.landingTerrace?2:0);
                var middle = (from.position+axis*ra+to.position-axis*rb)*.5f;
                var island = peaks.FirstOrDefault(p => p.id == cut.island);
                var neighbours = terraces.Where(p => p != from && p != to &&
                    Vector2.Distance(p.position, middle) < cut.span*.5f + p.summitSize.magnitude*.5f).ToArray();
                return (cut, from, to, axis, middle, island, neighbours);
            }).ToArray();
            Func<float,float,float> height=(x,z)=> {
                float floor=c.basinHeight+c.terrainHeightVariation*(Mathf.Sin(x*.028f+z*.012f)+Mathf.Cos(z*.039f-x*.009f))*.5f;
                var position = new Vector2(x,z);
                float y = ridgeline != null ? ridgeline.Height(position, floor) : floor;
                foreach(var p in peaks)
                {
                    if (ridgeline != null && p.landingTerrace) continue;
                    float peakHeight = RockHeight(p, x, z, floor, ridgeline != null);
                    if (ridgeline != null && p == c.start)
                        peakHeight = Mathf.Lerp(floor, peakHeight, MountainRidgelineV3.StartCrownWeight(z - c.start.position.y));
                    y=Mathf.Max(y,peakHeight);
                }
                if (ridgeline == null)
                    foreach(var p in ranges) y=Mathf.Max(y,RockHeight(p,x,z,floor));
                // Cut authored landing terraces into any overlapping higher geological shoulder.
                // This changes the rendered mesh and its collider together; no support object is added.
                if (ridgeline == null) foreach(var p in terraces) {
                    float distance=Vector2.Distance(new Vector2(x,z),p.position)-(Mathf.Min(p.summitSize.x,p.summitSize.y)*.5f+2);
                    if(distance<6) y=Mathf.Lerp(Mathf.Min(y,p.height),y,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,6,distance)));
                }
                // Real canyon cuts separate walkable endpoint shoulders while leaving summit ellipses intact.
                foreach(var (cut, from, to, axis, middle, island, neighbours) in cuts) {
                    Vector2 delta=position-middle;
                    if(island!=null) {
                        float ring=Mathf.Max(island.summitSize.x,island.summitSize.y)*.5f+7;
                        if(Mathf.Abs(Vector2.Distance(position,island.position)-ring)<cut.width*.5f)
                            y=Mathf.Min(y,Mathf.Min(from.height,to.height)-cut.depth);
                    } else {
                        float across = Mathf.Abs(Vector2.Dot(delta,axis));
                        float along = Mathf.Abs(delta.x*axis.y-delta.y*axis.x);
                        if (across >= cut.width*.5f || along >= cut.span*.5f) continue;
                        float wall = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cut.width*.24f,cut.width*.5f,across));
                        float ends = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cut.span*.34f,cut.span*.5f,along));
                        float bed = Mathf.Min(from.height,to.height)-cut.depth + 3*Mathf.Sin(x*.13f+z*.087f);
                        float clearance = 1;
                        foreach (var neighbour in neighbours)
                        {
                            var offset = position-neighbour.position;
                            float radius = SupportRadius(neighbour,offset,true)+2.5f;
                            clearance = Mathf.Min(clearance, Mathf.SmoothStep(0,1,Mathf.InverseLerp(radius,radius+4,offset.magnitude)));
                        }
                        y = Mathf.Lerp(y, Mathf.Min(y,bed), wall*ends*clearance);
                    }
                }
                // Carve a short socket through the destination crest, so the exit stub is exposed.
                // This stops at the stub: it never connects across the next section.
                if(Mathf.Abs(x-c.bridgeAnchor.x)<c.bridgeWidth*.9f && z>c.bridgeAnchor.z-18 && z<c.bridgeAnchor.z+c.bridgeStubLength+3)
                    y=Mathf.Min(y,c.bridgeAnchor.y-1.2f);
                return y;
            };
            for(int j=0;j<nz;j++) for(int i=0;i<nx;i++) {
                float x=-530+i*c.meshSpacing+c.samplingJitter*c.meshSpacing*Mathf.Sin(i*17.13f+j*37.77f+c.seed);
                float z=-595+j*c.meshSpacing+c.samplingJitter*c.meshSpacing*Mathf.Cos(i*27.43f-j*13.19f+c.seed);
                float a=Mathf.Atan2((z-30)/540,x/460);
                float edge=1+.09f*Mathf.Sin(5*a)+.045f*Mathf.Cos(9*a);
                float radius=Mathf.Sqrt(x*x/(460*460)+(z-30)*(z-30)/(540*540))/edge;
                inside[j*nx+i]=radius<=1;
                float y=inside[j*nx+i]?height(x,z):c.basinHeight;
                if(radius>1-c.boundaryTaper)y=Mathf.Lerp(y,c.basinHeight-25,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1-c.boundaryTaper,1,radius)));
                y = ridgeline != null ? ridgeline.Foothill(new Vector2(x,z), y) : TrailHeight(c,x,z,y);
                vertices.Add(new Vector3(x,y,z));
            }
            for(int j=0;j<nz-1;j++) for(int i=0;i<nx-1;i++) {
                int a=j*nx+i,b=a+1,d=a+nx,e=d+1;
                if(inside[a]&&inside[d]&&inside[b])triangles.AddRange(new[]{a,d,b});
                if(inside[b]&&inside[d]&&inside[e])triangles.AddRange(new[]{b,d,e});
            }
            var remap = new int[vertices.Count];
            Array.Fill(remap, -1);
            var surface = new List<Vector3>();
            for (int i = 0; i < triangles.Count; i++)
            {
                int index = triangles[i];
                if (remap[index] == -1)
                {
                    remap[index] = surface.Count;
                    surface.Add(vertices[index]);
                }
                triangles[i] = remap[index];
            }
            var mesh=new Mesh {name="Mountain_ConnectedGeology",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(surface);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
