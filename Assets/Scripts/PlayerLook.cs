using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class PlayerLook : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El objeto Player (el padre de esta cámara).")]
    public Transform playerBody;
    [Tooltip("El script PlayerMovement del Player.")]
    public PlayerMovement movement;

    [Header("Mirada")]
    [Tooltip("Grados por píxel de movimiento del mouse.")]
    public float sensitivity = 0.1f;
    public float maxPitch = 89f;
    public bool lockCursorOnStart = true;

    [Header("FOV dinámico (FOV vertical)")]
    public float baseFov = 70f;
    public float maxFovBoost = 15f;
    public float fovMinSpeed = 8f;
    public float fovMaxSpeed = 16f;
    public float fovSmooth = 6f;

    [Header("Inclinación de cámara")]
    [Tooltip("Grados de inclinación al moverte de lado (A/D).")]
    public float strafeTilt = 2.5f;
    [Tooltip("Grados de inclinación al estar junto a una pared en el aire.")]
    public float wallTilt = 12f;
    public float tiltSmooth = 10f;

    [Header("Deslizamiento / agacharse")]
    [Tooltip("Cuánto baja la cámara al deslizarte o agacharte (metros).")]
    public float crouchCameraDrop = 0.8f;
    [Tooltip("Qué tan rápido baja/sube la cámara.")]
    public float crouchCameraSmooth = 14f;
    [Tooltip("FOV extra mientras te deslizas.")]
    public float slideFovBoost = 6f;

    Camera cam;
    Vector3 baseLocalPos;
    float camDrop;
    float yaw;
    float pitch;
    float roll;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (playerBody != null) yaw = playerBody.eulerAngles.y;
        baseLocalPos = transform.localPosition;
        cam.fieldOfView = baseFov;
        if (lockCursorOnStart) LockCursor(true);
    }

    void Update()
    {
        HandleCursor();

        // Mirada
        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        }

        if (playerBody != null)
            playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

        UpdateEffects(Time.deltaTime);
        transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    // Efectos de cámara
    void UpdateEffects(float dt)
    {
        if (movement == null) return;

        // FOV
        float t = Mathf.InverseLerp(fovMinSpeed, fovMaxSpeed, movement.HorizontalSpeed);
        float targetFov = baseFov + maxFovBoost * t + (movement.IsSliding ? slideFovBoost : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmooth * dt));

        // Inclinación
        float targetRoll = -movement.MoveInput.x * strafeTilt;
        if (movement.IsOnWallAirborne && movement.WallSide != 0f)
            targetRoll = movement.WallSide * wallTilt;

        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-tiltSmooth * dt));

        // Altura de cámara
        float targetDrop = movement.IsCrouched ? crouchCameraDrop : 0f;
        camDrop = Mathf.Lerp(camDrop, targetDrop, 1f - Mathf.Exp(-crouchCameraSmooth * dt));
        transform.localPosition = baseLocalPos + Vector3.down * camDrop;
    }

    // Cursor
    void HandleCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            LockCursor(false);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void SetYaw(float newYaw)
    {
        yaw = newYaw;
        pitch = 0f;
    }
}
