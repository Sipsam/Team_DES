using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float distance = 5f;
    [SerializeField] private float height;

    [Header("Look")]
    [SerializeField, Min(0f)] private float sensitivity = 0.15f;
    [SerializeField] private float minimumPitch = -25f;
    [SerializeField] private float maximumPitch = 65f;
    [SerializeField, Min(0f)] private float positionSmoothTime = 0.05f;

    private float yaw;
    private float pitch = 15f;
    private Vector3 positionVelocity;
    private bool hasInitializedPosition;

    private void Awake()
    {
        yaw = transform.eulerAngles.y;
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        HandleCursor();

        if (Cursor.lockState != CursorLockMode.Locked || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * sensitivity;
        pitch -= mouseDelta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 lookPoint = target.position + Vector3.up * height;
        Vector3 desiredPosition = lookPoint - orbitRotation * Vector3.forward * distance;

        if (!hasInitializedPosition || positionSmoothTime <= 0f)
        {
            transform.position = desiredPosition;
            hasInitializedPosition = true;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);
        }

        transform.rotation = orbitRotation;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        hasInitializedPosition = false;
        if (target != null && !enabled)
        {
            enabled = true;
        }
    }

    public void ClearTarget(Transform expectedTarget)
    {
        if (target != expectedTarget)
        {
            return;
        }

        target = null;
        enabled = false;
    }

    private static void HandleCursor()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                 !NetworkDebugUI.IsPointerOverMenu(mouse.position.ReadValue()) &&
                 !DoorPuzzleUI.IsPointerOverPanel(mouse.position.ReadValue()))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
