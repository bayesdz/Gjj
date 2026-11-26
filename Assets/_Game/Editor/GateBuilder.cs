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
    /// Меню False Paradise → Build Elven Gate. Собирает переход «Врата эльфов» (N13) из кода:
    /// грот из серого камня с провалом в своде, через который падает лунный луч на ритуальный круг;
    /// Страж в круге, лагерь эльфов со старейшиной и серебряным деревом, древние ворота с рунами
    /// (открываются после победы над Стражем), проход дальше — к деревне огров.
    /// Палитра зоны (TASKS.md, раздел 4): серебро, бирюза, лунный свет — одинаково в иллюзии и правде.
    ///
    /// Маршрут (метры, x — восток, z — север):
    ///   Тоннель из логова (-40,0) → Площадь (0,0) r20 [лагерь (-12,13), Страж (4,-1)]
    ///   → Ворота (28,0) → Проход к деревне (46,0).
    /// Схема: docs/levels/LEVEL_MAP.png (врата: площадка ~42×42).
    /// </summary>
    public static class GateBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Gate_Elven.unity";
        const string ArtDir = "Assets/_Game/Art/Gate";
        const string MatDir = ArtDir + "/Materials";
        const string MeshPath = ArtDir + "/Gate_Meshes.asset";
        const string PostPath = ArtDir + "/Gate_Post.asset";
        const int Seed = 4242;

        // ---------------------------------------------------------------- планировка

        struct Room
        {
            public Vector2 c; public float r; public float h;
            public Room(float x, float z, float r, float h) { c = new Vector2(x, z); this.r = r; this.h = h; }
        }

        struct Corr
        {
            public Vector2 a, b; public float r;
            public Corr(float ax, float az, float bx, float bz, float r) { a = new Vector2(ax, az); b = new Vector2(bx, bz); this.r = r; }
        }

        static readonly Room Tunnel = new Room(-40f, 0f, 5f, -0.4f);
        static readonly Room Plaza = new Room(0f, 0f, 20f, 0f);
        static readonly Room Camp = new Room(-12f, 13f, 8.5f, 0.9f);
        static readonly Room Apron = new Room(20f, 0f, 7f, 0.4f);
        static readonly Room Beyond = new Room(46f, 0f, 6f, 1.2f);
        static readonly Room[] Rooms = { Tunnel, Plaza, Camp, Apron, Beyond };

        static readonly Corr[] Corridors =
        {
            new Corr(-46f, 0f, -17f, 0f, 4f),
            new Corr(0f, 0f, 20f, 0f, 6.5f),
            new Corr(20f, 0f, 52f, 0f, 3.6f),
        };

        const float GateX = 28f;
        const float GateHalf = 4f;      // половина ширины проёма
        const float GateHeight = 8f;

        // Провал в своде и лунный луч
        static readonly Vector2 HoleC = new Vector2(2f, 11f);
        const float HoleR = 6f;
        static readonly Quaternion MoonRot = Quaternion.Euler(51f, 180f, 0f);

        // Точки интереса
        static readonly Vector2 SpawnPos = new Vector2(-42f, 0f);
        static readonly Vector2 CircleC = new Vector2(3f, -1f);
        const float CircleR = 6.5f;
        static readonly Vector2 GuardianPos = new Vector2(4f, -1f);
        static readonly Vector2 ElderPos = new Vector2(-9f, 10.5f);
        static readonly Vector2 BrazierPos = new Vector2(-13f, 13f);
        static readonly Vector2 TreePos = new Vector2(-16f, 18.5f);
        static readonly Vector2[] TentPos = { new Vector2(-17.5f, 10f), new Vector2(-9f, 19f), new Vector2(-4.5f, 16.5f) };
        static readonly Vector2[] ElfPos = { new Vector2(-5f, 9f), new Vector2(15f, 6f), new Vector2(15f, -6.5f) };

        // Сетка рельефа
        const float MinX = -66f, MaxX = 72f, MinZ = -44f, MaxZ = 44f;
        const float Cell = 2f;
        const int ChunkCells = 20;

        // ---------------------------------------------------------------- поля высот

        static float Noise(float x, float z, float freq, float offset) =>
            Mathf.PerlinNoise(x * freq + offset, z * freq + offset * 1.37f + 41.7f);

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        /// <summary>&lt; 0 — внутри грота, &gt; 0 — в скале.</summary>
        static float Sdf(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            foreach (var r in Rooms) d = Mathf.Min(d, Vector2.Distance(p, r.c) - r.r);
            foreach (var c in Corridors) d = Mathf.Min(d, SegDist(p, c.a, c.b) - c.r);
            d += (Noise(x, z, 0.08f, 210f) - 0.5f) * 3.6f;
            d += (Noise(x, z, 0.27f, 330f) - 0.5f) * 1.1f;
            return d;
        }

        static float PathDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            foreach (var c in Corridors) d = Mathf.Min(d, SegDist(p, c.a, c.b));
            return d;
        }

        static float FloorH(float x, float z)
        {
            var p = new Vector2(x, z);
            float sw = 0f, sh = 0f;
            foreach (var r in Rooms)
            {
                float d = Mathf.Max(0f, Vector2.Distance(p, r.c) - r.r);
                float w = 1f / (d * d + 16f);
                sw += w;
                sh += w * r.h;
            }
            float h = sh / sw + (Noise(x, z, 0.06f, 520f) - 0.5f) * 0.9f;
            // Ритуальный круг и порог ворот — ровные
            float circle = 1f - Smooth(CircleR, CircleR + 4f, Vector2.Distance(p, CircleC));
            if (circle > 0f) return Mathf.Lerp(h, 0f, circle);
            float gate = (1f - Smooth(4f, 9f, Mathf.Abs(x - GateX))) * (1f - Smooth(9f, 15f, Mathf.Abs(z)));
            return Mathf.Lerp(h, Apron.h, gate);
        }

        internal static float Height(float x, float z)
        {
            float h = FloorH(x, z) + (Noise(x, z, 0.4f, 760f) - 0.5f) * 0.18f;
            float d = Sdf(x, z);
            if (d > 0f)
            {
                float n = Noise(x, z, 0.12f, 940f);
                h += Smooth(0f, 4f, d) * (5f + 4f * n) + Smooth(2f, 20f, d) * (16f + 14f * n);
                h += (Noise(x, z, 0.22f, 1180f) - 0.5f) * 2f * Smooth(0f, 3f, d);
            }
            return h;
        }

        static Vector3 Ground(Vector2 p, float lift = 0f) => new Vector3(p.x, Height(p.x, p.y) + lift, p.y);

        static float Hash(int x, int z, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(s * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Свод: высокий купол над площадью (до ~20 м), ниже в проходах.</summary>
        static float CeilingH(float x, float z)
        {
            float d = Sdf(x, z);
            float dome = d < 0f ? 7.5f + 12.5f * Smooth(0f, 17f, -d) : 7.5f - d * 0.7f;
            dome += (Noise(x, z, 0.14f, 2550f) - 0.5f) * 3f + (Noise(x, z, 0.45f, 2770f) - 0.5f) * 0.5f;
            // Высокий свод над воротами: монументальная арка не должна упираться в потолок
            dome += 9f * (1f - Smooth(8f, 20f, Vector2.Distance(new Vector2(x, z), new Vector2(GateX - 2f, 0f))));
            return FloorH(x, z) + dome;
        }

        static float HoleSdf(float x, float z) =>
            Vector2.Distance(new Vector2(x, z), HoleC) - HoleR + (Noise(x, z, 0.35f, 3300f) - 0.5f) * 2.4f;

        // ---------------------------------------------------------------- состояние сборки

        static readonly List<Mesh> generated = new List<Mesh>();
        static readonly List<Light> shadowLights = new List<Light>();
        static System.Random rng;

        static Material mFloor, mMoss, mWall, mWallHigh, mCeiling, mRock, mRockDark;
        static Material mMarble, mMarbleDark, mSilver, mRune, mRuneDim, mCrystal;
        static Material mCloth, mClothTeal, mClothDark, mWood, mWoodPale, mLeaf, mGrass, mBark;
        static Material mElfSkin, mElfCloak, mElfHair, mElderRobe, mElfAccent;
        static Material mShadow, mGuardLight, mGuardEyes, mIron;
        static Material mSkin, mSleeve, mWoodDark, mSteel;
        static Material gMoon, gStars, gShaft, gVeil, gFlowers;
        static Material pFire, pSmoke, pEmbers, pHalo, pDust, pMotes, pShadowSmoke, pGold, pHaze;
        static Material mBlock;
        static readonly List<Vector4> ruinSegs = new List<Vector4>();

        [MenuItem("False Paradise/Build Elven Gate")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/_Game/Scenes");
            EnsureFolder(ArtDir);
            EnsureFolder(MatDir);
            if (AssetDatabase.LoadAssetAtPath<Object>(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
            if (AssetDatabase.LoadAssetAtPath<Object>(PostPath) != null) AssetDatabase.DeleteAsset(PostPath);

            generated.Clear();
            shadowLights.Clear();
            ruinSegs.Clear();
            rng = new System.Random(Seed);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            try
            {
                EditorUtility.DisplayProgressBar("Elven Gate", "Материалы", 0.05f);
                CreateMaterials();
                var world = new GameObject("Gate_Elven").transform;

                EditorUtility.DisplayProgressBar("Elven Gate", "Рельеф и свод", 0.15f);
                BuildTerrain(world);
                BuildCeiling(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Древние стены и фрески", 0.3f);
                BuildRuins(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Скалы, кристаллы, колонны", 0.35f);
                BuildDecor(world);
                BuildColumns(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Ритуальный круг и трава", 0.45f);
                BuildCircle(world);
                BuildMoonGarden(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Ворота", 0.55f);
                var gate = BuildGate(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Лагерь эльфов", 0.65f);
                BuildCamp(world);
                BuildTree(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Эльфы и Страж", 0.75f);
                BuildElves(world, gate);
                BuildGuardian(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Игрок, луна, атмосфера", 0.88f);
                var cam = BuildPlayer();
                BuildMoon();
                BuildAtmosphere(cam);
                BuildNarration(world);

                EditorUtility.DisplayProgressBar("Elven Gate", "Сохранение", 0.96f);
                SaveMeshes();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
                Debug.Log($"[False Paradise] Врата эльфов собраны: {ScenePath}. Мешей: {generated.Count}. Нажмите Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- материалы

        static void CreateMaterials()
        {
            // Холодный серый камень с голубым отливом
            mFloor = Lit("G_Floor", new Color(0.44f, 0.45f, 0.47f));
            mMoss = Lit("G_Moss", new Color(0.3f, 0.37f, 0.35f));
            mWall = Lit("G_Wall", new Color(0.5f, 0.51f, 0.54f));
            mWallHigh = Lit("G_WallHigh", new Color(0.4f, 0.41f, 0.45f));
            mCeiling = Lit("G_Ceiling", new Color(0.33f, 0.34f, 0.38f));
            mRock = Lit("G_Rock", new Color(0.55f, 0.56f, 0.58f));
            mRockDark = Lit("G_RockDark", new Color(0.34f, 0.35f, 0.38f));

            // Эльфийский камень и металл
            mMarble = Lit("G_Marble", new Color(0.84f, 0.85f, 0.86f), 0f, 0.3f);
            mMarbleDark = Lit("G_MarbleDark", new Color(0.58f, 0.61f, 0.64f), 0f, 0.2f);
            mSilver = Lit("G_Silver", new Color(0.78f, 0.81f, 0.86f), 0f, 0.62f, 0.35f);
            mRune = Lit("G_Rune", new Color(0.3f, 1f, 0.88f), 3.2f);
            mRuneDim = Lit("G_RuneDim", new Color(0.3f, 1f, 0.88f), 1.1f);
            mCrystal = Lit("G_Crystal", new Color(0.28f, 0.88f, 0.84f), 0.75f, 0.85f);

            mCloth = Lit("G_Cloth", new Color(0.86f, 0.87f, 0.9f));
            mClothTeal = Lit("G_ClothTeal", new Color(0.12f, 0.48f, 0.5f));
            mClothDark = Lit("G_ClothDark", new Color(0.12f, 0.14f, 0.17f));
            mWood = Lit("G_Wood", new Color(0.38f, 0.32f, 0.27f));
            mWoodPale = Lit("G_WoodPale", new Color(0.8f, 0.78f, 0.73f));
            mBark = Lit("G_Bark", new Color(0.72f, 0.74f, 0.76f), 0f, 0.15f);
            mLeaf = Lit("G_Leaf", new Color(0.62f, 0.86f, 0.84f), 0.45f, 0.3f);
            mGrass = Lit("G_Grass", new Color(0.5f, 0.64f, 0.56f), 0f, 0.2f);
            mGrass.SetFloat("_Cull", (float)CullMode.Off);
            mIron = Lit("G_Iron", new Color(0.2f, 0.21f, 0.23f), 0f, 0.45f, 0.6f);

            mElfSkin = Lit("G_ElfSkin", new Color(0.88f, 0.8f, 0.74f));
            mElfCloak = Lit("G_ElfCloak", new Color(0.3f, 0.4f, 0.42f));
            mElfHair = Lit("G_ElfHair", new Color(0.9f, 0.9f, 0.92f), 0f, 0.35f);
            mElderRobe = Lit("G_ElderRobe", new Color(0.92f, 0.92f, 0.94f));
            mElfAccent = Lit("G_ElfAccent", new Color(0.3f, 1f, 0.88f), 0.8f);

            // Страж: тень и свет богини
            mShadow = Lit("G_GuardShadow", new Color(0.02f, 0.02f, 0.03f), 0f, 0.75f);
            mGuardLight = Emissive("G_GuardLight", new Color(1f, 0.8f, 0.48f), 3.2f);
            mGuardEyes = Emissive("G_GuardEyes", new Color(1f, 0.95f, 0.85f), 6f);

            // Рука героя (как в зоне 1)
            mSkin = Lit("G_HeroSkin", new Color(0.78f, 0.58f, 0.46f));
            mSleeve = Lit("G_HeroSleeve", new Color(0.3f, 0.26f, 0.32f));
            mWoodDark = Lit("G_WoodDark", new Color(0.3f, 0.2f, 0.13f));
            mSteel = Lit("G_Steel", new Color(0.42f, 0.44f, 0.48f), 0f, 0.45f, 0.7f);

            gMoon = Glow("G_Moon", new Color(0.86f, 0.92f, 1f), 2.2f);
            gStars = Glow("G_Stars", new Color(0.8f, 0.88f, 1f), 2f);
            gShaft = Glow("G_Shaft", new Color(0.7f, 0.82f, 1f), 0.3f);
            gVeil = Glow("G_Veil", new Color(1f, 0.62f, 0.4f), 1.2f);
            gFlowers = Glow("G_Flowers", new Color(0.6f, 1f, 0.95f), 2.2f);

            pFire = Soft("G_Fire", new Color(1f, 0.45f, 0.12f), 2.2f, true, 1.4f);
            pSmoke = Soft("G_Smoke", new Color(0.25f, 0.27f, 0.3f, 0.12f), 1f, false, 2.6f);
            pEmbers = Soft("G_Embers", new Color(1f, 0.55f, 0.18f), 4f, true, 2f);
            pHalo = Soft("G_Halo", new Color(0.3f, 1f, 0.88f), 0.45f, true, 2.6f);
            pDust = Soft("G_Dust", new Color(0.8f, 0.9f, 1f), 1.2f, true, 2f);
            pMotes = Soft("G_Motes", new Color(0.35f, 1f, 0.9f), 3f, true, 2f);
            pShadowSmoke = Soft("G_ShadowSmoke", new Color(0.01f, 0.01f, 0.02f, 0.5f), 1f, false, 2f);
            pGold = Soft("G_GoldMotes", new Color(1f, 0.78f, 0.4f), 3.5f, true, 2f);

            TextureKit.BeginBuild();
            var rock = TextureKit.Rock();
            var masonry = TextureKit.Masonry();
            var planks = TextureKit.Planks();
            void Tex(Material m, float tile, float bump) => TextureKit.Apply(m, rock, tile, bump);
            mBlock = Lit("G_Block", new Color(0.62f, 0.64f, 0.67f));
            Tex(mBlock, 2.2f, 1.4f);
            pHaze = Soft("G_Haze", Color.white, 1f, false, 2.2f);
            // Пол грота вымощен эльфами: старые плиты; кладка ворот и колонн — блоки
            TextureKit.Apply(mFloor, TextureKit.Flagstone(), 0.45f, 0.8f);
            Tex(mMoss, 1.6f, 1.2f);
            Tex(mWall, 0.8f, 1.3f);
            Tex(mWallHigh, 0.8f, 1.3f);
            Tex(mCeiling, 0.9f, 1.3f);
            Tex(mRock, 1.8f, 1.2f);
            Tex(mRockDark, 1.8f, 1.2f);
            TextureKit.Apply(mMarble, masonry, 0.9f, 1.1f);
            TextureKit.Apply(mMarbleDark, masonry, 0.9f, 1.2f);
            TextureKit.Apply(mWood, planks, 1.6f, 0.8f);
            TextureKit.Apply(mWoodPale, planks, 1.6f, 0.8f);
            RuinKit.Reflective(mSilver);
            RuinKit.Reflective(mIron);
            RuinKit.Reflective(mCrystal);
            Tex(mBark, 2.5f, 0.8f);
        }

        static Material Lit(string name, Color c, float emission = 0f, float smooth = 0.06f, float metallic = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Emissive(string name, Color c, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            var hdr = new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f);
            m.color = hdr;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Glow(string name, Color c, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("FalseParadise/UnlitGlow") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Soft(string name, Color c, float intensity, bool additive, float softness)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("FalseParadise/ParticleSoft") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
            if (m.HasProperty("_Softness")) m.SetFloat("_Softness", softness);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- рельеф и свод

        static void BuildTerrain(Transform world)
        {
            var root = new GameObject("Terrain").transform;
            root.SetParent(world, false);
            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell) + 1;
            int nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell) + 1;
            var pos = new Vector3[nx, nz];
            var sdf = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = MinX + ix * Cell + (Hash(ix, iz, 1) - 0.5f) * Cell * 0.45f;
                float z = MinZ + iz * Cell + (Hash(ix, iz, 2) - 0.5f) * Cell * 0.45f;
                pos[ix, iz] = new Vector3(x, Height(x, z), z);
                sdf[ix, iz] = Sdf(x, z);
            }

            // 0 пол, 1 мох, 2 стена, 3 верх стены
            var mats = new[] { mFloor, mMoss, mWall, mWallHigh };
            var chunks = new Dictionary<Vector2Int, LowPolyMesh>();
            for (int ix = 0; ix < nx - 1; ix++)
            for (int iz = 0; iz < nz - 1; iz++)
            {
                if (sdf[ix, iz] > 26f && sdf[ix + 1, iz] > 26f && sdf[ix, iz + 1] > 26f && sdf[ix + 1, iz + 1] > 26f) continue;
                var key = new Vector2Int(ix / ChunkCells, iz / ChunkCells);
                if (!chunks.TryGetValue(key, out var lp)) chunks[key] = lp = new LowPolyMesh(mats.Length);
                var p00 = pos[ix, iz];
                var p10 = pos[ix + 1, iz];
                var p01 = pos[ix, iz + 1];
                var p11 = pos[ix + 1, iz + 1];
                if (Hash(ix, iz, 3) < 0.5f) { TerrainTri(lp, p00, p01, p11); TerrainTri(lp, p00, p11, p10); }
                else { TerrainTri(lp, p00, p01, p10); TerrainTri(lp, p10, p01, p11); }
            }
            foreach (var kv in chunks)
            {
                var mesh = Register(kv.Value.ToMesh($"G_Terrain_{kv.Key.x}_{kv.Key.y}"));
                var go = Z.MeshObject($"Chunk_{kv.Key.x}_{kv.Key.y}", root, mesh, mats, true);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static Vector3 HeightNormal(float x, float z)
        {
            const float e = 1.5f;
            float dx = Height(x + e, z) - Height(x - e, z);
            float dz = Height(x, z + e) - Height(x, z - e);
            var n = new Vector3(-dx, 2f * e, -dz).normalized;
            return n.y < 0.35f ? Vector3.up : n;
        }

        static Vector3 SlopeNormal(float x, float z, bool ceiling)
        {
            const float e = 1.5f;
            if (ceiling)
            {
                float cx = CeilingH(x + e, z) - CeilingH(x - e, z);
                float cz = CeilingH(x, z + e) - CeilingH(x, z - e);
                return new Vector3(cx, -2f * e, cz).normalized;
            }
            float dx = Height(x + e, z) - Height(x - e, z);
            float dz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        static void TriBlend(LowPolyMesh lp, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, float k)
        {
            var f = Vector3.Cross(b - a, c - a);
            if (f.sqrMagnitude < 1e-12f) return;
            f.Normalize();
            if (Vector3.Dot(f, na + nb + nc) < 0f) f = -f;
            lp.TriSmooth(sub, a, b, c, Vector3.Lerp(f, na, k).normalized, Vector3.Lerp(f, nb, k).normalized, Vector3.Lerp(f, nc, k).normalized);
        }

        static void TerrainTri(LowPolyMesh lp, Vector3 a, Vector3 b, Vector3 c)
        {
            var center = (a + b + c) / 3f;
            var n = Vector3.Cross(b - a, c - a);
            if (n.y < 0f) n = -n;
            n.Normalize();
            float rel = center.y - FloorH(center.x, center.z);
            int sub;
            if (n.y > 0.8f && rel < 1.5f)
            {
                // Мох у стен и в сырых пятнах; ритуальный круг и порог ворот — чистый камень
                float wall = Sdf(center.x, center.z);
                bool clean = Vector2.Distance(new Vector2(center.x, center.z), CircleC) < CircleR + 1f;
                sub = !clean && (wall > -3.5f || Noise(center.x, center.z, 0.12f, 2210f) > 0.6f) ? 1 : 0;
            }
            else sub = rel > 11f ? 3 : 2;
            if (sub <= 1)
            {
                lp.TriSmooth(sub, a, b, c, HeightNormal(a.x, a.z), HeightNormal(b.x, b.z), HeightNormal(c.x, c.z));
                return;
            }
            TriBlend(lp, sub, a, b, c, SlopeNormal(a.x, a.z, false), SlopeNormal(b.x, b.z, false), SlopeNormal(c.x, c.z, false), 0.65f);
        }

        static void BuildCeiling(Transform world)
        {
            var root = new GameObject("Ceiling").transform;
            root.SetParent(world, false);
            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell) + 1;
            int nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell) + 1;
            var pos = new Vector3[nx, nz];
            var sdf = new float[nx, nz];
            var hole = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = MinX + ix * Cell + (Hash(ix, iz, 4) - 0.5f) * Cell * 0.7f;
                float z = MinZ + iz * Cell + (Hash(ix, iz, 5) - 0.5f) * Cell * 0.7f;
                pos[ix, iz] = new Vector3(x, CeilingH(x, z), z);
                sdf[ix, iz] = Sdf(x, z);
                hole[ix, iz] = HoleSdf(x, z);
            }
            var mats = new[] { mCeiling, mWallHigh };
            var chunks = new Dictionary<Vector2Int, LowPolyMesh>();
            for (int ix = 0; ix < nx - 1; ix++)
            for (int iz = 0; iz < nz - 1; iz++)
            {
                float minD = Mathf.Min(Mathf.Min(sdf[ix, iz], sdf[ix + 1, iz]), Mathf.Min(sdf[ix, iz + 1], sdf[ix + 1, iz + 1]));
                if (minD > 6f) continue;
                // Провал к небу: клетки внутри круга не строим
                float minHole = Mathf.Min(Mathf.Min(hole[ix, iz], hole[ix + 1, iz]), Mathf.Min(hole[ix, iz + 1], hole[ix + 1, iz + 1]));
                if (minHole < 1.5f) continue; // край прячет юбка трубы (см. ниже)
                var key = new Vector2Int(ix / ChunkCells, iz / ChunkCells);
                if (!chunks.TryGetValue(key, out var lp)) chunks[key] = lp = new LowPolyMesh(mats.Length);
                var p00 = pos[ix, iz];
                var p10 = pos[ix + 1, iz];
                var p01 = pos[ix, iz + 1];
                var p11 = pos[ix + 1, iz + 1];
                int sub = Hash(ix, iz, 6) < 0.3f ? 1 : 0;
                Vector3 N(Vector3 v) => SlopeNormal(v.x, v.z, true);
                if (Hash(ix, iz, 7) < 0.5f)
                {
                    TriBlend(lp, sub, p00, p01, p11, N(p00), N(p01), N(p11), 0.6f);
                    TriBlend(lp, sub, p00, p11, p10, N(p00), N(p11), N(p10), 0.6f);
                }
                else
                {
                    TriBlend(lp, sub, p00, p01, p10, N(p00), N(p01), N(p10), 0.6f);
                    TriBlend(lp, sub, p10, p01, p11, N(p10), N(p01), N(p11), 0.6f);
                }
            }
            foreach (var kv in chunks)
            {
                var mesh = Register(kv.Value.ToMesh($"G_Ceiling_{kv.Key.x}_{kv.Key.y}"));
                var go = Z.MeshObject($"Ceiling_{kv.Key.x}_{kv.Key.y}", root, mesh, mats, false);
                // Луна светит сверху на тыльную сторону свода: тень должна считаться с обеих сторон
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }

            // Провал: каменная «труба» к небу и юбка под ней, закрывающая рваный край свода
            var rim = new LowPolyMesh(4); // 0 камень, 1 тёмный камень, 2 кора корней, 3 листья
            var r = new System.Random(91);
            const int seg = 22;
            float[] ringH = { -1.2f, 1.5f, 4.5f, 8f };
            float[] ringR = { 0f, 0.6f, 1.3f, 2.4f }; // расширение кверху
            var pts = new Vector3[ringH.Length + 1, seg];
            for (int j = 0; j < seg; j++)
            {
                float a = j * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float baseR = HoleR + 0.4f + Rr(r, -0.5f, 0.5f);
                var e = new Vector3(HoleC.x, 0f, HoleC.y) + dir * baseR;
                float ceil = CeilingH(e.x, e.z);
                // Юбка: внешнее кольцо прижато к своду снизу
                var outer = new Vector3(HoleC.x, 0f, HoleC.y) + dir * (HoleR + 4.5f);
                pts[0, j] = new Vector3(outer.x, CeilingH(outer.x, outer.z) - 0.35f, outer.z);
                for (int k = 0; k < ringH.Length; k++)
                {
                    var q = new Vector3(HoleC.x, 0f, HoleC.y) + dir * (baseR + ringR[k] + Rr(r, -0.35f, 0.35f));
                    pts[k + 1, j] = new Vector3(q.x, ceil + ringH[k] + Rr(r, -0.3f, 0.3f), q.z);
                }
            }
            var hc = new Vector3(HoleC.x, 0f, HoleC.y);
            for (int k = 0; k < ringH.Length; k++)
            for (int j = 0; j < seg; j++)
            {
                int j1 = (j + 1) % seg;
                var a = pts[k, j]; var b = pts[k, j1]; var c = pts[k + 1, j1]; var d = pts[k + 1, j];
                // k = 0 — юбка смотрит вниз, выше — стенки трубы смотрят внутрь
                Vector3 Out(Vector3 p) => k == 0 ? Vector3.down : (hc - new Vector3(p.x, 0f, p.z)).normalized;
                int sub = Hash(j, k, 11) < 0.35f ? 1 : 0;
                var mid = (a + b + c + d) * 0.25f;
                rim.Tri(sub, a, b, c, Out(mid));
                rim.Tri(sub, a, c, d, Out(mid));
            }
            // Несколько корней с листьями свисают в провал
            for (int i = 0; i < 14; i++)
            {
                float a = Rr(r, 0f, Mathf.PI * 2f);
                float rad = HoleR + Rr(r, 0f, 0.6f);
                float x = HoleC.x + Mathf.Cos(a) * rad, z = HoleC.y + Mathf.Sin(a) * rad;
                float y = CeilingH(x, z) - 1f;
                float len = Rr(r, 1.5f, 5f);
                var m = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(180f + Rr(r, -10f, 10f), Rr(r, 0f, 360f), Rr(r, -10f, 10f)), Vector3.one);
                rim.Lathe(2, m, new[] { 0f, len * 0.5f, len }, new[] { 0.07f, 0.05f, 0.015f }, 4, r, 0.2f);
                for (float t = 0.5f; t < len; t += Rr(r, 0.35f, 0.7f))
                    rim.Box(3, m * Matrix4x4.TRS(new Vector3(0f, t, 0f), Quaternion.Euler(Rr(r, -50f, 50f), Rr(r, 0f, 360f), 0f), Vector3.one),
                        new Vector3(0.14f, 0f, 0f), new Vector3(0.26f, 0.03f, 0.14f));
            }
            var rimGo = Z.MeshObject("HoleRim", root, Register(rim.ToMesh("G_HoleRim")), new[] { mWallHigh, mCeiling, mWood, mGrass }, false);
            rimGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        // ---------------------------------------------------------------- декор

        static bool NearRuins(float x, float z, float dist)
        {
            var p = new Vector2(x, z);
            foreach (var sg in ruinSegs) if (SegDist(p, new Vector2(sg.x, sg.y), new Vector2(sg.z, sg.w)) < dist) return true;
            return false;
        }

        static bool Reserved(float x, float z)
        {
            var p = new Vector2(x, z);
            if (NearRuins(x, z, 2.2f)) return true;
            if (Vector2.Distance(p, SpawnPos) < 4f) return true;
            if (Vector2.Distance(p, CircleC) < CircleR + 3f) return true;
            if (Vector2.Distance(p, Camp.c) < Camp.r + 1f) return true;
            if (Mathf.Abs(x - GateX) < 7f && Mathf.Abs(z) < 14f) return true;
            return false;
        }

        static Vector3 Gradient(float x, float z)
        {
            const float e = 0.75f;
            var g = new Vector3(Sdf(x + e, z) - Sdf(x - e, z), 0f, Sdf(x, z + e) - Sdf(x, z - e));
            return g.sqrMagnitude > 1e-6f ? g.normalized : Vector3.forward;
        }

        static void BuildDecor(Transform world)
        {
            var root = new GameObject("Decor").transform;
            root.SetParent(world, false);
            var solidMats = new[] { mRock, mRockDark };
            var hangMats = new[] { mRock, mRockDark, mCeiling };
            var solid = new Dictionary<Vector2Int, LowPolyMesh>();
            var hang = new Dictionary<Vector2Int, LowPolyMesh>();
            var crystals = new Dictionary<Vector2Int, LowPolyMesh>();
            var detail = new Dictionary<Vector2Int, LowPolyMesh>();

            LowPolyMesh Chunk(Dictionary<Vector2Int, LowPolyMesh> dict, int subs, float x, float z)
            {
                var key = new Vector2Int(Mathf.FloorToInt((x - MinX) / (Cell * ChunkCells)), Mathf.FloorToInt((z - MinZ) / (Cell * ChunkCells)));
                if (!dict.TryGetValue(key, out var lp)) dict[key] = lp = new LowPolyMesh(subs);
                return lp;
            }

            const float step = 2.5f;
            for (float gx = MinX; gx < MaxX; gx += step)
            for (float gz = MinZ; gz < MaxZ; gz += step)
            {
                float x = gx + R(-0.5f, 0.5f) * step;
                float z = gz + R(-0.5f, 0.5f) * step;
                float d = Sdf(x, z);
                if (d > 8f) continue;
                float path = PathDist(x, z);
                bool reserved = Reserved(x, z);
                var p = new Vector3(x, Height(x, z), z);

                if (d > -1.5f && d < 3.5f && R01() < 0.5f && !NearRuins(x, z, 1.4f) && !(Mathf.Abs(x - GateX) < 4f && Mathf.Abs(z) < 14f))
                    Boulder(Chunk(solid, 2, x, z), p, R(1.2f, 3.2f));

                if (d > -3.5f && d < 1f && path > 3.5f && !reserved && R01() < 0.12f)
                    Stalagmite(Chunk(solid, 2, x, z), p, R(1.2f, 4f));

                if (d < 2.5f && HoleSdf(x, z) > 3f && R01() < 0.22f)
                {
                    float len = path < 4f ? R(0.5f, 1.8f) : R(1f, 4.5f);
                    Stalactite(Chunk(hang, 3, x, z), x, z, len);
                }

                // Бирюзовые кристаллы у подножия стен
                if (d > -2.6f && d < 0.8f && path > 2.5f && !reserved && Noise(x, z, 0.09f, 3500f) > 0.52f && R01() < 0.3f)
                    CrystalCluster(Chunk(crystals, 1, x, z), p, -Gradient(x, z));

                // Камешки на полу
                if (d < -0.8f && path > 1.2f && R01() < 0.16f)
                    Pebbles(Chunk(detail, 2, x, z), p);
            }

            foreach (var kv in solid)
            {
                var go = Z.MeshObject($"Solid_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"G_Decor_{kv.Key.x}_{kv.Key.y}")), solidMats, true);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            foreach (var kv in hang)
            {
                var go = Z.MeshObject($"Stalactites_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"G_Hang_{kv.Key.x}_{kv.Key.y}")), hangMats, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            foreach (var kv in crystals)
            {
                var go = Z.MeshObject($"Crystals_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"G_Crystals_{kv.Key.x}_{kv.Key.y}")), new[] { mCrystal }, false);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var kv in detail)
            {
                var go = Z.MeshObject($"Pebbles_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"G_Pebbles_{kv.Key.x}_{kv.Key.y}")), solidMats, false);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static void Boulder(LowPolyMesh lp, Vector3 p, float s)
        {
            var m = Matrix4x4.TRS(p + Vector3.down * s * 0.25f, Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)),
                new Vector3(s * R(0.8f, 1.3f), s * R(0.5f, 0.9f), s * R(0.8f, 1.3f)));
            lp.Lathe(R01() < 0.35f ? 1 : 0, m, new[] { -0.3f, 0.25f, 0.7f, 1f }, new[] { 0.85f, 1f, 0.7f, 0.25f }, rng.Next(5, 8), rng, 0.28f);
        }

        static void Stalagmite(LowPolyMesh lp, Vector3 p, float h)
        {
            float w = h * R(0.17f, 0.25f);
            var m = Matrix4x4.TRS(p + Vector3.down * 0.3f, Quaternion.Euler(R(-7f, 7f), R(0f, 360f), R(-7f, 7f)), Vector3.one);
            lp.Lathe(R01() < 0.6f ? 0 : 1, m, new[] { 0f, h * 0.3f, h * 0.65f, h }, new[] { w, w * 0.7f, w * 0.38f, 0f }, rng.Next(5, 8), rng, 0.18f, false, 0.3f);
        }

        static void Stalactite(LowPolyMesh lp, float x, float z, float len)
        {
            float top = CeilingH(x, z) + 0.6f;
            float w = len * R(0.15f, 0.24f);
            var m = Matrix4x4.TRS(new Vector3(x, top, z), Quaternion.Euler(180f + R(-6f, 6f), R(0f, 360f), R(-6f, 6f)), Vector3.one);
            float t = R01();
            lp.Lathe(t < 0.45f ? 2 : t < 0.8f ? 0 : 1, m, new[] { 0f, len * 0.3f, len * 0.65f, len + 0.6f }, new[] { w, w * 0.68f, w * 0.33f, 0f }, rng.Next(5, 8), rng, 0.18f, false, 0.3f);
        }

        // ---------------------------------------------------------------- древние стены площади

        /// <summary>
        /// Площадь окружена остатками эльфийских стен из блоков: пилястры, руинный верх,
        /// на стенах — фрески истории эльфов (древо, Око, Страж с нимбом, врата).
        /// </summary>
        static void BuildRuins(Transform world)
        {
            var root = new GameObject("AncientWalls").transform;
            root.SetParent(world, false);
            var r = new System.Random(515);
            var stone = new LowPolyMesh(1);
            var panel = Register(RuinKit.Panel("G_MuralPanel", 1f, 1f));
            int murals = 0;
            int count = Mathf.RoundToInt(Plaza.r * Mathf.PI * 2f / 6.5f);
            var spots = RuinKit.FindWall(Plaza.c, Plaza.r + 14f, count, 1.1f, Sdf, Height, (x, z) =>
                PathDist(x, z) < 6.5f || Vector2.Distance(new Vector2(x, z), Camp.c) < Camp.r + 2.5f
                || Mathf.Abs(x - GateX) < 10f && Mathf.Abs(z) < 16f);
            for (int i = 0; i < spots.Count; i++)
            {
                var a = spots[i];
                if (float.IsNaN(a.pos.x)) continue;
                float wallH = 4.6f + (float)r.NextDouble() * 1.6f;
                RuinKit.Pilaster(stone, 0, a.pos - a.inward * 0.1f, a.inward, wallH + 1.2f, 1.1f, r);
                // Фонарь у каждой второй пилястры: стены и фрески читаются в темноте
                if (i % 2 == 0) Lantern(root, a.pos + a.inward * 1.3f);
                var b = spots[(i + 1) % spots.Count];
                if (float.IsNaN(b.pos.x)) continue;
                float len = Vector2.Distance(new Vector2(a.pos.x, a.pos.z), new Vector2(b.pos.x, b.pos.z));
                if (len > 10f || len < 2f) continue;
                var inward = (a.inward + b.inward).normalized;
                var a2 = a.pos + (b.pos - a.pos).normalized * 0.6f;
                var b2 = b.pos - (b.pos - a.pos).normalized * 0.6f;
                RuinKit.Wall(stone, 0, a2 - inward * 0.25f, b2 - inward * 0.25f, inward, wallH, 0.8f, r, true);
                ruinSegs.Add(new Vector4(a.pos.x, a.pos.z, b.pos.x, b.pos.z));
                if (len > 4.5f && murals < 5 && r.NextDouble() < 0.75)
                {
                    var tex = TextureKit.Mural(TextureKit.MuralTheme.ElvenHistory, 21 + murals % 2);
                    var mat = RuinKit.MuralMaterial(MatDir + "/G_Mural_" + (murals % 2) + ".mat", tex, new Color(0.86f, 0.88f, 0.92f));
                    float w = Mathf.Min(len - 1.6f, 6f), h = Mathf.Min(w * 0.5f, 2.8f);
                    var mid = (a2 + b2) * 0.5f;
                    var go = new GameObject("Mural_" + murals);
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(mid.x, Mathf.Min(a.pos.y, b.pos.y) + 1.15f, mid.z) + inward * (0.4f - 0.25f + 0.09f);
                    go.transform.rotation = Quaternion.LookRotation(inward);
                    go.transform.localScale = new Vector3(w, h, 1f);
                    go.AddComponent<MeshFilter>().sharedMesh = panel;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    murals++;
                }
            }
            var sgo = Z.MeshObject("Masonry", root, Register(stone.ToMesh("G_Masonry")), new[] { mBlock }, true);
            GameObjectUtility.SetStaticEditorFlags(sgo, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Пучок шестигранных кристаллов, наклонённых от стены.</summary>
        static void CrystalCluster(LowPolyMesh lp, Vector3 p, Vector3 inward)
        {
            int n = rng.Next(3, 7);
            for (int i = 0; i < n; i++)
            {
                float h = R(0.5f, 2.2f) * (i == 0 ? 1.4f : 1f);
                float w = h * R(0.12f, 0.2f);
                var off = new Vector3(R(-0.6f, 0.6f), -0.15f, R(-0.6f, 0.6f));
                // Lathe растёт по локальному +y: наклоняем ось вверх и от стены
                var axis = (Vector3.up + inward * R(0.2f, 0.8f) + new Vector3(R(-0.4f, 0.4f), 0f, R(-0.4f, 0.4f))).normalized;
                var m = Matrix4x4.TRS(p + off, Quaternion.FromToRotation(Vector3.up, axis) * Quaternion.Euler(0f, R(0f, 60f), 0f), Vector3.one);
                lp.Lathe(0, m, new[] { 0f, h * 0.75f, h }, new[] { w, w, 0f }, 6);
            }
        }

        static void Pebbles(LowPolyMesh lp, Vector3 p)
        {
            int n = rng.Next(1, 4);
            for (int i = 0; i < n; i++)
            {
                float s = R(0.12f, 0.35f);
                var q = p + new Vector3(R(-0.8f, 0.8f), -s * 0.3f, R(-0.8f, 0.8f));
                var m = Matrix4x4.TRS(q, Quaternion.Euler(R(0f, 30f), R(0f, 360f), 0f), new Vector3(s, s * 0.6f, s * R(0.8f, 1.4f)));
                lp.Lathe(R01() < 0.5f ? 0 : 1, m, new[] { 0f, 0.6f, 1f }, new[] { 0.9f, 0.8f, 0.2f }, 5, rng, 0.25f);
            }
        }

        // ---------------------------------------------------------------- колонны древних эльфов

        static void BuildColumns(Transform world)
        {
            var lp = new LowPolyMesh(2);
            var fallen = new LowPolyMesh(2);
            float[] angles = { 30f, 62f, 150f, 205f, 238f, 275f, 310f, 335f };
            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i] * Mathf.Deg2Rad;
                var q = Plaza.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 15.5f;
                if (Sdf(q.x, q.y) > -1.5f) q = Plaza.c + (q - Plaza.c) * 0.88f;
                bool broken = i % 3 == 1;
                float full = CeilingH(q.x, q.y) - Height(q.x, q.y) + 0.5f;
                float h = broken ? R(2.5f, 5f) : Mathf.Min(full, 11f);
                Column(lp, Ground(q, -0.1f), h, broken);
                if (broken)
                {
                    // Упавшие барабаны колонны
                    var dir = new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)).normalized;
                    for (int k = 0; k < 3; k++)
                    {
                        var c = Ground(q + new Vector2(dir.x, dir.z) * (2.2f + k * 1.5f), 0.45f);
                        var m = Matrix4x4.TRS(c, Quaternion.LookRotation(dir) * Quaternion.Euler(90f, R(-15f, 15f), 0f), Vector3.one);
                        fallen.Lathe(0, m, new[] { -0.65f, 0.65f }, new[] { 0.52f, 0.52f }, 10, rng, 0.04f, true);
                    }
                }
            }
            var root = new GameObject("Columns").transform;
            root.SetParent(world, false);
            var go = Z.MeshObject("Columns", root, Register(lp.ToMesh("G_Columns")), new[] { mMarble, mRuneDim }, true);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            var fgo = Z.MeshObject("FallenDrums", root, Register(fallen.ToMesh("G_FallenDrums")), new[] { mMarble, mRuneDim }, true);
            GameObjectUtility.SetStaticEditorFlags(fgo, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Колонна с каннелюрами: база, ствол, капитель; у сломанной — рваный верх.</summary>
        static void Column(LowPolyMesh lp, Vector3 p, float h, bool broken)
        {
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
            lp.Box(0, m, new Vector3(0f, 0.3f, 0f), new Vector3(1.7f, 0.6f, 1.7f));
            lp.Lathe(0, m, new[] { 0.6f, 0.85f }, new[] { 0.75f, 0.6f }, 10);
            if (broken)
            {
                lp.Lathe(0, m, new[] { 0.85f, h * 0.7f, h }, new[] { 0.55f, 0.52f, 0.3f }, 10, rng, 0.12f);
            }
            else
            {
                lp.Lathe(0, m, new[] { 0.85f, h - 1.1f }, new[] { 0.55f, 0.48f }, 10, null, 0f, false, 0f);
                lp.Lathe(0, m, new[] { h - 1.1f, h - 0.6f, h - 0.4f }, new[] { 0.5f, 0.85f, 0.9f }, 10);
                lp.Box(0, m, new Vector3(0f, h - 0.2f, 0f), new Vector3(2f, 0.4f, 2f));
                // Светлая рунная полоса под капителью
                lp.Lathe(1, m, new[] { h - 1.5f, h - 1.38f }, new[] { 0.51f, 0.51f }, 10);
            }
        }

        // ---------------------------------------------------------------- ритуальный круг, трава в лунном свете

        static void BuildCircle(Transform world)
        {
            var root = new GameObject("RitualCircle").transform;
            root.SetParent(world, false);
            var c = Ground(CircleC);
            var lp = new LowPolyMesh(3);
            var m = Matrix4x4.TRS(c + Vector3.down * 0.25f, Quaternion.identity, Vector3.one);
            // Ступенчатая платформа из плит
            lp.Lathe(0, m, new[] { 0f, 0.35f }, new[] { CircleR + 0.6f, CircleR + 0.6f }, 28);
            lp.Lathe(1, m, new[] { 0.35f, 0.5f }, new[] { CircleR, CircleR }, 28);
            // Швы плит: тёмные полоски-лучи
            for (int i = 0; i < 12; i++)
            {
                var rm = m * Matrix4x4.TRS(new Vector3(0f, 0.5f, 0f), Quaternion.Euler(0f, i * 30f, 0f), Vector3.one);
                lp.Box(2, rm, new Vector3(0f, 0.005f, CircleR * 0.6f), new Vector3(0.06f, 0.02f, CircleR * 0.75f));
            }
            var go = Z.MeshObject("Platform", root, Register(lp.ToMesh("G_CirclePlatform")), new[] { mMarbleDark, mMarble, mRockDark }, true);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            // Светящиеся руны: два кольца с разрывами и знаки между ними
            var runes = new LowPolyMesh(1);
            var top = Matrix4x4.TRS(c + Vector3.up * 0.26f, Quaternion.identity, Vector3.one);
            for (int ring = 0; ring < 2; ring++)
            {
                float rr = ring == 0 ? CircleR - 0.6f : CircleR - 2.2f;
                int seg = ring == 0 ? 40 : 28;
                for (int i = 0; i < seg; i++)
                {
                    if (i % 7 == 3) continue;
                    float a = (i + 0.5f) * 360f / seg;
                    float len = 2f * Mathf.PI * rr / seg * 0.8f;
                    runes.Box(0, top * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, a, 0f), Vector3.one), new Vector3(0f, 0f, rr), new Vector3(len, 0.03f, 0.08f));
                }
            }
            for (int i = 0; i < 8; i++)
            {
                var sm = top * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, i * 45f + 22.5f, 0f), Vector3.one);
                float rr = CircleR - 1.4f;
                runes.Box(0, sm, new Vector3(0f, 0f, rr), new Vector3(0.06f, 0.03f, 0.6f));
                runes.Box(0, sm * Matrix4x4.TRS(new Vector3(0f, 0f, rr + 0.1f), Quaternion.Euler(0f, 45f, 0f), Vector3.one), Vector3.zero, new Vector3(0.3f, 0.03f, 0.06f));
                runes.Box(0, sm * Matrix4x4.TRS(new Vector3(0f, 0f, rr - 0.15f), Quaternion.Euler(0f, -45f, 0f), Vector3.one), Vector3.zero, new Vector3(0.3f, 0.03f, 0.06f));
            }
            var rgo = Z.MeshObject("Runes", root, Register(runes.ToMesh("G_CircleRunes")), new[] { mRune }, false);
            rgo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildMoonGarden(Transform world)
        {
            var root = new GameObject("MoonGarden").transform;
            root.SetParent(world, false);
            var grass = new LowPolyMesh(1);
            var flowers = new GlowPointsMesh();
            // Пятно лунного света — вокруг круга, плюс редкие пучки по всей площади
            for (int i = 0; i < 520; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                bool pool = i < 380;
                float rr = pool ? CircleR + 0.4f + Mathf.Pow(R01(), 1.6f) * 6f : R(0f, 19f);
                var q = (pool ? CircleC : Plaza.c) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (Sdf(q.x, q.y) > -1.2f || Vector2.Distance(q, CircleC) < CircleR + 0.4f || PathDist(q.x, q.y) < 1.2f && !pool) continue;
                if (!pool && (R01() < 0.8f || Vector2.Distance(q, Camp.c) < Camp.r)) continue;
                Tuft(grass, Ground(q, -0.05f), R(0.25f, pool ? 0.75f : 0.45f));
                if (pool && R01() < 0.22f)
                    flowers.Point(Ground(q, R(0.25f, 0.5f)), R(0.03f, 0.06f), new Color(0.6f, 1f, 0.95f) * R(0.6f, 1f), R(0f, 360f));
            }
            var go = Z.MeshObject("Grass", root, Register(grass.ToMesh("G_Grass")), new[] { mGrass }, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            var fl = new GameObject("MoonFlowers");
            fl.transform.SetParent(root, false);
            fl.AddComponent<MeshFilter>().sharedMesh = Register(flowers.ToMesh("G_MoonFlowers"));
            fl.AddComponent<MeshRenderer>().sharedMaterial = gFlowers;
        }

        /// <summary>Пучок травы: тонкие треугольные стебли, двусторонние.</summary>
        static void Tuft(LowPolyMesh lp, Vector3 p, float h)
        {
            int n = rng.Next(4, 8);
            for (int i = 0; i < n; i++)
            {
                float yaw = R(0f, 360f) * Mathf.Deg2Rad;
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var fwd = new Vector3(-side.z, 0f, side.x);
                var basePos = p + new Vector3(R(-0.15f, 0.15f), 0f, R(-0.15f, 0.15f));
                float bh = h * R(0.6f, 1.2f);
                float w = R(0.03f, 0.06f);
                var tip = basePos + Vector3.up * bh + fwd * R(-0.2f, 0.25f) * bh + side * R(-0.1f, 0.1f);
                var a = basePos - side * w;
                var b = basePos + side * w;
                // Одна грань, материал без отсечения; нормаль почти вверх — трава светится как земля под луной
                var bn = (Vector3.up + fwd * 0.35f).normalized;
                lp.TriSmooth(0, a, b, tip, bn, bn, bn);
            }
        }

        // ---------------------------------------------------------------- ворота

        static ElvenGateDoor BuildGate(Transform world)
        {
            var root = new GameObject("ElvenGate").transform;
            root.SetParent(world, false);
            float baseY = Apron.h - 0.05f;
            root.position = new Vector3(GateX, baseY, 0f);
            root.rotation = Quaternion.LookRotation(Vector3.left); // локальный +z смотрит на площадь, +x — на север
            var M = root.localToWorldMatrix;
            Matrix4x4 L(Vector3 pos, Vector3 euler) => Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one);

            var lp = new LowPolyMesh(4); // 0 мрамор, 1 тёмный мрамор, 2 серебро, 3 руны
            // Площадка под воротами и три ступени к площади (локальный +z)
            const float floorTop = 0.7f;
            lp.Box(1, Matrix4x4.identity, new Vector3(0f, 0.05f, 0.2f), new Vector3(15f, 1.3f, 5f));
            for (int k = 0; k < 3; k++)
            {
                float top = floorTop - (k + 1) * 0.22f;
                lp.Box(1, Matrix4x4.identity, new Vector3(0f, (top - 0.8f) * 0.5f, 2.7f + 0.55f + k * 1.1f), new Vector3(15f, top + 0.8f, 1.1f));
            }

            // Пилоны
            for (int s = -1; s <= 1; s += 2)
            {
                float px = s * (GateHalf + 1.3f);
                lp.Box(1, Matrix4x4.identity, new Vector3(px, floorTop + 0.6f, 0f), new Vector3(3.2f, 1.2f, 3.6f));
                lp.Box(0, Matrix4x4.identity, new Vector3(px, floorTop + 6.5f, 0f), new Vector3(2.5f, 11f, 3f));
                lp.Box(1, Matrix4x4.identity, new Vector3(px, floorTop + 12.2f, 0f), new Vector3(3f, 0.6f, 3.4f));
                // Шпиль
                lp.Lathe(0, L(new Vector3(px, floorTop + 12.5f, 0f), Vector3.zero), new[] { 0f, 1f, 3.2f }, new[] { 1.1f, 0.8f, 0f }, 4, null, 0f, false, 0.5f);
                lp.Lathe(3, L(new Vector3(px, floorTop + 13.1f, 0f), Vector3.zero), new[] { 0f, 0.5f, 1.3f }, new[] { 0.25f, 0.3f, 0f }, 4, null, 0f, true, 0.5f);
                // Рунная полоса и ромбы на пилоне
                lp.Box(3, Matrix4x4.identity, new Vector3(px, floorTop + 6.2f, 1.52f), new Vector3(0.12f, 8.4f, 0.05f));
                for (int k = 0; k < 6; k++)
                    lp.Box(3, L(new Vector3(px, floorTop + 2.8f + k * 1.35f, 1.53f), new Vector3(0f, 0f, 45f)), Vector3.zero, new Vector3(0.34f, 0.34f, 0.05f));
                // Боковые стены, уходящие в скалу
                for (int k = 0; k < 3; k++)
                {
                    float wx = s * (GateHalf + 4f + k * 2.6f);
                    float wh = 10.5f - k * 2.2f;
                    lp.Box(k == 0 ? 0 : 1, Matrix4x4.identity, new Vector3(wx, floorTop + wh * 0.5f, -0.4f), new Vector3(2.8f, wh, 2.6f));
                }
            }
            // Арка: стрельчатое обрамление проёма и стена над ним
            for (int s = -1; s <= 1; s += 2)
            {
                lp.Box(2, L(new Vector3(s * 2.1f, floorTop + GateHeight + 1.1f, 0.9f), new Vector3(0f, 0f, -s * 32f)), Vector3.zero, new Vector3(5.2f, 0.5f, 0.6f));
                lp.Box(3, L(new Vector3(s * 2.1f, floorTop + GateHeight + 1.1f, 1.22f), new Vector3(0f, 0f, -s * 32f)), Vector3.zero, new Vector3(4.6f, 0.08f, 0.05f));
            }
            lp.Box(0, Matrix4x4.identity, new Vector3(0f, floorTop + GateHeight + 2.4f, 0f), new Vector3(GateHalf * 2f + 0.4f, 4.8f, 2.6f));
            lp.Box(1, Matrix4x4.identity, new Vector3(0f, floorTop + GateHeight + 4.9f, 0f), new Vector3(14f, 0.6f, 3.2f));
            // Знак над вратами: Древо (ствол, ветви, кольцо)
            var crest = L(new Vector3(0f, floorTop + GateHeight + 2.6f, 1.33f), Vector3.zero);
            lp.Box(3, crest, new Vector3(0f, 0f, 0f), new Vector3(0.12f, 2.4f, 0.05f));
            for (int s = -1; s <= 1; s += 2)
            for (int k = 0; k < 3; k++)
                lp.Box(3, crest * L(new Vector3(s * 0.35f, -0.3f + k * 0.55f, 0f), new Vector3(0f, 0f, -s * 50f)), Vector3.zero, new Vector3(0.8f - k * 0.15f, 0.08f, 0.05f));
            for (int i = 0; i < 16; i++)
                lp.Box(3, crest * L(Vector3.zero, new Vector3(0f, 0f, i * 22.5f)), new Vector3(0f, 1.45f, 0f), new Vector3(0.52f, 0.08f, 0.05f));
            // Порог под проёмом
            lp.Box(2, Matrix4x4.identity, new Vector3(0f, floorTop + 0.05f, 0f), new Vector3(GateHalf * 2f, 0.12f, 1.4f));

            var frame = Z.MeshObject("Frame", root, Register(lp.ToMesh("G_GateFrame")), new[] { mMarble, mMarbleDark, mSilver, mRune }, true);
            GameObjectUtility.SetStaticEditorFlags(frame, StaticEditorFlags.BatchingStatic);

            // Створки на петлях
            Transform Door(string name, int side)
            {
                var hinge = new GameObject(name).transform;
                hinge.SetParent(root, false);
                hinge.localPosition = new Vector3(side * GateHalf, floorTop + 0.1f, 0.15f);
                var d = new LowPolyMesh(3); // 0 серебро, 1 тёмный металл, 2 руны
                float w = GateHalf, h = GateHeight;
                float cx = -side * w * 0.5f;
                d.Box(0, Matrix4x4.identity, new Vector3(cx, h * 0.5f, 0f), new Vector3(w, h, 0.45f));
                // Рама и филёнки
                d.Box(1, Matrix4x4.identity, new Vector3(cx, h * 0.5f, 0.24f), new Vector3(w - 0.5f, h - 0.5f, 0.05f));
                d.Box(0, Matrix4x4.identity, new Vector3(cx, h * 0.5f, 0.28f), new Vector3(w - 1.1f, h - 1.1f, 0.05f));
                for (int k = 0; k < 4; k++)
                    d.Box(1, Matrix4x4.identity, new Vector3(cx, 1.2f + k * 1.9f, 0.32f), new Vector3(w - 1.1f, 0.12f, 0.04f));
                // Половина древа: ствол у стыка створок, ветви наружу
                float inner = -side * (w - 0.18f);
                d.Box(2, Matrix4x4.identity, new Vector3(inner, h * 0.45f, 0.34f), new Vector3(0.1f, h * 0.7f, 0.04f));
                for (int k = 0; k < 5; k++)
                {
                    float y = 2f + k * 1.05f;
                    d.Box(2, Matrix4x4.TRS(new Vector3(inner - side * 0.7f, y + 0.35f, 0.34f), Quaternion.Euler(0f, 0f, side * 35f), Vector3.one), Vector3.zero, new Vector3(1.7f - k * 0.18f, 0.07f, 0.04f));
                }
                // Кольцо-ручка
                d.Lathe(1, Matrix4x4.TRS(new Vector3(inner - side * 0.6f, 3.6f, 0.42f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new[] { 0f, 0.08f }, new[] { 0.22f, 0.22f }, 8);
                // Тыльная сторона тоже отделана — видна, когда створки открыты
                d.Box(1, Matrix4x4.identity, new Vector3(cx, h * 0.5f, -0.24f), new Vector3(w - 0.5f, h - 0.5f, 0.05f));
                Z.MeshObject("Leaf", hinge, Register(d.ToMesh($"G_{name}")), new[] { mSilver, mIron, mRune }, true);
                return hinge;
            }
            var left = Door("DoorLeft", -1);
            var right = Door("DoorRight", 1);

            // Свет рун перед вратами; за вратами — тёплый закатный свет следующей зоны
            var rune = Z.PointLight(root, "RuneLight", M.MultiplyPoint3x4(new Vector3(0f, floorTop + 7f, 4f)), new Color(0.35f, 1f, 0.88f), 20f, 7f);
            rune.shadows = LightShadows.None;
            var beyond = Z.PointLight(root, "BeyondGlow", Ground(Beyond.c, 3.5f), new Color(1f, 0.62f, 0.42f), 16f, 9f);
            beyond.shadows = LightShadows.None;
            var veil = new GameObject("BeyondVeil");
            veil.transform.SetParent(root, false);
            veil.transform.position = Ground(new Vector2(Beyond.c.x + 4f, 0f), -0.2f);
            veil.transform.rotation = Quaternion.LookRotation(Vector3.right);
            veil.AddComponent<MeshFilter>().sharedMesh = Register(Z.GlowQuad("G_BeyondVeil", 8f, 7f, new Color(1f, 0.6f, 0.4f, 0.8f), new Color(1f, 0.6f, 0.4f, 0f)));
            veil.AddComponent<MeshRenderer>().sharedMaterial = gVeil;
            var block = veil.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, 3.5f, 0f);
            block.size = new Vector3(8f, 7f, 1f);

            var door = root.gameObject.AddComponent<ElvenGateDoor>();
            SetRef(door, "leftDoor", left);
            SetRef(door, "rightDoor", right);
            SetRef(door, "runeLight", rune);
            return door;
        }

        // ---------------------------------------------------------------- лагерь эльфов

        static void BuildCamp(Transform world)
        {
            var root = new GameObject("ElfCamp").transform;
            root.SetParent(world, false);
            var lp = new LowPolyMesh(6); // 0 ткань, 1 бирюзовая ткань, 2 тёмная ткань, 3 светлое дерево, 4 дерево, 5 серебро
            foreach (var t in TentPos) Tent(lp, Ground(t, -0.1f), Flat(Camp.c - t));

            // Скатки и ящики у палаток
            for (int i = 0; i < TentPos.Length; i++)
            {
                var t = TentPos[i];
                var dir = Flat(Camp.c - t);
                var side = Vector3.Cross(Vector3.up, dir);
                var p = Ground(t) + dir * 2.6f + side * 1.6f;
                var m = Matrix4x4.TRS(p, Quaternion.LookRotation(side), Vector3.one);
                lp.Lathe(i % 2 == 0 ? 1 : 2, m * Matrix4x4.TRS(new Vector3(0f, 0.18f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one), new[] { -0.45f, 0.45f }, new[] { 0.18f, 0.18f }, 7, null, 0f, true);
                var c = Ground(t) - side * 2.2f + dir * 1.2f;
                lp.Box(3, Matrix4x4.TRS(c, Quaternion.Euler(0f, R(0f, 90f), 0f), Vector3.one), new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.55f));
                lp.Box(5, Matrix4x4.TRS(c, Quaternion.Euler(0f, R(0f, 90f), 0f), Vector3.one), new Vector3(0f, 0.6f, 0f), new Vector3(0.74f, 0.05f, 0.59f));
            }

            // Стойка с луками
            var rackPos = Ground(new Vector2(-6f, 13.5f));
            var rack = Matrix4x4.TRS(rackPos, Quaternion.Euler(0f, 70f, 0f), Vector3.one);
            for (int s = -1; s <= 1; s += 2) lp.Box(4, rack, new Vector3(s * 0.9f, 0.8f, 0f), new Vector3(0.08f, 1.6f, 0.08f));
            lp.Box(4, rack, new Vector3(0f, 1.4f, 0f), new Vector3(1.9f, 0.07f, 0.07f));
            for (int k = 0; k < 3; k++)
            {
                var bow = rack * Matrix4x4.TRS(new Vector3(-0.55f + k * 0.55f, 0.85f, 0.06f), Quaternion.Euler(0f, 0f, 4f), Vector3.one);
                Bow(lp, bow, 3, 5);
            }

            // Знамёна
            foreach (var bp in new[] { new Vector2(-14f, 6.5f), new Vector2(-3.5f, 19.5f), new Vector2(-19.5f, 14.5f) })
            {
                var p = Ground(bp, -0.2f);
                var m = Matrix4x4.TRS(p, Quaternion.LookRotation(Flat(Plaza.c - bp)), Vector3.one);
                lp.Lathe(3, m, new[] { 0f, 5.4f }, new[] { 0.07f, 0.05f }, 5);
                lp.Box(5, m, new Vector3(0f, 5.1f, 0f), new Vector3(1.3f, 0.06f, 0.06f));
                lp.Box(1, m, new Vector3(0f, 3.6f, 0.06f), new Vector3(1.1f, 2.9f, 0.03f));
                lp.Lathe(1, m * Matrix4x4.TRS(new Vector3(0f, 2.15f, 0.06f), Quaternion.Euler(0f, 45f, 180f), Vector3.one), new[] { 0f, 0.55f }, new[] { 0.78f, 0f }, 4);
                lp.Box(5, m, new Vector3(0f, 4.1f, 0.09f), new Vector3(0.45f, 0.45f, 0.02f));
            }

            var go = Z.MeshObject("Props", root, Register(lp.ToMesh("G_CampProps")), new[] { mCloth, mClothTeal, mClothDark, mWoodPale, mWood, mSilver }, true);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            BuildBrazier(root);

            // Фонари с кристаллами: лагерь, проход к площади, ворота
            var lanternSpots = new[]
            {
                new Vector2(-6f, 7f), new Vector2(-17f, 5.5f), new Vector2(-12f, 20.5f), new Vector2(-2f, 13f),
                new Vector2(-24f, 4.5f), new Vector2(-24f, -4.5f), new Vector2(-35f, 2.6f), new Vector2(17f, 9f), new Vector2(17f, -9.5f),
            };
            foreach (var s in lanternSpots) Lantern(root, Ground(s));

            // Светлячки над лагерем
            var motes = new GameObject("Motes");
            motes.transform.SetParent(root, false);
            motes.transform.position = Ground(Camp.c, 2.5f);
            var ps = Z.Particles(motes, pMotes, 7f, 5f, 9f, 0.05f, 0.2f, 0.03f, 0.07f,
                new Color(0.4f, 1f, 0.9f), new Color(0.7f, 1f, 0.95f), -0.004f, 80, Z.FadeCurve(), 0.35f);
            Z.ShapeBox(ps, new Vector3(16f, 4f, 16f));
        }

        /// <summary>Эльфийский шатёр: восьмигранный конус, бирюзовый пояс, тёмный вход, шест с вымпелом.</summary>
        static void Tent(LowPolyMesh lp, Vector3 p, Vector3 front)
        {
            var m = Matrix4x4.TRS(p, Quaternion.LookRotation(front) * Quaternion.Euler(0f, 22.5f, 0f), Vector3.one);
            float H = R(3.2f, 3.8f), Rb = R(2f, 2.4f);
            lp.Lathe(0, m, new[] { 0f, 0.4f, H * 0.82f, H }, new[] { Rb, Rb * 0.95f, 0.22f, 0f }, 8);
            // Бирюзовый пояс поверх конуса: радиус ткани на этой высоте + 4 см
            float RadAt(float y) => Mathf.Lerp(Rb * 0.95f, 0.22f, (y - 0.4f) / (H * 0.82f - 0.4f)) + 0.04f;
            lp.Lathe(1, m, new[] { 0.95f, 1.25f }, new[] { RadAt(0.95f), RadAt(1.25f) }, 8);
            lp.Lathe(1, m, new[] { 0f, 0.4f }, new[] { Rb + 0.03f, Rb * 0.95f + 0.03f }, 8);
            var fm = Matrix4x4.TRS(p, Quaternion.LookRotation(front), Vector3.one);
            lp.Box(2, fm * Matrix4x4.TRS(new Vector3(0f, 0.85f, Rb * 0.82f), Quaternion.Euler(-28f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(1.1f, 1.7f, 0.05f));
            lp.Lathe(3, m, new[] { H - 0.1f, H + 0.9f }, new[] { 0.05f, 0.03f }, 4);
            lp.Box(1, m, new Vector3(0.35f, H + 0.7f, 0f), new Vector3(0.7f, 0.22f, 0.02f));
        }

        static void Bow(LowPolyMesh lp, Matrix4x4 m, int woodSub, int stringSub)
        {
            for (int k = -2; k <= 2; k++)
            {
                float y = k * 0.3f;
                float z = -Mathf.Abs(k) * 0.06f + 0.12f;
                lp.Box(woodSub, m * Matrix4x4.TRS(new Vector3(0f, y, z * 1f), Quaternion.Euler(k * 12f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(0.04f, 0.32f, 0.05f));
            }
            lp.Box(stringSub, m, new Vector3(0f, 0f, -0.02f), new Vector3(0.01f, 1.4f, 0.01f));
        }

        static void BuildBrazier(Transform parent)
        {
            var t = new GameObject("Brazier").transform;
            t.SetParent(parent, false);
            var p = Ground(BrazierPos);
            t.position = p;
            var lp = new LowPolyMesh(2);
            for (int i = 0; i < 3; i++)
                lp.Box(0, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, i * 120f, 0f), Vector3.one) * Matrix4x4.TRS(new Vector3(0f, 0.5f, 0.35f), Quaternion.Euler(-14f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(0.07f, 1.05f, 0.07f));
            lp.Lathe(1, Matrix4x4.TRS(new Vector3(0f, 0.9f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.2f, 0.32f }, new[] { 0.22f, 0.5f, 0.58f }, 8, null, 0f, true);
            Z.MeshObject("Mesh", t, Register(lp.ToMesh("G_Brazier")), new[] { mIron, mSilver }, true);

            var flame = p + Vector3.up * 1.25f;
            var l = Z.PointLight(t, "Light", flame + Vector3.up * 0.4f, new Color(1f, 0.66f, 0.42f), 14f, 14f);
            ShadowLight(l);
            var flick = l.gameObject.AddComponent<TorchFlicker>();
            SetFloat(flick, "baseIntensity", 14f);
            SetFloat(flick, "flickerAmount", 2.2f);

            var fire = new GameObject("Fire");
            fire.transform.SetParent(t, false);
            fire.transform.position = flame - Vector3.up * 0.1f;
            var fps = Z.Particles(fire, pFire, 50f, 0.4f, 0.75f, 0.5f, 1.3f, 0.35f, 0.7f,
                new Color(1f, 0.8f, 0.45f), new Color(1f, 0.45f, 0.12f), -0.1f, 60, Z.ShrinkCurve(), 0.2f);
            Z.ShapeCone(fps, 10f, 0.3f);
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(t, false);
            smoke.transform.position = flame + Vector3.up * 0.6f;
            var sps = Z.Particles(smoke, pSmoke, 7f, 3.5f, 5f, 0.4f, 0.7f, 0.9f, 1.5f,
                new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.85f, 0.9f, 0.7f), -0.015f, 40, Z.GrowCurve(), 0.4f);
            Z.ShapeCone(sps, 8f, 0.2f);
            var embers = new GameObject("Embers");
            embers.transform.SetParent(t, false);
            embers.transform.position = flame;
            var eps = Z.Particles(embers, pEmbers, 12f, 1f, 2.4f, 0.7f, 1.8f, 0.02f, 0.05f,
                new Color(1f, 0.75f, 0.35f), new Color(1f, 0.4f, 0.1f), -0.05f, 30, Z.FadeCurve(), 0.8f);
            Z.ShapeCone(eps, 18f, 0.2f);
        }

        static Mesh lanternMesh;

        /// <summary>Шест с подвешенным кристальным фонарём: бирюзовый свет без теней.</summary>
        static void Lantern(Transform parent, Vector3 pos)
        {
            if (lanternMesh == null || !generated.Contains(lanternMesh))
            {
                var lp = new LowPolyMesh(3);
                lp.Lathe(0, Matrix4x4.identity, new[] { -0.3f, 2.6f }, new[] { 0.06f, 0.045f }, 5);
                lp.Box(0, Matrix4x4.identity, new Vector3(0.3f, 2.5f, 0f), new Vector3(0.7f, 0.05f, 0.05f));
                lp.Lathe(1, Matrix4x4.TRS(new Vector3(0.55f, 2.1f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.06f, 0.32f, 0.38f }, new[] { 0.12f, 0.16f, 0.16f, 0.1f }, 6);
                lp.Lathe(2, Matrix4x4.TRS(new Vector3(0.55f, 2.14f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.12f, 0.24f }, new[] { 0.06f, 0.1f, 0f }, 6, null, 0f, true);
                lp.Lathe(1, Matrix4x4.TRS(new Vector3(0.55f, 2.48f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.02f }, new[] { 0.01f, 0.01f }, 3);
                lanternMesh = Register(lp.ToMesh("G_Lantern"));
            }
            var t = new GameObject("Lantern").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            Z.MeshObject("Mesh", t, lanternMesh, new[] { mWoodPale, mSilver, mRune }, false);
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.2f, 0f);
            col.radius = 0.12f;
            col.height = 2.6f;
            var glow = t.TransformPoint(new Vector3(0.55f, 2.25f, 0f));
            var l = Z.PointLight(t, "Light", glow, new Color(0.38f, 1f, 0.88f), 9f, 4.5f);
            l.shadows = LightShadows.None;
            var halo = new GameObject("Halo");
            halo.transform.SetParent(t, false);
            halo.transform.position = glow;
            var ps = Z.Particles(halo, pHalo, 4f, 1.2f, 1.6f, 0f, 0.02f, 0.9f, 1.3f,
                new Color(0.4f, 1f, 0.9f, 0.3f), new Color(0.4f, 1f, 0.9f, 0.24f), 0f, 8, Z.FlatCurve(), 0f);
            Z.ShapeSphere(ps, 0.05f);
        }

        // ---------------------------------------------------------------- серебряное дерево

        static void BuildTree(Transform world)
        {
            var root = new GameObject("SilverTree").transform;
            root.SetParent(world, false);
            var p = Ground(TreePos, -0.3f);
            root.position = p;
            var r = new System.Random(77);
            var wood = new LowPolyMesh(1);
            var leaves = new LowPolyMesh(1);
            var trunk = Matrix4x4.TRS(p, Quaternion.Euler(0f, 0f, 3f), Vector3.one);
            wood.Lathe(0, trunk, new[] { 0f, 0.6f, 2.4f, 4.2f, 5.2f }, new[] { 0.75f, 0.5f, 0.38f, 0.28f, 0.16f }, 7, r, 0.12f, false, 0.35f);
            // Корни
            for (int i = 0; i < 5; i++)
            {
                var m = trunk * Matrix4x4.TRS(new Vector3(0f, 0.3f, 0f), Quaternion.Euler(0f, i * 72f + Rr(r, -15f, 15f), 0f) * Quaternion.Euler(70f, 0f, 0f), Vector3.one);
                wood.Lathe(0, m, new[] { 0f, 1.6f }, new[] { 0.22f, 0.02f }, 5);
            }
            // Ветви и кроны
            for (int i = 0; i < 7; i++)
            {
                float y = Rr(r, 2.6f, 4.8f);
                var bm = trunk * Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.Euler(0f, i * 51f + Rr(r, -10f, 10f), 0f) * Quaternion.Euler(Rr(r, 40f, 65f), 0f, 0f), Vector3.one);
                float len = Rr(r, 1.8f, 3f);
                wood.Lathe(0, bm, new[] { 0f, len }, new[] { 0.14f, 0.05f }, 5);
                var tip = bm.MultiplyPoint3x4(new Vector3(0f, len, 0f));
                for (int k = 0; k < 3; k++)
                {
                    float s = Rr(r, 0.9f, 1.5f);
                    var lm = Matrix4x4.TRS(tip + new Vector3(Rr(r, -0.6f, 0.6f), Rr(r, -0.3f, 0.5f), Rr(r, -0.6f, 0.6f)), Quaternion.Euler(Rr(r, 0f, 30f), Rr(r, 0f, 360f), 0f), new Vector3(s, s * 0.7f, s));
                    leaves.Lathe(0, lm, new[] { -0.6f, -0.2f, 0.3f, 0.6f }, new[] { 0.3f, 0.9f, 0.75f, 0.1f }, 6, r, 0.25f);
                }
            }
            var top = trunk.MultiplyPoint3x4(new Vector3(0f, 5.4f, 0f));
            leaves.Lathe(0, Matrix4x4.TRS(top, Quaternion.identity, new Vector3(1.6f, 1.1f, 1.6f)), new[] { -0.6f, -0.2f, 0.3f, 0.7f }, new[] { 0.3f, 0.9f, 0.75f, 0.1f }, 7, r, 0.2f);

            var w = Z.MeshObject("Wood", root, Register(wood.ToMesh("G_TreeWood")), new[] { mBark }, true);
            GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic);
            var lv = Z.MeshObject("Leaves", root, Register(leaves.ToMesh("G_TreeLeaves")), new[] { mLeaf }, false);
            GameObjectUtility.SetStaticEditorFlags(lv, StaticEditorFlags.BatchingStatic);

            var l = Z.PointLight(root, "TreeGlow", top + Vector3.down * 1.2f, new Color(0.45f, 1f, 0.9f), 10f, 3f);
            l.shadows = LightShadows.None;
            // Падающие светящиеся листья
            var fall = new GameObject("FallingLeaves");
            fall.transform.SetParent(root, false);
            fall.transform.position = top;
            var ps = Z.Particles(fall, pMotes, 3f, 6f, 9f, 0.02f, 0.1f, 0.05f, 0.09f,
                new Color(0.55f, 1f, 0.92f), new Color(0.8f, 1f, 0.95f), 0.01f, 40, Z.FadeCurve(), 0.5f);
            Z.ShapeBox(ps, new Vector3(5f, 1f, 5f));
        }

        // ---------------------------------------------------------------- эльфы и старейшина

        static void BuildElves(Transform world, ElvenGateDoor gate)
        {
            var root = new GameObject("Elves").transform;
            root.SetParent(world, false);
            var elfMesh = Register(ElfMesh(false));
            var mats = new[] { mElfSkin, mElfCloak, mElfHair, mElfAccent, mWoodPale };
            // Лучники смотрят на Стража
            string[] names = { "Elf_Camp", "Elf_GateNorth", "Elf_GateSouth" };
            for (int i = 0; i < ElfPos.Length; i++)
            {
                var go = new GameObject(names[i]);
                go.transform.SetParent(root, false);
                go.transform.position = Ground(ElfPos[i]);
                go.transform.rotation = Quaternion.LookRotation(Flat(GuardianPos - ElfPos[i]));
                Z.MeshObject("Model", go.transform, elfMesh, mats, false);
                var col = go.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.95f, 0f);
                col.height = 1.9f;
                col.radius = 0.35f;
            }

            var elder = new GameObject("Elf_Elder");
            elder.transform.SetParent(root, false);
            elder.transform.position = Ground(ElderPos);
            elder.transform.rotation = Quaternion.LookRotation(Flat(new Vector2(-20f, 0f) - ElderPos));
            Z.MeshObject("Model", elder.transform, Register(ElfMesh(true)), new[] { mElfSkin, mElderRobe, mElfHair, mRune, mBark }, false);
            var ec = elder.AddComponent<CapsuleCollider>();
            ec.center = new Vector3(0f, 1f, 0f);
            ec.height = 2f;
            ec.radius = 0.45f;
            var npc = elder.AddComponent<ElderNpc>();
            SetRef(npc, "gate", gate);
            var staffGlow = Z.PointLight(elder.transform, "StaffGlow", elder.transform.TransformPoint(new Vector3(0.45f, 2.25f, 0.15f)), new Color(0.4f, 1f, 0.9f), 6f, 2.5f);
            staffGlow.shadows = LightShadows.None;
        }

        /// <summary>
        /// Эльф ~1.9 м, вперёд по +z: стройный, в плаще, серебряные волосы, острые уши, лук.
        /// elder — старейшина: длинная белая мантия, капюшон, посох с кристаллом.
        /// Подмеши: 0 кожа, 1 одежда, 2 волосы, 3 бирюзовый акцент, 4 дерево.
        /// </summary>
        static Mesh ElfMesh(bool elder)
        {
            var lp = new LowPolyMesh(5);
            const int skin = 0, cloth = 1, hair = 2, accent = 3, wood = 4;
            float s = elder ? 1.05f : 1f;
            Matrix4x4 T(float x, float y, float z, float ex = 0f, float ey = 0f, float ez = 0f) =>
                Matrix4x4.TRS(new Vector3(x, y, z) * s, Quaternion.Euler(ex, ey, ez), Vector3.one * s);

            if (elder)
            {
                // Мантия до земли
                lp.Lathe(cloth, T(0f, 0f, 0f), new[] { 0f, 0.6f, 1.25f, 1.5f }, new[] { 0.42f, 0.3f, 0.21f, 0.17f }, 8, null, 0f, true);
                lp.Lathe(accent, T(0f, 0f, 0f), new[] { 0.04f, 0.12f }, new[] { 0.43f, 0.415f }, 8);
                lp.Lathe(accent, T(0f, 0f, 0f), new[] { 0.95f, 1.0f }, new[] { 0.262f, 0.255f }, 8);
                // Наплечная накидка
                lp.Lathe(cloth, T(0f, 1.18f, -0.02f), new[] { 0f, 0.22f, 0.36f }, new[] { 0.34f, 0.27f, 0.12f }, 8);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    lp.Lathe(cloth, T(side * 0.1f, 0f, 0f), new[] { 0f, 0.5f, 0.95f }, new[] { 0.06f, 0.065f, 0.085f }, 6, null, 0f, true);
                    lp.Box(cloth, T(side * 0.1f, 0.04f, 0.05f), Vector3.zero, new Vector3(0.1f, 0.08f, 0.24f));
                }
                lp.Lathe(cloth, T(0f, 0.88f, 0f), new[] { 0f, 0.25f, 0.55f, 0.62f }, new[] { 0.16f, 0.15f, 0.19f, 0.1f }, 8, null, 0f, true);
                lp.Lathe(accent, T(0f, 0.95f, 0f), new[] { 0f, 0.05f }, new[] { 0.165f, 0.165f }, 8);
                // Плащ за спиной
                lp.Box(cloth, T(0f, 0.85f, -0.17f, -6f), Vector3.zero, new Vector3(0.42f, 0.75f, 0.03f));
                // Колчан
                lp.Lathe(wood, T(0.08f, 1.05f, -0.2f, -15f, 0f, -20f), new[] { 0f, 0.55f }, new[] { 0.06f, 0.07f }, 6, null, 0f, true);
                for (int k = 0; k < 3; k++)
                    lp.Box(accent, T(0.08f + k * 0.02f - 0.02f, 1.62f, -0.12f - k * 0.02f, -15f, 0f, -20f), Vector3.zero, new Vector3(0.015f, 0.12f, 0.04f));
            }
            // Шея и голова
            lp.Lathe(skin, T(0f, 1.48f, 0f), new[] { 0f, 0.1f }, new[] { 0.05f, 0.05f }, 6);
            var head = T(0f, 1.58f, 0.01f);
            lp.Lathe(skin, head, new[] { 0f, 0.06f, 0.17f, 0.24f }, new[] { 0.06f, 0.095f, 0.09f, 0.04f }, 7, null, 0f, true);
            // Острые уши
            for (int side = -1; side <= 1; side += 2)
                lp.Lathe(skin, head * Matrix4x4.TRS(new Vector3(side * 0.085f, 0.12f, -0.01f), Quaternion.Euler(-20f, 0f, -side * 62f), new Vector3(1f, 1f, 0.35f)), new[] { 0f, 0.16f }, new[] { 0.035f, 0f }, 4);
            // Волосы: шапка и длинный хвост по спине
            lp.Lathe(hair, head * Matrix4x4.TRS(new Vector3(0f, 0.1f, -0.025f), Quaternion.identity, new Vector3(1.08f, 1f, 1.1f)), new[] { 0f, 0.09f, 0.16f }, new[] { 0.1f, 0.098f, 0.04f }, 7);
            lp.Box(hair, head, new Vector3(0f, -0.1f, -0.09f), new Vector3(0.17f, elder ? 0.6f : 0.42f, 0.04f));
            if (elder)
            {
                // Капюшон, откинутый на плечи, и обруч с камнем
                lp.Lathe(cloth, head * Matrix4x4.TRS(new Vector3(0f, -0.12f, -0.1f), Quaternion.Euler(-30f, 0f, 0f), Vector3.one), new[] { 0f, 0.12f, 0.22f }, new[] { 0.16f, 0.14f, 0.05f }, 7);
                lp.Lathe(accent, head, new[] { 0.14f, 0.16f }, new[] { 0.1f, 0.1f }, 7);
                lp.Box(accent, head, new Vector3(0f, 0.15f, 0.1f), new Vector3(0.04f, 0.04f, 0.02f));
            }
            // Руки
            for (int side = -1; side <= 1; side += 2)
            {
                bool bowArm = !elder && side == -1;
                bool staffArm = elder && side == 1;
                var arm = T(side * 0.2f, 1.42f, 0f, bowArm ? -70f : staffArm ? -35f : 10f, 0f, side * (bowArm ? 8f : 10f) + 180f);
                lp.Lathe(cloth, arm, new[] { 0f, 0.3f, 0.52f }, new[] { 0.055f, 0.048f, 0.045f }, 5, null, 0f, true);
                lp.Lathe(skin, arm, new[] { 0.52f, 0.62f }, new[] { 0.04f, 0.035f }, 5, null, 0f, true);
                if (bowArm)
                {
                    var hand = arm * Matrix4x4.TRS(new Vector3(0f, 0.6f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                    Bow(lp, hand, wood, accent);
                }
                if (staffArm)
                {
                    // Посох стоит рядом, рука держит его на уровне груди
                    var st = T(0.45f, 0f, 0.15f);
                    lp.Lathe(wood, st, new[] { 0f, 1.9f, 2.05f }, new[] { 0.035f, 0.03f, 0.08f }, 5, null, 0f, true, 0.4f);
                    lp.Lathe(accent, st * Matrix4x4.TRS(new Vector3(0f, 2.05f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.12f, 0.32f }, new[] { 0.05f, 0.08f, 0f }, 5, null, 0f, true);
                }
            }
            return lp.ToMesh(elder ? "G_ElfElder" : "G_ElfArcher");
        }

        // ---------------------------------------------------------------- Страж

        static void BuildGuardian(Transform world)
        {
            var root = new GameObject("Enemies").transform;
            root.SetParent(world, false);
            var go = new GameObject("Guardian");
            go.transform.SetParent(root, false);
            go.transform.position = Ground(GuardianPos, 0.25f);
            go.transform.rotation = Quaternion.LookRotation(Flat(new Vector2(-20f, 0f) - GuardianPos));

            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 2.4f, 0f);
            col.height = 4.8f;
            col.radius = 1.1f;
            var health = go.AddComponent<Health>();
            SetFloat(health, "max", 450f);
            var enemy = go.AddComponent<EnemyController>();
            SetString(enemy, "enemyId", "gate_guardian");
            SetString(enemy, "narrationKeyOnSeen", "gate.guardian_appears");
            var so = new SerializedObject(enemy);
            so.FindProperty("canBeSpared").boolValue = false; // Страж — создание богини, пощады нет (N15)
            so.FindProperty("detectRange").floatValue = 13f;
            so.FindProperty("loseRange").floatValue = 30f;
            so.FindProperty("attackRange").floatValue = 3.2f;
            so.FindProperty("moveSpeed").floatValue = 2.3f;
            so.FindProperty("attackDamage").floatValue = 18f;
            so.FindProperty("attackWindup").floatValue = 0.75f;
            so.FindProperty("attackCooldown").floatValue = 2.1f;
            so.FindProperty("truthFlashChance").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<HitFlash>();

            var model = Z.MeshObject("Model", go.transform, Register(GuardianMesh()), new[] { mShadow, mGuardLight, mGuardEyes }, false);
            model.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

            var glow = Z.PointLight(go.transform, "Glow", go.transform.TransformPoint(new Vector3(0f, 3.2f, 0.9f)), new Color(1f, 0.78f, 0.48f), 11f, 7f);
            glow.shadows = LightShadows.None;

            // Тёмный дым с тела и золотые искры
            var smoke = new GameObject("ShadowSmoke");
            smoke.transform.SetParent(go.transform, false);
            smoke.transform.localPosition = new Vector3(0f, 2.4f, -0.3f);
            var ps = Z.Particles(smoke, pShadowSmoke, 16f, 1.6f, 2.6f, 0.2f, 0.6f, 0.8f, 1.6f,
                new Color(1f, 1f, 1f, 0.8f), new Color(0.6f, 0.6f, 0.7f, 0.6f), -0.06f, 60, Z.GrowCurve(), 0.6f);
            Z.ShapeBox(ps, new Vector3(1.6f, 3.4f, 1.2f));
            var gold = new GameObject("GoldMotes");
            gold.transform.SetParent(go.transform, false);
            gold.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            var gps = Z.Particles(gold, pGold, 14f, 1.5f, 3f, 0.1f, 0.5f, 0.03f, 0.07f,
                new Color(1f, 0.85f, 0.5f), new Color(1f, 0.7f, 0.35f), -0.03f, 60, Z.FadeCurve(), 0.7f);
            Z.ShapeSphere(gps, 1.8f);
        }

        /// <summary>
        /// Страж ~5 м, вперёд по +z: сгорбленный великан из тени с трещинами золотого света,
        /// корона из лучей, нимб за головой, длинные руки с когтями из света.
        /// Подмеши: 0 тень, 1 свет, 2 глаза.
        /// </summary>
        static Mesh GuardianMesh()
        {
            var lp = new LowPolyMesh(3);
            var r = new System.Random(5);
            const int dark = 0, light = 1, eyes = 2;
            // Ноги: бедро и голень
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = Matrix4x4.TRS(new Vector3(s * 0.55f, 2.2f, 0f), Quaternion.Euler(160f, 0f, s * 6f), Vector3.one);
                lp.Lathe(dark, hip, new[] { 0f, 0.6f, 1.15f }, new[] { 0.42f, 0.36f, 0.24f }, 7, r, 0.1f);
                var knee = hip.MultiplyPoint3x4(new Vector3(0f, 1.15f, 0f));
                var shin = Matrix4x4.TRS(knee, Quaternion.Euler(195f, 0f, -s * 3f), Vector3.one);
                lp.Lathe(dark, shin, new[] { 0f, 0.6f, 1.15f }, new[] { 0.26f, 0.2f, 0.28f }, 6, r, 0.1f, true);
                lp.Box(light, shin, new Vector3(0f, 0.55f, 0.2f), new Vector3(0.05f, 0.7f, 0.05f));
            }
            // Сгорбленный торс с широкой грудью
            var torso = Matrix4x4.TRS(new Vector3(0f, 2.1f, 0f), Quaternion.Euler(18f, 0f, 0f), Vector3.one);
            lp.Lathe(dark, torso, new[] { 0f, 0.5f, 1.3f, 1.9f, 2.25f }, new[] { 0.7f, 0.85f, 1.2f, 1.05f, 0.5f }, 8, r, 0.12f, true);
            // Трещины света на груди: шов и «рёбра»
            lp.Box(light, torso, new Vector3(0f, 1.25f, 1.08f), new Vector3(0.08f, 1.3f, 0.06f));
            for (int k = 0; k < 4; k++)
            for (int s = -1; s <= 1; s += 2)
                lp.Box(light, torso * Matrix4x4.TRS(new Vector3(s * 0.4f, 0.85f + k * 0.28f, 0.98f - k * 0.02f), Quaternion.Euler(0f, s * 25f, s * (20f - k * 8f)), Vector3.one), Vector3.zero, new Vector3(0.55f, 0.05f, 0.05f));
            lp.Lathe(light, torso * Matrix4x4.TRS(new Vector3(0f, 1.3f, 1.08f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new[] { 0f, 0.06f }, new[] { 0.22f, 0.15f }, 6);
            // Шипы на спине
            for (int k = 0; k < 5; k++)
                lp.Lathe(dark, torso * Matrix4x4.TRS(new Vector3(0f, 0.7f + k * 0.32f, -0.85f + k * 0.04f), Quaternion.Euler(-60f, 0f, 0f), Vector3.one), new[] { 0f, 0.5f + k * 0.08f }, new[] { 0.14f, 0f }, 4);
            // Голова, вынесенная вперёд
            var neckTop = torso.MultiplyPoint3x4(new Vector3(0f, 2.25f, 0.35f));
            var head = Matrix4x4.TRS(neckTop + new Vector3(0f, 0.05f, 0.25f), Quaternion.Euler(10f, 0f, 0f), new Vector3(1f, 1f, 1.25f));
            lp.Lathe(dark, head, new[] { -0.2f, 0f, 0.3f, 0.48f }, new[] { 0.25f, 0.36f, 0.3f, 0.1f }, 7, r, 0.1f, true);
            for (int s = -1; s <= 1; s += 2)
                lp.Box(eyes, head, new Vector3(s * 0.13f, 0.1f, 0.33f), new Vector3(0.12f, 0.035f, 0.04f));
            // Корона из лучей
            for (int k = -3; k <= 3; k++)
            {
                var ray = head * Matrix4x4.TRS(new Vector3(k * 0.08f, 0.35f, -0.05f), Quaternion.Euler(-15f, 0f, -k * 16f), Vector3.one);
                lp.Lathe(light, ray, new[] { 0f, 0.7f + (3 - Mathf.Abs(k)) * 0.2f }, new[] { 0.05f, 0f }, 4);
            }
            // Нимб за головой (вертикальное кольцо)
            var haloC = head.MultiplyPoint3x4(new Vector3(0f, 0.3f, -0.55f));
            for (int i = 0; i < 24; i++)
            {
                if (i % 6 == 5) continue; // разрывы — «сломанный» нимб
                float a = i * 15f;
                var hm = Matrix4x4.TRS(haloC, Quaternion.Euler(0f, 0f, a), Vector3.one);
                lp.Box(light, hm, new Vector3(0f, 1.35f, 0f), new Vector3(0.33f, 0.07f, 0.07f));
            }
            // Длинные руки до колен и когти света
            for (int s = -1; s <= 1; s += 2)
            {
                var shoulder = torso.MultiplyPoint3x4(new Vector3(s * 1.05f, 1.85f, 0.1f));
                var upper = Matrix4x4.TRS(shoulder, Quaternion.Euler(160f, 0f, s * 18f), Vector3.one);
                lp.Lathe(dark, upper, new[] { -0.2f, 0.2f, 1.3f }, new[] { 0.42f, 0.34f, 0.24f }, 7, r, 0.12f);
                var elbow = upper.MultiplyPoint3x4(new Vector3(0f, 1.3f, 0f));
                var fore = Matrix4x4.TRS(elbow, Quaternion.Euler(205f, 0f, s * 8f), Vector3.one);
                lp.Lathe(dark, fore, new[] { 0f, 1.1f, 1.3f }, new[] { 0.22f, 0.18f, 0.24f }, 6, r, 0.12f, true);
                lp.Box(light, fore, new Vector3(0f, 0.6f, 0.2f), new Vector3(0.04f, 0.8f, 0.04f));
                for (int c = -1; c <= 1; c++)
                {
                    var claw = fore * Matrix4x4.TRS(new Vector3(c * 0.12f, 1.3f, 0.06f), Quaternion.Euler(20f, 0f, c * 12f), Vector3.one);
                    lp.Lathe(light, claw, new[] { 0f, 0.75f }, new[] { 0.05f, 0f }, 4);
                }
            }
            // Рваный теневой плащ сзади
            var cape = Matrix4x4.TRS(torso.MultiplyPoint3x4(new Vector3(0f, 1.9f, -0.5f)), Quaternion.Euler(170f, 0f, 0f), new Vector3(1f, 1f, 0.55f));
            lp.Lathe(dark, cape, new[] { 0f, 1.2f, 2.6f, 3.4f }, new[] { 0.6f, 1.0f, 1.3f, 1.4f }, 7, r, 0.3f);
            return lp.ToMesh("G_Guardian");
        }

        // ---------------------------------------------------------------- игрок, луна, атмосфера

        static Camera BuildPlayer()
        {
            var cam = CreatePlayer(Ground(SpawnPos, 1.2f));
            var player = GameObject.FindWithTag("Player");
            player.transform.rotation = Quaternion.LookRotation(Vector3.right);

            // Око выдаёт старейшина после победы над Стражем
            var eyeSo = new SerializedObject(player.GetComponent<EyeOfTruth>());
            eyeSo.FindProperty("unlocked").boolValue = false;
            eyeSo.ApplyModifiedPropertiesWithoutUndo();

            var spawn = new GameObject("Checkpoint_CP2").transform;
            spawn.position = Ground(SpawnPos, 1.2f);
            spawn.rotation = player.transform.rotation;
            var fall = player.AddComponent<FallRespawn>();
            var so = new SerializedObject(fall);
            var arr = so.FindProperty("safePoints");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = spawn;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Фонарь героя: слабый нейтральный, в гроте хватает луны и кристаллов
            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(cam.transform, false);
            lantern.transform.localPosition = new Vector3(0f, 0.5f, -0.7f);
            lantern.type = LightType.Point;
            lantern.range = 8f;
            lantern.intensity = 0.8f;
            lantern.color = new Color(0.95f, 0.88f, 0.8f);
            lantern.shadows = LightShadows.None;

            var dust = new GameObject("Dust");
            dust.transform.SetParent(player.transform, false);
            dust.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var ps = Z.Particles(dust, pDust, 26f, 6f, 10f, 0.02f, 0.12f, 0.015f, 0.035f,
                new Color(0.8f, 0.9f, 1f, 0.6f), new Color(0.7f, 0.95f, 1f, 0.4f), -0.003f, 360, Z.FadeCurve(), 0.25f);
            Z.ShapeBox(ps, new Vector3(22f, 6f, 22f));

            cam.transform.parent.gameObject.AddComponent<HeadBob>();
            var hand = new GameObject("ViewModel").transform;
            hand.SetParent(cam.transform, false);
            hand.localPosition = new Vector3(0.17f, -0.15f, 0.36f);
            hand.localRotation = Quaternion.Euler(12f, -6f, 10f);
            hand.localScale = Vector3.one * 0.55f;
            var vm = Z.MeshObject("Mesh", hand, Register(Z.HandMesh()), new[] { mSkin, mSleeve, mWoodDark, mSteel }, false);
            var vmr = vm.GetComponent<MeshRenderer>();
            vmr.shadowCastingMode = ShadowCastingMode.Off;
            vmr.receiveShadows = false;
            hand.gameObject.AddComponent<ViewModel>();

            new GameObject("HUD").AddComponent<GameHUD>();
            return cam;
        }

        /// <summary>Луна и звёзды за провалом: свечение без тумана (UnlitGlow), видно только через дыру в своде.</summary>
        static void BuildMoon()
        {
            var root = new GameObject("NightSky").transform;
            var fwd = MoonRot * Vector3.forward;
            var holeTop = new Vector3(HoleC.x, CeilingH(HoleC.x, HoleC.y) + 2f, HoleC.y);
            var moonPos = holeTop - fwd * 75f;

            var moon = new GameObject("Moon");
            moon.transform.SetParent(root, false);
            moon.transform.position = moonPos;
            moon.transform.rotation = Quaternion.LookRotation(fwd);
            moon.AddComponent<MeshFilter>().sharedMesh = Register(DiscMesh("G_MoonDisc", 4.2f, 9.5f, 24));
            moon.AddComponent<MeshRenderer>().sharedMaterial = gMoon;

            var stars = new GlowPointsMesh();
            var r = new System.Random(3);
            for (int i = 0; i < 260; i++)
            {
                // Звёзды на полусфере над провалом, в пределах видимого конуса
                var dir = new Vector3(Rr(r, -1f, 1f), Rr(r, 0.35f, 1f), Rr(r, -1f, 1f)).normalized;
                var p = holeTop + dir * Rr(r, 60f, 90f);
                float b = Mathf.Pow(Rr(r, 0f, 1f), 3f);
                stars.Point(p, 0.12f + b * 0.28f, new Color(0.75f, 0.85f, 1f) * (0.35f + b), Rr(r, 0f, 360f));
            }
            var sg = new GameObject("Stars");
            sg.transform.SetParent(root, false);
            sg.AddComponent<MeshFilter>().sharedMesh = Register(stars.ToMesh("G_Stars"));
            sg.AddComponent<MeshRenderer>().sharedMaterial = gStars;
        }

        /// <summary>Диск с мягким ореолом: центр непрозрачный, к краю ореола альфа → 0.</summary>
        static Mesh DiscMesh(string name, float r, float haloR, int seg)
        {
            var v = new List<Vector3> { Vector3.zero };
            var c = new List<Color> { Color.white };
            var t = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v.Add(d * r); c.Add(new Color(1f, 1f, 1f, 1f));
                v.Add(d * (r * 1.05f)); c.Add(new Color(0.8f, 0.88f, 1f, 0.35f));
                v.Add(d * haloR); c.Add(new Color(0.5f, 0.6f, 0.9f, 0f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = 1 + i * 3, b0 = 1 + ((i + 1) % seg) * 3;
                t.Add(0); t.Add(b0); t.Add(a0);
                for (int k = 0; k < 2; k++)
                {
                    int a = a0 + k, b = b0 + k, a1 = a + 1, b1 = b + 1;
                    t.Add(a); t.Add(b); t.Add(b1);
                    t.Add(a); t.Add(b1); t.Add(a1);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void BuildAtmosphere(Camera cam)
        {
            var fog = new Color(0.03f, 0.05f, 0.07f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.19f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.12f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.07f, 0.075f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = fog;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.03f, 0.06f);
            cam.farClipPlane = 250f;

            // Луна: единственный направленный свет, тени считает свод — светлое пятно только под провалом
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = MoonRot;
            moon.color = new Color(0.66f, 0.78f, 1f);
            moon.intensity = 2.1f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 1f;
            moon.shadowBias = 0.06f;
            moon.shadowNormalBias = 0.5f;
            RenderSettings.sun = moon;

            // Отражённый от пятна света холодный подсвет и общий заполняющий свет площади
            var fill = new GameObject("BounceLights").transform;
            var pool = Ground(CircleC, 1.2f);
            Z.PointLight(fill, "PoolBounce", pool, new Color(0.62f, 0.74f, 1f), 16f, 7f).shadows = LightShadows.None;
            Z.PointLight(fill, "PlazaFill", Ground(Plaza.c, 9f), new Color(0.56f, 0.62f, 0.76f), 38f, 34f).shadows = LightShadows.None;
            Z.PointLight(fill, "TunnelFill", Ground(new Vector2(-30f, 0f), 3f), new Color(0.5f, 0.58f, 0.8f), 20f, 8f).shadows = LightShadows.None;

            // Лунный луч: скрещённые плоскости вдоль направления света + пыль
            var fwd = MoonRot * Vector3.forward;
            var holeY = CeilingH(HoleC.x, HoleC.y);
            var shaft = new GameObject("MoonShaft").transform;
            shaft.position = new Vector3(HoleC.x, holeY, HoleC.y);
            shaft.rotation = Quaternion.FromToRotation(Vector3.down, fwd);
            float len = (holeY - Height(CircleC.x, CircleC.y)) / Mathf.Max(0.3f, -fwd.y);
            var beam = new GameObject("Beam");
            beam.transform.SetParent(shaft, false);
            beam.AddComponent<MeshFilter>().sharedMesh = Register(Z.ShaftMesh("G_MoonShaft", HoleR * 0.85f, HoleR * 1.05f, len, 7, 0.16f));
            var mr = beam.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gShaft;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var dust = new GameObject("Dust");
            dust.transform.SetParent(shaft, false);
            dust.transform.localPosition = Vector3.down * len * 0.5f;
            var ps = Z.Particles(dust, pDust, 26f, 5f, 8f, 0.02f, 0.08f, 0.02f, 0.05f,
                new Color(0.85f, 0.92f, 1f, 0.85f), new Color(0.9f, 0.97f, 1f, 0.6f), -0.002f, 260, Z.FadeCurve(), 0.15f);
            Z.ShapeBox(ps, new Vector3(HoleR * 1.6f, len * 0.85f, HoleR * 1.6f));

            // Слоистая дымка: холодная на площади, бирюзовая в лагере, светящаяся в лунном луче
            var haze = new GameObject("Haze").transform;
            RuinKit.Haze(haze, "PlazaHaze", Ground(Plaza.c, 2.8f), new Vector3(34f, 4.5f, 34f), pHaze, new Color(0.32f, 0.37f, 0.44f, 0.05f), 26, 7f, 12f);
            RuinKit.Haze(haze, "CampHaze", Ground(Camp.c, 2.2f), new Vector3(14f, 3.5f, 14f), pHaze, new Color(0.26f, 0.44f, 0.44f, 0.05f), 10, 5f, 9f);
            RuinKit.Haze(haze, "TunnelHaze", Ground(new Vector2(-32f, 0f), 2f), new Vector3(20f, 3f, 6f), pHaze, new Color(0.22f, 0.26f, 0.32f, 0.05f), 8, 4f, 7f);
            RuinKit.Haze(haze, "MoonHaze", Ground(CircleC, 3.5f), new Vector3(11f, 6f, 11f), pDust, new Color(0.55f, 0.66f, 0.85f, 0.035f), 14, 6f, 10f);
            RuinKit.Probe(haze, Ground(Plaza.c, 4f), new Vector3(44f, 20f, 44f), 1f);
            RuinKit.Probe(haze, Ground(new Vector2(GateX - 4f, 0f), 4f), new Vector3(16f, 16f, 20f), 1f);

            // В игре тени только у ближайших точечных источников
            var budget = new GameObject("ShadowBudget").AddComponent<ShadowBudget>();
            var bso = new SerializedObject(budget);
            var cands = bso.FindProperty("candidates");
            cands.arraySize = shadowLights.Count;
            for (int i = 0; i < shadowLights.Count; i++) cands.GetArrayElementAtIndex(i).objectReferenceValue = shadowLights[i];
            bso.ApplyModifiedPropertiesWithoutUndo();

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
            DynamicGI.UpdateEnvironment();
        }

        static VolumeProfile PostProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostPath);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(0.85f, 0.95f, 1f));
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(1.1f);
            color.contrast.Override(20f);
            color.saturation.Override(-6f);
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.36f, 0.5f, 0.58f));
            split.highlights.Override(new Color(0.55f, 0.58f, 0.64f));
            split.balance.Override(-20f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.9f, 0.98f, 1.08f, -0.03f));
            smh.highlights.Override(new Vector4(0.98f, 1f, 1.04f, 0f));
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-6f);
            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.96f, 0.99f, 1.05f, -0.01f));
            lgg.gain.Override(new Vector4(1.01f, 1f, 0.98f, 0f));
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.85f);
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(30f);
            dof.gaussianEnd.Override(80f);
            dof.gaussianMaxRadius.Override(0.8f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static void BuildNarration(Transform world)
        {
            var root = new GameObject("Narration").transform;
            root.SetParent(world, false);
            NarrationZone(root, "gate.enter", Ground(SpawnPos, 1f), 3f, true);
            // Выход к деревне огров — SceneFlow (N11) повесит сюда переход
            var exit = new GameObject("ZoneExit_ToOgreVillage");
            exit.transform.SetParent(root, false);
            exit.transform.position = Ground(Beyond.c, 1f);
            var sc = exit.AddComponent<BoxCollider>();
            sc.isTrigger = true;
            sc.size = new Vector3(3f, 4f, 8f);
        }

        static void NarrationZone(Transform parent, string key, Vector3 pos, float radius, bool onStart)
        {
            var go = new GameObject($"Narration_{key}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = radius;
            var nt = go.AddComponent<NarrationTrigger>();
            SetString(nt, "lineKey", key);
            var so = new SerializedObject(nt);
            so.FindProperty("raiseOnStart").boolValue = onStart;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- утилиты

        static float R01() => (float)rng.NextDouble();
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static float Rr(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static Vector3 Flat(Vector2 v) => v.sqrMagnitude > 1e-6f ? new Vector3(v.x, 0f, v.y).normalized : Vector3.forward;

        static Mesh Register(Mesh m)
        {
            generated.Add(m);
            return m;
        }

        static void SaveMeshes()
        {
            if (generated.Count == 0) return;
            AssetDatabase.CreateAsset(generated[0], MeshPath);
            for (int i = 1; i < generated.Count; i++) AssetDatabase.AddObjectToAsset(generated[i], generated[0]);
            AssetDatabase.SaveAssets();
        }

        static void ShadowLight(Light l)
        {
            l.shadows = LightShadows.Soft;
            if (!shadowLights.Contains(l)) shadowLights.Add(l);
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            var data = l.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = l.gameObject.AddComponent<UniversalAdditionalLightData>();
            var so = new SerializedObject(data);
            var tier = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
            if (tier != null) { tier.intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
