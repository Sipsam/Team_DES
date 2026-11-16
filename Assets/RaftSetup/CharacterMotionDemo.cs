using UnityEngine;

// Animation viewer only. No player locomotion, collision or network logic.
public sealed class CharacterMotionDemo : MonoBehaviour
{
    public Animator animator;
    public Transform character;
    public string[] states;
    public int selected;
    public bool paused;
    public float speed=1;
    public void Choose(int index)
    {
        if(!animator||states==null||index<0||index>=states.Length)return;
        selected=index;animator.CrossFadeInFixedTime(states[index],.12f,0,0);paused=false;animator.speed=speed;
    }
    void Start(){Choose(0);}
    void OnGUI()
    {
        // Scale the UI to fit the Game view, including small editor windows.
        float scale=Mathf.Min(Screen.width/850f,Screen.height/480f);scale=Mathf.Max(.3f,scale);
        var previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        GUI.Box(new Rect(12,12,810,120),"CHARACTER ANIMATION TEST - original FBX motions");
        for(int i=0;i<states.Length;i++)if(GUI.Button(new Rect(24+i*130,42,120,30),states[i]))Choose(i);
        if(GUI.Button(new Rect(24,84,100,28),paused?"Resume":"Pause")){paused=!paused;animator.speed=paused?0:speed;}
        if(GUI.Button(new Rect(134,84,100,28),"Turn 45"))character.Rotate(0,45,0,Space.World);
        if(GUI.Button(new Rect(244,84,100,28),"Speed x0.5")){speed=.5f;if(!paused)animator.speed=speed;}
        if(GUI.Button(new Rect(354,84,100,28),"Speed x1")){speed=1;if(!paused)animator.speed=speed;}
        GUI.Label(new Rect(470,88,340,24),"Current: "+states[selected]+"  | In-place preview");GUI.matrix=previous;
    }
}
