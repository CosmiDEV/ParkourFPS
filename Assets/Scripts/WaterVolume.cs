using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
public class WaterVolume : MonoBehaviour
{
    static readonly List<WaterVolume> all = new List<WaterVolume>();

    [Header("Aspecto del agua")]
    [Tooltip("Si el objeto tiene un MeshRenderer, se le asigna un material de agua translúcido al iniciar.")]
    public bool applyWaterLook = true;
    public Color waterColor = new Color(0.10f, 0.45f, 0.75f, 0.45f);

    [Header("Bajo el agua")]
    [Tooltip("Color con el que se tiñe la pantalla cuando la cámara está sumergida.")]
    public Color underwaterTint = new Color(0.05f, 0.25f, 0.45f, 0.40f);

    Collider col;
    Camera cam;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        if (applyWaterLook) ApplyLook();
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    // Consulta de agua
    public static bool TryGetWater(Vector3 point, out float surfaceY)
    {
        for (int i = 0; i < all.Count; i++)
        {
            WaterVolume w = all[i];
            if (w == null || w.col == null) continue;

            Bounds b = w.col.bounds;
            if (b.Contains(point))
            {
                surfaceY = b.max.y;
                return true;
            }
        }

        surfaceY = 0f;
        return false;
    }

    // Tinte bajo el agua
    void OnGUI()
    {
        if (col == null) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (col.bounds.Contains(cam.transform.position))
        {
            GUI.depth = 100;
            Color old = GUI.color;
            GUI.color = underwaterTint;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }

    // Aspecto
    void ApplyLook()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;

        r.sharedMaterial = CreateWaterMaterial(waterColor);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    public static Material CreateWaterMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        bool urp = shader != null;
        if (!urp) shader = Shader.Find("Standard");

        Material m = new Material(shader);
        m.color = color;

        if (urp)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.9f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        return m;
    }
}
