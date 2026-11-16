using UnityEngine;
public sealed class MedievalReviewViewer : MonoBehaviour
{
    public GameObject[] items;
    public string[] labels;
    int index=-1;
    Camera view;
    Vector3 home;Quaternion angle;float zoom;
    void Start(){view=GetComponent<Camera>();home=transform.position;angle=transform.rotation;zoom=view.orthographicSize;}
    void Focus(int value)
    {
        if(items==null||items.Length==0)return;index=(value+items.Length)%items.Length;
        var bounds=new Bounds(items[index].transform.position,Vector3.one);
        foreach(var r in items[index].GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(r.bounds);
        transform.position=bounds.center+new Vector3(5,4,-7);transform.LookAt(bounds.center);view.orthographicSize=Mathf.Max(2.2f,bounds.size.magnitude*.65f);
    }
    void OnGUI()
    {
        GUI.Box(new Rect(10,10,660,95),"MEDIEVAL ASSET MOTION REVIEW");
        if(GUI.Button(new Rect(20,38,95,28),"Previous"))Focus(index-1);
        if(GUI.Button(new Rect(125,38,95,28),"Next"))Focus(index+1);
        if(GUI.Button(new Rect(230,38,95,28),"Overview")){index=-1;transform.SetPositionAndRotation(home,angle);view.orthographicSize=zoom;}
        GUI.Label(new Rect(20,73,640,25),index<0?"E = Essential / S = Optional. Click Next to inspect each asset.":(index+1)+" / "+items.Length+"  "+labels[index]);
    }
}
