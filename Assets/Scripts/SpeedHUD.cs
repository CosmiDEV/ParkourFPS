using UnityEngine;

public class SpeedHUD : MonoBehaviour
{
    public PlayerMovement movement;

    GUIStyle labelStyle;
    GUIStyle crosshairStyle;

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
    }

    void OnGUI()
    {
        if (movement == null) return;

        // Estilos
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;

            crosshairStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            crosshairStyle.normal.textColor = Color.white;
        }

        // Estado
        string state = movement.IsSwimming ? "Nadando"
                     : movement.IsSliding ? "Deslizando"
                     : movement.IsCrouched ? "Agachado"
                     : movement.IsGrounded ? "Suelo"
                     : movement.IsOnWallAirborne ? "Pared"
                     : "Aire";

        // Textos y mira
        GUI.Label(new Rect(20, 20, 600, 36), $"Velocidad: {movement.HorizontalSpeed:F1} m/s", labelStyle);
        GUI.Label(new Rect(20, 54, 600, 36), $"Estado: {state}", labelStyle);
        GUI.Label(new Rect(20, 88, 600, 36), $"Dobles saltos: {movement.AirJumpsLeft}", labelStyle);

        GUI.Label(new Rect(Screen.width / 2f - 20f, Screen.height / 2f - 20f, 40f, 40f), "+", crosshairStyle);
    }
}
