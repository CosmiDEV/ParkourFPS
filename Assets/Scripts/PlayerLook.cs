using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cámara en primera persona. Va en la cámara (hija del jugador).
/// - Mirada con el mouse (el cuerpo gira en horizontal, la cámara en vertical).
/// - FOV que aumenta con la velocidad.
/// - Inclinación leve al moverte de lado y más marcada en paredes.
/// Sin suavizado en la mirada a propósito: la respuesta es inmediata.
/// </summary>
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

    Camera cam;
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
        cam.fieldOfView = baseFov;
        if (lockCursorOnStart) LockCursor(true);
    }

    void Update()
    {
        HandleCursor();

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

    void UpdateEffects(float dt)
    {
        if (movement == null) return;

        // FOV según velocidad
        float t = Mathf.InverseLerp(fovMinSpeed, fovMaxSpeed, movement.HorizontalSpeed);
        float targetFov = baseFov + maxFovBoost * t;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmooth * dt));

        // Inclinación (roll). Si se siente al revés, cambia el signo de los valores.
        float targetRoll = -movement.MoveInput.x * strafeTilt;
        if (movement.IsOnWallAirborne && movement.WallSide != 0f)
            targetRoll = movement.WallSide * wallTilt;

        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-tiltSmooth * dt));
    }

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

    /// <summary>Gira al jugador a un ángulo horizontal (lo usa el reaparecer).</summary>
    public void SetYaw(float newYaw)
    {
        yaw = newYaw;
        pitch = 0f;
    }
}
