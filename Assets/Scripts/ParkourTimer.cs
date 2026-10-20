using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Cronómetro del nivel (sin Canvas, igual que SpeedHUD). Va en el objeto Player.
/// - Arranca solo cuando te mueves por primera vez.
/// - Se detiene en la meta (FinishZone) y guarda tu mejor tiempo por nivel.
/// - T = reiniciar la carrera desde el inicio (en cualquier momento).
/// </summary>
public class ParkourTimer : MonoBehaviour
{
    public static ParkourTimer Instance { get; private set; }

    public PlayerMovement movement;
    public PlayerRespawn respawn;
    [Tooltip("Identificador para guardar el mejor tiempo de este nivel.")]
    public string levelId = "Level1";
    [Tooltip("Texto que se muestra al terminar el nivel.")]
    public string finishHint = "Cruza un portal";

    float time;
    float bestTime;
    bool running;
    bool finished;
    bool newRecord;

    string message;
    float messageUntil;

    GUIStyle timeStyle;
    GUIStyle smallStyle;
    GUIStyle centerStyle;

    string BestKey => "ParkourBest_" + levelId;

    void Awake()
    {
        Instance = this;
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (respawn == null) respawn = GetComponent<PlayerRespawn>();
        bestTime = PlayerPrefs.GetFloat(BestKey, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!running && !finished && movement != null && movement.HorizontalSpeed > 0.5f)
            running = true;

        if (running) time += Time.deltaTime;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.tKey.wasPressedThisFrame) Restart();
    }

    public void Finish()
    {
        if (finished) return;
        finished = true;
        running = false;

        newRecord = bestTime <= 0f || time < bestTime;
        if (newRecord)
        {
            bestTime = time;
            PlayerPrefs.SetFloat(BestKey, bestTime);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Reinicia el nivel desde cero (recarga la escena para dejar todo como al inicio).</summary>
    public void Restart()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (Application.CanStreamedLevelBeLoaded(scene))
        {
            SceneManager.LoadScene(scene);
            return;
        }

        // Plan B si la escena no está en Build Settings: volver al inicio sin recargar
        time = 0f;
        running = false;
        finished = false;
        newRecord = false;
        if (respawn != null)
        {
            respawn.SetCheckpoint(null);
            respawn.Respawn();
        }
        ShowMessage("¡Carrera reiniciada!");
    }

    public void ShowMessage(string text, float seconds = 2f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    static string Format(float t)
    {
        int m = (int)(t / 60f);
        int s = (int)(t % 60f);
        int cs = (int)((t * 100f) % 100f);
        return string.Format("{0:00}:{1:00}.{2:00}", m, s, cs);
    }

    void OnGUI()
    {
        if (timeStyle == null)
        {
            timeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            timeStyle.normal.textColor = Color.white;

            smallStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            smallStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);

            centerStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            centerStyle.normal.textColor = Color.white;
        }

        float w = Screen.width;
        GUI.Label(new Rect(w - 320f, 16f, 300f, 44f), Format(time), timeStyle);
        if (bestTime > 0f)
            GUI.Label(new Rect(w - 320f, 60f, 300f, 28f), "Mejor: " + Format(bestTime), smallStyle);

        if (Time.time < messageUntil && !finished)
            GUI.Label(new Rect(0f, Screen.height * 0.18f, w, 50f), message, centerStyle);

        if (finished)
        {
            string title = newRecord ? "¡NUEVO RÉCORD!" : "¡META!";
            GUI.Label(new Rect(0f, Screen.height * 0.25f, w, 50f), title, centerStyle);
            GUI.Label(new Rect(0f, Screen.height * 0.25f + 46f, w, 50f), "Tiempo: " + Format(time), centerStyle);
            GUI.Label(new Rect(0f, Screen.height * 0.25f + 92f, w, 50f), finishHint, centerStyle);
            if (Time.time < messageUntil)
                GUI.Label(new Rect(0f, Screen.height * 0.25f + 138f, w, 50f), message, centerStyle);
        }
    }
}
