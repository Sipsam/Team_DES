using UnityEngine;
public class FantasyJaw:MonoBehaviour{
 public Transform jaw; public bool preview;
 public void SetOpen(float amount){if(jaw)jaw.localRotation=Quaternion.Euler(-55*Mathf.Clamp01(amount),0,0);}
 void Update(){if(preview)SetOpen((1-Mathf.Cos(Time.time*1.5f))*.5f);}
}
