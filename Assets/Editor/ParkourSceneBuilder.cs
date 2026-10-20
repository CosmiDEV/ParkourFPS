using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Construye las escenas de parkour con un clic. Menú superior:
///   Parkour > Crear TODOS los niveles   (recomendado: crea Nivel 1 y Nivel 2 enlazados con portales)
///   Parkour > Crear Nivel 1
///   Parkour > Crear Nivel 2
///
/// Usa tus scripts: PlayerMovement, PlayerLook, PlayerRespawn y SpeedHUD,
/// más Checkpoint, FinishZone, Portal y ParkourTimer.
/// Las distancias están calculadas con los valores por defecto de PlayerMovement
/// (maxSpeed 12, jumpHeight 2.2, gravity 25, 1 doble salto, salto en pared, slide).
/// Si cambias esos valores, ajusta los huecos en BuildLevel1() / BuildLevel2().
/// </summary>
public static class ParkourSceneBuilder
{
    const string RootFolder = "Assets/Parkour";
    const string MatFolder = "Assets/Parkour/Materials";
    const string SceneFolder = "Assets/Scenes";
    const string GameTitle = "PARKOUR FPS";

    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    static string SceneName(int level) { return "ParkourLevel" + level; }
    static string ScenePath(int level) { return SceneFolder + "/" + SceneName(level) + ".unity"; }

    // ------------------------------------------------------------------
    // Menús
    // ------------------------------------------------------------------
    [MenuItem("Parkour/Crear TODOS los niveles (recomendado)", false, 0)]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildLevelScene(1);
        BuildLevelScene(2);
        EditorSceneManager.OpenScene(ScenePath(1));

