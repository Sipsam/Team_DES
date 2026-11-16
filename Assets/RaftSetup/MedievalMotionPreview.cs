using UnityEngine;

// Visual review only: no damage, player controls, networking or passenger transport.
public sealed class MedievalMotionPreview : MonoBehaviour
{
    public string mode;
    public Transform target;
    public float duration = 4f;
    Vector3 origin, scale;
    Quaternion rotation;
    Renderer[] renderers;
    bool[] visibility;
    RaftPair pair;
    Transform left, right;
    Quaternion leftRot, rightRot;
    bool ready;
    void Awake() { Initialize(); }
    public void Initialize()
    {
        if(ready)return;
        if(target==null)target=transform;
        origin=target.localPosition;scale=target.localScale;rotation=target.localRotation;
        renderers=target.GetComponentsInChildren<Renderer>(true);
        visibility=new bool[renderers.Length];for(int i=0;i<renderers.Length;i++)visibility[i]=renderers[i].enabled;
        pair=GetComponent<RaftPair>();
        foreach(var t in GetComponentsInChildren<Transform>(true)) { if(t.name=="LeftLeaf_Hinge")left=t;if(t.name=="RightLeaf_Hinge")right=t; }
        if(left)leftRot=left.localRotation;if(right)rightRot=right.localRotation;
        ready=true;
    }
    void Update(){Sample(Time.time);}
    public void Sample(float time)
    {
        Initialize();float phase=Mathf.Repeat(time/Mathf.Max(.1f,duration),1);float wave=Mathf.Sin(phase*Mathf.PI*2);float ease=(1-Mathf.Cos(phase*Mathf.PI*2))*.5f;
        switch(mode)
        {
            case "float": target.localPosition=origin+Vector3.up*(wave*.15f);target.localRotation=rotation*Quaternion.Euler(0,0,wave*4);break;
            case "swim":target.localPosition=origin+Vector3.forward*(wave*.65f);target.localRotation=rotation*Quaternion.Euler(0,wave*12,0);break;
            case "spin":target.localRotation=rotation*Quaternion.Euler(0,phase*360,0);break;
            case "gem":target.localPosition=origin+Vector3.up*(wave*.15f);target.localRotation=rotation*Quaternion.Euler(0,phase*360,0);break;
            case "move":target.localPosition=origin+Vector3.right*(ease*2);break;
            case "slide":target.localPosition=origin+Vector3.forward*(wave*.7f);break;
            case "press":target.localPosition=origin-Vector3.up*(ease*.12f);break;
            case "oar":target.localRotation=rotation*Quaternion.Euler(wave*28,0,wave*10);break;
            case "fall":target.localPosition=origin-Vector3.up*(ease*.7f);target.localRotation=rotation*Quaternion.Euler(0,0,ease*22);break;
            case "arrow":target.localPosition=origin+Vector3.forward*(phase*2);break;
            case "vanish":for(int i=0;i<renderers.Length;i++)renderers[i].enabled=visibility[i]&&(phase<.65f);target.localPosition=origin+Vector3.up*(phase>.45f&&phase<.65f?Mathf.Sin(time*45)*.035f:0);break;
            case "gate":if(left)left.localRotation=leftRot*Quaternion.Euler(0,-ease*85,0);if(right)right.localRotation=rightRot*Quaternion.Euler(0,ease*85,0);break;
            case "raft":if(pair){if(phase>.5f)pair.SplitForReturn();else pair.JoinForOutbound();}target.localPosition=origin+Vector3.up*(wave*.08f);break;
        }
    }
    void OnDisable(){ResetPreview();}
    public void ResetPreview()
    {
        if(!ready)return;target.localPosition=origin;target.localRotation=rotation;target.localScale=scale;
        for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=visibility[i];
        if(left)left.localRotation=leftRot;if(right)right.localRotation=rightRot;
        if(pair&&mode=="raft")pair.JoinForOutbound();
    }
}
