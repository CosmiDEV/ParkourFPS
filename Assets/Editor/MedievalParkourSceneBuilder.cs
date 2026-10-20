using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Versión MEDIEVAL del constructor de parkour, hecha con el KayKit Medieval Hexagon Pack (CC0).
/// Menú superior:
///   Parkour Medieval > Crear TODOS los niveles   (recomendado)
///   Parkour Medieval > Crear Nivel 1
///   Parkour Medieval > Crear Nivel 2
///
/// IMPORTANTE
///  - Pon este archivo dentro de una carpeta "Editor" (p. ej. Assets/Editor).
///  - Copia el pack de KayKit en cualquier subcarpeta de Assets (p. ej. Assets/KayKit) y espera a que Unity
///    termine de importarlo. El script busca los modelos por nombre (hex_grass, wall_straight, ...), así que
///    NO importa dónde los pongas ni si dejas la estructura original (Assets/fbx(unity)/tiles/base/...).
///  - La física NO cambia: las plataformas, paredes y rampas siguen siendo los mismos colliders (cajas) de siempre,
///    así que todas las distancias del parkour son idénticas. Los modelos de KayKit son solo visuales (sin collider).
///  - Usa tus scripts: PlayerMovement, PlayerLook, PlayerRespawn, SpeedHUD, Checkpoint, FinishZone, Portal, ParkourTimer.
///  - Crea escenas aparte (ParkourMedievalLevel1/2) y una carpeta aparte (Assets/ParkourMedieval), así que
///    convive con tu ParkourSceneBuilder original sin pisarlo.
/// </summary>
public static class MedievalParkourSceneBuilder
{
    const string RootFolder = "Assets/ParkourMedieval";
    const string MatFolder = RootFolder + "/Materials";
    const string SceneFolder = "Assets/Scenes";
    const string GameTitle = "PARKOUR MEDIEVAL";

    // ---- Geometría del pack (hexágonos "pointy-top": 2 m de ancho en X, 2.31 m punta a punta en Z, 1 m de grosor) ----
    const float HexW = 2f;
    const float HexR = 1.1547f;      // distancia centro-vértice
    const float RowStep = 1.7320508f; // separación entre filas
    const float HexLift = 0.03f;      // el hexágono flota 3 cm sobre el collider para evitar z-fighting
    const float FitTol = 0.15f;       // cuánto se permite que un hexágono se pase del borde del collider

    // Medidas de wall_straight (2 de largo, 1.1 de alto, 0.8 de grosor)
    const float WallLen = 2f, WallH = 1.1f, WallT = 0.8f;

    static readonly Color DirtColor = new Color(0.47f, 0.36f, 0.27f);
    static readonly Color StoneColor = new Color(0.55f, 0.54f, 0.52f);
    static readonly Color DarkStoneColor = new Color(0.30f, 0.30f, 0.34f);

    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    static readonly Dictionary<string, GameObject> modelCache = new Dictionary<string, GameObject>();
    static readonly HashSet<string> missing = new HashSet<string>();
    static Texture2D atlasCache;

    static readonly string[] naturePool =
    {
        "trees_A_large", "trees_B_large", "trees_A_medium", "hills_A_trees", "hills_B_trees",
        "mountain_A_grass_trees", "mountain_B_grass_trees", "mountain_C_grass_trees",
        "hills_C", "tree_single_A", "tree_single_B", "rock_single_A"
    };
    static readonly string[] buildingPool =
    {
        "building_home_A", "building_home_B", "building_tower_A", "building_windmill", "building_church",
        "building_tavern", "building_market", "building_lumbermill", "building_blacksmith", "building_well"
    };
    static readonly string[] teams = { "blue", "red", "green", "yellow" };

    /// <summary>Una "zona" = material de la base (tierra), material de los hexágonos (textura KayKit + tinte) y color del cartel.</summary>
    class Zone
    {
        public Material baseMat;
        public Material hexMat;
        public Color color;
        public string tile;
    }

    static string SceneName(int level) { return "ParkourMedievalLevel" + level; }
    static string ScenePath(int level) { return SceneFolder + "/" + SceneName(level) + ".unity"; }

