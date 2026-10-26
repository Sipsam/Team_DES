using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;
    [SerializeField] private Transform cameraTransform;

    [Header("Jump and Gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField, Min(0f)] private float gravity = 20f;
    [SerializeField, Min(0f)] private float groundedDownForce = 2f;

    private CharacterController characterController;
    private float verticalSpeed;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        IsGrounded = characterController.isGrounded;

        if (IsGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -groundedDownForce;
        }

        Vector2 input = ReadMovementInput();
        Vector3 moveDirection = GetCameraRelativeDirection(input);

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        Keyboard keyboard = Keyboard.current;
        if (IsGrounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        verticalSpeed -= gravity * Time.deltaTime;
        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalSpeed;
        characterController.Move(velocity * Time.deltaTime);

        IsGrounded = characterController.isGrounded;
    }

    public void ResetVerticalVelocity()
    {
        verticalSpeed = 0f;
        IsGrounded = false;
    }

    private static Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed) horizontal += 1f;
        if (keyboard.sKey.isPressed) vertical -= 1f;
        if (keyboard.wKey.isPressed) vertical += 1f;

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Transform reference = cameraTransform;
        if (reference == null && Camera.main != null)
        {
            reference = Camera.main.transform;
        }

        Vector3 forward = reference != null ? reference.forward : transform.forward;
        Vector3 right = reference != null ? reference.right : transform.right;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;

        Vector3 direction = forward * input.y + right * input.x;
        return Vector3.ClampMagnitude(direction, 1f);
    }
}
