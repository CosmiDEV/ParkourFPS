using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reaparece al jugador si cae por debajo de cierta altura o si presiona R.
/// Si no asignas un punto de reaparición, usa la posición inicial.
/// </summary>
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

    /// <summary>Cambia el punto de reaparición (útil para checkpoints en el futuro).</summary>
    public void SetCheckpoint(Transform newPoint)
    {
        respawnPoint = newPoint;
    }

    public void Respawn()
    {
        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPosition;
        float yaw = respawnPoint != null ? respawnPoint.eulerAngles.y : startYaw;

        // El CharacterController hay que apagarlo para poder teletransportar
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;

        movement.ResetState();
        if (look != null) look.SetYaw(yaw);
    }
}
