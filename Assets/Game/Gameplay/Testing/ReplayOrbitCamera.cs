#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TwinEdge.Gameplay
{
    [RequireComponent(typeof(Camera))]
    internal sealed class ReplayOrbitCamera : MonoBehaviour
    {
        internal Transform Target { private get; set; }
        Camera source;
        float yaw, pitch, distance = 8f;
        bool orbiting, applied;
        Pose gameplayPose;

        void OnEnable()
        {
            source = GetComponent<Camera>();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RenderPipelineManager.beginCameraRendering += ApplyView;
            RenderPipelineManager.endCameraRendering += RestoreView;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ApplyView;
            RenderPipelineManager.endCameraRendering -= RestoreView;
            RestoreView(default, source);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused) return;
            if (mouse.middleButton.wasPressedThisFrame)
            {
                orbiting = false;
                distance = 8f;
                return;
            }
            float zoom = mouse.scroll.ReadValue().y;
            if (!mouse.rightButton.isPressed && Mathf.Approximately(zoom, 0)) return;
            if (!orbiting)
            {
                yaw = transform.eulerAngles.y;
                pitch = Mathf.DeltaAngle(0, transform.eulerAngles.x);
                orbiting = true;
            }
            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * .2f;
                pitch = Mathf.Clamp(pitch - delta.y * .2f, -25f, 80f);
            }
            distance = Mathf.Clamp(distance * Mathf.Pow(1.002f, -zoom), 2f, 70f);
        }

        void ApplyView(ScriptableRenderContext context, Camera camera)
        {
            if (camera != source || !orbiting || applied || Target == null) return;
            gameplayPose = new Pose(transform.position, transform.rotation);
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var focus = Target.position + Vector3.up * 1.7f;
            var direction = rotation * Vector3.back;
            float viewDistance = distance;
            if (Physics.SphereCast(focus, .2f, direction, out var wall, distance, ~0, QueryTriggerInteraction.Ignore) &&
                !wall.transform.IsChildOf(Target))
                viewDistance = Mathf.Max(.25f, wall.distance - .1f);
            transform.SetPositionAndRotation(focus + direction * viewDistance, rotation);
            applied = true;
        }

        void RestoreView(ScriptableRenderContext context, Camera camera)
        {
            if (camera != source || !applied) return;
            transform.SetPositionAndRotation(gameplayPose.position, gameplayPose.rotation);
            applied = false;
        }

        void OnGUI() => GUI.Box(new Rect(12, Screen.height - 38, 380, 26),
            "Right drag: orbit   |   Scroll: zoom   |   Middle click: reset");
    }
}
#endif
