using System.Collections.Generic;
using System;
using TwinEdge.Gameplay;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public sealed class LobbyCharacterPreview : IDisposable
    {
        readonly GameObject stage;
        readonly Camera camera;
        readonly RenderTexture texture;
        readonly RawImage image;
        readonly MeshRenderer cape;
        readonly MaterialPropertyBlock capeProperties = new();
        byte capeIndex = byte.MaxValue;
        bool disposed;
        PlayableGraph resultGraph;
        AnimationClipPlayable resultMotion;
        float resultDuration, resultTime;

        public LobbyCharacterPreview(RawImage image, Transform source, int slot, AnimationClip result = null)
        {
            this.image = image;
            int layer = LayerMask.NameToLayer("Lobby Preview");
            int mask = 1 << layer;
            stage = new GameObject(result ? "Result character preview" : "Lobby character preview " + slot);
            stage.transform.position = new Vector3(slot * 32, 10000, 0);
            var model = CopyVisual(source, stage.transform, layer, out cape, out var animator);
            model.localRotation = Quaternion.Euler(0, result ? -10 : slot == 0 ? -20 : 20, 0);
            if (result)
            {
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                resultGraph = PlayableGraph.Create(result.name);
                resultGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                resultMotion = AnimationClipPlayable.Create(resultGraph, result);
                var output = AnimationPlayableOutput.Create(resultGraph, "Result portrait", animator);
                output.SetSourcePlayable(resultMotion);
                resultDuration = Mathf.Max(0, result.length - 1 / result.frameRate);
                resultGraph.Play();
                resultGraph.Evaluate(0);
            }
            ApplyUnassignedCape();

            camera = new GameObject("Portrait Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(47, 43, 37, 255);
            camera.cullingMask = mask;
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 12;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderShadows = false;
            Bounds bounds = new(model.position, Vector3.zero);
            foreach (var render in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(render.bounds);
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * (result ? 1.48f : 1.25f);
            camera.transform.SetPositionAndRotation(bounds.center + new Vector3(0, .03f, 5),
                Quaternion.Euler(0, 180, 0));

            var light = new GameObject("Portrait Key", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(stage.transform, false);
            light.type = LightType.Point;
            light.cullingMask = mask;
            light.transform.position = bounds.center + new Vector3(-.7f, 1.5f, 2);
            light.range = 6;
            light.color = new Color32(255, 232, 200, 255);
            light.intensity = 8f;

            int size = result ? 512 : 320;
            texture = new RenderTexture(size, size, 16) { name = result ? "Result Portrait" : "Lobby Portrait " + slot };
            camera.targetTexture = texture;
            image.texture = texture;
            image.enabled = true;
        }

        internal void TickResult()
        {
            if (!resultGraph.IsValid() || resultTime >= resultDuration) return;
            resultTime = Mathf.Min(resultTime + Time.unscaledDeltaTime, resultDuration);
            resultMotion.SetTime(resultTime);
            resultGraph.Evaluate(0);
        }

        public void SetCape(byte index)
        {
            if (index == capeIndex) return;
            capeIndex = index;
            if (index < GameplayCharacter.CapeColorCount)
                GameplayCharacter.ApplyCapeAppearance(cape, capeProperties, index);
            else ApplyUnassignedCape();
        }

        void ApplyUnassignedCape()
        {
            capeProperties.SetColor("_BaseColor", PresentationTheme.Stone);
            cape.SetPropertyBlock(capeProperties);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (resultGraph.IsValid()) resultGraph.Destroy();
            if (image)
            {
                image.texture = null;
                image.enabled = false;
            }
            if (camera)
            {
                camera.enabled = false;
                camera.targetTexture = null;
            }
            if (stage) stage.SetActive(false);
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(stage);
        }

        static Transform CopyVisual(Transform source, Transform parent, int layer, out MeshRenderer cape, out Animator animator)
        {
            var nodes = new Dictionary<Transform, Transform>();
            var root = CopyTransforms(source, parent, layer, nodes);
            cape = null;
            animator = null;
            foreach (var pair in nodes)
            {
                var original = pair.Key;
                var copy = pair.Value.gameObject;
                if (original.TryGetComponent(out MeshRenderer mesh))
                {
                    copy.AddComponent<MeshFilter>().sharedMesh = original.GetComponent<MeshFilter>().sharedMesh;
                    var renderer = copy.AddComponent<MeshRenderer>();
                    CopyAppearance(mesh, renderer);
                    if (original.name == "Cape") cape = renderer;
                }
                if (original.TryGetComponent(out SkinnedMeshRenderer skin))
                {
                    var renderer = copy.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = skin.sharedMesh;
                    renderer.localBounds = skin.localBounds;
                    renderer.rootBone = skin.rootBone ? nodes[skin.rootBone] : null;
                    var bones = skin.bones;
                    for (int i = 0; i < bones.Length; i++) bones[i] = bones[i] ? nodes[bones[i]] : null;
                    renderer.bones = bones;
                    CopyAppearance(skin, renderer);
                }
                if (original.TryGetComponent(out Animator animation))
                {
                    animator = copy.AddComponent<Animator>();
                    animator.avatar = animation.avatar;
                    animator.runtimeAnimatorController = animation.runtimeAnimatorController;
                    animator.applyRootMotion = false;
                    animator.Play("Idle", 0, 0);
                }
            }
            return root;
        }

        static Transform CopyTransforms(Transform source, Transform parent, int layer,
            Dictionary<Transform, Transform> nodes)
        {
            var copy = new GameObject(source.name) { layer = layer }.transform;
            copy.SetParent(parent, false);
            copy.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            copy.localScale = source.localScale;
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            nodes.Add(source, copy);
            foreach (Transform child in source) CopyTransforms(child, copy, layer, nodes);
            return copy;
        }

        static void CopyAppearance(Renderer source, Renderer target)
        {
            target.sharedMaterials = source.sharedMaterials;
            target.enabled = source.enabled;
            target.shadowCastingMode = ShadowCastingMode.Off;
            target.receiveShadows = false;
        }
    }
}
