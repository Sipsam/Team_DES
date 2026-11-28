#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using UnityEngine;
namespace TwinEdge.Gameplay {
 // Opt-in test capture of live scene state. Does not move either participant or the pole.
 public static class PoleRuntimeCapture {
  public static void Save(CooperativePole pole,string path) {
   if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   var go=new GameObject("TemporaryPoleEvidenceCamera"){hideFlags=HideFlags.HideAndDontSave};
   var cam=go.AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=45;cam.farClipPlane=3000;
   cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.10f,.14f,.18f);
   var s=pole.Snapshot;var middle=(s.a+s.b)*.5f+Vector3.up;
   var side=Vector3.Cross(Vector3.up,s.b-s.a).normalized;
   cam.transform.position=middle+side*42+Vector3.up*26;cam.transform.LookAt(middle);
   try { Render(cam,path); }
   finally { Object.Destroy(go); }
  }
  public static void SaveOwnerView(string path) {
   if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
   Render(V3GameplayBootstrap.Current.standbyCamera,path);
  }
  internal static void SaveGrip(Vector3 endpoint, Vector3 otherEndpoint, string path) {
   if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
   var view = new GameObject("TemporaryGripEvidenceCamera") { hideFlags = HideFlags.HideAndDontSave };
   var camera = view.AddComponent<Camera>();
   camera.enabled = false;
   camera.fieldOfView = 42;
   var forward = Vector3.ProjectOnPlane(otherEndpoint - endpoint, Vector3.up).normalized;
   var side = Vector3.Cross(Vector3.up, forward);
   camera.transform.position = endpoint + side * 3.4f + forward * 1.8f + Vector3.up * 1.8f;
   camera.transform.LookAt(endpoint + Vector3.up * 1.05f + forward * .25f);
   try { Render(camera, path); }
   finally { Object.Destroy(view); }
  }
  static void Render(Camera cam,string path) {
   var rt=new RenderTexture(1280,720,24);var previous=RenderTexture.active;var priorTarget=cam.targetTexture;Texture2D texture=null;
   try {
    cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
    texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
    File.WriteAllBytes(path,texture.EncodeToPNG());
   } finally {RenderTexture.active=previous;cam.targetTexture=priorTarget;rt.Release();Object.Destroy(rt);if(texture)Object.Destroy(texture);}
  }
 }
}
#endif