        Debug.Log("[Parkour] Niveles creados en " + SceneFolder + ". Abre ParkourLevel1 y dale Play.");
        EditorUtility.DisplayDialog(GameTitle,
            "Listo: se crearon los 2 niveles en " + SceneFolder + " y se agregaron a Build Settings.\n\n" +
            "Se abrió el Nivel 1. Dale Play.\n\n" +
            "WASD mover · Espacio saltar (doble salto)\n" +
            "Ctrl o C deslizar · Salta contra las paredes\n" +
            "R reaparecer · T reiniciar nivel", "Listo");
    }

    [MenuItem("Parkour/Crear Nivel 1", false, 1)]
    public static void BuildOnlyLevel1()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildLevelScene(1);
        Debug.Log("[Parkour] Nivel 1 creado en " + ScenePath(1));
    }

    [MenuItem("Parkour/Crear Nivel 2", false, 2)]
    public static void BuildOnlyLevel2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildLevelScene(2);
        Debug.Log("[Parkour] Nivel 2 creado en " + ScenePath(2));
    }

    // ------------------------------------------------------------------
    static void BuildLevelScene(int level)
    {
        EnsureFolder("Assets", "Parkour");
        EnsureFolder(RootFolder, "Materials");
        EnsureFolder("Assets", "Scenes");
        matCache.Clear();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        SetupEnvironment(level);

        Vector3 spawn;
        float killHeight;
        string hint;
        if (level == 1) BuildLevel1(out spawn, out killHeight, out hint);
        else BuildLevel2(out spawn, out killHeight, out hint);

        BuildPlayer(spawn, killHeight, "Level" + level, hint);

        EditorSceneManager.SaveScene(scene, ScenePath(level));
        AddToBuildSettings(ScenePath(level));
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------
    // Entorno: luz, cielo, niebla
    // ------------------------------------------------------------------
    static void SetupEnvironment(int level)
    {
        var lightGo = new GameObject("Luz Direccional");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;

        if (level == 1)
        {
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
        else
        {
            // Nivel 2: atardecer
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.72f, 0.50f);
            lightGo.transform.rotation = Quaternion.Euler(28f, -65f, 0f);
        }
        RenderSettings.sun = light;

        Shader sky = Shader.Find("Skybox/Procedural");
        if (sky != null)
        {
            string path = MatFolder + "/Cielo_N" + level + ".mat";
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (skyMat == null)
            {
                skyMat = new Material(sky);
                AssetDatabase.CreateAsset(skyMat, path);
            }
            if (level == 2)
            {
                if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", new Color(0.85f, 0.45f, 0.40f));
                if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", 1.6f);
                if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", 1.1f);
            }
            EditorUtility.SetDirty(skyMat);
            RenderSettings.skybox = skyMat;
        }

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = level == 1 ? 0.006f : 0.005f;
        RenderSettings.fogColor = level == 1 ? new Color(0.72f, 0.82f, 0.93f) : new Color(0.88f, 0.62f, 0.55f);
        DynamicGI.UpdateEnvironment();
    }

    // ==================================================================
    // NIVEL 1
    // ==================================================================
    static void BuildLevel1(out Vector3 spawn, out float killHeight, out string hint)
    {
        spawn = new Vector3(0f, 0.95f, -3f);
        killHeight = -15f;
        hint = "Cruza un portal: Reintentar o Siguiente nivel";

        Transform root = new GameObject("Nivel 1").transform;

        Transform zStart = Group(root, "Zona 0 - Inicio");
        Transform z1 = Group(root, "Zona 1 - Saltos basicos");
        Transform z2 = Group(root, "Zona 2 - Doble salto");
        Transform z3 = Group(root, "Zona 3 - Subida por paredes");
        Transform z4 = Group(root, "Zona 4 - Zigzag sobre el vacio");
        Transform z5 = Group(root, "Zona 5 - Pilares");
        Transform z6 = Group(root, "Zona 6 - Salto final");
        Transform extras = Group(root, "Extras");

        Material mStart = Mat("Inicio", new Color(0.35f, 0.40f, 0.50f));
        Material mZ1 = Mat("Zona1", new Color(0.20f, 0.65f, 0.72f));
        Material mZ2 = Mat("Zona2", new Color(0.95f, 0.55f, 0.20f));
        Material mZ3F = Mat("Zona3_Suelo", new Color(0.55f, 0.40f, 0.85f));
        Material mZ3W = Mat("Zona3_Pared", new Color(0.30f, 0.20f, 0.55f));
        Material mZ4F = Mat("Zona4_Suelo", new Color(0.90f, 0.35f, 0.65f));
        Material mZ4W = Mat("Zona4_Pared", new Color(0.55f, 0.15f, 0.40f));
        Material mZ5 = Mat("Zona5", new Color(0.35f, 0.75f, 0.40f));
        Material mZ6 = Mat("Zona6", new Color(0.90f, 0.80f, 0.30f));
        Material mGold = Mat("Meta_Oro", new Color(1f, 0.82f, 0.2f));

        // ---------- Zona 0: inicio ----------
        Plat(zStart, "Plataforma inicio", 0f, 0f, -5f, 5f, 10f, mStart);
        Sign(extras, GameTitle, new Vector3(0f, 5.5f, 9f), Color.white, 1.3f);
        Sign(extras, "WASD mover  |  ESPACIO saltar (x2)  |  CTRL o C deslizar  |  salta contra las paredes\nR reaparecer  |  T reiniciar nivel",
            new Vector3(0f, 3.6f, 9f), new Color(1f, 0.95f, 0.6f), 0.4f);

        // ---------- Zona 1: saltos basicos (huecos 3, 4, 5, 6 m) ----------
        Plat(z1, "Z1 A", 0f, 0f, 8f, 14f, 6f, mZ1);
        Plat(z1, "Z1 B", 2f, 0f, 18f, 24f, 6f, mZ1);
        Plat(z1, "Z1 C", -2f, 0f, 29f, 35f, 6f, mZ1);
        Plat(z1, "Z1 D", 0f, 0f, 41f, 47f, 6f, mZ1);
        Sign(extras, "1 - SALTOS", new Vector3(0f, 5f, 16f), mZ1.color, 1f);
        MakeCheckpoint(extras, 1, new Vector3(0f, 0f, 44f));

        // ---------- Zona 2: doble salto (huecos 9, 11, 11 m) ----------
        Plat(z2, "Z2 A", 0f, 0f, 56f, 61f, 5f, mZ2);
        Plat(z2, "Z2 B", 0f, 1f, 72f, 77f, 5f, mZ2);
        Plat(z2, "Z2 C", 0f, 2f, 88f, 99f, 5f, mZ2);   // plataforma larga: toma carrerilla para la pared
        Sign(extras, "2 - DOBLE SALTO", new Vector3(0f, 6f, 54f), mZ2.color, 1f);
        MakeCheckpoint(extras, 2, new Vector3(0f, 2f, 92f));

        // ---------- Zona 3: subida por paredes (pasillo de 4 m de ancho) ----------
        Plat(z3, "Z3 suelo", 0f, 2f, 99f, 113f, 4f, mZ3F);
        Wall(z3, "Z3 pared izq", -2.5f, 103f, 119f, 1f, 15f, mZ3W);
        Wall(z3, "Z3 pared der", 2.5f, 103f, 119f, 1f, 15f, mZ3W);
        Plat(z3, "Z3 salida", 0f, 12.5f, 119f, 129f, 8f, mZ3F);
        Sign(extras, "3 - SALTO EN PARED", new Vector3(0f, 7f, 101f), mZ3F.color, 1f);
        Sign(extras, "Salta hacia una pared y vuelve a saltar", new Vector3(0f, 5.2f, 101f), Color.white, 0.4f);
        MakeCheckpoint(extras, 3, new Vector3(0f, 12.5f, 124f));

        // ---------- Zona 4: zigzag sobre el vacio ----------
        Plat(z4, "Z4 entrada", 0f, 12.5f, 129f, 137f, 4f, mZ4F);
        Wall(z4, "Z4 pared izq", -2.5f, 133f, 177f, 6f, 22f, mZ4W);
        Wall(z4, "Z4 pared der", 2.5f, 133f, 177f, 6f, 22f, mZ4W);
        Plat(z4, "Z4 descanso", 0f, 12.5f, 153f, 157f, 4f, mZ4F);
        Plat(z4, "Z4 final tramo", 0f, 12.5f, 173f, 177f, 4f, mZ4F);
        Plat(z4, "Z4 salida", 0f, 12.5f, 177f, 187f, 10f, mZ4F);
        Sign(extras, "4 - ZIGZAG", new Vector3(0f, 17.5f, 131f), mZ4F.color, 1f);
        MakeCheckpoint(extras, 4, new Vector3(0f, 12.5f, 155f));
        MakeCheckpoint(extras, 5, new Vector3(0f, 12.5f, 182f));

        // ---------- Zona 5: pilares descendentes ----------
        float[] pilarTop = { 11f, 9.5f, 8f, 6.5f, 5f };
        float[] pilarZ = { 192f, 198f, 204f, 210f, 216f };
        for (int i = 0; i < pilarTop.Length; i++)
        {
            float x = (i % 2 == 0) ? -3f : 3f;
            Plat(z5, "Pilar " + (i + 1), x, pilarTop[i], pilarZ[i] - 1.1f, pilarZ[i] + 1.1f, 2.2f, mZ5);
        }
        Sign(extras, "5 - PILARES", new Vector3(0f, 16f, 190f), mZ5.color, 1f);

        // ---------- Zona 6: salto final ----------
        Plat(z6, "Z6 despegue", 0f, 3.5f, 221f, 227f, 8f, mZ6);
        Plat(z6, "META plataforma", 0f, 2f, 239f, 253f, 12f, mGold);
        Sign(extras, "6 - SALTO FINAL", new Vector3(0f, 9f, 224f), mZ6.color, 1f);
        MakeCheckpoint(extras, 6, new Vector3(0f, 3.5f, 224f));

        // Arco de meta + zona trigger + portales (aparecen al llegar a la meta)
        BuildFinish(extras, mGold, 241f, 2f, 12f, 253f, 239f, 245f);
        GameObject retry = MakePortal(extras, "REINTENTAR", "Reintentar", new Vector3(-3.5f, 2f, 248f),
            new Color(1f, 0.55f, 0.15f), Portal.Mode.ReloadCurrent, "");
        GameObject next = MakePortal(extras, "SIGUIENTE NIVEL", "Siguiente", new Vector3(3.5f, 2f, 248f),
            new Color(0.2f, 0.9f, 0.45f), Portal.Mode.LoadScene, SceneName(2));
        LinkFinish(extras, retry, next);

        // Lava decorativa (sin collider): el jugador reaparece solo por killHeight
        MakeLava(extras, new Vector3(0f, -13f, 120f));
    }

    // ==================================================================
    // NIVEL 2: deslizamiento (mucho más difícil)
    // ==================================================================
    static void BuildLevel2(out Vector3 spawn, out float killHeight, out string hint)
    {
        spawn = new Vector3(0f, 0.95f, -3f);
        killHeight = -30f;
        hint = "Cruza un portal: Reintentar o Volver al nivel 1";

        Transform root = new GameObject("Nivel 2").transform;

        Transform zStart = Group(root, "Zona 0 - Inicio");
        Transform z1 = Group(root, "Zona 1 - Tunel bajo");
        Transform z2 = Group(root, "Zona 2 - Rampa y salto");
        Transform z3 = Group(root, "Zona 3 - Subida por paredes");
        Transform z4 = Group(root, "Zona 4 - Tunel en pendiente");
        Transform z5 = Group(root, "Zona 5 - Cadena de slide jumps");
        Transform z6 = Group(root, "Zona 6 - Rampa final");
        Transform extras = Group(root, "Extras");

        Material mStart = Mat("N2_Inicio", new Color(0.30f, 0.30f, 0.42f));
        Material mZ1 = Mat("N2_Zona1", new Color(0.25f, 0.70f, 0.75f));
        Material mCeil = Mat("N2_Techo", new Color(0.18f, 0.20f, 0.28f));
        Material mRamp = Mat("N2_Rampa", new Color(0.95f, 0.45f, 0.25f));
        Material mZ2 = Mat("N2_Zona2", new Color(0.95f, 0.60f, 0.25f));
        Material mZ3F = Mat("N2_Zona3_Suelo", new Color(0.55f, 0.40f, 0.85f));
        Material mZ3W = Mat("N2_Zona3_Pared", new Color(0.30f, 0.20f, 0.55f));
        Material mZ4F = Mat("N2_Zona4_Suelo", new Color(0.90f, 0.35f, 0.65f));
        Material mZ5 = Mat("N2_Zona5", new Color(0.35f, 0.80f, 0.45f));
        Material mZ6 = Mat("N2_Zona6", new Color(0.35f, 0.55f, 0.95f));
        Material mGold = Mat("Meta_Oro", new Color(1f, 0.82f, 0.2f));

        const float clearance = 1.25f; // altura libre de los techos bajos: solo cabes deslizándote

        // ---------- Zona 0: inicio ----------
        Plat(zStart, "Plataforma inicio", 0f, 0f, -5f, 5f, 10f, mStart);
        Sign(extras, GameTitle + "\nNIVEL 2", new Vector3(0f, 5.5f, 9f), Color.white, 1.3f);
        Sign(extras, "CTRL o C = deslizar (corre y presiona Ctrl)\nBaja rampas para ganar velocidad. Salta deslizando: slide jump.\nMantén Ctrl al aterrizar para encadenar",
            new Vector3(0f, 3.2f, 9f), new Color(1f, 0.95f, 0.6f), 0.4f);

        // ---------- Zona 1: túnel bajo (hay que deslizarse) ----------
        Plat(z1, "Tunel suelo", 0f, 0f, 5f, 24f, 6f, mZ1);
        Box(z1, "Tunel pared izq", new Vector3(-3.5f, 0.875f, 19f), new Vector3(1f, 2.75f, 10f), mCeil);
        Box(z1, "Tunel pared der", new Vector3(3.5f, 0.875f, 19f), new Vector3(1f, 2.75f, 10f), mCeil);
        Box(z1, "Tunel techo", new Vector3(0f, clearance + 0.5f, 19f), new Vector3(8f, 1f, 10f), mCeil);
        Plat(z1, "Plataforma tunel", 0f, 0f, 24f, 40f, 8f, mZ1);
        Sign(extras, "1 - TUNEL BAJO\nCorre y desliza para pasar", new Vector3(0f, 4.5f, 11f), mZ1.color, 0.9f);
        MakeCheckpoint(extras, 1, new Vector3(0f, 0f, 31f));

        // ---------- Zona 2: rampa (18°) + salto de 18 m ----------
        float a1 = 18f;
        float y1 = -24f * Mathf.Tan(a1 * Mathf.Deg2Rad);          // altura al final de la rampa
        RampBox(z2, "Rampa 1", 0f, 0f, 40f, 64f, 8f, -1f, 1f, a1, mRamp);
        Plat(z2, "Rampa 1 base", 0f, y1, 64f, 67f, 8f, mRamp);
        Plat(z2, "Aterrizaje 1", 0f, y1, 85f, 97f, 10f, mZ2);
        Sign(extras, "2 - RAMPA\nDeslizate hasta el borde, salta y usa el doble salto", new Vector3(0f, 4.2f, 38f), mRamp.color, 0.9f);
        MakeCheckpoint(extras, 2, new Vector3(0f, y1, 90f));

        // ---------- Zona 3: subida por paredes ----------
        float exitTop = 4f;
        Plat(z3, "Pozo suelo", 0f, y1, 97f, 111f, 4f, mZ3F);
        Wall(z3, "Pozo pared izq", -2.5f, 101f, 123f, y1 - 1f, 6.5f, mZ3W);
        Wall(z3, "Pozo pared der", 2.5f, 101f, 123f, y1 - 1f, 6.5f, mZ3W);
        Plat(z3, "Pozo salida", 0f, exitTop, 123f, 133f, 8f, mZ3F);
        Sign(extras, "3 - PAREDES\nSube de pared en pared", new Vector3(0f, y1 + 5f, 98f), mZ3F.color, 0.9f);
        MakeCheckpoint(extras, 3, new Vector3(0f, exitTop, 128f));

        // ---------- Zona 4: túnel en pendiente (20°) + salto de 18 m ----------
        float a2 = 20f;
        float y2 = exitTop - 20f * Mathf.Tan(a2 * Mathf.Deg2Rad);
        RampBox(z4, "Tunel 2 suelo", 0f, exitTop, 133f, 153f, 4f, -1f, 1f, a2, mZ4F);
        RampBox(z4, "Tunel 2 techo", 0f, exitTop, 133f, 153f, 6f, clearance, 1f, a2, mCeil);
        RampBox(z4, "Tunel 2 pared izq", -2.5f, exitTop, 133f, 153f, 1f, -1f, clearance + 2f, a2, mCeil);
        RampBox(z4, "Tunel 2 pared der", 2.5f, exitTop, 133f, 153f, 1f, -1f, clearance + 2f, a2, mCeil);
        Plat(z4, "Tunel 2 base", 0f, y2, 153f, 156f, 4f, mZ4F);
        Plat(z4, "Aterrizaje 2", 0f, y2, 174f, 188f, 10f, mZ4F);
        Sign(extras, "4 - TUNEL EN PENDIENTE\nDesliza dentro y salta al final (hueco de 18 m)", new Vector3(0f, exitTop + 6f, 128f), mZ4F.color, 0.9f);
        MakeCheckpoint(extras, 4, new Vector3(0f, y2, 180f));

        // ---------- Zona 5: cadena de slide jumps (huecos 15, 16, 17 m) ----------
        Plat(z5, "Cadena 1", 0f, y2, 203f, 213f, 6f, mZ5);
        Plat(z5, "Cadena 2", 0f, y2, 229f, 239f, 6f, mZ5);
        Plat(z5, "Cadena 3", 0f, y2, 256f, 270f, 6f, mZ5);
        Sign(extras, "5 - CADENA\nMantén Ctrl al aterrizar: deslizas y saltas otra vez", new Vector3(0f, y2 + 6f, 193f), mZ5.color, 0.9f);
        MakeCheckpoint(extras, 5, new Vector3(0f, y2, 234f));
        MakeCheckpoint(extras, 6, new Vector3(0f, y2, 263f));

        // ---------- Zona 6: rampa final (15°) + salto de 18 m ----------
        float a3 = 15f;
        float y3 = y2 - 30f * Mathf.Tan(a3 * Mathf.Deg2Rad);
        RampBox(z6, "Rampa final", 0f, y2, 270f, 300f, 6f, -1f, 1f, a3, mZ6);
        Plat(z6, "Rampa final base", 0f, y3, 300f, 303f, 6f, mZ6);
        Plat(z6, "META plataforma", 0f, y3, 321f, 341f, 14f, mGold);
        Sign(extras, "6 - RAMPA FINAL\nUltimo salto", new Vector3(0f, y2 + 6f, 268f), mZ6.color, 0.9f);

        // Arco de meta + zona trigger + portales
        BuildFinish(extras, mGold, 324f, y3, 14f, 341f, 321f, 333f);
        GameObject retry = MakePortal(extras, "REINTENTAR", "Reintentar", new Vector3(-3.5f, y3, 335f),
            new Color(1f, 0.55f, 0.15f), Portal.Mode.ReloadCurrent, "");
        GameObject back = MakePortal(extras, "VOLVER AL NIVEL 1", "Nivel1", new Vector3(3.5f, y3, 335f),
            new Color(0.3f, 0.7f, 1f), Portal.Mode.LoadScene, SceneName(1));
        LinkFinish(extras, retry, back);

        MakeLava(extras, new Vector3(0f, -26f, 170f));
    }

    // ------------------------------------------------------------------
    // Jugador: usa tus scripts
    // ------------------------------------------------------------------
    static GameObject BuildPlayer(Vector3 spawn, float killHeight, string levelId, string finishHint)
    {
        var player = new GameObject("Player");
        // OJO: el pivote del Player va en el CENTRO de la capsula (tu DetectWall lanza los rayos desde ahi).
        player.transform.position = spawn;
        player.transform.rotation = Quaternion.identity; // mirando hacia +Z

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = Vector3.zero;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.05f;
        cc.slopeLimit = 50f;

        var movement = player.AddComponent<PlayerMovement>();
        var respawn = player.AddComponent<PlayerRespawn>();
        respawn.killHeight = killHeight;

        var hud = player.AddComponent<SpeedHUD>();
        hud.movement = movement;

        var timer = player.AddComponent<ParkourTimer>();
        timer.movement = movement;
        timer.respawn = respawn;
        timer.levelId = levelId;
        timer.finishHint = finishHint;

        var camGo = new GameObject("Camara");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 700f;
        camGo.AddComponent<AudioListener>();

        var look = camGo.AddComponent<PlayerLook>();
        look.playerBody = player.transform;
        look.movement = movement;

        return player;
    }

    // ------------------------------------------------------------------
    // Meta y portales
    // ------------------------------------------------------------------
    /// <summary>
    /// Arco de meta, muro trasero y zona de meta (de zoneMinZ a zoneMaxZ: cubre toda la zona de aterrizaje,
    /// así da igual en qué punto de la plataforma caigas).
    /// </summary>
    static void BuildFinish(Transform parent, Material gold, float gateZ, float topY, float platWidth, float endZ,
                            float zoneMinZ, float zoneMaxZ)
    {
        float half = platWidth * 0.5f;
        Box(parent, "Meta pilar izq", new Vector3(-(half - 0.5f), topY + 3f, gateZ), new Vector3(0.8f, 6f, 0.8f), gold);
        Box(parent, "Meta pilar der", new Vector3(half - 0.5f, topY + 3f, gateZ), new Vector3(0.8f, 6f, 0.8f), gold);
        Box(parent, "Meta viga", new Vector3(0f, topY + 6.2f, gateZ), new Vector3(platWidth - 0.2f, 0.8f, 0.8f), gold);
        Sign(parent, "META", new Vector3(0f, topY + 8.2f, gateZ), gold.color, 1.2f);

        // Muro trasero para no pasarse de largo con la velocidad
        Box(parent, "Muro trasero", new Vector3(0f, topY + 2.5f, endZ + 0.25f), new Vector3(platWidth, 5f, 0.5f), gold);

        var finish = new GameObject("Zona de META");
        finish.transform.SetParent(parent, false);
        finish.transform.position = new Vector3(0f, topY + 2f, (zoneMinZ + zoneMaxZ) * 0.5f);
        var fc = finish.AddComponent<BoxCollider>();
        fc.isTrigger = true;
        fc.size = new Vector3(platWidth, 4f, zoneMaxZ - zoneMinZ);
        finish.AddComponent<FinishZone>();
    }

    /// <summary>Conecta la zona de meta con los portales (aparecen al terminar).</summary>
    static void LinkFinish(Transform parent, params GameObject[] portals)
    {
        FinishZone fz = parent.GetComponentInChildren<FinishZone>();
        fz.activateOnFinish = portals;
    }

    static GameObject MakePortal(Transform parent, string label, string key, Vector3 basePos,
                                 Color color, Portal.Mode mode, string sceneName)
    {
        var root = new GameObject("Portal: " + label);
        root.transform.SetParent(parent, false);
        root.transform.position = basePos;

        Material frameMat = Mat("Portal_" + key, color);
        Material glowMat = Mat("PortalBrillo_" + key, Color.Lerp(color, Color.white, 0.55f));

        Box(root.transform, "Marco izq", basePos + new Vector3(-1.7f, 2f, 0f), new Vector3(0.4f, 4f, 0.4f), frameMat);
        Box(root.transform, "Marco der", basePos + new Vector3(1.7f, 2f, 0f), new Vector3(0.4f, 4f, 0.4f), frameMat);
        Box(root.transform, "Marco arriba", basePos + new Vector3(0f, 4.2f, 0f), new Vector3(3.8f, 0.4f, 0.4f), frameMat);

        // Interior brillante que "late" (sin collider para poder cruzarlo)
        GameObject disc = Box(root.transform, "Interior", basePos + new Vector3(0f, 2f, 0f), new Vector3(3f, 4f, 0.1f), glowMat);
        disc.isStatic = false;
        Object.DestroyImmediate(disc.GetComponent<Collider>());

        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.position = basePos + new Vector3(0f, 2f, -1.5f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = 3f;
        l.range = 9f;

        Sign(root.transform, label, basePos + new Vector3(0f, 5.3f, 0f), color, 0.7f);

        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 4f, 1.5f);
        trigger.center = new Vector3(0f, 2f, 0f);

        var portal = root.AddComponent<Portal>();
        portal.mode = mode;
        portal.sceneName = sceneName;
        portal.pulseTarget = disc.transform;

        root.SetActive(false); // aparece al llegar a la meta
        return root;
    }

    static void MakeLava(Transform parent, Vector3 pos)
    {
        var lava = Box(parent, "Lava (decorativa)", pos, new Vector3(800f, 0.2f, 800f),
            Mat("Lava", new Color(0.95f, 0.30f, 0.10f)));
        Object.DestroyImmediate(lava.GetComponent<Collider>());
    }

    // ------------------------------------------------------------------
    // Ayudantes de geometria
    // ------------------------------------------------------------------
    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    /// <summary>Plataforma: topY = altura de la superficie; ocupa de zMin a zMax; ancho en X.</summary>
    static GameObject Plat(Transform parent, string name, float x, float topY, float zMin, float zMax,
                           float width, Material mat, float thickness = 1f)
    {
        float len = zMax - zMin;
        return Box(parent, name, new Vector3(x, topY - thickness * 0.5f, (zMin + zMax) * 0.5f),
                   new Vector3(width, thickness, len), mat);
    }

    /// <summary>Pared vertical (plano YZ) centrada en x, con 1 m de grosor.</summary>
    static GameObject Wall(Transform parent, string name, float x, float zMin, float zMax,
                           float yBottom, float yTop, Material mat, float thickness = 1f)
    {
        return Box(parent, name, new Vector3(x, (yBottom + yTop) * 0.5f, (zMin + zMax) * 0.5f),
                   new Vector3(thickness, yTop - yBottom, zMax - zMin), mat);
    }

    /// <summary>
    /// Bloque inclinado hacia abajo en +Z (rampa). La superficie de la rampa va de (zStart, startTopY)
    /// a (zEnd, startTopY - span*tan(angulo)).
    /// offsetN = distancia sobre la superficie donde empieza el bloque (negativo = debajo),
    /// thickness = grosor medido perpendicular a la rampa.
    /// Suelo:  offsetN = -1, thickness = 1.   Techo bajo: offsetN = altura libre, thickness = 1.
    /// </summary>
    static GameObject RampBox(Transform parent, string name, float x, float startTopY, float zStart, float zEnd,
                              float width, float offsetN, float thickness, float angleDeg, Material mat)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        float span = zEnd - zStart;
        float len = span / Mathf.Cos(a);

        Vector3 mid = new Vector3(x, startTopY - span * 0.5f * Mathf.Tan(a), (zStart + zEnd) * 0.5f);
        Vector3 n = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)); // normal de la superficie
        Vector3 center = mid + n * (offsetN + thickness * 0.5f);

        GameObject go = Box(parent, name, center, new Vector3(width, thickness, len), mat);
        go.transform.rotation = Quaternion.Euler(angleDeg, 0f, 0f);
        return go;
    }

    static void MakeCheckpoint(Transform parent, int number, Vector3 basePos)
    {
        var go = new GameObject("Checkpoint " + number);
        go.transform.SetParent(parent, false);
        go.transform.position = basePos;

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(4f, 3f, 4f);
        col.center = new Vector3(0f, 1.5f, 0f);

        Material flagMat = Mat("Checkpoint", new Color(0.6f, 0.6f, 0.65f));

        var pole = Box(go.transform, "Mastil", basePos + new Vector3(1.4f, 1.5f, 0f), new Vector3(0.12f, 3f, 0.12f), flagMat);
        var flag = Box(go.transform, "Bandera", basePos + new Vector3(1.4f, 2.6f, 0.45f), new Vector3(0.06f, 0.7f, 0.9f), flagMat);
        pole.isStatic = false;
        flag.isStatic = false;
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        Object.DestroyImmediate(flag.GetComponent<Collider>());

        // Punto de reaparicion = centro del jugador (0.95 sobre el suelo)
        var spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(go.transform, false);
        spawn.transform.position = basePos + new Vector3(0f, 0.95f, 0f);

        var cp = go.AddComponent<Checkpoint>();
        cp.number = number;
        cp.spawnPoint = spawn.transform;
        cp.flagRenderers = new[] { pole.GetComponent<Renderer>(), flag.GetComponent<Renderer>() };
    }

    static void Sign(Transform parent, string text, Vector3 pos, Color color, float size)
    {
        var go = new GameObject("Cartel: " + text.Replace("\n", " "));
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = 0.15f * size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        Font font = GetBuiltinFont();
        if (font != null)
        {
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }

    static Font GetBuiltinFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    // ------------------------------------------------------------------
    // Materiales (funcionan en Built-in y URP)
    // ------------------------------------------------------------------
    static Material Mat(string name, Color color)
    {
        if (matCache.TryGetValue(name, out Material cached)) return cached;

        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(FindLitShader());
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        matCache[name] = mat;
        return mat;
    }

    static Shader FindLitShader()
    {
        Shader s = null;
        if (GraphicsSettings.currentRenderPipeline != null)
            s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        return s;
    }

    // ------------------------------------------------------------------
    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    static void AddToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
