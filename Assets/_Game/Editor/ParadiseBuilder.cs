using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static FalseParadise.EditorTools.SandboxBuilder;
using Z = FalseParadise.EditorTools.Zone1Builder;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Меню False Paradise → Build Paradise Test Scene: ложный рай и его ад (задача N10)
    /// в едином стиле с Zone1_Lair / Gate_Elven / Zone2_OgreVillage (LowPolyMesh, TextureKit, RuinKit, SkyDome).
    /// Персонажи (ангелы, богиня, демоны, высший демон) не изменены.
    /// Подойдите к богине — она произнесёт речь, затем рай рухнет. F9 — запустить/сбросить вручную.
    /// </summary>
    public static class ParadiseBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Paradise_Test.unity";
        const string ArtDir = "Assets/_Game/Art/Paradise";
        const string MatDir = ArtDir + "/Materials";
        const string MeshPath = ArtDir + "/Paradise_Meshes.asset";
        const string SandboxMatDir = "Assets/_Game/Art/Sandbox";

        // Одинаковые позиции для обеих версий мира
        static readonly Vector3 GoddessPos = new Vector3(0f, 0f, 22f);
        static readonly Vector3 CastlePos = new Vector3(0f, 0f, 56f);
        static readonly Vector3 PondPos = new Vector3(-16f, 0f, 14f);

        static readonly List<Mesh> generated = new List<Mesh>();

        [MenuItem("False Paradise/Build Paradise Test Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/_Game/Scenes");
            EnsureFolder(ArtDir);
            EnsureFolder(MatDir);
            EnsureFolder(SandboxMatDir);
            if (AssetDatabase.LoadAssetAtPath<Object>(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
            generated.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            TextureKit.BeginBuild();
            var texRock = TextureKit.Rock();
            var texMasonry = TextureKit.Masonry();
            var texFlag = TextureKit.Flagstone();
            var texGround = TextureKit.Ground();
            var texPlanks = TextureKit.Planks();

            // ---------- материалы Рая ----------
            var pGrass = Lit("P_Grass", new Color(0.42f, 0.66f, 0.28f), 0f, 0.14f);
            TextureKit.Apply(pGrass, texGround, 0.22f, 0.65f);
            var pHillRock = Lit("P_HillRock", new Color(0.66f, 0.64f, 0.58f), 0f, 0.18f);
            TextureKit.Apply(pHillRock, texRock, 0.18f, 1.1f);
            var pTrunk = Lit("P_Trunk", new Color(0.46f, 0.33f, 0.21f), 0f, 0.12f);
            TextureKit.Apply(pTrunk, texRock, 1.4f, 0.9f);
            var pLeaves = Lit("P_Leaves", new Color(0.44f, 0.72f, 0.32f), 0f, 0.18f);
            var pLeavesAlt = Lit("P_LeavesAlt", new Color(0.32f, 0.58f, 0.24f), 0f, 0.16f);
            var pBlossom = Lit("P_Blossom", new Color(0.98f, 0.74f, 0.84f), 0.15f, 0.22f);
            var pMarble = Lit("P_Marble", new Color(0.93f, 0.91f, 0.86f), 0f, 0.42f);
            TextureKit.Apply(pMarble, texMasonry, 0.35f, 0.75f);
            var pPath = Lit("P_Path", new Color(0.94f, 0.92f, 0.86f), 0f, 0.35f);
            TextureKit.Apply(pPath, texFlag, 0.55f, 0.7f);
            var pWood = Lit("P_Wood", new Color(0.58f, 0.42f, 0.26f), 0f, 0.2f);
            TextureKit.Apply(pWood, texPlanks, 1.2f, 0.8f);
            var pGold = Lit("P_Gold", new Color(1f, 0.82f, 0.36f), 0.55f, 0.72f, 0.65f);
            RuinKit.Reflective(pGold);
            var pWindow = EmissiveMat("P_Window", new Color(1f, 0.92f, 0.62f), 2.4f);
            var pWater = Lit("P_Water", new Color(0.24f, 0.68f, 0.88f), 0.22f, 0.92f, 0.15f);
            RuinKit.Reflective(pWater);
            var pSkyMat = SkyMaterial("P_SkyDome", 1f);

            // Материалы персонажей Рая (не трогаем)
            var pAngel = EmissiveMat("P_Angel", new Color(1f, 0.97f, 0.9f), 0.8f);
            var pGoddess = EmissiveMat("P_Goddess", new Color(1f, 0.92f, 0.75f), 2.5f);
            var pWhiteWing = Mat("P_White", new Color(0.96f, 0.95f, 0.92f));

            // ---------- материалы Ада ----------
            var hGround = Lit("H_Ground", new Color(0.22f, 0.11f, 0.09f), 0f, 0.18f);
            TextureKit.Apply(hGround, texGround, 0.24f, 1.25f);
            var hCliff = Lit("H_Cliff", new Color(0.18f, 0.09f, 0.08f), 0f, 0.22f);
            TextureKit.Apply(hCliff, texRock, 0.20f, 1.4f);
            var hBlack = Lit("H_Black", new Color(0.13f, 0.09f, 0.10f), 0f, 0.35f, 0.2f);
            TextureKit.Apply(hBlack, texMasonry, 0.35f, 1.25f);
            var hRock = Lit("H_Rock", new Color(0.26f, 0.13f, 0.10f), 0f, 0.22f);
            TextureKit.Apply(hRock, texRock, 0.6f, 1.3f);
            var hCharred = Lit("H_Charred", new Color(0.12f, 0.07f, 0.06f), 0f, 0.12f);
            TextureKit.Apply(hCharred, texRock, 1.5f, 1.2f);
            var hLava = EmissiveMat("H_Lava", new Color(1f, 0.30f, 0.04f), 4.2f);
            var hIron = Lit("H_Iron", new Color(0.20f, 0.18f, 0.19f), 0f, 0.5f, 0.7f);
            RuinKit.Reflective(hIron);
            var hSkyMat = SkyMaterial("H_SkyDome", 1.1f);

            // Материалы персонажей Ада (не трогаем)
            var hEyes = EmissiveMat("H_Eyes", new Color(1f, 0.05f, 0.02f), 8f);
            var hDemon = Mat("H_Demon", new Color(0.35f, 0.04f, 0.04f));

            // Частицы (авторский ParticleSoft.shader)
            var pSoft = ParticleMat("P_ParticleSoft", true);
            var pSmokeMat = ParticleMat("P_ParticleSmoke", false);

            // ---------- окружение ----------
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) sun = l;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(26f, -24f, 0f);
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
            }

            var cam = CreatePlayer(new Vector3(0f, 1.1f, -12f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 600f;
            new GameObject("HUD").AddComponent<GameHUD>();
            new GameObject("GlitchOverlay").AddComponent<GlitchOverlay>();

            var rng = new System.Random(7);
            var treeSpots = TreeSpots(rng);
            var terrainMesh = BuildValleyTerrain();

            // ==================== РАЙ ====================
            var paradise = new GameObject("Paradise").transform;

            // Купол неба Рая (золотисто-лазурный с облаками и солнцем)
            BuildSkyDome(paradise, "SkyDome_Paradise", pSkyMat,
                zenith: new Color(0.30f, 0.56f, 0.88f),
                horizon: new Color(0.98f, 0.88f, 0.64f),
                sunDir: -(Quaternion.Euler(26f, -24f, 0f) * Vector3.forward),
                sunColor: new Color(1f, 0.96f, 0.80f),
                cloudColor: new Color(1f, 0.97f, 0.90f));

            // Рельеф долины (луг + скалистые холмы по периметру)
            Z.MeshObject("Ground", paradise, terrainMesh, new[] { pGrass, pHillRock }, true);

            // Парадная мраморная аллея, триумфальные арки и подиум Богини
            BuildAvenue(paradise, pPath, pMarble, pGold, pWindow, pSoft, hellish: false);

            // Пруд с берегом, валунами, кустами и деревянным причалом
            BuildPond(paradise, pWater, pHillRock, pLeaves, pBlossom, pWood, pSoft, hellish: false);

            // Ветвистые деревья и кусты (через LowPolyMesh.Lathe)
            var trng = new System.Random(7);
            var treesMesh = BuildTreesMesh(treeSpots, trng, hellish: false);
            Z.MeshObject("Trees", paradise, treesMesh, new[] { pTrunk, pLeaves, pLeavesAlt, pBlossom }, false);

            // Величественный Белый замок
            BuildCastle(paradise, pMarble, pGold, pWindow, hellish: false);

            // Атмосферные частицы (лепестки + золотая пыльца + солнечная дымка)
            BuildParticlesBox(paradise, "BlossomPetals", new Vector3(0f, 7f, 14f), new Vector3(46f, 9f, 52f), pSmokeMat,
                new Color(1f, 0.78f, 0.88f, 0.85f), new Color(1f, 0.94f, 0.98f, 0.75f), 28f, 0.14f, 0.26f, 0.03f, 0.45f);
            BuildParticlesBox(paradise, "GoldPollen", new Vector3(0f, 3.5f, 16f), new Vector3(34f, 5f, 40f), pSoft,
                new Color(1f, 0.92f, 0.55f, 0.65f), new Color(1f, 0.82f, 0.35f, 0.55f), 22f, 0.08f, 0.16f, -0.015f, 0.3f);
            RuinKit.Probe(paradise, new Vector3(0f, 4f, 14f), new Vector3(70f, 24f, 80f), 0.9f);

            // Ангелы и Богиня (персонажей не трогаем!)
            for (int i = 0; i < 7; i++)
            {
                var pos = AngelPos(i);
                var angel = Figure(paradise, $"Angel_{i}", pos, 1f, pAngel, pWhiteWing, null, null);
                angel.gameObject.AddComponent<Hover>();
            }
            var goddess = Figure(paradise, "Goddess", GoddessPos + Vector3.up * 1.9f, 1.6f, pGoddess, pGold, null, null);
            goddess.gameObject.AddComponent<Hover>();
            var halo = new GameObject("Halo").AddComponent<Light>();
            halo.transform.SetParent(goddess, false);
            halo.transform.localPosition = new Vector3(0f, 2f, -1f);
            halo.type = LightType.Point;
            halo.range = 16f;
            halo.intensity = 4.5f;
            halo.color = new Color(1f, 0.86f, 0.52f);

            // ==================== АД (те же места, эпичный и читаемый!) ====================
            var hell = new GameObject("Hell").transform;

            // Купол неба Ада (багрово-пепельный закат с кровавым солнцем — не чёрный экран!)
            BuildSkyDome(hell, "SkyDome_Hell", hSkyMat,
                zenith: new Color(0.14f, 0.025f, 0.03f),
                horizon: new Color(0.72f, 0.16f, 0.06f),
                sunDir: -(Quaternion.Euler(20f, -24f, 0f) * Vector3.forward),
                sunColor: new Color(1f, 0.32f, 0.08f),
                cloudColor: new Color(0.32f, 0.06f, 0.04f));

            // Вулканическая долина
            Z.MeshObject("Ground", hell, terrainMesh, new[] { hGround, hCliff }, true);

            // Расколотая обсидиановая дорога, обломки колонн и 6 пылающих жаровен со светом
            BuildAvenue(hell, hRock, hBlack, hIron, hLava, pSoft, hellish: true);

            // Кипящее лавовое озеро с ярким заревом и искрами
            BuildPond(hell, hLava, hBlack, hRock, hLava, hCharred, pSoft, hellish: true);

            // Обугленные мёртвые деревья на тех же местах
            var drng = new System.Random(7);
            var deadTreesMesh = BuildTreesMesh(treeSpots, drng, hellish: true);
            Z.MeshObject("DeadTrees", hell, deadTreesMesh, new[] { hCharred, hBlack, hRock, hBlack }, false);

            // Обсидиановая цитадель с красной подсветкой фасада
            BuildCastle(hell, hBlack, hRock, hLava, hellish: true);

            // Поднимающиеся угли, пепел и багровая дымка
            BuildParticlesBox(hell, "HellEmbers", new Vector3(0f, 3f, 16f), new Vector3(50f, 8f, 56f), pSoft,
                new Color(1f, 0.42f, 0.08f, 0.9f), new Color(1f, 0.16f, 0.03f, 0.8f), 42f, 0.08f, 0.22f, -0.05f, 0.7f);
            RuinKit.Haze(hell, "HellHaze", new Vector3(0f, 3f, 22f), new Vector3(56f, 6f, 64f), pSmokeMat,
                new Color(0.55f, 0.12f, 0.06f, 0.06f), 22, 8f, 14f);

            // Демоны и Высший демон (персонажей не трогаем!)
            for (int i = 0; i < 7; i++)
            {
                var demon = Figure(hell, $"Demon_{i}", AngelPos(i), 1.15f, hDemon, hBlack, hEyes, hRock);
                demon.gameObject.AddComponent<Hover>();
            }
            var demonQueen = Figure(hell, "HighDemon", GoddessPos + Vector3.up * 1.9f, 2.4f, hBlack, hDemon, hEyes, hRock);
            demonQueen.gameObject.AddComponent<Hover>();
            var redGlow = new GameObject("RedGlow").AddComponent<Light>();
            redGlow.transform.SetParent(demonQueen, false);
            redGlow.transform.localPosition = new Vector3(0f, 2.5f, -2f);
            redGlow.type = LightType.Point;
            redGlow.range = 28f;
            redGlow.intensity = 16f;
            redGlow.color = new Color(1f, 0.22f, 0.08f);
            hell.gameObject.SetActive(false);

            // ---------- постобработка ----------
            var pVol = GlobalVolume("Volume_Paradise", ParadiseProfile(), 1f, 10);
            var hVol = GlobalVolume("Volume_Hell", HellProfile(), 0f, 11);
            var gVol = GlobalVolume("Volume_Glitch", GlitchProfile(), 0f, 12);

            // ---------- контроллер падения (настраиваем читаемый свет Ада!) ----------
            var fall = new GameObject("ParadiseFall").AddComponent<ParadiseFall>();
            var sfx = fall.gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            var trigger = new GameObject("FallTrigger").transform;
            trigger.position = GoddessPos;
            SetRef(fall, "paradiseRoot", paradise.gameObject);
            SetRef(fall, "hellRoot", hell.gameObject);
            SetRef(fall, "paradiseVolume", pVol);
            SetRef(fall, "hellVolume", hVol);
            SetRef(fall, "glitchVolume", gVol);
            SetRef(fall, "sun", sun);
            SetRef(fall, "sfx", sfx);
            SetRef(fall, "triggerPoint", trigger);

            var fso = new SerializedObject(fall);
            fso.FindProperty("paradiseSun").colorValue = new Color(1f, 0.93f, 0.76f);
            fso.FindProperty("paradiseSunIntensity").floatValue = 1.9f;
            fso.FindProperty("paradiseSky").colorValue = new Color(0.88f, 0.84f, 0.70f);
            fso.FindProperty("paradiseAmbient").colorValue = new Color(0.68f, 0.68f, 0.62f);
            fso.FindProperty("paradiseFogDensity").floatValue = 0.0055f;
            fso.FindProperty("hellSun").colorValue = new Color(1f, 0.34f, 0.16f);
            fso.FindProperty("hellSunIntensity").floatValue = 1.65f;
            fso.FindProperty("hellSky").colorValue = new Color(0.28f, 0.06f, 0.05f);
            fso.FindProperty("hellAmbient").colorValue = new Color(0.38f, 0.15f, 0.14f);
            fso.FindProperty("hellFogDensity").floatValue = 0.011f;
            fso.ApplyModifiedPropertiesWithoutUndo();

            SaveMeshes();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[False Paradise] Сцена рая и ада собрана: {ScenePath}. Мешей: {generated.Count}. Play → идите к богине (или F9).");
        }

        // ==================== РЕЛЬЕФ И НЕБО ====================

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        static float SampleHeight(float wx, float wz)
        {
            float dist = new Vector2(wx * 0.88f, (wz - 20f) * 0.72f).magnitude;
            float rim = Smooth(36f, 78f, dist);
            float n1 = Mathf.PerlinNoise(wx * 0.026f + 11.4f, wz * 0.026f + 7.2f);
            float n2 = Mathf.PerlinNoise(wx * 0.07f + 37.1f, wz * 0.07f + 19.8f);
            float hills = (n1 * 22f + n2 * 6.5f) * rim;

            float pathClear = Smooth(4f, 12f, Mathf.Abs(wx));
            float castleClear = Smooth(18f, 28f, Vector2.Distance(new Vector2(wx, wz), new Vector2(CastlePos.x, CastlePos.z)));
            float godClear = Smooth(8f, 15f, Vector2.Distance(new Vector2(wx, wz), new Vector2(GoddessPos.x, GoddessPos.z)));
            float meadow = (Mathf.PerlinNoise(wx * 0.05f + 5f, wz * 0.05f + 5f) - 0.5f) * 1.4f * pathClear * castleClear * godClear;

            float dx = (wx - PondPos.x) / 7.2f;
            float dz = (wz - PondPos.z) / 5.2f;
            float pd = Mathf.Sqrt(dx * dx + dz * dz);
            float dip = (1f - Smooth(0.65f, 1.25f, pd)) * -1.4f;
            return hills + meadow + dip;
        }

        static Mesh BuildValleyTerrain()
        {
            const int res = 68;
            const float size = 168f;
            float step = size / res;
            float half = size * 0.5f;

            var grid = new Vector3[res + 1, res + 1];
            var norms = new Vector3[res + 1, res + 1];
            for (int z = 0; z <= res; z++)
            for (int x = 0; x <= res; x++)
            {
                float wx = -half + x * step;
                float wz = -half + z * step + 18f;
                grid[x, z] = new Vector3(wx, SampleHeight(wx, wz), wz);
            }
            for (int z = 0; z <= res; z++)
            for (int x = 0; x <= res; x++)
            {
                var l = grid[Mathf.Max(0, x - 1), z];
                var r = grid[Mathf.Min(res, x + 1), z];
                var d = grid[x, Mathf.Max(0, z - 1)];
                var u = grid[x, Mathf.Min(res, z + 1)];
                norms[x, z] = Vector3.Cross(u - d, r - l).normalized;
            }

            var lp = new LowPolyMesh(2); // 0 луг/земля, 1 скалистые холмы
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                var a = grid[x, z]; var b = grid[x + 1, z];
                var c = grid[x + 1, z + 1]; var d = grid[x, z + 1];
                float avgH = (a.y + b.y + c.y + d.y) * 0.25f;
                int sub = avgH > 5.5f ? 1 : 0;
                if (sub == 0)
                {
                    lp.TriSmooth(0, a, c, b, norms[x, z], norms[x + 1, z + 1], norms[x + 1, z]);
                    lp.TriSmooth(0, a, d, c, norms[x, z], norms[x, z + 1], norms[x + 1, z + 1]);
                }
                else
                {
                    lp.Tri(1, a, c, b, Vector3.up);
                    lp.Tri(1, a, d, c, Vector3.up);
                }
            }
            return Register(lp.ToMesh("Par_ValleyTerrain"));
        }

        static void BuildSkyDome(Transform parent, string name, Material mat, Color zenith, Color horizon, Vector3 sunDir, Color sunColor, Color cloudColor)
        {
            sunDir.Normalize();
            var mesh = new Mesh { name = name };
            const int rings = 14, segs = 28;
            const float rad = 320f;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            for (int r = 0; r <= rings; r++)
            {
                float elev = Mathf.Lerp(-0.15f, 1f, r / (float)rings) * Mathf.PI * 0.5f;
                float y = Mathf.Sin(elev);
                float cr = Mathf.Cos(elev);
                for (int s = 0; s <= segs; s++)
                {
                    float a = s / (float)segs * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a) * cr, y, Mathf.Sin(a) * cr);
                    verts.Add(dir * rad);

                    float t = Mathf.Clamp01(y);
                    var col = Color.Lerp(horizon, zenith, Mathf.Pow(t, 0.65f));
                    // Мягкие облака
                    float cn = Mathf.PerlinNoise(dir.x * 2.2f + 13f, dir.z * 2.2f + 7f) * Mathf.PerlinNoise(dir.x * 5f + 3f, dir.z * 5f + 9f);
                    float cloud = Smooth(0.24f, 0.52f, cn) * Smooth(0.05f, 0.35f, t) * (1f - Smooth(0.75f, 1f, t));
                    col = Color.Lerp(col, cloudColor, cloud * 0.65f);
                    // Солнце и ореол
                    float sd = Mathf.Max(0f, Vector3.Dot(dir, sunDir));
                    col += sunColor * (Mathf.Pow(sd, 90f) * 1.4f + Mathf.Pow(sd, 10f) * 0.35f);
                    cols.Add(col);
                }
            }
            int stride = segs + 1;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segs; s++)
            {
                int i = r * stride + s;
                tris.Add(i); tris.Add(i + stride); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + stride); tris.Add(i + stride + 1);
            }
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Register(mesh);

            var go = Z.MeshObject(name, parent, mesh, new[] { mat }, false);
            go.transform.position = new Vector3(0f, 0f, 18f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ==================== АРХИТЕКТУРА И ПРИРОДА ====================

        static void BuildAvenue(Transform parent, Material tileMat, Material stoneMat, Material accentMat, Material glowMat, Material sparkMat, bool hellish)
        {
            var lp = new LowPolyMesh(4); // 0 плиты, 1 камень, 2 акцент/золото/железо, 3 свечение
            var rng = new System.Random(101);

            for (int i = 0; i < 10; i++)
            {
                float z = -10f + i * 3.1f;
                var pos = new Vector3(hellish ? (float)(rng.NextDouble() * 0.35 - 0.175) : 0f, 0.08f, z);
                var rot = hellish ? Quaternion.Euler((float)(rng.NextDouble() * 5 - 2.5), (float)(rng.NextDouble() * 12 - 6), (float)(rng.NextDouble() * 5 - 2.5)) : Quaternion.identity;
                var m = Matrix4x4.TRS(pos, rot, Vector3.one);
                lp.Box(0, m, Vector3.zero, new Vector3(3.4f, 0.18f, 2.75f));
                for (int s = -1; s <= 1; s += 2)
                {
                    if (hellish && rng.NextDouble() < 0.25) continue;
                    lp.Box(2, m, new Vector3(s * 1.92f, 0.12f, 0f), new Vector3(0.32f, 0.28f, 2.55f));
                }
            }

            // Подиум Богини / Алтарь Демона
            var gm = Matrix4x4.TRS(GoddessPos, Quaternion.identity, Vector3.one);
            lp.Lathe(1, gm, new[] { -0.2f, 0.3f }, new[] { 4.8f, 4.6f }, 16);
            lp.Lathe(hellish ? 3 : 2, gm, new[] { 0.3f, 0.52f }, new[] { 3.8f, 3.6f }, 16);
            lp.Lathe(1, gm, new[] { 0.52f, 0.68f }, new[] { 3.3f, 3.2f }, 16);

            // Арки в Раю / сломанные колонны + пылающие жаровни в Аду
            float[] archZ = { -3.5f, 6f, 14.5f };
            for (int a = 0; a < archZ.Length; a++)
            {
                float z = archZ[a];
                if (!hellish)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        RuinKit.Pilaster(lp, 1, new Vector3(s * 3.1f, 0f, z), Vector3.back, 4.6f, 0.72f, rng);
                        lp.Lathe(3, Matrix4x4.TRS(new Vector3(s * 3.1f, 4.9f, z), Quaternion.identity, Vector3.one), new[] { 0f, 0.25f, 0.5f }, new[] { 0.18f, 0.26f, 0.08f }, 8);
                    }
                    // Перемычка арки плотно лежит на капителях колонн
                    lp.Box(1, Matrix4x4.identity, new Vector3(0f, 4.55f, z), new Vector3(7.4f, 0.65f, 0.85f));
                    lp.Box(2, Matrix4x4.identity, new Vector3(0f, 4.95f, z), new Vector3(7.9f, 0.24f, 1.05f));
                }
                else
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float h = (a == 1 && s == 1) ? 2.4f : 4.2f;
                        RuinKit.Pilaster(lp, 1, new Vector3(s * 3.2f, 0f, z), Vector3.back, h, 0.75f, rng);
                        // Пылающие жаровни вдоль аллеи Ада — дают драматичный свет и читаемость!
                        var bp = new Vector3(s * 2.5f, 0f, z - 1.2f);
                        lp.Lathe(2, Matrix4x4.TRS(bp, Quaternion.identity, Vector3.one), new[] { 0f, 0.2f, 1.0f, 1.25f, 1.55f }, new[] { 0.45f, 0.22f, 0.18f, 0.58f, 0.65f }, 8);
                        lp.Lathe(3, Matrix4x4.TRS(bp + Vector3.up * 1.4f, Quaternion.identity, Vector3.one), new[] { 0f, 0.25f, 0.45f }, new[] { 0.45f, 0.35f, 0.05f }, 8);
                        var bl = Z.PointLight(parent, $"HellBrazierLight_{a}_{s}", bp + Vector3.up * 2.3f, new Color(1f, 0.38f, 0.10f), 18f, 14f);
                        bl.shadows = LightShadows.None;
                        var fl = bl.gameObject.AddComponent<TorchFlicker>();
                        SetFloat(fl, "baseIntensity", 14f);
                        SetFloat(fl, "flickerAmount", 2.5f);
                        BuildParticlesBox(parent, $"BrazierFire_{a}_{s}", bp + Vector3.up * 1.65f, new Vector3(0.35f, 0.25f, 0.35f), sparkMat,
                            new Color(1f, 0.75f, 0.25f, 0.85f), new Color(1f, 0.25f, 0.05f, 0.75f), 14f, 0.08f, 0.18f, -0.18f, 0.45f);
                    }
                }
            }

            Z.MeshObject(hellish ? "HellAvenue" : "MarbleAvenue", parent, Register(lp.ToMesh(hellish ? "H_Avenue" : "P_Avenue")),
                new[] { tileMat, stoneMat, accentMat, glowMat }, true);
        }

        static void BuildPond(Transform parent, Material waterMat, Material rockMat, Material leafMat, Material accentMat, Material woodMat, Material sparkMat, bool hellish)
        {
            var lp = new LowPolyMesh(5); // 0 вода/лава, 1 камни, 2 листва, 3 акцент, 4 дерево
            var rng = new System.Random(202);

            // Поверхность пруда / лавового озера
            lp.Lathe(0, Matrix4x4.TRS(PondPos + Vector3.down * 0.22f, Quaternion.identity, new Vector3(1.35f, 1f, 0.96f)),
                new[] { -0.05f, 0.05f }, new[] { 5.4f, 5.4f }, 20);

            // Камни и кусты по берегу
            for (int i = 0; i < 16; i++)
            {
                float ang = i / 16f * Mathf.PI * 2f;
                float wx = PondPos.x + Mathf.Cos(ang) * 6.9f;
                float wz = PondPos.z + Mathf.Sin(ang) * 4.9f;
                float wy = SampleHeight(wx, wz);
                float r = 0.75f + (float)rng.NextDouble() * 0.65f;
                lp.Lathe(1, Matrix4x4.TRS(new Vector3(wx, wy - 0.1f, wz), Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f), Vector3.one),
                    new[] { 0f, r * 0.55f, r * 0.9f }, new[] { r, r * 0.85f, 0.15f }, 7, rng, 0.22f);
                if (!hellish && i % 3 == 0)
                {
                    var bp = new Vector3(wx + Mathf.Cos(ang) * 1.1f, wy + 0.35f, wz + Mathf.Sin(ang) * 1.1f);
                    lp.Lathe(i % 6 == 0 ? 3 : 2, Matrix4x4.TRS(bp, Quaternion.identity, Vector3.one),
                        new[] { -0.35f, 0.1f, 0.55f, 0.85f }, new[] { 0.45f, 0.95f, 0.72f, 0.1f }, 8, rng, 0.18f);
                }
            }

            if (!hellish)
            {
                // Деревянный мостик-причал
                var dm = Matrix4x4.TRS(new Vector3(PondPos.x + 5.9f, 0.05f, PondPos.z), Quaternion.identity, Vector3.one);
                lp.Box(4, dm, Vector3.zero, new Vector3(3.2f, 0.18f, 1.85f));
                for (int px = -1; px <= 1; px += 2)
                for (int pz = -1; pz <= 1; pz += 2)
                    lp.Box(4, dm, new Vector3(px * 1.3f, -0.35f, pz * 0.78f), new Vector3(0.18f, 0.85f, 0.18f));
            }
            else
            {
                // Яркое зарево и искры над лавовым озером
                var ll = Z.PointLight(parent, "LavaPoolLight", PondPos + Vector3.up * 3.2f, new Color(1f, 0.32f, 0.06f), 28f, 22f);
                ll.shadows = LightShadows.None;
                var fl = ll.gameObject.AddComponent<TorchFlicker>();
                SetFloat(fl, "baseIntensity", 22f);
                SetFloat(fl, "flickerAmount", 4f);
                BuildParticlesBox(parent, "LavaSparks", PondPos + Vector3.up * 0.6f, new Vector3(10f, 1f, 7f), sparkMat,
                    new Color(1f, 0.65f, 0.18f, 0.9f), new Color(1f, 0.22f, 0.04f, 0.85f), 26f, 0.14f, 0.32f, -0.22f, 1.1f);
            }

            Z.MeshObject(hellish ? "LavaLake" : "ParadisePond", parent, Register(lp.ToMesh(hellish ? "H_Pond" : "P_Pond")),
                new[] { waterMat, rockMat, leafMat, accentMat, woodMat }, false);
        }

        static Mesh BuildTreesMesh(Vector3[] spots, System.Random rng, bool hellish)
        {
            var lp = new LowPolyMesh(4); // 0 ствол, 1 листва, 2 тёмная листва, 3 сакура
            foreach (var p in spots)
            {
                float y = SampleHeight(p.x, p.z) - 0.15f;
                float sc = 0.85f + (float)rng.NextDouble() * 0.55f;
                float yaw = (float)rng.NextDouble() * 360f;
                int leafSub = rng.NextDouble() < 0.35 ? 3 : (rng.NextDouble() < 0.5 ? 1 : 2);
                var tm = Matrix4x4.TRS(new Vector3(p.x, y, p.z), Quaternion.Euler(0f, yaw, 0f), Vector3.one * sc);

                // Ствол с утолщением у корней
                float trunkH = hellish ? 4.2f : 3.6f;
                lp.Lathe(0, tm, new[] { 0f, 0.6f, trunkH * 0.55f, trunkH },
                    new[] { hellish ? 0.42f : 0.52f, 0.32f, 0.24f, hellish ? 0.12f : 0.18f }, 7, rng, 0.12f);

                int branches = hellish ? 5 : 4;
                for (int b = 0; b < branches; b++)
                {
                    float byaw = b * (360f / branches) + (float)(rng.NextDouble() * 30 - 15);
                    float pitch = 34f + (float)rng.NextDouble() * 24f;
                    float blen = 1.4f + (float)rng.NextDouble() * 0.9f;
                    float bh = trunkH * (0.58f + (b % 2) * 0.2f);
                    var bm = tm * Matrix4x4.TRS(new Vector3(0f, bh, 0f), Quaternion.Euler(0f, byaw, 0f) * Quaternion.Euler(pitch, 0f, 0f), Vector3.one);
                    lp.Lathe(0, bm, new[] { 0f, blen }, new[] { 0.15f, 0.05f }, 5, rng, 0.1f);
                    if (!hellish)
                    {
                        float cr = 1.15f + (float)rng.NextDouble() * 0.55f;
                        lp.Lathe(leafSub, bm * Matrix4x4.TRS(new Vector3(0f, blen, 0f), Quaternion.identity, Vector3.one),
                            new[] { -cr * 0.45f, 0f, cr * 0.55f, cr * 0.9f }, new[] { cr * 0.55f, cr, cr * 0.78f, 0.1f }, 8, rng, 0.16f);
                    }
                }
                if (!hellish)
                {
                    lp.Lathe(leafSub, tm * Matrix4x4.TRS(new Vector3(0f, trunkH + 1.2f, 0f), Quaternion.identity, Vector3.one),
                        new[] { -0.9f, 0f, 0.95f, 1.55f }, new[] { 0.9f, 1.75f, 1.35f, 0.15f }, 9, rng, 0.15f);
                }
            }
            return Register(lp.ToMesh(hellish ? "H_Trees" : "P_Trees"));
        }

        static void BuildCastle(Transform parent, Material walls, Material roofs, Material windows, bool hellish)
        {
            var lp = new LowPolyMesh(3); // 0 стены, 1 крыши/золото/шпили, 2 окна
            var cm = Matrix4x4.TRS(CastlePos, Quaternion.identity, Vector3.one);
            float h = hellish ? 1.32f : 1f;

            // Фундамент и парадная лестница
            lp.Box(0, cm, new Vector3(0f, 1.2f, 0f), new Vector3(28f, 2.4f, 18f));
            for (int st = 0; st < 5; st++)
                lp.Box(0, cm, new Vector3(0f, 0.25f + st * 0.45f, -9.5f - (4 - st) * 0.9f), new Vector3(7.6f, 0.48f, 1.1f));

            // Донжон и центральная башня со шпилем
            float keepH = 16f * h;
            lp.Box(0, cm, new Vector3(0f, 2.4f + keepH * 0.5f, 0f), new Vector3(17f, keepH, 11f));
            float topY = 2.4f + keepH;
            lp.Lathe(0, cm * Matrix4x4.TRS(new Vector3(0f, topY, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 9f * h }, new[] { 3.3f, 3.0f }, 12);
            lp.Lathe(1, cm * Matrix4x4.TRS(new Vector3(0f, topY + 9f * h, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.4f, hellish ? 14f : 10f }, new[] { 3.8f, 3.5f, 0f }, 12);

            // Ворота и окно-роза
            lp.Box(0, cm, new Vector3(0f, 5.4f, -5.8f), new Vector3(6.2f, 6.4f, 1.2f));
            lp.Box(1, cm, new Vector3(0f, 4.9f, -6.3f), new Vector3(4.2f, 5.2f, 0.4f));
            lp.Lathe(2, cm * Matrix4x4.TRS(new Vector3(0f, 11.8f * h, -5.65f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new[] { 0f, 0.2f }, new[] { 1.6f, 1.6f }, 12);

            // Зубцы фасада
            for (int m = -7; m <= 7; m += 2)
                lp.Box(0, cm, new Vector3(m, topY + 0.7f, -5.3f), new Vector3(1.1f, 1.4f, 0.7f));

            // 6 башен с коническими шпилями и светящимися окнами
            Vector3[] towers =
            {
                new Vector3(-11f, 2.4f, -6.5f), new Vector3(11f, 2.4f, -6.5f),
                new Vector3(-11f, 2.4f, 6.5f), new Vector3(11f, 2.4f, 6.5f),
                new Vector3(-6.2f, 2.4f, -7.8f), new Vector3(6.2f, 2.4f, -7.8f)
            };
            for (int i = 0; i < towers.Length; i++)
            {
                bool sm = i >= 4;
                float th = sm ? (hellish ? 19f : 14f) : (hellish ? 27f : 20f);
                float tr = sm ? 1.6f : 2.3f;
                var tm = cm * Matrix4x4.TRS(towers[i], Quaternion.identity, Vector3.one);
                lp.Lathe(0, tm, new[] { 0f, th }, new[] { tr, tr * 0.95f }, 12);
                lp.Lathe(1, tm, new[] { th - 0.4f, th + 0.3f }, new[] { tr * 1.18f, tr * 1.18f }, 12);
                lp.Lathe(1, tm * Matrix4x4.TRS(Vector3.up * (th + 0.3f), Quaternion.identity, Vector3.one),
                    new[] { 0f, hellish ? (sm ? 9f : 13f) : (sm ? 5.5f : 7.5f) }, new[] { tr * 1.25f, 0f }, 12);
                int wc = sm ? 2 : 3;
                for (int k = 0; k < wc; k++)
                    lp.Box(2, tm, new Vector3(0f, 4.5f + k * 4.6f, -tr - 0.04f), new Vector3(0.85f, 1.85f, 0.14f));
            }

            if (hellish)
            {
                // Красная подсветка фасада Демонической цитадели, чтобы она величественно возвышалась вдали!
                var cl = Z.PointLight(parent, "CitadelGlow", CastlePos + new Vector3(0f, 14f, -14f), new Color(1f, 0.24f, 0.06f), 46f, 36f);
                cl.shadows = LightShadows.None;
            }

            Z.MeshObject(hellish ? "DemonFortress" : "WhiteCastle", parent, Register(lp.ToMesh(hellish ? "H_Castle" : "P_Castle")),
                new[] { walls, roofs, windows }, true);
        }

        static Vector3[] TreeSpots(System.Random rng)
        {
            var list = new List<Vector3>();
            while (list.Count < 32)
            {
                var p = new Vector3((float)(rng.NextDouble() * 88 - 44), 0f, (float)(rng.NextDouble() * 76 - 16));
                if (Mathf.Abs(p.x) < 6.4f) continue;
                if (Vector3.Distance(p, PondPos) < 9.5f) continue;
                if (Vector3.Distance(p, CastlePos) < 20f) continue;
                if (Vector3.Distance(p, GoddessPos) < 8.5f) continue;
                list.Add(p);
            }
            return list.ToArray();
        }

        static Vector3 AngelPos(int i)
        {
            float a = i / 7f * Mathf.PI * 1.4f - Mathf.PI * 0.7f;
            return GoddessPos + new Vector3(Mathf.Sin(a) * 12f, 6f + (i % 3) * 2.5f, Mathf.Cos(a) * 6f + 6f);
        }

        /// <summary>Фигура: тело-капсула, крылья, (для демонов) рога и горящие глаза. Не трогаем.</summary>
        static Transform Figure(Transform parent, string name, Vector3 pos, float scale, Material body, Material wings, Material eyes, Material horns)
        {
            var f = new GameObject(name).transform;
            f.SetParent(parent, false);
            f.position = pos;
            f.localScale = Vector3.one * scale;
            f.rotation = Quaternion.Euler(0f, 180f, 0f);
            Prim(PrimitiveType.Capsule, f, "Body", Vector3.zero, new Vector3(0.9f, 1.2f, 0.9f), body, false, local: true);
            Prim(PrimitiveType.Sphere, f, "Head", new Vector3(0f, 1.6f, 0f), Vector3.one * 0.7f, body, false, local: true);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = Prim(PrimitiveType.Cube, f, "Wing", new Vector3(0.9f * s, 0.6f, 0.35f), new Vector3(1.8f, 1.2f, 0.06f), wings, false, local: true);
                w.transform.localRotation = Quaternion.Euler(0f, -25f * s, 25f * s);
                if (eyes != null)
                    Prim(PrimitiveType.Cube, f, "Eye", new Vector3(0.14f * s, 1.65f, -0.33f), new Vector3(0.12f, 0.06f, 0.05f), eyes, false, local: true);
                if (horns != null)
                {
                    var horn = Prim(PrimitiveType.Cube, f, "Horn", new Vector3(0.22f * s, 2.05f, 0f), new Vector3(0.1f, 0.5f, 0.1f), horns, false, local: true);
                    horn.transform.localRotation = Quaternion.Euler(0f, 0f, -25f * s);
                }
            }
            if (eyes == null)
                Prim(PrimitiveType.Cylinder, f, "Halo", new Vector3(0f, 2.2f, 0f), new Vector3(0.7f, 0.02f, 0.7f), wings, false, local: true);
            return f;
        }

        // ==================== УТИЛИТЫ ====================

        static void BuildParticlesBox(Transform parent, string name, Vector3 pos, Vector3 box, Material mat, Color a, Color b, float rate, float minS, float maxS, float grav, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = Z.Particles(go, mat, rate, 3.5f, 6.5f, speed * 0.4f, speed, minS, maxS, a, b, grav, 240, Z.FadeCurve(), 0.35f);
            Z.ShapeBox(ps, box);
        }

        static Material Lit(string name, Color c, float emissive = 0f, float smooth = 0.18f, float metal = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emissive > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emissive); }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SkyMaterial(string name, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var sh = Shader.Find("FalseParadise/SkyDome") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ParticleMat(string name, bool additive)
        {
            string path = $"{MatDir}/{name}.mat";
            var sh = Shader.Find("FalseParadise/ParticleSoft") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh Register(Mesh m) { generated.Add(m); return m; }

        static void SaveMeshes()
        {
            if (generated.Count == 0) return;
            AssetDatabase.CreateAsset(generated[0], MeshPath);
            for (int i = 1; i < generated.Count; i++) AssetDatabase.AddObjectToAsset(generated[i], generated[0]);
            AssetDatabase.SaveAssets();
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool keepCollider, bool local = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Volume GlobalVolume(string name, VolumeProfile profile, float weight, int priority)
        {
            var v = new GameObject(name).AddComponent<Volume>();
            v.isGlobal = true;
            v.weight = weight;
            v.priority = priority;
            v.sharedProfile = profile;
            return v;
        }

        static VolumeProfile ResetProfile(string name)
        {
            string path = $"{SandboxMatDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        static void Finish(VolumeProfile p)
        {
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            AssetDatabase.SaveAssets();
        }

        static VolumeProfile ParadiseProfile()
        {
            var p = ResetProfile("ParadisePost");
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.05f);
            bloom.scatter.Override(0.72f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(14f);
            ca.saturation.Override(16f);
            ca.colorFilter.Override(new Color(1f, 0.96f, 0.88f));
            p.Add<Vignette>(true).intensity.Override(0.14f);
            Finish(p);
            return p;
        }

        static VolumeProfile HellProfile()
        {
            var p = ResetProfile("HellPost");
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.72f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.55f);
            ca.contrast.Override(24f);
            ca.saturation.Override(-4f);
            ca.colorFilter.Override(new Color(1f, 0.72f, 0.65f));
            var v = p.Add<Vignette>(true);
            v.intensity.Override(0.34f);
            v.color.Override(new Color(0.22f, 0.02f, 0.02f));
            Finish(p);
            return p;
        }

        static VolumeProfile GlitchProfile()
        {
            var p = ResetProfile("GlitchPost");
            p.Add<ChromaticAberration>(true).intensity.Override(1f);
            p.Add<LensDistortion>(true).intensity.Override(-0.45f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.hueShift.Override(25f);
            ca.contrast.Override(40f);
            Finish(p);
            return p;
        }
    }
}
