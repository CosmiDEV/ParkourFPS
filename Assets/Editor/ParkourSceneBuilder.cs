using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Construye la escena de parkour completa con un clic:
/// menú  Parkour > Crear escena de parkour
///
/// Usa tus scripts: PlayerMovement, PlayerLook, PlayerRespawn y SpeedHUD,
/// más Checkpoint, FinishZone y ParkourTimer (incluidos).
/// Las distancias están calculadas con tus valores actuales de PlayerMovement
/// (maxSpeed 12, jumpHeight 2.2, gravity 25, 1 doble salto, salto en pared).
/// Si cambias esos valores, ajusta los huecos en BuildLevel().
/// </summary>
public static class ParkourSceneBuilder
{
    const string RootFolder = "Assets/Parkour";
    const string MatFolder = "Assets/Parkour/Materials";
    const string SceneFolder = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/ParkourLevel1.unity";

    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    // Colores por zona
    static readonly Color cStart = new Color(0.35f, 0.40f, 0.50f);
    static readonly Color cZ1 = new Color(0.20f, 0.65f, 0.72f);
    static readonly Color cZ2 = new Color(0.95f, 0.55f, 0.20f);
    static readonly Color cZ3Floor = new Color(0.55f, 0.40f, 0.85f);
    static readonly Color cZ3Wall = new Color(0.30f, 0.20f, 0.55f);
    static readonly Color cZ4Floor = new Color(0.90f, 0.35f, 0.65f);
    static readonly Color cZ4Wall = new Color(0.55f, 0.15f, 0.40f);
    static readonly Color cZ5 = new Color(0.35f, 0.75f, 0.40f);
    static readonly Color cZ6 = new Color(0.90f, 0.80f, 0.30f);

    [MenuItem("Parkour/Crear escena de parkour")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets", "Parkour");
        EnsureFolder(RootFolder, "Materials");
        EnsureFolder("Assets", "Scenes");
        matCache.Clear();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        SetupEnvironment();
        BuildLevel();
        GameObject player = BuildPlayer();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Selection.activeGameObject = player;
        AssetDatabase.SaveAssets();

        Debug.Log("[Parkour] Escena creada en " + ScenePath + ". ¡Dale Play!");
        EditorUtility.DisplayDialog("Parkour",
            "Escena creada en:\n" + ScenePath + "\n\nDale Play.\n\nWASD mover · Espacio saltar (doble salto)\nSalta contra las paredes para rebotar\nR reaparecer · T reiniciar carrera", "Listo");
    }

