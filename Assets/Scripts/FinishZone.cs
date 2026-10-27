using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinishZone : MonoBehaviour
{
    [Tooltip("Objetos que se activan al terminar (los portales).")]
    public GameObject[] activateOnFinish;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerRespawn>() == null) return;

        if (ParkourTimer.Instance != null) ParkourTimer.Instance.Finish();

        if (activateOnFinish == null) return;
        foreach (GameObject go in activateOnFinish)
            if (go != null) go.SetActive(true);
    }
}
