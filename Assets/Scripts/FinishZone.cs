using UnityEngine;

/// <summary>Meta del nivel: al entrar el jugador, se detiene el cronómetro.</summary>
[RequireComponent(typeof(Collider))]
public class FinishZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerRespawn>() == null) return;
        if (ParkourTimer.Instance != null) ParkourTimer.Instance.Finish();
    }
}
