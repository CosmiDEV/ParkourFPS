using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    public enum Mode { LoadScene, ReloadCurrent }

    public Mode mode = Mode.LoadScene;
    [Tooltip("Nombre de la escena a cargar (solo en modo LoadScene).")]
    public string sceneName;

    [Header("Animación")]
    [Tooltip("Objeto que 'late' para dar vida al portal (opcional).")]
    public Transform pulseTarget;
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.06f;

    [Tooltip("Segundos tras aparecer antes de poder usarlo (evita cruzarlo sin querer al llegar a la meta).")]
    public float armDelay = 1f;

    Vector3 baseScale = Vector3.one;
    bool used;
    bool warned;
    float armedAt;

    void OnEnable()
    {
        armedAt = Time.time + armDelay;
    }

    void Awake()
    {
        if (pulseTarget != null) baseScale = pulseTarget.localScale;
    }

    // Animación
    void Update()
    {
        if (pulseTarget == null) return;
        float s = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        pulseTarget.localScale = baseScale * s;
    }

    // Uso del portal
    void OnTriggerEnter(Collider other) { TryUse(other); }

    void OnTriggerStay(Collider other) { TryUse(other); }

    void TryUse(Collider other)
    {
        if (used || Time.time < armedAt) return;
        if (other.GetComponent<PlayerRespawn>() == null) return;

        string target = mode == Mode.ReloadCurrent ? SceneManager.GetActiveScene().name : sceneName;

        if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
        {
            if (!warned)
            {
                warned = true;
                Debug.LogError("[Portal] La escena '" + target + "' no está en Build Settings.");
                if (ParkourTimer.Instance != null)
                    ParkourTimer.Instance.ShowMessage("Falta la escena '" + target + "' en Build Settings", 4f);
            }
            return;
        }

        used = true;
        SceneManager.LoadScene(target);
    }
}
