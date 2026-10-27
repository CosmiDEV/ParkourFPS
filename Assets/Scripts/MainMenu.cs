using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string title = "PARKOUR FPS";
    public string subtitle = "Un caballero corre por su vida";
    public string firstLevel = "ParkourMedievalLevel1";
    public string[] levelScenes = { "ParkourMedievalLevel1", "ParkourMedievalLevel2", "ParkourMedievalLevel3" };

    GUIStyle titleStyle, subStyle, buttonStyle, smallStyle, smallButton;
    string message;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
    }

    // Carga de escenas
    void Load(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            message = "Falta la escena '" + scene + "' en Build Settings";
            return;
        }
        SceneManager.LoadScene(scene);
    }

    void OnGUI()
    {
        // Estilos
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            subStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            subStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 32, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            smallStyle.normal.textColor = new Color(1f, 0.95f, 0.6f);
            smallButton = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        }

        // Título
        float w = Screen.width, h = Screen.height;
        GUI.Label(new Rect(0, h * 0.10f, w, 100), title, titleStyle);
        GUI.Label(new Rect(0, h * 0.10f + 95, w, 40), subtitle, subStyle);

        // Botones
        float bw = 320, bh = 64, x = (w - bw) * 0.5f, y = h * 0.40f;
        if (GUI.Button(new Rect(x, y, bw, bh), "JUGAR", buttonStyle)) Load(firstLevel);

        GUI.Label(new Rect(0, y + 85, w, 30), "Elegir nivel", smallStyle);
        float sw = 100, gap = 10, total = levelScenes.Length * sw + (levelScenes.Length - 1) * gap;
        for (int i = 0; i < levelScenes.Length; i++)
            if (GUI.Button(new Rect((w - total) * 0.5f + i * (sw + gap), y + 120, sw, 46), "Nivel " + (i + 1), smallButton))
                Load(levelScenes[i]);

        if (GUI.Button(new Rect(x, y + 195, bw, bh), "SALIR", buttonStyle))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Mensajes y controles
        if (!string.IsNullOrEmpty(message))
            GUI.Label(new Rect(0, h * 0.88f, w, 30), message, smallStyle);

        GUI.Label(new Rect(0, h - 40, w, 30), "WASD mover · Espacio saltar · Ctrl/C deslizar · R reaparecer · T reiniciar", smallStyle);
    }
}
