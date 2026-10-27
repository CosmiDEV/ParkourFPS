using UnityEngine;

public class PortalFX : MonoBehaviour
{
    [Header("Llamas")]
    public Transform[] flames;
    public Light[] flameLights;
    public float flickerSpeed = 11f;
    [Range(0f, 0.6f)] public float flickerAmount = 0.25f;
    public bool embers = true;

    [Header("Gema")]
    public Transform gem;
    public float gemSpinSpeed = 70f;
    public float gemBobAmount = 0.08f;
    public float gemBobSpeed = 2f;

    [Header("Velo mágico")]
    public float veilPulseSpeed = 2.2f;
    [Range(0f, 1f)] public float veilPulseAmount = 0.35f;

    Vector3[] flameBaseScale;
    float[] lightBase;
    float[] seeds;
    Vector3 gemBasePos;
    float gemYaw;

    Material veilMat;
    Color veilEmission;
    bool veilHasEmission;
    const string EmissionId = "_EmissionColor";

    void Awake()
    {
        int n = flames != null ? flames.Length : 0;
        flameBaseScale = new Vector3[n];
        seeds = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (flames[i] != null) flameBaseScale[i] = flames[i].localScale;
            seeds[i] = Random.value * 100f;
        }

        int m = flameLights != null ? flameLights.Length : 0;
        lightBase = new float[m];
        for (int i = 0; i < m; i++)
            if (flameLights[i] != null) lightBase[i] = flameLights[i].intensity;

        if (gem != null) gemBasePos = gem.localPosition;

        Portal portal = GetComponent<Portal>();
        if (portal != null && portal.pulseTarget != null)
        {
            Renderer r = portal.pulseTarget.GetComponent<Renderer>();
            if (r != null)
            {
                veilMat = r.material;
                if (veilMat.HasProperty(EmissionId))
                {
                    veilEmission = veilMat.GetColor(EmissionId);
                    veilHasEmission = true;
                }
            }
        }
    }

    void Start()
    {
        if (embers && flames != null)
            foreach (Transform f in flames)
                if (f != null) CreateEmbers(f);
    }

    void OnDestroy()
    {
        if (veilMat != null) Destroy(veilMat);
    }

    void Update()
    {
        float t = Time.time;

        // Llamas
        for (int i = 0; flames != null && i < flames.Length; i++)
        {
            if (flames[i] == null) continue;
            float s = seeds[i];
            float k = 1f + (Mathf.PerlinNoise(t * flickerSpeed, s) - 0.5f) * 2f * flickerAmount;
            float sway = 1f + (Mathf.PerlinNoise(s, t * flickerSpeed * 0.8f) - 0.5f) * flickerAmount;
            flames[i].localScale = new Vector3(flameBaseScale[i].x * sway, flameBaseScale[i].y * k, flameBaseScale[i].z * sway);
        }

        for (int i = 0; flameLights != null && i < flameLights.Length; i++)
        {
            if (flameLights[i] == null) continue;
            float s = seeds.Length > 0 ? seeds[i % seeds.Length] : 0f;
            float k = 1f + (Mathf.PerlinNoise(t * flickerSpeed, s + 7f) - 0.5f) * 2f * flickerAmount;
            flameLights[i].intensity = lightBase[i] * k;
        }

        // Gema
        if (gem != null)
        {
            gemYaw += gemSpinSpeed * Time.deltaTime;
            gem.localPosition = gemBasePos + Vector3.up * (Mathf.Sin(t * gemBobSpeed) * gemBobAmount);
            gem.localRotation = Quaternion.Euler(0f, gemYaw, 45f);
        }

        // Velo
        if (veilMat != null && veilHasEmission)
        {
            float p = 1f + Mathf.Sin(t * veilPulseSpeed) * veilPulseAmount;
            veilMat.SetColor(EmissionId, veilEmission * p);
        }
    }

    // Brasas
    static void CreateEmbers(Transform flame)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return;

        var go = new GameObject("Brasas");
        go.transform.SetParent(flame, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 0.9f;
        main.startSize = 0.07f;
        main.startColor = new Color(1f, 0.75f, 0.3f, 1f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var em = ps.emission;
        em.rateOverTime = 12f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.2f;

        var life = ps.colorOverLifetime;
        life.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.30f, 0.10f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        life.color = g;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = new Material(sh);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        ps.Play();
    }
}