    // ------------------------------------------------------------------
    // Entorno: luz, cielo, niebla
    // ------------------------------------------------------------------
    static void SetupEnvironment()
    {
        var lightGo = new GameObject("Luz Direccional");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = light;

        Shader sky = Shader.Find("Skybox/Procedural");
        if (sky != null)
        {
            string path = MatFolder + "/Cielo.mat";
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (skyMat == null)
            {
                skyMat = new Material(sky);
                AssetDatabase.CreateAsset(skyMat, path);
            }
            RenderSettings.skybox = skyMat;
        }

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.006f;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.93f);
        DynamicGI.UpdateEnvironment();
    }

    // ------------------------------------------------------------------
    // Nivel
    // ------------------------------------------------------------------
    static void BuildLevel()
    {
        Transform root = new GameObject("Nivel").transform;

        Transform zStart = Group(root, "Zona 0 - Inicio");
        Transform z1 = Group(root, "Zona 1 - Saltos basicos");
        Transform z2 = Group(root, "Zona 2 - Doble salto");
        Transform z3 = Group(root, "Zona 3 - Subida por paredes");
        Transform z4 = Group(root, "Zona 4 - Zigzag sobre el vacio");
        Transform z5 = Group(root, "Zona 5 - Pilares");
        Transform z6 = Group(root, "Zona 6 - Salto final");
        Transform extras = Group(root, "Extras");

        Material mStart = Mat("Inicio", cStart);
        Material mZ1 = Mat("Zona1", cZ1);
        Material mZ2 = Mat("Zona2", cZ2);
        Material mZ3F = Mat("Zona3_Suelo", cZ3Floor);
        Material mZ3W = Mat("Zona3_Pared", cZ3Wall);
        Material mZ4F = Mat("Zona4_Suelo", cZ4Floor);
        Material mZ4W = Mat("Zona4_Pared", cZ4Wall);
        Material mZ5 = Mat("Zona5", cZ5);
        Material mZ6 = Mat("Zona6", cZ6);
        Material mGold = Mat("Meta_Oro", new Color(1f, 0.82f, 0.2f));

        // ---------- Zona 0: inicio ----------
        Plat(zStart, "Plataforma inicio", 0f, 0f, -5f, 5f, 10f, mStart);
        Sign(extras, "SANTIAGO JOVEN\nPARKOUR", new Vector3(0f, 5.5f, 9f), Color.white, 1.2f);
        Sign(extras, "WASD mover  |  ESPACIO saltar (x2)  |  salta contra las paredes\nR reaparecer  |  T reiniciar carrera",
            new Vector3(0f, 3.6f, 9f), new Color(1f, 0.95f, 0.6f), 0.45f);

        // ---------- Zona 1: saltos basicos (huecos 3, 4, 5, 6 m) ----------
        Plat(z1, "Z1 A", 0f, 0f, 8f, 14f, 6f, mZ1);
        Plat(z1, "Z1 B", 2f, 0f, 18f, 24f, 6f, mZ1);
        Plat(z1, "Z1 C", -2f, 0f, 29f, 35f, 6f, mZ1);
        Plat(z1, "Z1 D", 0f, 0f, 41f, 47f, 6f, mZ1);
        Sign(extras, "1 - SALTOS", new Vector3(0f, 5f, 16f), cZ1, 1f);
        MakeCheckpoint(extras, 1, new Vector3(0f, 0f, 44f));

        // ---------- Zona 2: doble salto (huecos 9, 11, 11 m) ----------
        Plat(z2, "Z2 A", 0f, 0f, 56f, 61f, 5f, mZ2);
        Plat(z2, "Z2 B", 0f, 1f, 72f, 77f, 5f, mZ2);
        Plat(z2, "Z2 C", 0f, 2f, 88f, 99f, 5f, mZ2);   // plataforma larga: toma carrerilla para la pared
        Sign(extras, "2 - DOBLE SALTO", new Vector3(0f, 6f, 54f), cZ2, 1f);
        MakeCheckpoint(extras, 2, new Vector3(0f, 2f, 92f));

        // ---------- Zona 3: subida por paredes (pasillo de 4 m de ancho) ----------
        // Suelo entre las paredes + dos paredes altas. Salta de pared en pared para subir.
        Plat(z3, "Z3 suelo", 0f, 2f, 99f, 113f, 4f, mZ3F);
        Wall(z3, "Z3 pared izq", -2.5f, 103f, 119f, 1f, 15f, mZ3W);
        Wall(z3, "Z3 pared der", 2.5f, 103f, 119f, 1f, 15f, mZ3W);
        Plat(z3, "Z3 salida", 0f, 12.5f, 119f, 129f, 8f, mZ3F);
        Sign(extras, "3 - SALTO EN PARED", new Vector3(0f, 7f, 101f), cZ3Floor, 1f);
        Sign(extras, "Salta hacia una pared y vuelve a saltar", new Vector3(0f, 5.2f, 101f), Color.white, 0.4f);
        MakeCheckpoint(extras, 3, new Vector3(0f, 12.5f, 124f));

        // ---------- Zona 4: zigzag sobre el vacio ----------
        Plat(z4, "Z4 entrada", 0f, 12.5f, 129f, 137f, 4f, mZ4F);
        Wall(z4, "Z4 pared izq", -2.5f, 133f, 177f, 6f, 22f, mZ4W);
        Wall(z4, "Z4 pared der", 2.5f, 133f, 177f, 6f, 22f, mZ4W);
        Plat(z4, "Z4 descanso", 0f, 12.5f, 153f, 157f, 4f, mZ4F);
        Plat(z4, "Z4 final tramo", 0f, 12.5f, 173f, 177f, 4f, mZ4F);
        Plat(z4, "Z4 salida", 0f, 12.5f, 177f, 187f, 10f, mZ4F);
        Sign(extras, "4 - ZIGZAG", new Vector3(0f, 17.5f, 131f), cZ4Floor, 1f);
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
        Sign(extras, "5 - PILARES", new Vector3(0f, 16f, 190f), cZ5, 1f);

        // ---------- Zona 6: salto final ----------
        Plat(z6, "Z6 despegue", 0f, 3.5f, 221f, 227f, 8f, mZ6);
        Plat(z6, "META plataforma", 0f, 2f, 239f, 251f, 12f, mGold);
        Sign(extras, "6 - SALTO FINAL", new Vector3(0f, 9f, 224f), cZ6, 1f);
        MakeCheckpoint(extras, 6, new Vector3(0f, 3.5f, 224f));

        // Arco de meta + zona trigger
        Box(extras, "Meta pilar izq", new Vector3(-5f, 5f, 248f), new Vector3(0.8f, 6f, 0.8f), mGold);
        Box(extras, "Meta pilar der", new Vector3(5f, 5f, 248f), new Vector3(0.8f, 6f, 0.8f), mGold);
        Box(extras, "Meta viga", new Vector3(0f, 8.2f, 248f), new Vector3(10.8f, 0.8f, 0.8f), mGold);
        Sign(extras, "META", new Vector3(0f, 10.2f, 248f), mGold.color, 1.2f);

        var finish = new GameObject("Zona de META");
        finish.transform.SetParent(extras, false);
        finish.transform.position = new Vector3(0f, 4f, 246f);
        var fc = finish.AddComponent<BoxCollider>();
        fc.isTrigger = true;
        fc.size = new Vector3(12f, 4f, 10f);
        finish.AddComponent<FinishZone>();

        // Lava decorativa (sin collider): el jugador reaparece solo por killHeight
        var lava = Box(extras, "Lava (decorativa)", new Vector3(0f, -13f, 120f), new Vector3(500f, 0.2f, 500f),
            Mat("Lava", new Color(0.95f, 0.30f, 0.10f)));
        Object.DestroyImmediate(lava.GetComponent<Collider>());
    }

    // ------------------------------------------------------------------
    // Jugador: usa tus scripts
    // ------------------------------------------------------------------
    static GameObject BuildPlayer()
    {
        var player = new GameObject("Player");
        // OJO: el pivote del Player va en el CENTRO de la capsula (tu DetectWall lanza los rayos desde ahi).
        player.transform.position = new Vector3(0f, 0.95f, -3f);
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
        respawn.killHeight = -15f;

        var hud = player.AddComponent<SpeedHUD>();
        hud.movement = movement;

        var timer = player.AddComponent<ParkourTimer>();
        timer.movement = movement;
        timer.respawn = respawn;

        var camGo = new GameObject("Camara");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 600f;
        camGo.AddComponent<AudioListener>();

        var look = camGo.AddComponent<PlayerLook>();
        look.playerBody = player.transform;
        look.movement = movement;

        return player;
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
