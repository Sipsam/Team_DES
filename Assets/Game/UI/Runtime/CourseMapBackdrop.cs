using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    internal sealed class CourseMapBackdrop
    {
        readonly RawImage image;
        readonly Action<RenderTargetIdentifier, CommandBuffer> capture;
        Camera camera;
        RenderTexture snapshot;
        CameraClearFlags clearFlags;
        int cullingMask;
        bool capturePending, captured, frozen;

        internal CourseMapBackdrop(Transform overlay)
        {
            var rect = UiKit.Rect(overlay, "Course map world backdrop");
            rect.SetAsFirstSibling();
            UiKit.Stretch(rect);
            image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            capture = Capture;
        }

        internal void Refresh(Camera source)
        {
            if (!source || !source.isActiveAndEnabled || source.targetTexture ||
                !CameraCaptureBridge.enabled || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Release();
                return;
            }

            if (camera != source || !snapshot || !snapshot.IsCreated() ||
                snapshot.width != source.pixelWidth || snapshot.height != source.pixelHeight)
            {
                Release();
                if (source.cullingMask != 0 && source.pixelWidth > 0 && source.pixelHeight > 0)
                    BeginCapture(source);
                return;
            }

            if (!capturePending) return;
            StopCapture();
            if (!captured) return;

            cullingMask = camera.cullingMask;
            clearFlags = camera.clearFlags;
            // Keep the camera available to the player rig, Camera.main consumers and spatial audio.
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            image.texture = snapshot;
            image.enabled = true;
            frozen = true;
        }

        void BeginCapture(Camera source)
        {
            camera = source;
            snapshot = new RenderTexture(source.pixelWidth, source.pixelHeight, 0, RenderTextureFormat.Default)
            {
                name = "Course map world backdrop",
                wrapMode = TextureWrapMode.Clamp
            };
            if (!snapshot.Create())
            {
                Release();
                return;
            }
            CameraCaptureBridge.AddCaptureAction(camera, capture);
            capturePending = true;
        }

        void Capture(RenderTargetIdentifier color, CommandBuffer commands)
        {
            commands.Blit(color, snapshot);
            captured = true;
        }

        void StopCapture()
        {
            if (capturePending && camera) CameraCaptureBridge.RemoveCaptureAction(camera, capture);
            capturePending = false;
        }

        internal void Release()
        {
            if (!snapshot && !camera) return;
            StopCapture();
            if (frozen && camera)
            {
                camera.cullingMask = cullingMask;
                camera.clearFlags = clearFlags;
            }
            if (image)
            {
                image.enabled = false;
                image.texture = null;
            }
            if (snapshot)
            {
                snapshot.Release();
                UnityEngine.Object.Destroy(snapshot);
            }
            snapshot = null;
            camera = null;
            captured = frozen = false;
        }
    }
}
