using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Opcional. Si está vacío, reaparece donde empezó.")]
    public Transform respawnPoint;
    [Tooltip("Si el jugador baja de esta altura (eje Y), reaparece.")]
    public float killHeight = -20f;

    CharacterController controller;
    PlayerMovement movement;
    PlayerLook look;
    Vector3 startPosition;
    float startYaw;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
        look = GetComponentInChildren<PlayerLook>();
        startPosition = transform.position;
        startYaw = transform.eulerAngles.y;
    }

    void Update()
    {
        bool pressedR = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        if (transform.position.y < killHeight || pressedR)
            Respawn();
    }

    // Reaparición
    public void SetCheckpoint(Transform newPoint)
    {
        respawnPoint = newPoint;
    }

    public void Respawn()
    {
        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPosition;
        float yaw = respawnPoint != null ? respawnPoint.eulerAngles.y : startYaw;

        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;

        movement.ResetState();
        if (look != null) look.SetYaw(yaw);
    }
}
