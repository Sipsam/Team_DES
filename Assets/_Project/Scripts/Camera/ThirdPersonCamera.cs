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
    private bool v3Target;
    private readonly RaycastHit[] occlusionHits = new RaycastHit[16];

    private void Awake()
    {
        yaw = transform.eulerAngles.y;
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
    }

    private void OnEnable()
    {
        if (!v3Target)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        HandleCursor();

        if (target == null || Cursor.lockState != CursorLockMode.Locked || Mouse.current == null)
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
        if (v3Target)
        {
            var direction = (desiredPosition - lookPoint).normalized;
            float safeDistance = distance;
            int count = Physics.SphereCastNonAlloc(lookPoint, .22f, direction, occlusionHits,
                distance, ~0, QueryTriggerInteraction.Ignore);
            // A full buffer can omit a closer wall. Preserve the unrestricted query in that case.
            var hits = count == occlusionHits.Length
                ? Physics.SphereCastAll(lookPoint, .22f, direction, distance, ~0, QueryTriggerInteraction.Ignore)
                : occlusionHits;
            for (int i = 0; i < (hits == occlusionHits ? count : hits.Length); i++)
            {
                var hit = hits[i];
                if (!hit.transform.IsChildOf(target.root)) safeDistance = Mathf.Min(safeDistance, Mathf.Max(.15f, hit.distance - .1f));
            }
            desiredPosition = lookPoint + direction * safeDistance;
            // Resolve immediately against walls; smoothing can otherwise leave the lens inside geometry.
            hasInitializedPosition = false;
        }

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
        v3Target = target != null && target.GetComponentInParent<TwinEdge.Gameplay.GameplayCharacter>() != null;
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
        v3Target = false;
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
                 !TwinEdge.Gameplay.V3GameplayBootstrap.PointerOverMenu(mouse.position.ReadValue()) &&
                 !DoorPuzzleUI.IsPointerOverPanel(mouse.position.ReadValue()))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
