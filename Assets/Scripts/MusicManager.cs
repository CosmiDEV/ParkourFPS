using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    const string MenuScene = "MainMenu";

    [Range(0f, 1f)] public float menuVolume = 0.05f;
    [Range(0f, 1f)] public float levelVolume = 0.6f;
    [Range(0f, 1f)] public float finishVolume = 0.10f;
    public float riseTime = 3f;
    public float fadeDownTime = 2f;

    AudioSource src;
    Collider finishCol;
    Transform player;
    bool finished;
    float target;
    float speed;

    // Creación automática
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("MusicManager");
        DontDestroyOnLoad(go);
        go.AddComponent<MusicManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        src = gameObject.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;

        AudioClip clip = Resources.Load<AudioClip>("Music/Theme");
        if (clip == null)
            Debug.LogWarning("[Música] No encuentro Assets/Resources/Music/Theme (mp3/ogg/wav).");
        else
        {
            src.clip = clip;
            src.Play();
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Cambio de escena
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        finished = false;
#if UNITY_2023_1_OR_NEWER
        FinishZone fz = FindFirstObjectByType<FinishZone>();
        PlayerRespawn pr = FindFirstObjectByType<PlayerRespawn>();
#else
        FinishZone fz = FindObjectOfType<FinishZone>();
        PlayerRespawn pr = FindObjectOfType<PlayerRespawn>();
#endif
        finishCol = fz != null ? fz.GetComponent<Collider>() : null;
        player = pr != null ? pr.transform : null;

        if (scene.name == MenuScene) SetTarget(menuVolume, riseTime);
        else SetTarget(levelVolume, riseTime);
    }

    // Volumen
    public void FadeOut()
    {
        if (finished) return;
        finished = true;
        SetTarget(finishVolume, fadeDownTime);
    }

    void SetTarget(float volume, float seconds)
    {
        target = volume;
        speed = Mathf.Abs(volume - src.volume) / Mathf.Max(0.01f, seconds);
    }

    void Update()
    {
        if (!finished && finishCol != null && player != null && finishCol.bounds.Contains(player.position))
            FadeOut();

        src.volume = Mathf.MoveTowards(src.volume, target, speed * Time.unscaledDeltaTime);
    }
}
