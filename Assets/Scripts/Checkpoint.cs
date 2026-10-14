using UnityEngine;

/// <summary>
/// Checkpoint: al tocarlo, el jugador reaparecerá aquí (tecla R o al caer).
/// Usa el SetCheckpoint() que ya tiene PlayerRespawn.
/// Necesita un Collider con "Is Trigger" activado.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Punto exacto donde aparece el jugador (su centro). Si está vacío usa este objeto.")]
    public Transform spawnPoint;
    public int number = 1;

    [Header("Visual")]
    public Renderer[] flagRenderers;
    public Color inactiveColor = new Color(0.6f, 0.6f, 0.65f);
    public Color activeColor = new Color(0.2f, 1f, 0.5f);

    bool activated;

    void Start()
    {
        Paint(inactiveColor);
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();
        if (respawn == null) return;

        respawn.SetCheckpoint(spawnPoint != null ? spawnPoint : transform);

        if (!activated)
        {
            activated = true;
            Paint(activeColor);
            if (ParkourTimer.Instance != null)
                ParkourTimer.Instance.ShowMessage("Checkpoint " + number);
        }
    }

    void Paint(Color c)
    {
        if (flagRenderers == null) return;
        foreach (Renderer r in flagRenderers)
            if (r != null) r.material.color = c;
    }
}
