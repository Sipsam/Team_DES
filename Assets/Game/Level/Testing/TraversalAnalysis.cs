using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TwinEdge.Levels.Testing
{
    [Serializable] public class TraversalParameters
    {
        [Tooltip("Experimental hypotheses, not approved pole mechanics.")]
        public float reach = 110, maxRise = 65, maxDrop = 85, arcHeight = 35, duration = 2.5f;
        public float speed = 5, jumpHeight = 1.5f, gravity = 20;
        public float radius = .45f, height = 2, slope = 45, edgeMargin = .4f;
        public bool Allows(Vector3 a, Vector3 b) => Horizontal(a,b) <= reach && b.y-a.y <= maxRise && a.y-b.y <= maxDrop;
        public static float Horizontal(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        public Vector3 Arc(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a,b,t)+Vector3.up*(4*arcHeight*t*(1-t));
        public TraversalParameters Copy() => JsonUtility.FromJson<TraversalParameters>(JsonUtility.ToJson(this));
    }
    [Serializable] public class TraversalNode
    {
        public string id;
        public Vector3 authored, landing;
        public Vector2 size;
        public bool hasLanding, twoCapsules;
        public float sampledArea, sampledEdgeClearance, maximumSlope;
        public List<Vector3> safe = new List<Vector3>();
        public Vector3 second;
    }
    [Serializable] public class TraversalGraph
    {
        public string evidence = "Static coordinate hypotheses; no collision or human-play guarantee";
        public float meanSpacing, minSpacing, maxSpacing, meanHeightDifference;
        public int edges, branches;
        public string[] isolated, unreachable;
        public string[] fewestMoves, conservative, highVariation, left, right, returnRoute;
        public static TraversalGraph Analyze(List<TraversalNode> n, TraversalParameters p)
        {
            var g = new TraversalGraph(); int count=n.Count, pairs=0; float sum=0, heights=0;
            g.minSpacing=float.MaxValue;
            var links=new List<int>[count];
            for(int i=0;i<count;i++) { links[i]=new List<int>(); for(int j=0;j<count;j++) if(i!=j) {
                float d=TraversalParameters.Horizontal(n[i].authored,n[j].authored);
                sum+=d; heights+=Mathf.Abs(n[j].authored.y-n[i].authored.y); pairs++;
                g.minSpacing=Mathf.Min(g.minSpacing,d);g.maxSpacing=Mathf.Max(g.maxSpacing,d);
                if(p.Allows(n[i].authored,n[j].authored)) { links[i].Add(j);g.edges++; }
            }}
            g.meanSpacing=sum/Mathf.Max(1,pairs);g.meanHeightDifference=heights/Mathf.Max(1,pairs);
            g.branches=links.Count(x=>x.Count>=3);
            g.isolated=Enumerable.Range(0,count).Where(i=>links[i].Count==0 && !links.Any(x=>x.Contains(i))).Select(i=>n[i].id).ToArray();
            var reached=new HashSet<int>{0};var queue=new Queue<int>();queue.Enqueue(0);
            while(queue.Count>0) foreach(int j in links[queue.Dequeue()]) if(reached.Add(j))queue.Enqueue(j);
            g.unreachable=Enumerable.Range(0,count).Where(i=>!reached.Contains(i)).Select(i=>n[i].id).ToArray();
            g.fewestMoves=Route(n,links,0,1,0);g.conservative=Route(n,links,0,1,1);
            g.highVariation=Route(n,links,0,1,2);g.left=Route(n,links,0,1,3);g.right=Route(n,links,0,1,4);
            g.returnRoute=Route(n,links,1,0,1);return g;
        }
        static string[] Route(List<TraversalNode> n,List<int>[] links,int start,int end,int mode)
        {
            int c=n.Count;var dist=Enumerable.Repeat(float.PositiveInfinity,c).ToArray();var prev=Enumerable.Repeat(-1,c).ToArray();var done=new bool[c];dist[start]=0;
            for(int step=0;step<c;step++) {
                int i=-1;for(int k=0;k<c;k++)if(!done[k]&&(i<0||dist[k]<dist[i]))i=k;
                if(i<0||float.IsPositiveInfinity(dist[i]))break;done[i]=true;
                foreach(int j in links[i]) {
                    float d=TraversalParameters.Horizontal(n[i].authored,n[j].authored),h=Mathf.Abs(n[j].authored.y-n[i].authored.y);
                    float cost=mode==0?1:mode==1?1+d/100+h/30+(n[j].hasLanding?1/Mathf.Max(1,n[j].sampledArea):10):mode==2?1+d/200+30/(1+h):1+d/100+Mathf.Max(0,n[j].authored.x*(mode==3?1:-1))/30;
                    if(dist[i]+cost<dist[j]){dist[j]=dist[i]+cost;prev[j]=i;}
                }
            }
            if(float.IsPositiveInfinity(dist[end]))return Array.Empty<string>();
            var result=new List<string>();for(int i=end;i>=0;i=prev[i]){result.Add(n[i].id);if(i==start)break;}result.Reverse();return result.ToArray();
        }
    }
    public static class TraversalSurface
    {
        // Raycasts against the actual terrain, then footprint ring support and capsule headroom.
        public static bool Safe(MeshCollider terrain,Vector3 point,TraversalParameters p,out Vector3 feet,out float slope)
        {
            feet=point;slope=90;
            if(!terrain.Raycast(new Ray(point+Vector3.up*100,Vector3.down),out var hit,200))return false;
            feet=hit.point;slope=Vector3.Angle(hit.normal,Vector3.up);if(slope>p.slope)return false;
            float r=p.radius+p.edgeMargin;
            for(int k=0;k<12;k++) {
                float a=k*Mathf.PI/6;var offset=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r;
                if(!terrain.Raycast(new Ray(feet+offset+Vector3.up*.6f,Vector3.down),out var edge,1.2f)||Vector3.Angle(edge.normal,Vector3.up)>p.slope||Mathf.Abs(edge.point.y-feet.y)>.35f)return false;
            }
            return !Physics.CheckCapsule(feet+Vector3.up*(p.radius+.1f),feet+Vector3.up*(p.height-p.radius+.1f),p.radius*.95f,~(1<<2),QueryTriggerInteraction.Ignore);
        }
        public static void Sample(TraversalNode n,MeshCollider terrain,TraversalParameters p)
        {
            n.safe.Clear();n.hasLanding=false;n.twoCapsules=false;n.maximumSlope=0;
            const float spacing=1.5f;
            for(float x=-n.size.x*.65f;x<=n.size.x*.65f;x+=spacing)
                for(float z=-n.size.y*.65f;z<=n.size.y*.65f;z+=spacing) {
                    if(Safe(terrain,n.authored+new Vector3(x,0,z),p,out var foot,out var slope)&&Mathf.Abs(foot.y-n.authored.y)<15) n.safe.Add(foot);
                    n.maximumSlope=Mathf.Max(n.maximumSlope,slope);
                }
            n.sampledArea=n.safe.Count*spacing*spacing;n.sampledEdgeClearance=p.radius+p.edgeMargin;
            if(n.safe.Count==0)return;
            n.hasLanding=true;n.landing=n.safe.OrderBy(v=>(v-n.authored).sqrMagnitude).First();
            foreach(var point in n.safe) if(TraversalParameters.Horizontal(point,n.landing)>=2*(p.radius+p.edgeMargin)&&Mathf.Abs(point.y-n.landing.y)<.4f){n.twoCapsules=true;n.second=point;break;}
        }
    }
}