    // ------------------------------------------------------------------
    // Menús
    // ------------------------------------------------------------------
    [MenuItem("Parkour Medieval/Crear TODOS los niveles (recomendado)", false, 0)]
    public static void BuildAll()
    {
        if (!CheckPack()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildLevelScene(1);
        BuildLevelScene(2);
        EditorSceneManager.OpenScene(ScenePath(1));
        ReportMissing();

        Debug.Log("[Parkour Medieval] Niveles creados en " + SceneFolder + ". Abre ParkourMedievalLevel1 y dale Play.");
        EditorUtility.DisplayDialog(GameTitle,
            "Listo: se crearon los 2 niveles en " + SceneFolder + " y se agregaron a Build Settings.\n\n" +
            "Se abrió el Nivel 1. Dale Play.\n\n" +
            "WASD mover · Espacio saltar (doble salto)\n" +
            "Ctrl o C deslizar · Salta contra las paredes\n" +
            "R reaparecer · T reiniciar nivel", "Listo");
    }

    [MenuItem("Parkour Medieval/Crear Nivel 1", false, 1)]
    public static void BuildOnlyLevel1()
    {
        if (!CheckPack()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildLevelScene(1);
        ReportMissing();
        Debug.Log("[Parkour Medieval] Nivel 1 creado en " + ScenePath(1));
    }

    [MenuItem("Parkour Medieval/Crear Nivel 2", false, 2)]
    public static void BuildOnlyLevel2()
    {
        if (!CheckPack()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildLevelScene(2);
        ReportMissing();
        Debug.Log("[Parkour Medieval] Nivel 2 creado en " + ScenePath(2));
    }

    // ------------------------------------------------------------------
    static void BuildLevelScene(int level)
    {
        EnsureFolder("Assets", "ParkourMedieval");
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
    // Comprobación del pack
    // ------------------------------------------------------------------
    static bool CheckPack()
    {
        modelCache.Clear();
        missing.Clear();
        atlasCache = null;

        if (FindModel("hex_grass") == null || FindAtlas() == null)
        {
            EditorUtility.DisplayDialog(GameTitle,
                "No encuentro el KayKit Medieval Hexagon Pack en el proyecto.\n\n" +
                "Copia la carpeta del pack (la que contiene Assets/fbx(unity) y Textures) dentro de tu carpeta Assets " +
                "(cualquier subcarpeta, por ejemplo Assets/KayKit), espera a que Unity termine de importarla y vuelve a intentarlo.",
                "Entendido");
            return false;
        }
        missing.Clear();
        return true;
    }

    static void ReportMissing()
    {
        if (missing.Count == 0) return;
        var list = new List<string>(missing);
        list.Sort();
        Debug.LogWarning("[Parkour Medieval] No encontré estos modelos del pack (se omitieron, el juego sigue funcionando): " +
                         string.Join(", ", list));
    }

    /// <summary>Busca un modelo FBX por nombre en todo el proyecto (prefiere la carpeta "fbx(unity)").</summary>
    static GameObject FindModel(string name)
    {
        if (modelCache.TryGetValue(name, out GameObject cached)) return cached;

        GameObject best = null;
        int bestScore = int.MinValue;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Model"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) != name) continue;
            string lower = p.ToLowerInvariant();
            bool isFbx = lower.EndsWith(".fbx");
            if (!isFbx && !lower.EndsWith(".obj")) continue;

            int score = isFbx ? 20 : 1;   // prefiere FBX; si solo hay OBJ (p. ej. bajaste el ZIP de GitHub), usa OBJ
            if (lower.Contains("(unity)")) score += 10;
            if (lower.Contains("waterless")) score -= 5;
            if (score > bestScore)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) { best = go; bestScore = score; }
            }
        }

        if (best == null) missing.Add(name);
        modelCache[name] = best;
        return best;
    }

    static Texture2D FindAtlas()
    {
        if (atlasCache != null) return atlasCache;
        int bestScore = int.MinValue;
        foreach (string guid in AssetDatabase.FindAssets("hexagons_medieval t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) != "hexagons_medieval") continue;
            int score = p.Contains("/Textures/") ? 5 : 0;
            if (score > bestScore)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) { atlasCache = t; bestScore = score; }
            }
        }
        return atlasCache;
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

        Zone mStart = MakeZone("Inicio", new Color(0.35f, 0.40f, 0.50f), Color.white);
        Zone mZ1 = MakeZone("Zona1", new Color(0.20f, 0.65f, 0.72f), Color.white);
        Zone mZ2 = MakeZone("Zona2", new Color(0.95f, 0.55f, 0.20f), new Color(1f, 0.86f, 0.62f));
        Zone mZ3 = MakeZone("Zona3", new Color(0.55f, 0.40f, 0.85f), new Color(0.88f, 0.92f, 1f));
        Zone mZ4 = MakeZone("Zona4", new Color(0.90f, 0.35f, 0.65f), new Color(1f, 0.88f, 0.92f));
        Zone mZ5 = MakeZone("Zona5", new Color(0.35f, 0.75f, 0.40f), new Color(0.88f, 1f, 0.88f));
        Zone mZ6 = MakeZone("Zona6", new Color(0.90f, 0.80f, 0.30f), new Color(1f, 0.96f, 0.75f));
        Zone mGold = MakeZone("Meta", new Color(1f, 0.82f, 0.2f), new Color(1f, 0.92f, 0.6f));

        Material stone = Mat("Piedra", StoneColor);
        Material wallHex = HexMat("Hex_Muro", Color.white);

        // ---------- Zona 0: inicio ----------
        Plat(zStart, "Plataforma inicio", 0f, 0f, -5f, 5f, 10f, mStart);
        StartDecor(zStart);
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
        Plat(z3, "Z3 suelo", 0f, 2f, 99f, 113f, 4f, mZ3);
        StoneWall(z3, "Z3 pared izq", -2.5f, 103f, 119f, 1f, 15f, stone, wallHex);
        StoneWall(z3, "Z3 pared der", 2.5f, 103f, 119f, 1f, 15f, stone, wallHex);
        Plat(z3, "Z3 salida", 0f, 12.5f, 119f, 129f, 8f, mZ3);
        Sign(extras, "3 - SALTO EN PARED", new Vector3(0f, 7f, 101f), mZ3.color, 1f);
        Sign(extras, "Salta hacia una pared y vuelve a saltar", new Vector3(0f, 5.2f, 101f), Color.white, 0.4f);
        MakeCheckpoint(extras, 3, new Vector3(0f, 12.5f, 124f));

        // ---------- Zona 4: zigzag sobre el vacio ----------
        Plat(z4, "Z4 entrada", 0f, 12.5f, 129f, 137f, 4f, mZ4);
        StoneWall(z4, "Z4 pared izq", -2.5f, 133f, 177f, 6f, 22f, stone, wallHex);
        StoneWall(z4, "Z4 pared der", 2.5f, 133f, 177f, 6f, 22f, stone, wallHex);
        Plat(z4, "Z4 descanso", 0f, 12.5f, 153f, 157f, 4f, mZ4);
        Plat(z4, "Z4 final tramo", 0f, 12.5f, 173f, 177f, 4f, mZ4);
        Plat(z4, "Z4 salida", 0f, 12.5f, 177f, 187f, 10f, mZ4);
        Sign(extras, "4 - ZIGZAG", new Vector3(0f, 17.5f, 131f), mZ4.color, 1f);
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

        // Mar decorativo (sin collider): el jugador reaparece solo por killHeight
        MakeSea(extras, new Vector3(0f, -13f, 120f), new Color(0.20f, 0.45f, 0.70f));
        BuildScenery(extras, 1, 253f, 2f);
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

        Zone mStart = MakeZone("N2_Inicio", new Color(0.30f, 0.30f, 0.42f), new Color(1f, 0.92f, 0.85f));
        Zone mZ1 = MakeZone("N2_Zona1", new Color(0.25f, 0.70f, 0.75f), new Color(1f, 0.92f, 0.85f));
        Zone mRamp = MakeZone("N2_Rampa", new Color(0.95f, 0.45f, 0.25f), new Color(1f, 0.82f, 0.70f));
        Zone mZ2 = MakeZone("N2_Zona2", new Color(0.95f, 0.60f, 0.25f), new Color(1f, 0.85f, 0.65f));
        Zone mZ3 = MakeZone("N2_Zona3", new Color(0.55f, 0.40f, 0.85f), new Color(0.92f, 0.90f, 1f));
        Zone mZ4 = MakeZone("N2_Zona4", new Color(0.90f, 0.35f, 0.65f), new Color(1f, 0.85f, 0.88f));
        Zone mZ5 = MakeZone("N2_Zona5", new Color(0.35f, 0.80f, 0.45f), new Color(0.90f, 1f, 0.88f));
        Zone mZ6 = MakeZone("N2_Zona6", new Color(0.35f, 0.55f, 0.95f), new Color(0.90f, 0.95f, 1f));
        Zone mGold = MakeZone("Meta", new Color(1f, 0.82f, 0.2f), new Color(1f, 0.92f, 0.6f));

        Material stone = Mat("Piedra", StoneColor);
        Material mCeil = Mat("Techo", DarkStoneColor);
        Material wallHex = HexMat("Hex_Muro", Color.white);

        const float clearance = 1.25f; // altura libre de los techos bajos: solo cabes deslizándote

        // ---------- Zona 0: inicio ----------
        Plat(zStart, "Plataforma inicio", 0f, 0f, -5f, 5f, 10f, mStart);
        StartDecor(zStart);
        Sign(extras, GameTitle + "\nNIVEL 2", new Vector3(0f, 5.5f, 9f), Color.white, 1.3f);
        Sign(extras, "CTRL o C = deslizar (corre y presiona Ctrl)\nBaja rampas para ganar velocidad. Salta deslizando: slide jump.\nMantén Ctrl al aterrizar para encadenar",
            new Vector3(0f, 3.2f, 9f), new Color(1f, 0.95f, 0.6f), 0.4f);

        // ---------- Zona 1: túnel bajo (hay que deslizarse) ----------
        Plat(z1, "Tunel suelo", 0f, 0f, 5f, 24f, 6f, mZ1);
        Box(z1, "Tunel pared izq", new Vector3(-3.5f, 0.875f, 19f), new Vector3(1f, 2.75f, 10f), mCeil);
        Box(z1, "Tunel pared der", new Vector3(3.5f, 0.875f, 19f), new Vector3(1f, 2.75f, 10f), mCeil);
        Box(z1, "Tunel techo", new Vector3(0f, clearance + 0.5f, 19f), new Vector3(8f, 1f, 10f), mCeil);
        WallRun(z1, new Vector3(-3.5f, 0f, 19f), 10f, 2.25f, 1f, 90f, wallHex);
        WallRun(z1, new Vector3(3.5f, 0f, 19f), 10f, 2.25f, 1f, 90f, wallHex);
        FillHex(z1, mZ1, new Vector3(0f, clearance + 1f, 14f), Quaternion.identity, 8f, 10f); // techo con césped
        Plat(z1, "Plataforma tunel", 0f, 0f, 24f, 40f, 8f, mZ1);
        Sign(extras, "1 - TUNEL BAJO\nCorre y desliza para pasar", new Vector3(0f, 4.5f, 11f), mZ1.color, 0.9f);
        MakeCheckpoint(extras, 1, new Vector3(0f, 0f, 31f));

        // ---------- Zona 2: rampa (18°) + salto de 18 m ----------
        float a1 = 18f;
        float y1 = -24f * Mathf.Tan(a1 * Mathf.Deg2Rad);          // altura al final de la rampa
        RampPlat(z2, "Rampa 1", 0f, 0f, 40f, 64f, 8f, a1, mRamp);
        Plat(z2, "Rampa 1 base", 0f, y1, 64f, 67f, 8f, mRamp);
        Plat(z2, "Aterrizaje 1", 0f, y1, 85f, 97f, 10f, mZ2);
        Sign(extras, "2 - RAMPA\nDeslizate hasta el borde, salta y usa el doble salto", new Vector3(0f, 4.2f, 38f), mRamp.color, 0.9f);
        MakeCheckpoint(extras, 2, new Vector3(0f, y1, 90f));

        // ---------- Zona 3: subida por paredes ----------
        float exitTop = 4f;
        Plat(z3, "Pozo suelo", 0f, y1, 97f, 111f, 4f, mZ3);
        StoneWall(z3, "Pozo pared izq", -2.5f, 101f, 123f, y1 - 1f, 6.5f, stone, wallHex);
        StoneWall(z3, "Pozo pared der", 2.5f, 101f, 123f, y1 - 1f, 6.5f, stone, wallHex);
        Plat(z3, "Pozo salida", 0f, exitTop, 123f, 133f, 8f, mZ3);
        Sign(extras, "3 - PAREDES\nSube de pared en pared", new Vector3(0f, y1 + 5f, 98f), mZ3.color, 0.9f);
        MakeCheckpoint(extras, 3, new Vector3(0f, exitTop, 128f));

        // ---------- Zona 4: túnel en pendiente (20°) + salto de 18 m ----------
        float a2 = 20f;
        float y2 = exitTop - 20f * Mathf.Tan(a2 * Mathf.Deg2Rad);
        RampPlat(z4, "Tunel 2 suelo", 0f, exitTop, 133f, 153f, 4f, a2, mZ4);
        RampBox(z4, "Tunel 2 techo", 0f, exitTop, 133f, 153f, 6f, clearance, 1f, a2, mCeil);
        RampBox(z4, "Tunel 2 pared izq", -2.5f, exitTop, 133f, 153f, 1f, -1f, clearance + 2f, a2, mCeil);
        RampBox(z4, "Tunel 2 pared der", 2.5f, exitTop, 133f, 153f, 1f, -1f, clearance + 2f, a2, mCeil);
        {   // césped sobre el techo del túnel inclinado
            float ar = a2 * Mathf.Deg2Rad;
            Vector3 n = new Vector3(0f, Mathf.Cos(ar), Mathf.Sin(ar));
            Vector3 roofOrigin = new Vector3(0f, exitTop, 133f) + n * (clearance + 1f);
            FillHex(z4, mZ4, roofOrigin, Quaternion.Euler(a2, 0f, 0f), 6f, 20f / Mathf.Cos(ar));
        }
        Plat(z4, "Tunel 2 base", 0f, y2, 153f, 156f, 4f, mZ4);
        Plat(z4, "Aterrizaje 2", 0f, y2, 174f, 188f, 10f, mZ4);
        Sign(extras, "4 - TUNEL EN PENDIENTE\nDesliza dentro y salta al final (hueco de 18 m)", new Vector3(0f, exitTop + 6f, 128f), mZ4.color, 0.9f);
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
        RampPlat(z6, "Rampa final", 0f, y2, 270f, 300f, 6f, a3, mZ6);
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

        MakeSea(extras, new Vector3(0f, -26f, 170f), new Color(0.55f, 0.30f, 0.38f));
        BuildScenery(extras, 2, 341f, y3);
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
    /// Arco de meta (dos torres de KayKit + viga), muro trasero y zona de meta
    /// (de zoneMinZ a zoneMaxZ: cubre toda la zona de aterrizaje, así da igual en qué punto de la plataforma caigas).
    /// </summary>
    static void BuildFinish(Transform parent, Zone gold, float gateZ, float topY, float platWidth, float endZ,
                            float zoneMinZ, float zoneMaxZ)
    {
        float half = platWidth * 0.5f;
        Material goldMat = Mat("Meta_Oro", gold.color);
        Material natural = HexMat("Hex_Natural", Color.white);

        // Torres a cada lado (si el modelo no existe, vuelve a los pilares de caja)
        const float towerScale = 2.8f;
        GameObject tl = Spawn(parent, "building_tower_A_yellow", new Vector3(-(half - 1.6f), topY, gateZ),
            Quaternion.Euler(0f, 180f, 0f), Vector3.one * towerScale, natural);
        GameObject tr = Spawn(parent, "building_tower_A_yellow", new Vector3(half - 1.6f, topY, gateZ),
            Quaternion.Euler(0f, 180f, 0f), Vector3.one * towerScale, natural);
        if (tl == null || tr == null)
        {
            Box(parent, "Meta pilar izq", new Vector3(-(half - 0.5f), topY + 3f, gateZ), new Vector3(0.8f, 6f, 0.8f), goldMat);
            Box(parent, "Meta pilar der", new Vector3(half - 0.5f, topY + 3f, gateZ), new Vector3(0.8f, 6f, 0.8f), goldMat);
        }
        Box(parent, "Meta viga", new Vector3(0f, topY + 6.2f, gateZ), new Vector3(platWidth - 0.2f, 0.8f, 0.8f), goldMat);
        Sign(parent, "META", new Vector3(0f, topY + 8.2f, gateZ), gold.color, 1.2f);

        // Muro trasero para no pasarse de largo con la velocidad
        Box(parent, "Muro trasero", new Vector3(0f, topY + 2.5f, endZ + 0.25f), new Vector3(platWidth, 5f, 0.5f), Mat("Piedra", StoneColor));
        WallRun(parent, new Vector3(0f, topY, endZ + 0.25f), platWidth, 5f, 0.5f, 0f, HexMat("Hex_Muro", Color.white));

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

    static void MakeSea(Transform parent, Vector3 pos, Color color)
    {
        var sea = Box(parent, "Mar (decorativo)", pos, new Vector3(800f, 0.2f, 800f), Mat("Mar", color));
        Object.DestroyImmediate(sea.GetComponent<Collider>());
    }

    // ------------------------------------------------------------------
    // Escenario lejano: islas flotantes con castillos, casas, árboles, montañas y nubes (solo visual)
    // ------------------------------------------------------------------
    static void BuildScenery(Transform parent, int level, float zEnd, float goalY)
    {
        var rng = new System.Random(1000 + level);
        Material natural = HexMat("Hex_Natural", Color.white);
        Transform sc = Group(parent, "Escenario lejano");

        for (float z = -20f; z <= zEnd + 20f; z += 55f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (32f + (float)rng.NextDouble() * 30f);
                float baseY = level == 1 ? -5f : Mathf.Lerp(-14f, goalY - 10f, Mathf.Clamp01(z / zEnd));
                float y = baseY + ((float)rng.NextDouble() * 2f - 1f) * 3f;
                Island(sc, new Vector3(x, y, z + (float)rng.NextDouble() * 20f), rng.Next(0, 2), 4f, rng, false, natural);
            }
        }

        // Isla con castillo al fondo, más allá de la meta
        Island(sc, new Vector3(0f, goalY - 8f, zEnd + 70f), 1, 5f, rng, true, natural);

        // Nubes
        for (int i = 0; i < 14; i++)
        {
            float side = rng.Next(2) == 0 ? -1f : 1f;
            Vector3 pos = new Vector3(side * (20f + (float)rng.NextDouble() * 90f),
                                      goalY + 25f + (float)rng.NextDouble() * 20f,
                                      -20f + (float)rng.NextDouble() * (zEnd + 80f));
            string cloud = rng.Next(3) == 0 ? "cloud_small" : "cloud_big";
            Spawn(sc, cloud, pos, Quaternion.Euler(0f, rng.Next(4) * 90f, 0f), Vector3.one * 6f, natural, false);
        }
    }

    static string Team(System.Random rng) { return teams[rng.Next(teams.Length)]; }

    /// <summary>Isla de hexágonos (coordenadas axiales) a escala k, con un modelo de decoración sobre cada hexágono.</summary>
    static void Island(Transform parent, Vector3 center, int radius, float k, System.Random rng, bool castle, Material mat)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                Vector3 pos = center + new Vector3(k * HexW * (q + r * 0.5f), 0f, k * RowStep * r);
                Spawn(parent, "hex_grass", pos, Quaternion.identity, Vector3.one * k, mat);

                string prop = null;
                if (q == 0 && r == 0 && castle)
                {
                    prop = "building_castle_" + Team(rng);
                }
                else
                {
                    double roll = rng.NextDouble();
                    if (roll < 0.55) prop = naturePool[rng.Next(naturePool.Length)];
                    else if (roll < 0.80) prop = buildingPool[rng.Next(buildingPool.Length)] + "_" + Team(rng);
                    else if (radius == 0) prop = naturePool[rng.Next(naturePool.Length)];
                }

                if (prop != null)
                    Spawn(parent, prop, pos, Quaternion.Euler(0f, 60f * rng.Next(6), 0f), Vector3.one * k, mat);
            }
        }
    }

    /// <summary>Decoración de la plataforma de inicio (en los bordes, lejos de la línea de carrera).</summary>
    static void StartDecor(Transform parent)
    {
        Material m = HexMat("Hex_Natural", Color.white);
        float y = HexLift;
        Spawn(parent, "tent", new Vector3(-3.6f, y, 0.5f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 4f, m);
        Spawn(parent, "weaponrack", new Vector3(3.9f, y, -1.2f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 5f, m);
        Spawn(parent, "barrel", new Vector3(4.0f, y, -3.6f), Quaternion.identity, Vector3.one * 5f, m);
        Spawn(parent, "barrel", new Vector3(3.2f, y, -4.1f), Quaternion.identity, Vector3.one * 5f, m);
        Spawn(parent, "crate_A_big", new Vector3(-4.0f, y, -3.8f), Quaternion.Euler(0f, 20f, 0f), Vector3.one * 5f, m);
        Spawn(parent, "wheelbarrow", new Vector3(3.8f, y, 2.6f), Quaternion.Euler(0f, -60f, 0f), Vector3.one * 4f, m);
        Spawn(parent, "flag_red", new Vector3(-4.4f, y, 4.2f), Quaternion.identity, Vector3.one * 8f, m);
        Spawn(parent, "flag_blue", new Vector3(4.4f, y, 4.2f), Quaternion.identity, Vector3.one * 8f, m);
    }

    // ------------------------------------------------------------------
    // Spawn de modelos del pack
    // ------------------------------------------------------------------
    /// <summary>Instancia un modelo del pack (o devuelve null si no está). mat reemplaza los materiales del FBX.</summary>
    static GameObject Spawn(Transform parent, string model, Vector3 pos, Quaternion rot, Vector3 scale,
                            Material mat, bool isStatic = true)
    {
        GameObject prefab = FindModel(model);
        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.transform.localScale = Vector3.Scale(prefab.transform.localScale, scale);

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (mat == null) continue;
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        if (isStatic)
            foreach (Transform t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        return go;
    }

    /// <summary>
    /// Rellena un rectángulo (ancho en X local, largo en Z local) con hexágonos. origin = punto de la superficie en el centro
    /// del borde inicial; rot = orientación de la superficie (identidad en plano, Euler(a,0,0) en rampas).
    /// Solo se colocan hexágonos que caben (con una pequeña tolerancia); el borde queda con la base de tierra.
    /// </summary>
    static void FillHex(Transform parent, Zone z, Vector3 origin, Quaternion rot, float width, float length)
    {
        int rows = Mathf.Max(1, Mathf.FloorToInt((length - 2f * HexR + FitTol) / RowStep) + 1);
        float span = 2f * HexR + (rows - 1) * RowStep;
        float v0 = HexR + (length - span) * 0.5f;
        float maxX = width * 0.5f - HexW * 0.5f + FitTol;
        Vector3 lift = rot * (Vector3.up * HexLift);

        for (int r = 0; r < rows; r++)
        {
            float v = v0 + r * RowStep;
            float xOff = (r % 2 == 1) ? HexW * 0.5f : 0f;
            for (int k = -30; k <= 30; k++)
            {
                float x = xOff + k * HexW;
                if (Mathf.Abs(x) > maxX) continue;
                Vector3 p = origin + rot * new Vector3(x, 0f, v) + lift;
                Spawn(parent, z.tile, p, rot, Vector3.one, z.hexMat);
            }
        }
    }

    /// <summary>
    /// Tira de piezas "wall_straight" (muro de piedra de KayKit) estirada para cubrir un largo/alto exactos.
    /// baseCenter = centro de la base; yawDeg 0 = corre a lo largo de X, 90 = a lo largo de Z.
    /// </summary>
    static void WallRun(Transform parent, Vector3 baseCenter, float length, float height, float thickness,
                        float yawDeg, Material mat)
    {
        int nx = Mathf.Max(1, Mathf.RoundToInt(length / (WallLen * 2f)));
        int ny = Mathf.Max(1, Mathf.RoundToInt(height / (WallH * 2f)));
        float pieceLen = length / nx;
        float pieceH = height / ny;
        Vector3 scale = new Vector3(pieceLen / WallLen, pieceH / WallH, (thickness + 0.04f) / WallT);
        Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);

        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                Vector3 local = new Vector3(-length * 0.5f + (i + 0.5f) * pieceLen, j * pieceH, 0f);
                Spawn(parent, "wall_straight", baseCenter + rot * local, rot, scale, mat);
            }
    }

    // ------------------------------------------------------------------
    // Ayudantes de geometria (los colliders son los mismos de siempre)
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

    /// <summary>Plataforma: collider de caja (base de tierra) + hexágonos de KayKit encima. topY = altura de la superficie.</summary>
    static GameObject Plat(Transform parent, string name, float x, float topY, float zMin, float zMax,
                           float width, Zone z, float thickness = 1f)
    {
        float len = zMax - zMin;
        GameObject go = Box(parent, name, new Vector3(x, topY - thickness * 0.5f, (zMin + zMax) * 0.5f),
                            new Vector3(width, thickness, len), z.baseMat);
        FillHex(parent, z, new Vector3(x, topY, zMin), Quaternion.identity, width, len);
        return go;
    }

    /// <summary>Pared vertical (plano YZ) centrada en x, con 1 m de grosor.</summary>
    static GameObject Wall(Transform parent, string name, float x, float zMin, float zMax,
                           float yBottom, float yTop, Material mat, float thickness = 1f)
    {
        return Box(parent, name, new Vector3(x, (yBottom + yTop) * 0.5f, (zMin + zMax) * 0.5f),
                   new Vector3(thickness, yTop - yBottom, zMax - zMin), mat);
    }

    /// <summary>Pared con collider de caja + muro de piedra de KayKit por encima.</summary>
    static void StoneWall(Transform parent, string name, float x, float zMin, float zMax,
                          float yBottom, float yTop, Material stone, Material wallHex)
    {
        Wall(parent, name, x, zMin, zMax, yBottom, yTop, stone);
        WallRun(parent, new Vector3(x, yBottom, (zMin + zMax) * 0.5f), zMax - zMin, yTop - yBottom, 1f, 90f, wallHex);
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

    /// <summary>Rampa: collider inclinado + hexágonos inclinados encima.</summary>
    static void RampPlat(Transform parent, string name, float x, float startTopY, float zStart, float zEnd,
                         float width, float angleDeg, Zone z)
    {
        RampBox(parent, name, x, startTopY, zStart, zEnd, width, -1f, 1f, angleDeg, z.baseMat);
        float len = (zEnd - zStart) / Mathf.Cos(angleDeg * Mathf.Deg2Rad);
        FillHex(parent, z, new Vector3(x, startTopY, zStart), Quaternion.Euler(angleDeg, 0f, 0f), width, len);
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

        // Bandera de KayKit (material propio por checkpoint, por si tu script de Checkpoint le cambia el color)
        Material flagMat = HexMat("Hex_Checkpoint_" + number, Color.white);
        GameObject flagGo = Spawn(go.transform, "flag_blue", basePos + new Vector3(1.6f, HexLift, 0f),
                                  Quaternion.identity, Vector3.one * 8f, flagMat, false);
        Spawn(go.transform, "barrel", basePos + new Vector3(-1.6f, HexLift, 0.4f), Quaternion.identity, Vector3.one * 5f,
              HexMat("Hex_Natural", Color.white));

        Renderer[] flagRenderers;
        if (flagGo != null)
        {
            flagRenderers = flagGo.GetComponentsInChildren<Renderer>();
        }
        else
        {
            // Plan B: mástil y bandera de cajas, como en la versión original
            Material fallback = Mat("Checkpoint", new Color(0.6f, 0.6f, 0.65f));
            var pole = Box(go.transform, "Mastil", basePos + new Vector3(1.4f, 1.5f, 0f), new Vector3(0.12f, 3f, 0.12f), fallback);
            var flag = Box(go.transform, "Bandera", basePos + new Vector3(1.4f, 2.6f, 0.45f), new Vector3(0.06f, 0.7f, 0.9f), fallback);
            pole.isStatic = false;
            flag.isStatic = false;
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            Object.DestroyImmediate(flag.GetComponent<Collider>());
            flagRenderers = new[] { pole.GetComponent<Renderer>(), flag.GetComponent<Renderer>() };
        }

        // Punto de reaparicion = centro del jugador (0.95 sobre el suelo)
        var spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(go.transform, false);
        spawn.transform.position = basePos + new Vector3(0f, 0.95f, 0f);

        var cp = go.AddComponent<Checkpoint>();
        cp.number = number;
        cp.spawnPoint = spawn.transform;
        cp.flagRenderers = flagRenderers;
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
    static Zone MakeZone(string key, Color signColor, Color tint, string tile = "hex_grass")
    {
        return new Zone
        {
            baseMat = Mat("Tierra", DirtColor),
            hexMat = HexMat("Hex_" + key, tint),
            color = signColor,
            tile = tile
        };
    }

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

    /// <summary>Material con la textura de gradientes de KayKit (hexagons_medieval.png) multiplicada por un tinte.</summary>
    static Material HexMat(string name, Color tint)
    {
        string key = "hex:" + name;
        if (matCache.TryGetValue(key, out Material cached)) return cached;

        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(FindLitShader());
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = tint;
        Texture2D atlas = FindAtlas();
        if (atlas != null) mat.mainTexture = atlas;
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        matCache[key] = mat;
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
