using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    // One private copy of the project's Player map, enabled only for the owning player.
    public sealed class GameplayInput : MonoBehaviour
    {
        public InputActionAsset actions;
        InputActionAsset instance;
        public InputAction Launch { get; private set; }
        public InputAction LaunchCancel { get; private set; }
        public InputAction Attack { get; private set; }
        public InputAction Grab { get; private set; }
        bool capturedLastFrame;
        int interactFrame = -1;
        public bool AttackReady => capturedLastFrame;
        void LateUpdate() => capturedLastFrame = CapturedCursor;
        public InputAction Move { get; private set; }
        public InputAction Jump { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction Interact { get; private set; }
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Headless smoke has no OS cursor. Never enabled by normal gameplay or release builds.
        public bool SimulateCapturedCursor { private get; set; }
#endif
        bool CapturedCursor
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (SimulateCapturedCursor) return true;
#endif
                return Cursor.lockState == CursorLockMode.Locked;
            }
        }
        public bool AcceptsInput => CapturedCursor && !DoorPuzzleUI.IsPanelVisible &&
            LaunchCancel?.WasPressedThisFrame() != true;

        public bool TryConsumeInteract()
        {
            if (!AcceptsInput || interactFrame == Time.frameCount || Interact?.WasPressedThisFrame() != true)
                return false;
            interactFrame = Time.frameCount;
            return true;
        }

        public void Activate()
        {
            if (instance != null) return;
            instance = Instantiate(actions);
            var map = instance.FindActionMap("Player", true);
            Move = map.FindAction("Move", true); Jump = map.FindAction("Jump", true);
            Sprint = map.FindAction("Sprint", true); Interact = map.FindAction("Interact", true);
            Attack = map.FindAction("Attack", true);
            Grab = map.AddAction("GrabOpponent", InputActionType.Button, "<Keyboard>/f");
            Launch = map.AddAction("ReturnLaunch", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/r");
            LaunchCancel = map.AddAction("ReturnCancel", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/escape");
            map.Enable();
        }
        void OnDestroy() { if (instance != null) { instance.Disable(); Destroy(instance); } }
    }
}
