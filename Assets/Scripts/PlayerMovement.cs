using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class HorrorPlayerController : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference moveAction;       // Vector2 (WASD)
    public InputActionReference lookAction;       // Vector2 (Mouse Delta)
    public InputActionReference jumpAction;       // Button (Space)
    public InputActionReference toggleShakeAction;// Button (F)

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Mouse Look")]
    public Transform cameraTransform;
    public float sensitivity = 1.2f;
    public float minLookX = -80f;
    public float maxLookX = 80f;

    [Header("Camera Shake Test")]
    public bool testCameraShake = false;
    public float shakeAmplitude = 0.05f;
    public float shakeFrequency = 8f;

    CharacterController controller;

    float rotationX;
    float verticalVelocity;

    Vector3 camOriginalPos;
    float shakeTime;

    void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        toggleShakeAction.action.Enable();

        jumpAction.action.performed += OnJump;
        toggleShakeAction.action.performed += OnToggleShake;
    }

    void OnDisable()
    {
        jumpAction.action.performed -= OnJump;
        toggleShakeAction.action.performed -= OnToggleShake;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (!cameraTransform) cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        camOriginalPos = cameraTransform.localPosition;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        HandleCameraShake();
    }

    void HandleLook()
    {
        Vector2 look = lookAction.action.ReadValue<Vector2>();

        float mouseX = look.x * sensitivity * Time.deltaTime;
        float mouseY = look.y * sensitivity * Time.deltaTime;

        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minLookX, maxLookX);

        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y) * moveSpeed;

        if (controller.isGrounded)
            verticalVelocity = -1f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    void OnJump(InputAction.CallbackContext ctx)
    {
        if (controller.isGrounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    void OnToggleShake(InputAction.CallbackContext ctx)
    {
        testCameraShake = !testCameraShake;
    }

    void HandleCameraShake()
    {
        if (!testCameraShake)
        {
            cameraTransform.localPosition = camOriginalPos;
            shakeTime = 0;
            return;
        }

        shakeTime += Time.deltaTime * shakeFrequency;

        float offsetX = (Mathf.PerlinNoise(shakeTime, 0f) - 0.5f) * 2f * shakeAmplitude;
        float offsetY = (Mathf.PerlinNoise(0f, shakeTime) - 0.5f) * 2f * shakeAmplitude;

        cameraTransform.localPosition =
            camOriginalPos + new Vector3(offsetX, offsetY, 0f);
    }
}
