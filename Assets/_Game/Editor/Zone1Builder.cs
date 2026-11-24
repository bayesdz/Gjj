using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static FalseParadise.EditorTools.SandboxBuilder;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Меню False Paradise → Build Zone 1 (Goblin Lair).
    /// Собирает зону 1 целиком из кода в стиле low-poly: тёплая пещера из песчаника со сводом
    /// и сталактитами, колонны, реквизит гоблинов (заборы, лестницы, колья, ящики, бочки, кости),
    /// факелы с тенями, дымом и искрами, пропасть с мостом,
    /// лагерь гоблинов (иллюзия/правда), клетку Сияющего, выход к Вратам эльфов, троих гоблинов,
    /// свет, туман и постобработку. Повторный запуск пересоздаёт всё с нуля.
    ///
    /// Маршрут (метры, x — восток, z — север):
    ///   Расщелина (0,0) → Зал первого боя (8,48) → Логово-нора (-18,100) [тупик с рисунком (-54,110)]
    ///   → Мост через пропасть (4,125) → Зал с клеткой (30,155) → Выход к вратам (58,200).
    /// Схема: docs/levels/LEVEL_MAP.png.
    /// </summary>
    public static class Zone1Builder
    {
        const string ScenePath = "Assets/_Game/Scenes/Zone1_Lair.unity";
        const string ArtDir = "Assets/_Game/Art/Zone1";
        const string MatDir = ArtDir + "/Materials";
        const string MeshPath = ArtDir + "/Zone1_Meshes.asset";
        const string PostPath = ArtDir + "/Zone1_Post.asset";
        const int Seed = 1337;

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

        static readonly Room Entrance = new Room(0f, 0f, 11f, 0f);
        static readonly Room FirstFight = new Room(8f, 48f, 20f, -1f);
        static readonly Room Warren = new Room(-18f, 100f, 22f, -2f);
        static readonly Room Nook = new Room(-54f, 110f, 8f, -1.5f);
        static readonly Room BridgeHall = new Room(4f, 125f, 10f, -2.5f);
        static readonly Room CageHall = new Room(30f, 155f, 26f, -3f);
        static readonly Room ExitRoom = new Room(58f, 200f, 7f, -2f);

        static readonly Room[] Rooms = { Entrance, FirstFight, Warren, Nook, BridgeHall, CageHall, ExitRoom };

        static readonly Corr[] Corridors =
        {
            new Corr(0f, 0f, -5f, 22f, 5f),
            new Corr(-5f, 22f, 8f, 48f, 5.5f),
            new Corr(8f, 48f, -8f, 76f, 5f),
            new Corr(-8f, 76f, -18f, 100f, 5.5f),
            new Corr(-18f, 100f, -54f, 110f, 4f),
            new Corr(-18f, 100f, 4f, 125f, 5f),
            new Corr(4f, 125f, 30f, 155f, 5f),
            new Corr(30f, 155f, 50f, 186f, 4.5f),
            new Corr(50f, 186f, 58f, 200f, 4.5f),
        };

        // Пропасть поперёк пути между логовом и залом с клеткой
        static readonly Vector2 ChasmA = new Vector2(-11f, 138.2f);
        static readonly Vector2 ChasmB = new Vector2(19f, 111.8f);
        const float ChasmR = 5f;
        const float ChasmDepth = -34f;

        // Сетка рельефа
        const float MinX = -90f, MaxX = 110f, MinZ = -40f, MaxZ = 240f;
        const float Cell = 2f;
        const int ChunkCells = 20;

        // Точки интереса
        static readonly Vector2 SpawnPos = new Vector2(0f, -4f);
        static readonly Vector2 Goblin1Pos = new Vector2(11f, 56f);
        static readonly Vector2 Goblin2Pos = new Vector2(-20f, 104f);
        static readonly Vector2 Goblin3Pos = new Vector2(24f, 150f);
        static readonly Vector2 CagePos = new Vector2(40f, 156f);
        static readonly Vector2 FirePos = new Vector2(-26f, 94f);
        static readonly Vector2 BurrowPos = new Vector2(-16f, 120f);
        static readonly Vector2 ToyPos = new Vector2(-13f, 116f);
        static readonly Vector2 DrawingPos = new Vector2(-58f, 112f);
        static readonly Vector2 ExitArchPos = new Vector2(60.5f, 204f);

        // ---------------------------------------------------------------- поля высот

        static float Noise(float x, float z, float freq, float offset)
        {
            return Mathf.PerlinNoise(x * freq + offset, z * freq + offset * 1.37f + 17.3f);
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        /// <summary>Расстояние до проходимой области: &lt; 0 внутри пещеры, &gt; 0 в скале.</summary>
        static float Sdf(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            foreach (var r in Rooms) d = Mathf.Min(d, Vector2.Distance(p, r.c) - r.r);
            foreach (var c in Corridors) d = Mathf.Min(d, SegDist(p, c.a, c.b) - c.r);
            d += (Noise(x, z, 0.07f, 100f) - 0.5f) * 4f;
            d += (Noise(x, z, 0.25f, 300f) - 0.5f) * 1.2f;
            return d;
        }

        static float PathDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            foreach (var c in Corridors) d = Mathf.Min(d, SegDist(p, c.a, c.b));
            return d;
        }

        static float ChasmSdf(float x, float z)
        {
            return SegDist(new Vector2(x, z), ChasmA, ChasmB) - ChasmR + (Noise(x, z, 0.18f, 1500f) - 0.5f) * 2f;
        }

        /// <summary>Высота пола без скал: плавно между залами + мягкие волны.</summary>
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
            return sh / sw + (Noise(x, z, 0.05f, 500f) - 0.5f) * 1.2f;
        }

        internal static float Height(float x, float z)
        {
            float h = FloorH(x, z) + (Noise(x, z, 0.4f, 700f) - 0.5f) * 0.5f;
            float d = Sdf(x, z);
            if (d > 0f)
            {
                float n = Noise(x, z, 0.12f, 900f);
                float cliff = Smooth(0f, 4f, d) * (5f + 4f * n) + Smooth(2f, 20f, d) * (16f + 14f * n);
                cliff += (Noise(x, z, 0.35f, 1100f) - 0.5f) * 3f * Smooth(0f, 3f, d);
                h += cliff;
            }
            float dc = ChasmSdf(x, z);
            if (dc < 0f)
            {
                float bottom = ChasmDepth + (Noise(x, z, 0.3f, 1700f) - 0.5f) * 6f;
                h = Mathf.Lerp(h, bottom, Smooth(0f, 3f, -dc));
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

        // ---------------------------------------------------------------- состояние сборки

        static readonly List<Mesh> generated = new List<Mesh>();
        static readonly List<Light> moodLights = new List<Light>();
        static readonly List<Vector3> torchSpots = new List<Vector3>();
        static readonly List<Light> torchLights = new List<Light>();
        static System.Random rng;
        static int torchCount;

        // Материалы
        static Material mFloor, mFloorAlt, mWall, mWallHigh, mCeiling, mChasm;
        static Material mRock, mRockDark, mWood, mWoodDark, mWoodPale, mIron, mFlame, mBone;
        static Material mHide, mHideWarm, mClothRed, mClothBlue, mClothYellow;
        static Material mCharcoal, mOchre, mBlack, mRune;
        static Material mGobSkin, mGobEyes, mGobSkinTrue, mGobEyesTrue, mTunic;
        static Material mShining, mShiningWing;
        static Material pSmoke, pFire, pEmbers, pDust, gVeil;

        [MenuItem("False Paradise/Build Zone 1 (Goblin Lair)")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/_Game/Scenes");
            EnsureFolder(MatDir);
            if (AssetDatabase.LoadAssetAtPath<Object>(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
            if (AssetDatabase.LoadAssetAtPath<Object>(PostPath) != null) AssetDatabase.DeleteAsset(PostPath);

            generated.Clear();
            moodLights.Clear();
            torchSpots.Clear();
            torchLights.Clear();
            torchCount = 0;
            rng = new System.Random(Seed);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            try
            {
                EditorUtility.DisplayProgressBar("Zone 1", "Материалы", 0.05f);
                CreateMaterials();

                var world = new GameObject("Zone1_Lair").transform;

                EditorUtility.DisplayProgressBar("Zone 1", "Пол и стены пещеры", 0.12f);
                BuildTerrain(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Свод пещеры", 0.22f);
                BuildCeiling(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Скалы, сталактиты, реквизит", 0.35f);
                BuildDecor(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Пропасть и мост", 0.55f);
                var safePoints = BuildChasmAndBridge(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Лагерь гоблинов", 0.62f);
                BuildCamp(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Предметы, клетка, выход", 0.7f);
                BuildItems(world);
                var cagePos = BuildCage(world);
                BuildExit(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Факелы", 0.76f);
                BuildTorches(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Гоблины", 0.82f);
                BuildGoblins(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Игрок и атмосфера", 0.9f);
                var cam = BuildPlayer(safePoints);
                BuildAtmosphere(cam);
                BuildNarration(world, cagePos);

                EditorUtility.DisplayProgressBar("Zone 1", "Сохранение", 0.96f);
                SaveMeshes();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
                Debug.Log($"[False Paradise] Зона 1 собрана: {ScenePath}. Мешей: {generated.Count}, факелов: {torchCount}. Нажмите Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- материалы

        static void CreateMaterials()
        {
            // Тёплый песчаник: пол светлее, свод темнее (референс: тёплая пещера в свете факелов)
            mFloor = Lit("Z1_Floor", new Color(0.66f, 0.53f, 0.39f));
            mFloorAlt = Lit("Z1_FloorGravel", new Color(0.55f, 0.43f, 0.31f));
            mWall = Lit("Z1_Wall", new Color(0.72f, 0.58f, 0.43f));
            mWallHigh = Lit("Z1_WallHigh", new Color(0.6f, 0.48f, 0.36f));
            mCeiling = Lit("Z1_Ceiling", new Color(0.52f, 0.42f, 0.32f));
            mChasm = Lit("Z1_ChasmRock", new Color(0.32f, 0.13f, 0.08f));
            mRock = Lit("Z1_Rock", new Color(0.76f, 0.62f, 0.47f));
            mRockDark = Lit("Z1_RockDark", new Color(0.5f, 0.4f, 0.3f));

            mWood = Lit("Z1_Wood", new Color(0.46f, 0.31f, 0.19f));
            mWoodDark = Lit("Z1_WoodDark", new Color(0.3f, 0.2f, 0.13f));
            mWoodPale = Lit("Z1_WoodPale", new Color(0.68f, 0.52f, 0.36f));
            mIron = Lit("Z1_Iron", new Color(0.2f, 0.19f, 0.2f), 0f, 0.45f, 0.6f);
            mFlame = Lit("Z1_Flame", new Color(1f, 0.38f, 0.06f), 1.5f);
            mBone = Lit("Z1_Bone", new Color(0.86f, 0.8f, 0.68f));
            mHide = Lit("Z1_HideDark", new Color(0.24f, 0.16f, 0.12f));
            mHideWarm = Lit("Z1_HideWarm", new Color(0.62f, 0.42f, 0.26f));
            mClothRed = Lit("Z1_ClothRed", new Color(0.72f, 0.28f, 0.22f));
            mClothBlue = Lit("Z1_ClothBlue", new Color(0.28f, 0.44f, 0.7f));
            mClothYellow = Lit("Z1_ClothYellow", new Color(0.88f, 0.72f, 0.35f));
            mCharcoal = Lit("Z1_Charcoal", new Color(0.04f, 0.04f, 0.05f));
            mOchre = Lit("Z1_Ochre", new Color(0.85f, 0.5f, 0.2f), 0.4f);
            mBlack = Lit("Z1_Void", new Color(0.005f, 0.004f, 0.003f));
            mRune = Lit("Z1_ElvenRune", new Color(0.3f, 1f, 0.9f), 4f);

            // Гоблины: иллюзия — тёмные, с красными глазами; правда — тёплые, в одежде
            mGobSkin = Lit("Z1_GoblinSkin", new Color(0.17f, 0.2f, 0.15f));
            mGobEyes = Lit("Z1_GoblinEyes", new Color(1f, 0.05f, 0.02f), 8f);
            mGobSkinTrue = Lit("Z1_GoblinSkinTrue", new Color(0.46f, 0.62f, 0.36f));
            mGobEyesTrue = Lit("Z1_GoblinEyesTrue", new Color(0.06f, 0.05f, 0.04f));
            mTunic = Lit("Z1_Tunic", new Color(0.52f, 0.36f, 0.22f));

            mShining = Lit("Z1_Shining", new Color(1f, 0.78f, 0.45f), 1.6f);
            mShiningWing = Lit("Z1_ShiningWing", new Color(1f, 0.7f, 0.35f), 2f);

            pSmoke = Soft("Z1_Smoke", new Color(0.3f, 0.27f, 0.25f, 0.2f), 1f, false, 1.6f);
            pFire = Soft("Z1_Fire", new Color(1f, 0.45f, 0.12f), 2.2f, true, 1.4f);
            pEmbers = Soft("Z1_Embers", new Color(1f, 0.55f, 0.18f), 4f, true, 2f);
            pDust = Soft("Z1_Dust", new Color(1f, 0.8f, 0.55f), 1.2f, true, 2f);
            gVeil = Glow("Z1_GlowVeil", new Color(0.3f, 1f, 0.9f), 1.6f);
        }

        static Material Lit(string name, Color c, float emission = 0f, float smooth = 0.06f, float metallic = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
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

        static Material Glow(string name, Color c, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("FalseParadise/UnlitGlow");
            if (shader == null)
            {
                Debug.LogWarning("Шейдер FalseParadise/UnlitGlow не найден, свечение будет через URP Unlit.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
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

        /// <summary>Материал частиц: мягкий круг. additive — свечение (искры, пыль), иначе дым.</summary>
        static Material Soft(string name, Color c, float intensity, bool additive, float softness)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("FalseParadise/ParticleSoft");
            if (shader == null)
            {
                Debug.LogWarning("Шейдер FalseParadise/ParticleSoft не найден, частицы будут через URP Particles/Unlit.");
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }
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
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- рельеф

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
                float x = MinX + ix * Cell + (Hash(ix, iz, 1) - 0.5f) * Cell * 0.7f;
                float z = MinZ + iz * Cell + (Hash(ix, iz, 2) - 0.5f) * Cell * 0.7f;
                pos[ix, iz] = new Vector3(x, Height(x, z), z);
                sdf[ix, iz] = Sdf(x, z);
            }

            // 0 пол, 1 гравий, 2 стена, 3 верх стены, 4 пропасть
            var mats = new[] { mFloor, mFloorAlt, mWall, mWallHigh, mChasm };
            var chunks = new Dictionary<Vector2Int, LowPolyMesh>();

            for (int ix = 0; ix < nx - 1; ix++)
            for (int iz = 0; iz < nz - 1; iz++)
            {
                if (sdf[ix, iz] > 26f && sdf[ix + 1, iz] > 26f && sdf[ix, iz + 1] > 26f && sdf[ix + 1, iz + 1] > 26f)
                    continue; // глубоко в скале, не видно

                var key = new Vector2Int(ix / ChunkCells, iz / ChunkCells);
                if (!chunks.TryGetValue(key, out var lp)) chunks[key] = lp = new LowPolyMesh(mats.Length);

                var p00 = pos[ix, iz];
                var p10 = pos[ix + 1, iz];
                var p01 = pos[ix, iz + 1];
                var p11 = pos[ix + 1, iz + 1];
                if (Hash(ix, iz, 3) < 0.5f)
                {
                    TerrainTri(lp, p00, p01, p11);
                    TerrainTri(lp, p00, p11, p10);
                }
                else
                {
                    TerrainTri(lp, p00, p01, p10);
                    TerrainTri(lp, p10, p01, p11);
                }
            }

            foreach (var kv in chunks)
            {
                var mesh = Register(kv.Value.ToMesh($"Z1_Terrain_{kv.Key.x}_{kv.Key.y}"));
                var go = MeshObject($"Chunk_{kv.Key.x}_{kv.Key.y}", root, mesh, mats, true);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static void TerrainTri(LowPolyMesh lp, Vector3 a, Vector3 b, Vector3 c)
        {
            var center = (a + b + c) / 3f;
            var n = Vector3.Cross(b - a, c - a);
            if (n.y < 0f) n = -n;
            n.Normalize();
            float floor = FloorH(center.x, center.z);
            float rel = center.y - floor;
            int sub;
            if (rel < -3f) sub = 4;
            else if (n.y > 0.8f && rel < 1.5f) sub = Noise(center.x, center.z, 0.15f, 2100f) > 0.58f ? 1 : 0;
            else if (rel > 11f) sub = 3;
            else sub = 2;
            lp.Tri(sub, a, b, c, Vector3.up);
        }

        // ---------------------------------------------------------------- свод пещеры

        /// <summary>Высота свода: купол 8–15 м над полом в залах, ниже в проходах, у стен опускается в скалу.</summary>
        static float CeilingH(float x, float z)
        {
            float d = Sdf(x, z);
            float dome = d < 0f ? 7.5f + 7.5f * Smooth(0f, 18f, -d) : 7.5f - d * 0.7f;
            dome += (Noise(x, z, 0.15f, 2500f) - 0.5f) * 3f + (Noise(x, z, 0.45f, 2700f) - 0.5f) * 0.9f;
            return FloorH(x, z) + dome;
        }

        static void BuildCeiling(Transform world)
        {
            var root = new GameObject("Ceiling").transform;
            root.SetParent(world, false);

            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell) + 1;
            int nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell) + 1;
            var pos = new Vector3[nx, nz];
            var sdf = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = MinX + ix * Cell + (Hash(ix, iz, 4) - 0.5f) * Cell * 0.7f;
                float z = MinZ + iz * Cell + (Hash(ix, iz, 5) - 0.5f) * Cell * 0.7f;
                pos[ix, iz] = new Vector3(x, CeilingH(x, z), z);
                sdf[ix, iz] = Sdf(x, z);
            }

            var mats = new[] { mCeiling, mWallHigh };
            var chunks = new Dictionary<Vector2Int, LowPolyMesh>();
            for (int ix = 0; ix < nx - 1; ix++)
            for (int iz = 0; iz < nz - 1; iz++)
            {
                float minD = Mathf.Min(Mathf.Min(sdf[ix, iz], sdf[ix + 1, iz]), Mathf.Min(sdf[ix, iz + 1], sdf[ix + 1, iz + 1]));
                if (minD > 6f) continue;
                var key = new Vector2Int(ix / ChunkCells, iz / ChunkCells);
                if (!chunks.TryGetValue(key, out var lp)) chunks[key] = lp = new LowPolyMesh(mats.Length);
                var p00 = pos[ix, iz];
                var p10 = pos[ix + 1, iz];
                var p01 = pos[ix, iz + 1];
                var p11 = pos[ix + 1, iz + 1];
                int sub = Hash(ix, iz, 6) < 0.3f ? 1 : 0;
                if (Hash(ix, iz, 7) < 0.5f)
                {
                    lp.Tri(sub, p00, p01, p11, Vector3.down);
                    lp.Tri(sub, p00, p11, p10, Vector3.down);
                }
                else
                {
                    lp.Tri(sub, p00, p01, p10, Vector3.down);
                    lp.Tri(sub, p10, p01, p11, Vector3.down);
                }
            }
            foreach (var kv in chunks)
            {
                var mesh = Register(kv.Value.ToMesh($"Z1_Ceiling_{kv.Key.x}_{kv.Key.y}"));
                var go = MeshObject($"Ceiling_{kv.Key.x}_{kv.Key.y}", root, mesh, mats, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        // ---------------------------------------------------------------- декор и реквизит

        /// <summary>Места, где не ставим крупный реквизит: старт, лагерь, клетка, предметы, мост, выход.</summary>
        static bool Reserved(float x, float z)
        {
            var p = new Vector2(x, z);
            if (Vector2.Distance(p, SpawnPos) < 5f) return true;
            if (Vector2.Distance(p, FirePos) < 14f) return true;
            if (Vector2.Distance(p, BurrowPos) < 5f) return true;
            if (Vector2.Distance(p, CagePos) < 7f) return true;
            if (Vector2.Distance(p, ToyPos) < 3f) return true;
            if (Vector2.Distance(p, DrawingPos) < 3f) return true;
            if (Vector2.Distance(p, ExitArchPos) < 8f) return true;
            if (Vector2.Distance(p, BridgeHall.c) < 13f) return true;
            return false;
        }

        static void BuildDecor(Transform world)
        {
            var root = new GameObject("Decor").transform;
            root.SetParent(world, false);

            // Твёрдое (с коллайдером): 0 камень, 1 тёмный камень, 2 дерево, 3 тёмное дерево, 4 железо
            var solidMats = new[] { mRock, mRockDark, mWood, mWoodDark, mIron };
            // Мелочь на полу (без коллайдера): 0 камень, 1 тёмный камень, 2 светлое дерево, 3 дерево
            var detailMats = new[] { mRock, mRockDark, mWoodPale, mWood };
            // Сталактиты (без коллайдера): 0 камень, 1 тёмный камень, 2 цвет свода
            var hangMats = new[] { mRock, mRockDark, mCeiling };
            var solid = new Dictionary<Vector2Int, LowPolyMesh>();
            var detail = new Dictionary<Vector2Int, LowPolyMesh>();
            var hang = new Dictionary<Vector2Int, LowPolyMesh>();
            var bones = new Dictionary<Vector2Int, LowPolyMesh>();

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
                float dc = ChasmSdf(x, z);
                float path = PathDist(x, z);
                bool reserved = Reserved(x, z);
                var p = new Vector3(x, Height(x, z), z);
                var inward = -Gradient(x, z); // от стены в пещеру

                // Валуны у стен
                if (d > -1.5f && d < 3.5f && dc > 2f && R01() < 0.5f)
                    Boulder(Chunk(solid, 5, x, z), p, R(1.2f, 3.4f));

                // Сталагмиты (с маленькими соседями)
                if (d > -4f && d < 1f && dc > 2f && path > 3.5f && !reserved && R01() < 0.2f)
                {
                    var lp = Chunk(solid, 5, x, z);
                    Stalagmite(lp, p, R(1.4f, 5f));
                    int kids = rng.Next(0, 3);
                    for (int k = 0; k < kids; k++)
                    {
                        var q = p + new Vector3(R(-1.6f, 1.6f), 0f, R(-1.6f, 1.6f));
                        if (PathDist(q.x, q.z) < 3f) continue;
                        q.y = Height(q.x, q.z);
                        Stalagmite(lp, q, R(0.6f, 2f));
                    }
                }

                // Сталактиты со свода: над тропой короче, чтобы не задевать голову
                if (d < 2.5f && R01() < 0.32f)
                {
                    var lp = Chunk(hang, 3, x, z);
                    float len = path < 4f ? R(0.6f, 2.4f) : R(1.2f, 5.5f);
                    Stalactite(lp, x, z, len);
                    if (R01() < 0.5f) Stalactite(lp, x + R(-1f, 1f), z + R(-1f, 1f), len * R(0.3f, 0.6f));
                }

                // Реквизит гоблинов у стен
                if (d > -2.8f && d < -0.5f && dc > 3f && path > 3f && !reserved)
                {
                    var lp = Chunk(solid, 5, x, z);
                    float t = R01();
                    if (t < 0.1f) Fence(lp, p, inward);
                    else if (t < 0.15f) Ladder(lp, p, inward);
                    else if (t < 0.24f) Barricade(lp, p, inward);
                    else if (t < 0.32f) Palisade(lp, p, inward);
                    else if (t < 0.37f) Crates(lp, p, inward);
                    else if (t < 0.41f) Barrels(lp, p);
                    else if (t < 0.47f) Scaffold(lp, p, inward);
                }

                // Колья, вбитые в пол, и брошенные доски ближе к центру залов
                if (d < -3f && dc > 3f && path > 2.6f && !reserved && R01() < 0.05f)
                    StakeCluster(Chunk(solid, 5, x, z), p);

                // Мелочь на полу
                if (d < -0.8f && dc > 1.5f)
                {
                    if (path > 1.3f && R01() < 0.22f) Rubble(Chunk(detail, 4, x, z), p);
                    if (R01() < 0.1f) Planks(Chunk(detail, 4, x, z), p);
                    if (R01() < 0.14f) Bones(Chunk(bones, 1, x, z), p);
                }
            }

            // Колонны-сталагнаты от пола до свода
            Vector2[] pillars =
            {
                new Vector2(22f, 44f), new Vector2(-6f, 52f), new Vector2(16f, 64f), new Vector2(20f, 34f),
                new Vector2(-34f, 90f), new Vector2(-4f, 94f),
                new Vector2(44f, 144f), new Vector2(16f, 168f), new Vector2(50f, 168f), new Vector2(10f, 150f),
            };
            foreach (var pp in pillars)
                Pillar(Chunk(solid, 5, pp.x, pp.y), Ground(pp, -0.5f), R(0.8f, 1.15f));

            foreach (var kv in solid)
            {
                var go = MeshObject($"Solid_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"Z1_Decor_{kv.Key.x}_{kv.Key.y}")), solidMats, true);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            foreach (var kv in detail)
            {
                var go = MeshObject($"Detail_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"Z1_Detail_{kv.Key.x}_{kv.Key.y}")), detailMats, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            foreach (var kv in hang)
            {
                var go = MeshObject($"Stalactites_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"Z1_Hang_{kv.Key.x}_{kv.Key.y}")), hangMats, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            // Кости — часть иллюзии: с Оком истины исчезают
            foreach (var kv in bones)
            {
                var holder = new GameObject($"Bones_{kv.Key.x}_{kv.Key.y}");
                holder.transform.SetParent(root, false);
                var lie = MeshObject("Illusion", holder.transform, Register(kv.Value.ToMesh($"Z1_Bones_{kv.Key.x}_{kv.Key.y}")), new[] { mBone }, false);
                var iv = holder.AddComponent<IllusionVisual>();
                SetRef(iv, "illusionModel", lie);
            }
        }

        static Vector3 Gradient(float x, float z)
        {
            const float e = 0.75f;
            var g = new Vector3(Sdf(x + e, z) - Sdf(x - e, z), 0f, Sdf(x, z + e) - Sdf(x, z - e));
            return g.sqrMagnitude > 1e-6f ? g.normalized : Vector3.forward;
        }

        static void Boulder(LowPolyMesh lp, Vector3 p, float s)
        {
            var m = Matrix4x4.TRS(p + Vector3.down * s * 0.25f,
                Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)),
                new Vector3(s * R(0.8f, 1.3f), s * R(0.5f, 0.9f), s * R(0.8f, 1.3f)));
            lp.Lathe(R01() < 0.35f ? 1 : 0, m, new[] { -0.3f, 0.25f, 0.7f, 1f }, new[] { 0.85f, 1f, 0.7f, 0.25f },
                rng.Next(5, 8), rng, 0.28f);
        }

        static void Stalagmite(LowPolyMesh lp, Vector3 p, float h)
        {
            float w = h * R(0.17f, 0.25f);
            var m = Matrix4x4.TRS(p + Vector3.down * 0.3f, Quaternion.Euler(R(-7f, 7f), R(0f, 360f), R(-7f, 7f)), Vector3.one);
            lp.Lathe(R01() < 0.6f ? 0 : 1, m,
                new[] { 0f, h * 0.3f, h * 0.65f, h },
                new[] { w, w * 0.7f, w * 0.38f, 0f },
                rng.Next(5, 8), rng, 0.18f, false, 0.3f);
        }

        static void Stalactite(LowPolyMesh lp, float x, float z, float len)
        {
            float top = CeilingH(x, z) + 0.6f;
            float w = len * R(0.15f, 0.24f);
            var m = Matrix4x4.TRS(new Vector3(x, top, z), Quaternion.Euler(180f + R(-6f, 6f), R(0f, 360f), R(-6f, 6f)), Vector3.one);
            float t = R01();
            lp.Lathe(t < 0.45f ? 2 : t < 0.8f ? 0 : 1, m,
                new[] { 0f, len * 0.3f, len * 0.65f, len + 0.6f },
                new[] { w, w * 0.68f, w * 0.33f, 0f },
                rng.Next(5, 8), rng, 0.18f, false, 0.3f);
        }

        static void Pillar(LowPolyMesh lp, Vector3 p, float s)
        {
            float top = CeilingH(p.x, p.z) + 1.5f;
            float H = top - p.y;
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
            float[] hs = { 0f, 0.08f, 0.25f, 0.45f, 0.6f, 0.78f, 0.92f, 1f };
            float[] rs = { 4.2f, 3f, 2f, 1.5f, 1.6f, 2.3f, 3.4f, 4.6f };
            var h = new float[hs.Length];
            var r = new float[rs.Length];
            for (int i = 0; i < hs.Length; i++) { h[i] = hs[i] * H; r[i] = rs[i] * s; }
            lp.Lathe(0, m, h, r, 8, rng, 0.14f, false, 0.25f);
            for (int i = 0; i < 4; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.3f * s;
                q.y = Height(q.x, q.z);
                Boulder(lp, q, R(1f, 2.2f));
            }
        }

        /// <summary>Деревянный забор у стены: столбы, доски, раскос.</summary>
        static void Fence(LowPolyMesh lp, Vector3 p, Vector3 inward)
        {
            float L = R(2.5f, 4.5f), H = R(1.8f, 2.6f);
            var m = Matrix4x4.TRS(p - inward * 0.3f, Quaternion.LookRotation(inward) * Quaternion.Euler(R(-4f, 4f), R(-8f, 8f), 0f), Vector3.one);
            for (int i = -1; i <= 1; i += 2)
                lp.Box(3, m * Matrix4x4.TRS(new Vector3(i * L * 0.5f, H * 0.5f - 0.25f, 0f), Quaternion.Euler(0f, 0f, R(-4f, 4f)), Vector3.one), Vector3.zero, new Vector3(0.17f, H, 0.17f));
            int boards = rng.Next(2, 4);
            for (int k = 0; k < boards; k++)
                lp.Box(2, m * Matrix4x4.TRS(new Vector3(R(-0.1f, 0.1f), 0.55f + k * 0.6f, 0.1f), Quaternion.Euler(0f, 0f, R(-5f, 5f)), Vector3.one), Vector3.zero, new Vector3(L + R(0.1f, 0.5f), 0.2f, 0.06f));
            float ang = Mathf.Atan2(H * 0.8f, L) * Mathf.Rad2Deg;
            lp.Box(3, m * Matrix4x4.TRS(new Vector3(0f, H * 0.42f, 0.16f), Quaternion.Euler(0f, 0f, ang), Vector3.one), Vector3.zero, new Vector3(Mathf.Sqrt(L * L + H * H * 0.64f), 0.13f, 0.06f));
        }

        /// <summary>Лестница, прислонённая к стене.</summary>
        static void Ladder(LowPolyMesh lp, Vector3 p, Vector3 inward)
        {
            float H = R(2.6f, 3.6f);
            var m = Matrix4x4.TRS(p - inward * 0.5f, Quaternion.LookRotation(inward) * Quaternion.Euler(-15f, R(-10f, 10f), 0f), Vector3.one);
            for (int i = -1; i <= 1; i += 2)
                lp.Box(3, m, new Vector3(i * 0.27f, H * 0.5f, 0f), new Vector3(0.08f, H, 0.08f));
            for (float y = 0.3f; y < H - 0.1f; y += 0.36f)
                lp.Box(2, m, new Vector3(0f, y, 0f), new Vector3(0.6f, 0.06f, 0.06f));
        }

        /// <summary>Заграждение из заострённых кольев крест-накрест.</summary>
        static void Barricade(LowPolyMesh lp, Vector3 p, Vector3 inward)
        {
            float L = R(2.2f, 3.6f);
            var m = Matrix4x4.TRS(p + Vector3.up * 0.2f, Quaternion.LookRotation(inward) * Quaternion.Euler(0f, R(-15f, 15f), 0f), Vector3.one);
            lp.Box(3, m, Vector3.zero, new Vector3(L, 0.24f, 0.24f));
            int n = rng.Next(4, 7);
            for (int k = 0; k < n; k++)
            {
                float x = Mathf.Lerp(-L * 0.45f, L * 0.45f, n == 1 ? 0.5f : k / (float)(n - 1));
                for (int s = -1; s <= 1; s += 2)
                {
                    var dir = new Vector3(R(-0.12f, 0.12f), 0.8f, s * 0.6f).normalized;
                    float len = R(1.3f, 1.9f);
                    var sm = m * Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.FromToRotation(Vector3.up, dir), Vector3.one);
                    lp.Lathe(s < 0 ? 3 : 2, sm, new[] { -0.4f, len * 0.82f, len }, new[] { 0.07f, 0.065f, 0f }, 5);
                }
            }
        }

        /// <summary>Частокол: ряд заострённых брёвен разной высоты, связанных поперечиной.</summary>
        static void Palisade(LowPolyMesh lp, Vector3 p, Vector3 inward)
        {
            float L = R(2f, 4f);
            var m = Matrix4x4.TRS(p - inward * 0.2f, Quaternion.LookRotation(inward) * Quaternion.Euler(0f, R(-12f, 12f), 0f), Vector3.one);
            int n = Mathf.Max(3, Mathf.RoundToInt(L / 0.32f));
            for (int k = 0; k < n; k++)
            {
                float x = -L * 0.5f + L * k / (n - 1);
                float h = R(1.4f, 2.6f);
                var sm = m * Matrix4x4.TRS(new Vector3(x, -0.3f, R(-0.06f, 0.06f)), Quaternion.Euler(R(-6f, 6f), 0f, R(-7f, 7f)), Vector3.one);
                lp.Lathe(R01() < 0.5f ? 2 : 3, sm, new[] { 0f, h * 0.85f, h }, new[] { 0.12f, 0.11f, 0f }, 5);
            }
            lp.Box(3, m * Matrix4x4.TRS(new Vector3(0f, 0.9f, 0.14f), Quaternion.Euler(0f, 0f, R(-4f, 4f)), Vector3.one), Vector3.zero, new Vector3(L + 0.3f, 0.14f, 0.1f));
        }

        /// <summary>Деревянные мостки-подмостки у стены: стойки, настил, раскосы.</summary>
        static void Scaffold(LowPolyMesh lp, Vector3 p, Vector3 inward)
        {
            float L = R(2.4f, 3.6f), H = R(2.2f, 3.2f);
            var m = Matrix4x4.TRS(p - inward * 0.4f, Quaternion.LookRotation(inward) * Quaternion.Euler(0f, R(-10f, 10f), 0f), Vector3.one);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = 0; sz <= 1; sz++)
                lp.Box(3, m * Matrix4x4.TRS(new Vector3(sx * L * 0.5f, H * 0.5f - 0.3f, -sz * 1.1f), Quaternion.Euler(R(-3f, 3f), 0f, R(-3f, 3f)), Vector3.one), Vector3.zero, new Vector3(0.16f, H, 0.16f));
            for (int k = 0; k < 5; k++)
                lp.Box(k % 2 == 0 ? 2 : 3, m * Matrix4x4.TRS(new Vector3(R(-0.1f, 0.1f), H - 0.25f, -0.15f - k * 0.2f), Quaternion.Euler(0f, R(-3f, 3f), 0f), Vector3.one), Vector3.zero, new Vector3(L + R(0.2f, 0.6f), 0.07f, 0.19f));
            float ang = Mathf.Atan2(H * 0.7f, L) * Mathf.Rad2Deg;
            lp.Box(3, m * Matrix4x4.TRS(new Vector3(0f, H * 0.4f, 0.1f), Quaternion.Euler(0f, 0f, ang), Vector3.one), Vector3.zero, new Vector3(Mathf.Sqrt(L * L + H * H * 0.49f), 0.1f, 0.08f));
            if (R01() < 0.6f) Ladder(lp, p + inward * 0.2f + Quaternion.LookRotation(inward) * new Vector3(L * 0.5f + 0.5f, 0f, 0f), inward);
        }

        /// <summary>Пучок кольев, вбитых в пол под разными углами (как в референсе слева от тропы).</summary>
        static void StakeCluster(LowPolyMesh lp, Vector3 p)
        {
            int n = rng.Next(4, 9);
            for (int k = 0; k < n; k++)
            {
                var q = p + new Vector3(R(-0.9f, 0.9f), 0f, R(-0.9f, 0.9f));
                q.y = Height(q.x, q.z) - 0.2f;
                var dir = new Vector3(R(-0.5f, 0.5f), 1f, R(-0.5f, 0.5f)).normalized;
                float len = R(0.8f, 1.9f);
                lp.Lathe(R01() < 0.5f ? 2 : 3, Matrix4x4.TRS(q, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one),
                    new[] { 0f, len * 0.8f, len }, new[] { 0.075f, 0.065f, 0f }, 5);
            }
            if (R01() < 0.5f) Boulder(lp, p + new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)), R(0.5f, 0.9f));
        }

        static void Crates(LowPolyMesh lp, Vector3 p, Vector3 inward)
        {
            int n = rng.Next(1, 4);
            var basis = Quaternion.LookRotation(inward) * Quaternion.Euler(0f, R(-20f, 20f), 0f);
            for (int k = 0; k < n; k++)
            {
                float s = R(0.7f, 1.1f);
                var off = k == 2 ? new Vector3(R(-0.2f, 0.2f), 1f, 0f) : new Vector3(k * 1.15f - 0.5f, 0f, R(-0.2f, 0.2f));
                var m = Matrix4x4.TRS(p + basis * off, basis * Quaternion.Euler(0f, R(-12f, 12f), 0f), Vector3.one * s);
                lp.Box(2, m, new Vector3(0f, 0.5f, 0f), Vector3.one);
                for (int cx = -1; cx <= 1; cx += 2)
                for (int cz = -1; cz <= 1; cz += 2)
                    lp.Box(3, m, new Vector3(cx * 0.48f, 0.5f, cz * 0.48f), new Vector3(0.1f, 1.02f, 0.1f));
                lp.Box(3, m, new Vector3(0f, 0.5f, 0f), new Vector3(1.03f, 0.12f, 1.03f));
            }
        }

        static void Barrels(LowPolyMesh lp, Vector3 p)
        {
            int n = rng.Next(1, 4);
            for (int k = 0; k < n; k++)
            {
                float s = R(0.85f, 1.15f);
                var q = p + new Vector3(R(-0.8f, 0.8f), -0.05f, R(-0.8f, 0.8f));
                q.y = Height(q.x, q.z) - 0.05f;
                bool lying = R01() < 0.2f;
                var rot = lying ? Quaternion.Euler(90f, R(0f, 360f), 0f) : Quaternion.Euler(R(-3f, 3f), R(0f, 360f), R(-3f, 3f));
                var m = Matrix4x4.TRS(q + (lying ? Vector3.up * 0.38f * s : Vector3.zero), rot, Vector3.one * s);
                lp.Lathe(2, m, new[] { 0f, 0.12f, 0.45f, 0.78f, 0.9f }, new[] { 0.3f, 0.35f, 0.39f, 0.35f, 0.3f }, 9, null, 0f, true);
                lp.Lathe(4, m, new[] { 0.14f, 0.2f }, new[] { 0.36f, 0.365f }, 9);
                lp.Lathe(4, m, new[] { 0.7f, 0.76f }, new[] { 0.36f, 0.355f }, 9);
            }
        }

        static void Rubble(LowPolyMesh lp, Vector3 p)
        {
            int n = rng.Next(1, 5);
            for (int k = 0; k < n; k++)
            {
                float s = R(0.15f, 0.65f);
                var q = p + new Vector3(R(-1.2f, 1.2f), 0f, R(-1.2f, 1.2f));
                q.y = Height(q.x, q.z) - s * 0.2f;
                var m = Matrix4x4.TRS(q, Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f)),
                    new Vector3(s * R(0.8f, 1.5f), s * R(0.4f, 0.9f), s));
                lp.Lathe(R01() < 0.6f ? 0 : 1, m, new[] { 0f, 0.6f, 1f }, new[] { 1f, 0.75f, 0.2f }, rng.Next(4, 7), rng, 0.25f);
            }
        }

        static void Planks(LowPolyMesh lp, Vector3 p)
        {
            int n = rng.Next(1, 4);
            for (int k = 0; k < n; k++)
            {
                var q = p + new Vector3(R(-0.8f, 0.8f), 0f, R(-0.8f, 0.8f));
                q.y = Height(q.x, q.z) + 0.06f + k * 0.05f;
                var m = Matrix4x4.TRS(q, Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-6f, 6f)), Vector3.one);
                lp.Box(R01() < 0.6f ? 2 : 3, m, Vector3.zero, new Vector3(R(0.15f, 0.28f), 0.05f, R(0.8f, 2f)));
            }
        }

        static void Bones(LowPolyMesh lp, Vector3 p)
        {
            int n = rng.Next(1, 4);
            for (int k = 0; k < n; k++)
            {
                var q = p + new Vector3(R(-0.8f, 0.8f), 0f, R(-0.8f, 0.8f));
                q.y = Height(q.x, q.z) + 0.04f;
                float len = R(0.35f, 0.8f);
                var m = Matrix4x4.TRS(q, Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f)), Vector3.one);
                lp.Box(0, m, Vector3.zero, new Vector3(0.06f, 0.06f, len));
                lp.Box(0, m, new Vector3(0f, 0f, len * 0.5f), new Vector3(0.13f, 0.09f, 0.08f));
                lp.Box(0, m, new Vector3(0f, 0f, -len * 0.5f), new Vector3(0.13f, 0.09f, 0.08f));
            }
            if (R01() < 0.3f) Skull(lp, 0, new Vector3(p.x, Height(p.x, p.z) + 0.12f, p.z), R(0.16f, 0.24f));
        }

        // ---------------------------------------------------------------- пропасть и мост

        static Transform[] BuildChasmAndBridge(Transform world)
        {
            var root = new GameObject("ChasmBridge").transform;
            root.SetParent(world, false);

            // Мост вдоль пути, поперёк пропасти
            var c = BridgeHall.c;
            var dir2 = (CageHall.c - Warren.c).normalized;
            var dir = new Vector3(dir2.x, 0f, dir2.y);
            const float halfLen = 10f;
            var endA = new Vector2(c.x - dir2.x * halfLen, c.y - dir2.y * halfLen);
            var endB = new Vector2(c.x + dir2.x * halfLen, c.y + dir2.y * halfLen);
            float deckTop = Mathf.Max(Height(endA.x, endA.y), Height(endB.x, endB.y)) + 0.05f;
            var center = new Vector3(c.x, deckTop, c.y);
            var rot = Quaternion.LookRotation(dir);
            var bridge = new GameObject("StoneBridge").transform;
            bridge.SetParent(root, false);
            bridge.position = center;
            bridge.rotation = rot;

            var lp = new LowPolyMesh(2); // 0 камень, 1 тёмный камень
            // Плиты настила
            int slabs = 10;
            float slabLen = halfLen * 2f / slabs;
            for (int i = 0; i < slabs; i++)
            {
                float z = -halfLen + slabLen * (i + 0.5f);
                var m = Matrix4x4.TRS(new Vector3(R(-0.05f, 0.05f), -0.35f, z), Quaternion.Euler(0f, R(-1.5f, 1.5f), 0f), Vector3.one);
                lp.Box(i % 3 == 0 ? 1 : 0, m, Vector3.zero, new Vector3(3.8f, 0.7f, slabLen * 0.97f));
            }
            // Перила из камней
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 9; i++)
            {
                float z = -halfLen + 1.2f + i * (halfLen * 2f - 2.4f) / 8f;
                var m = Matrix4x4.TRS(new Vector3(side * 2.05f, 0.35f, z), Quaternion.Euler(R(-4f, 4f), R(-8f, 8f), R(-4f, 4f)), Vector3.one);
                lp.Box(1, m, Vector3.zero, new Vector3(0.45f, R(0.7f, 1f), R(1f, 1.5f)));
            }
            // Арка под мостом
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float z = Mathf.Lerp(-halfLen + 1f, halfLen - 1f, t);
                float drop = 1.2f + Mathf.Pow(Mathf.Abs(t - 0.5f) * 2f, 2f) * 5f;
                var m = Matrix4x4.TRS(new Vector3(0f, -0.7f - drop * 0.5f, z), Quaternion.identity, Vector3.one);
                lp.Box(1, m, Vector3.zero, new Vector3(2.8f, drop, 3f));
            }
            var bridgeMesh = Register(lp.ToMesh("Z1_Bridge"));
            var bridgeGo = MeshObject("Mesh", bridge, bridgeMesh, new[] { mRock, mRockDark }, true);
            GameObjectUtility.SetStaticEditorFlags(bridgeGo, StaticEditorFlags.BatchingStatic);

            // Багровый свет из глубины
            var glowRoot = new GameObject("ChasmGlow").transform;
            glowRoot.SetParent(root, false);
            for (int i = 0; i < 4; i++)
            {
                var p = Vector2.Lerp(ChasmA, ChasmB, (i + 0.5f) / 4f);
                var l = PointLight(glowRoot, $"Glow_{i}", new Vector3(p.x, -16f, p.y), new Color(1f, 0.16f, 0.06f), 32f, 9f);
                l.shadows = LightShadows.None;
                var f = l.gameObject.AddComponent<TorchFlicker>();
                SetFloat(f, "baseIntensity", 9f);
                SetFloat(f, "flickerAmount", 1.5f);
                SetFloat(f, "speed", 1.5f);
            }

            // Искры, поднимающиеся из пропасти
            var embers = new GameObject("Embers");
            embers.transform.SetParent(root, false);
            var mid = (ChasmA + ChasmB) * 0.5f;
            embers.transform.position = new Vector3(mid.x, -24f, mid.y);
            embers.transform.rotation = Quaternion.LookRotation(new Vector3(ChasmB.x - ChasmA.x, 0f, ChasmB.y - ChasmA.y));
            var emberPs = Particles(embers, pEmbers, 60f, 7f, 12f, 0.05f, 0.3f, 0.05f, 0.12f,
                new Color(1f, 0.55f, 0.15f), new Color(1f, 0.2f, 0.05f), -0.12f, 600, FadeCurve(), 0.35f);
            ShapeBox(emberPs, new Vector3(6f, 2f, 34f));

            // Точки возврата при падении — концы моста
            var safeA = new GameObject("SafePoint_A").transform;
            safeA.SetParent(root, false);
            safeA.position = Ground(endA - dir2 * 2f, 1.2f);
            safeA.rotation = rot;
            var safeB = new GameObject("SafePoint_B").transform;
            safeB.SetParent(root, false);
            safeB.position = Ground(endB + dir2 * 2f, 1.2f);
            safeB.rotation = rot;

            // Факелы у входа на мост
            Torch(root, Ground(endA + new Vector2(-dir2.y, dir2.x) * 2.8f - dir2 * 1.5f));
            Torch(root, Ground(endA + new Vector2(dir2.y, -dir2.x) * 2.8f - dir2 * 1.5f));
            return new[] { safeA, safeB };
        }

        // ---------------------------------------------------------------- лагерь гоблинов

        static void BuildCamp(Transform world)
        {
            var root = new GameObject("GoblinCamp").transform;
            root.SetParent(world, false);

            // Палатки: иллюзия — тёмные шкуры с шипами и черепом; правда — тёплые, с полосками ткани
            var tentLie = Register(TentMesh(false));
            var tentTrue = Register(TentMesh(true));
            var lieMats = new[] { mHide, mBone, mWood };
            var trueMats = new[] { mHideWarm, mClothRed, mWood, mClothBlue };
            Vector2[] tents = { new Vector2(-30f, 88f), new Vector2(-34f, 112f), new Vector2(-26f, 116f), new Vector2(-36f, 98f), new Vector2(-22f, 84f) };
            int i = 0;
            foreach (var t in tents)
            {
                var go = DualObject($"Tent_{++i}", root, Ground(t, -0.1f), Quaternion.LookRotation(Flat(FirePos - t)) * Quaternion.Euler(0f, R(-20f, 20f), 0f),
                    tentLie, lieMats, tentTrue, trueMats);
                go.transform.localScale = Vector3.one * R(0.9f, 1.15f);
                var col = go.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1.5f, 0f);
                col.radius = 1.7f;
                col.height = 3f;
            }

            // Кострище
            var fire = new GameObject("Firepit").transform;
            fire.SetParent(root, false);
            fire.position = Ground(FirePos);
            MeshObject("Mesh", fire, Register(FirepitMesh()), new[] { mRock, mWood, mFlame }, false);
            var fc = fire.gameObject.AddComponent<CapsuleCollider>();
            fc.center = new Vector3(0f, 0.3f, 0f);
            fc.radius = 1.3f;
            fc.height = 0.6f;
            var fl = PointLight(fire, "FireLight", fire.position + Vector3.up * 1.6f, TorchColor, 22f, 40f);
            ShadowLight(fl);
            var flick = fl.gameObject.AddComponent<TorchFlicker>();
            SetFloat(flick, "baseIntensity", 40f);
            SetFloat(flick, "flickerAmount", 6f);
            moodLights.Add(fl);
            torchSpots.Add(fire.position);
            var fireSmoke = new GameObject("Smoke");
            fireSmoke.transform.SetParent(fire, false);
            fireSmoke.transform.position = fire.position + Vector3.up * 1.4f;
            var fps = Particles(fireSmoke, pSmoke, 9f, 4f, 6f, 0.7f, 1.2f, 1f, 1.8f,
                new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.8f, 0.75f, 0.7f), -0.015f, 60, GrowCurve(), 0.4f);
            ShapeCone(fps, 10f, 0.4f);
            var flames = new GameObject("Fire");
            flames.transform.SetParent(fire, false);
            flames.transform.position = fire.position + Vector3.up * 0.3f;
            var ffs = Particles(flames, pFire, 50f, 0.5f, 0.9f, 0.8f, 1.6f, 0.5f, 0.9f,
                new Color(1f, 0.8f, 0.45f), new Color(1f, 0.45f, 0.12f), -0.1f, 80, ShrinkCurve(), 0.8f);
            ShapeCone(ffs, 10f, 0.55f);
            var fireSparks = new GameObject("Embers");
            fireSparks.transform.SetParent(fire, false);
            fireSparks.transform.position = fire.position + Vector3.up * 0.8f;
            fps = Particles(fireSparks, pEmbers, 14f, 1.5f, 3.5f, 0.8f, 2f, 0.03f, 0.06f,
                new Color(1f, 0.75f, 0.35f), new Color(1f, 0.4f, 0.1f), -0.05f, 60, FadeCurve(), 0.9f);
            ShapeCone(fps, 20f, 0.5f);

            // Кучи костей (иллюзия) ↔ одеяла и игрушки (правда)
            var pileLie = Register(BonePileMesh());
            var pileTrue = Register(BlanketMesh());
            Vector2[] piles = { new Vector2(-31f, 104f), new Vector2(-19f, 111f), new Vector2(-25f, 87f), new Vector2(-8f, 84f) };
            i = 0;
            foreach (var pp in piles)
                DualObject($"Pile_{++i}", root, Ground(pp, -0.02f), Quaternion.Euler(0f, R(0f, 360f), 0f),
                    pileLie, new[] { mBone, mRockDark }, pileTrue, new[] { mClothRed, mClothBlue, mWood, mClothYellow });

            // Тотемы с черепами (иллюзия) ↔ шесты с лентами и фонариком (правда)
            var totemLie = Register(TotemMesh(false));
            var totemTrue = Register(TotemMesh(true));
            Vector2[] totems = { new Vector2(-12f, 92f), new Vector2(-38f, 106f), new Vector2(-24f, 122f) };
            i = 0;
            foreach (var t in totems)
            {
                var go = DualObject($"Totem_{++i}", root, Ground(t, -0.2f), Quaternion.Euler(0f, R(0f, 360f), 0f),
                    totemLie, new[] { mWood, mBone, mGobEyes }, totemTrue, new[] { mWood, mClothYellow, mClothRed, mFlame });
                var col = go.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1.5f, 0f);
                col.radius = 0.25f;
                col.height = 3f;
            }

            // Нора, где спят младшие
            var burrow = new GameObject("Burrow").transform;
            burrow.SetParent(root, false);
            burrow.position = Ground(BurrowPos, -0.2f);
            burrow.rotation = Quaternion.LookRotation(Flat(Warren.c - BurrowPos));
            MeshObject("Mesh", burrow, Register(BurrowMesh()), new[] { mRock, mBlack, mRockDark }, true);
        }

        static Mesh TentMesh(bool truth)
        {
            var r = new System.Random(truth ? 11 : 12);
            var lp = new LowPolyMesh(truth ? 4 : 3);
            lp.Lathe(0, Matrix4x4.identity, new[] { 0f, 1.6f, 2.9f, 3.3f }, new[] { 2.1f, 1.5f, 0.35f, 0f }, 7, r, 0.08f);
            // Жерди из макушки
            for (int k = 0; k < 3; k++)
            {
                var d = Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0.35f, 1f, 0f);
                var m = Matrix4x4.TRS(new Vector3(0f, 2.5f, 0f), Quaternion.FromToRotation(Vector3.up, d), Vector3.one);
                lp.Lathe(2, m, new[] { 0f, 1.4f }, new[] { 0.05f, 0.04f }, 4);
            }
            if (!truth)
            {
                // Костяные шипы по кругу и череп на верхушке
                for (int k = 0; k < 7; k++)
                {
                    float a = k * Mathf.PI * 2f / 7f + 0.3f;
                    var dir = new Vector3(Mathf.Cos(a), 0.55f, Mathf.Sin(a)).normalized;
                    var m = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 1.9f, 0.4f, Mathf.Sin(a) * 1.9f), Quaternion.FromToRotation(Vector3.up, dir), Vector3.one);
                    lp.Lathe(1, m, new[] { 0f, 0.95f }, new[] { 0.09f, 0f }, 5);
                }
                Skull(lp, 1, new Vector3(0f, 3.35f, 0f), 0.28f);
            }
            else
            {
                // Полосы ткани и флажок
                lp.Lathe(1, Matrix4x4.identity, new[] { 0.9f, 1.2f }, new[] { 1.84f, 1.73f }, 7);
                lp.Lathe(3, Matrix4x4.identity, new[] { 0.25f, 0.45f }, new[] { 2.07f, 1.98f }, 7);
                lp.Box(3, Matrix4x4.TRS(new Vector3(0.45f, 3.75f, 0f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.5f, 0.3f, 0.03f));
                lp.Box(1, Matrix4x4.TRS(new Vector3(0f, 0.75f, 1.75f), Quaternion.Euler(-30f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(1f, 1.4f, 0.05f));
            }
            return lp.ToMesh(truth ? "Z1_TentTruth" : "Z1_TentLie");
        }

        static void Skull(LowPolyMesh lp, int sub, Vector3 p, float s)
        {
            var m = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * s);
            lp.Lathe(sub, m, new[] { -0.6f, -0.2f, 0.4f, 0.8f }, new[] { 0.45f, 0.75f, 0.7f, 0.2f }, 6, null, 0f, true);
            // Глазницы
            lp.Box(sub, Matrix4x4.TRS(p + new Vector3(0f, -0.15f * s, 0.6f * s), Quaternion.identity, Vector3.one * s), Vector3.zero, new Vector3(0.7f, 0.35f, 0.2f));
        }

        static Mesh FirepitMesh()
        {
            var r = new System.Random(21);
            var lp = new LowPolyMesh(3);
            for (int k = 0; k < 9; k++)
            {
                float a = k * Mathf.PI * 2f / 9f;
                var m = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 1.1f, -0.05f, Mathf.Sin(a) * 1.1f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), new Vector3(0.35f, 0.3f, 0.3f));
                lp.Lathe(0, m, new[] { 0f, 0.6f, 1f }, new[] { 1f, 0.85f, 0.3f }, 5, r, 0.2f);
            }
            for (int k = 0; k < 4; k++)
            {
                var m = Matrix4x4.TRS(new Vector3(0f, 0.15f, 0f), Quaternion.Euler(0f, k * 45f, 12f), Vector3.one);
                lp.Box(1, m, Vector3.zero, new Vector3(1.5f, 0.16f, 0.16f));
            }
            lp.Lathe(2, Matrix4x4.TRS(new Vector3(0f, 0.2f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.5f, 1.3f }, new[] { 0.45f, 0.35f, 0f }, 6, r, 0.15f);
            lp.Lathe(2, Matrix4x4.TRS(new Vector3(0.25f, 0.2f, 0.1f), Quaternion.Euler(0f, 0f, -10f), Vector3.one), new[] { 0f, 0.3f, 0.8f }, new[] { 0.25f, 0.18f, 0f }, 5, r, 0.15f);
            return lp.ToMesh("Z1_Firepit");
        }

        static Mesh BonePileMesh()
        {
            var r = new System.Random(31);
            var lp = new LowPolyMesh(2);
            for (int k = 0; k < 9; k++)
            {
                var m = Matrix4x4.TRS(new Vector3(Rr(r, -0.6f, 0.6f), 0.08f + k * 0.03f, Rr(r, -0.6f, 0.6f)),
                    Quaternion.Euler(Rr(r, -15f, 15f), Rr(r, 0f, 360f), Rr(r, -15f, 15f)), Vector3.one);
                lp.Box(0, m, Vector3.zero, new Vector3(0.09f, 0.09f, Rr(r, 0.5f, 0.9f)));
                lp.Box(0, m, new Vector3(0f, 0f, 0.45f), new Vector3(0.16f, 0.13f, 0.1f));
            }
            Skull(lp, 0, new Vector3(0.15f, 0.25f, 0.1f), 0.24f);
            Skull(lp, 0, new Vector3(-0.35f, 0.15f, -0.2f), 0.18f);
            lp.Lathe(1, Matrix4x4.identity, new[] { -0.1f, 0.05f }, new[] { 1f, 0.95f }, 7, r, 0.15f);
            return lp.ToMesh("Z1_BonePile");
        }

        static Mesh BlanketMesh()
        {
            var r = new System.Random(41);
            var lp = new LowPolyMesh(4);
            lp.Box(0, Matrix4x4.TRS(new Vector3(0f, 0.04f, 0f), Quaternion.Euler(0f, 8f, 0f), Vector3.one), Vector3.zero, new Vector3(1.7f, 0.08f, 1.2f));
            lp.Box(1, Matrix4x4.TRS(new Vector3(0.1f, 0.1f, 0.05f), Quaternion.Euler(0f, -12f, 0f), Vector3.one), Vector3.zero, new Vector3(1.2f, 0.06f, 0.9f));
            lp.Box(3, Matrix4x4.TRS(new Vector3(-0.55f, 0.18f, 0f), Quaternion.Euler(0f, 5f, 0f), Vector3.one), Vector3.zero, new Vector3(0.35f, 0.16f, 0.6f));
            // Игрушечный мячик и деревянная фигурка
            lp.Lathe(3, Matrix4x4.TRS(new Vector3(0.95f, 0f, 0.6f), Quaternion.identity, Vector3.one * 0.14f), new[] { 0f, 0.5f, 1.5f, 2f }, new[] { 0.6f, 1f, 1f, 0.6f }, 6, null, 0f, true);
            lp.Box(2, Matrix4x4.TRS(new Vector3(0.6f, 0.25f, -0.45f), Quaternion.Euler(0f, 30f, 0f), Vector3.one), Vector3.zero, new Vector3(0.12f, 0.3f, 0.1f));
            lp.Box(2, Matrix4x4.TRS(new Vector3(0.6f, 0.45f, -0.45f), Quaternion.Euler(0f, 30f, 0f), Vector3.one), Vector3.zero, new Vector3(0.14f, 0.12f, 0.12f));
            // Миска
            lp.Lathe(2, Matrix4x4.TRS(new Vector3(-0.9f, 0f, -0.55f), Quaternion.identity, Vector3.one), new[] { 0f, 0.12f }, new[] { 0.14f, 0.2f }, 7, r, 0f, true);
            return lp.ToMesh("Z1_Blanket");
        }

        static Mesh TotemMesh(bool truth)
        {
            var lp = new LowPolyMesh(truth ? 4 : 3);
            lp.Lathe(0, Matrix4x4.identity, new[] { 0f, 3.2f }, new[] { 0.12f, 0.09f }, 5);
            if (!truth)
            {
                Skull(lp, 1, new Vector3(0f, 3.4f, 0f), 0.35f);
                lp.Box(2, Matrix4x4.TRS(new Vector3(-0.08f, 3.38f, 0.22f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.07f, 0.05f, 0.05f));
                lp.Box(2, Matrix4x4.TRS(new Vector3(0.08f, 3.38f, 0.22f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.07f, 0.05f, 0.05f));
                lp.Box(1, Matrix4x4.TRS(new Vector3(0f, 2.6f, 0f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(1.2f, 0.1f, 0.1f));
                Skull(lp, 1, new Vector3(0.55f, 2.4f, 0f), 0.2f);
                Skull(lp, 1, new Vector3(-0.55f, 2.4f, 0f), 0.2f);
            }
            else
            {
                lp.Box(0, Matrix4x4.TRS(new Vector3(0f, 2.9f, 0f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(1f, 0.08f, 0.08f));
                for (int k = 0; k < 4; k++)
                    lp.Box(k % 2 == 0 ? 1 : 2, Matrix4x4.TRS(new Vector3(-0.4f + k * 0.27f, 2.5f, 0f), Quaternion.Euler(0f, 0f, (k - 1.5f) * 4f), Vector3.one), Vector3.zero, new Vector3(0.12f, 0.7f, 0.02f));
                lp.Lathe(3, Matrix4x4.TRS(new Vector3(0f, 3.25f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.15f, 0.3f }, new[] { 0.1f, 0.14f, 0f }, 6);
            }
            return lp.ToMesh(truth ? "Z1_TotemTruth" : "Z1_TotemLie");
        }

        static Mesh BurrowMesh()
        {
            var r = new System.Random(51);
            var lp = new LowPolyMesh(3);
            // Столбы арки
            for (int s = -1; s <= 1; s += 2)
            {
                var m = Matrix4x4.TRS(new Vector3(s * 2f, -0.3f, 0f), Quaternion.Euler(0f, 0f, -s * 6f), Vector3.one);
                lp.Lathe(0, m, new[] { 0f, 1.5f, 3.2f, 3.8f }, new[] { 0.9f, 0.75f, 0.65f, 0.5f }, 6, r, 0.15f);
            }
            lp.Box(2, Matrix4x4.TRS(new Vector3(0f, 3.5f, 0f), Quaternion.Euler(0f, 0f, 3f), Vector3.one), Vector3.zero, new Vector3(5.6f, 1f, 1.5f));
            // Тёмный провал
            lp.Lathe(1, Matrix4x4.TRS(new Vector3(0f, 1.6f, -0.6f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.6f, 1f, 1.9f)), new[] { 0f, 0.05f }, new[] { 1f, 1f }, 9, null, 0f, true);
            // Камни вокруг
            for (int k = 0; k < 6; k++)
            {
                float a = Rr(r, 0f, Mathf.PI);
                var m = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 3.2f, -0.2f, -Mathf.Abs(Mathf.Sin(a)) * 1.5f - 0.6f), Quaternion.Euler(0f, Rr(r, 0f, 360f), 0f), Vector3.one * Rr(r, 0.6f, 1.3f));
                lp.Lathe(2, m, new[] { 0f, 0.6f, 1f }, new[] { 1f, 0.8f, 0.3f }, 5, r, 0.25f);
            }
            return lp.ToMesh("Z1_Burrow");
        }

        // ---------------------------------------------------------------- факелы

        static Mesh torchMesh;

        /// <summary>
        /// Факелы вдоль всего пути, по очереди у левой и правой стены, плюс по кругу в залах.
        /// Каждый — свет с мерцанием, дым и искры; часть факелов отбрасывает тени.
        /// </summary>
        static void BuildTorches(Transform world)
        {
            var root = new GameObject("Torches").transform;
            root.SetParent(world, false);

            int side = 1;
            foreach (var c in Corridors)
            {
                var ab = c.b - c.a;
                float len = ab.magnitude;
                var dir = ab / len;
                var n = new Vector2(-dir.y, dir.x);
                for (float s = 5f; s < len - 2f; s += 10f)
                {
                    side = -side;
                    var center = c.a + dir * s;
                    var best = Vector2.zero;
                    bool found = false;
                    for (float off = 2.5f; off <= 9f; off += 0.5f)
                    {
                        var q = center + n * side * off;
                        if (Sdf(q.x, q.y) < -1.3f) { best = q; found = true; }
                        else break;
                    }
                    if (!found || ChasmSdf(best.x, best.y) < 3f || NearTorch(best, 6f)) continue;
                    Torch(root, Ground(best));
                }
            }

            foreach (var room in Rooms)
            {
                if (room.c == BridgeHall.c) continue;
                int count = Mathf.Max(2, Mathf.RoundToInt(room.r * Mathf.PI * 2f / 20f));
                for (int i = 0; i < count; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 2f / count + room.r;
                    var q = room.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (room.r - 3.5f);
                    if (Sdf(q.x, q.y) > -1f || ChasmSdf(q.x, q.y) < 3f || NearTorch(q, 7f) || PathDist(q.x, q.y) < 2f) continue;
                    if (Vector2.Distance(q, CagePos) < 6f || Vector2.Distance(q, ExitArchPos) < 6f) continue;
                    Torch(root, Ground(q));
                }
            }
            AssignTorchShadows();
        }

        /// <summary>
        /// Point-тень стоит 6 карт в атласе, поэтому тени только у факела, ближайшего к центру зала
        /// (у лагеря тени даёт костёр, у клетки — Сияющий). Итого 5 факелов + 2 источника на уровень.
        /// </summary>
        static void AssignTorchShadows()
        {
            foreach (var room in new[] { Entrance, FirstFight, BridgeHall, CageHall, ExitRoom })
            {
                Light best = null;
                float bestD = float.MaxValue;
                foreach (var l in torchLights)
                {
                    float d = Vector2.Distance(new Vector2(l.transform.position.x, l.transform.position.z), room.c);
                    if (d < bestD) { bestD = d; best = l; }
                }
                if (best != null) ShadowLight(best);
            }
        }

        static void ShadowLight(Light l)
        {
            l.shadows = LightShadows.Soft;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            var data = l.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = l.gameObject.AddComponent<UniversalAdditionalLightData>();
            var so = new SerializedObject(data); // свойство только для чтения, пишем сериализованное поле
            var tier = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
            if (tier != null) { tier.intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static bool NearTorch(Vector2 p, float dist)
        {
            foreach (var t in torchSpots)
                if (Vector2.Distance(p, new Vector2(t.x, t.z)) < dist) return true;
            return false;
        }

        static void Torch(Transform parent, Vector3 pos)
        {
            if (torchMesh == null || !generated.Contains(torchMesh))
            {
                var lp = new LowPolyMesh(4);
                lp.Lathe(0, Matrix4x4.identity, new[] { -0.3f, 2.25f }, new[] { 0.08f, 0.06f }, 5);
                lp.Box(3, Matrix4x4.TRS(new Vector3(0f, 1.9f, 0f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.2f, 0.12f, 0.2f));
                lp.Lathe(1, Matrix4x4.TRS(new Vector3(0f, 2.15f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.18f, 0.32f }, new[] { 0.09f, 0.22f, 0.26f }, 6);
                lp.Lathe(2, Matrix4x4.TRS(new Vector3(0f, 2.4f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.3f, 0.8f }, new[] { 0.2f, 0.16f, 0f }, 5, null, 0f, false, 0.5f);
                lp.Lathe(2, Matrix4x4.TRS(new Vector3(0.05f, 2.45f, 0.04f), Quaternion.Euler(0f, 36f, 8f), Vector3.one), new[] { 0f, 0.25f, 0.6f }, new[] { 0.12f, 0.09f, 0f }, 4);
                torchMesh = Register(lp.ToMesh("Z1_Torch"));
            }
            torchSpots.Add(pos);
            torchCount++;

            var t = new GameObject($"Torch_{torchCount}").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f));
            MeshObject("Mesh", t, torchMesh, new[] { mWood, mIron, mFlame, mWoodDark }, false);
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.2f, 0f);
            col.radius = 0.15f;
            col.height = 2.6f;

            var flameTop = pos + Vector3.up * 2.95f;
            var l = PointLight(t, "Light", flameTop + Vector3.up * 0.25f, TorchColor, 18f, 22f);
            // Тени только у каждого пятого факела: point-тень = 6 карт, держим бюджет под 60 FPS
            l.shadows = LightShadows.None; // тени раздаются в AssignTorchShadows
            torchLights.Add(l);
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            var flick = l.gameObject.AddComponent<TorchFlicker>();
            SetFloat(flick, "baseIntensity", 22f);
            SetFloat(flick, "flickerAmount", 3f);
            moodLights.Add(l);

            // Языки пламени
            var fire = new GameObject("Fire");
            fire.transform.SetParent(t, false);
            fire.transform.position = flameTop - Vector3.up * 0.45f;
            var fps = Particles(fire, pFire, 32f, 0.35f, 0.6f, 0.5f, 1.1f, 0.3f, 0.55f,
                new Color(1f, 0.8f, 0.45f), new Color(1f, 0.45f, 0.12f), -0.1f, 40, ShrinkCurve(), 0.6f);
            ShapeCone(fps, 6f, 0.1f);

            // Дым
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(t, false);
            smoke.transform.position = flameTop;
            var ps = Particles(smoke, pSmoke, 7f, 3f, 4.5f, 0.5f, 0.8f, 0.6f, 1.1f,
                new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.8f, 0.75f, 0.7f), -0.015f, 40, GrowCurve(), 0.4f);
            ShapeCone(ps, 8f, 0.1f);

            // Искры
            var sparks = new GameObject("Embers");
            sparks.transform.SetParent(t, false);
            sparks.transform.position = flameTop - Vector3.up * 0.2f;
            ps = Particles(sparks, pEmbers, 6f, 1.2f, 2.6f, 0.6f, 1.4f, 0.025f, 0.05f,
                new Color(1f, 0.75f, 0.35f), new Color(1f, 0.4f, 0.1f), -0.05f, 30, FadeCurve(), 0.8f);
            ShapeCone(ps, 18f, 0.1f);
        }

        // ---------------------------------------------------------------- предметы

        static void BuildItems(Transform world)
        {
            var root = new GameObject("Items").transform;
            root.SetParent(world, false);

            // Деревянная лошадка у входа в нору
            var toy = new GameObject("Item_Toy").transform;
            toy.SetParent(root, false);
            toy.position = Ground(ToyPos, 0.02f);
            toy.rotation = Quaternion.Euler(0f, 35f, 0f);
            var lp = new LowPolyMesh(2);
            lp.Box(0, Matrix4x4.TRS(new Vector3(0f, 0.3f, 0f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.16f, 0.15f, 0.42f));
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                lp.Box(0, Matrix4x4.TRS(new Vector3(sx * 0.06f, 0.15f, sz * 0.15f), Quaternion.Euler(sz * 8f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(0.05f, 0.22f, 0.05f));
            lp.Box(0, Matrix4x4.TRS(new Vector3(0f, 0.45f, 0.22f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(0.1f, 0.26f, 0.12f));
            lp.Box(1, Matrix4x4.TRS(new Vector3(0f, 0.5f, 0.15f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(0.04f, 0.22f, 0.06f));
            for (int sx = -1; sx <= 1; sx += 2)
                lp.Box(0, Matrix4x4.TRS(new Vector3(sx * 0.07f, 0.03f, 0f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.04f, 0.04f, 0.6f));
            MeshObject("Mesh", toy, Register(lp.ToMesh("Z1_ToyHorse")), new[] { mWood, mClothRed }, false);
            var tc = toy.gameObject.AddComponent<BoxCollider>();
            tc.center = new Vector3(0f, 0.3f, 0f);
            tc.size = new Vector3(0.6f, 0.6f, 0.8f);
            var toyItem = toy.gameObject.AddComponent<InspectItem>();
            SetString(toyItem, "itemId", "z1_toy");
            SetString(toyItem, "title", "Детская игрушка");
            SetString(toyItem, "illusionText", "Трофей их кровавых обрядов. Не трогайте.");
            SetString(toyItem, "truthText", "Деревянная лошадка. На боку неровно вырезано имя ребёнка.");
            SetString(toyItem, "narrationKey", "z1.item_toy");
            ItemGlint(toy);

            // Рисунок на каменной плите в тупике
            var drawing = new GameObject("Item_Drawing").transform;
            drawing.SetParent(root, false);
            drawing.position = Ground(DrawingPos);
            drawing.rotation = Quaternion.LookRotation(Flat(Nook.c - DrawingPos));
            lp = new LowPolyMesh(3);
            var slab = Matrix4x4.TRS(new Vector3(0f, 0.85f, 0f), Quaternion.Euler(-12f, 0f, 0f), Vector3.one);
            lp.Box(0, slab, Vector3.zero, new Vector3(1.5f, 1.8f, 0.25f));
            // Семья у костра: фигурки углём, костёр охрой
            float[] heights = { 0.55f, 0.5f, 0.32f, 0.28f, 0.22f };
            float[] xs = { -0.55f, -0.3f, 0.3f, 0.48f, 0.64f };
            for (int k = 0; k < xs.Length; k++)
            {
                float h = heights[k];
                lp.Box(1, slab, new Vector3(xs[k], -0.45f + h * 0.5f, 0.14f), new Vector3(0.035f, h, 0.02f));
                lp.Box(1, slab, new Vector3(xs[k], -0.45f + h + 0.06f, 0.14f), new Vector3(0.1f, 0.1f, 0.02f));
                lp.Box(1, slab, new Vector3(xs[k], -0.45f + h * 0.7f, 0.14f), new Vector3(h * 0.5f, 0.03f, 0.02f));
            }
            lp.Box(2, slab, new Vector3(0f, -0.4f, 0.14f), new Vector3(0.16f, 0.2f, 0.02f));
            lp.Box(2, slab, new Vector3(0.45f, 0.55f, 0.14f), new Vector3(0.18f, 0.18f, 0.02f));
            MeshObject("Mesh", drawing, Register(lp.ToMesh("Z1_DrawingSlab")), new[] { mRock, mCharcoal, mOchre }, false);
            var dc = drawing.gameObject.AddComponent<BoxCollider>();
            dc.center = new Vector3(0f, 0.85f, 0f);
            dc.size = new Vector3(1.5f, 1.8f, 0.5f);
            var drawItem = drawing.gameObject.AddComponent<InspectItem>();
            SetString(drawItem, "itemId", "z1_drawing");
            SetString(drawItem, "title", "Рисунок на камне");
            SetString(drawItem, "illusionText", "Знаки тёмного культа. Они призывают смерть.");
            SetString(drawItem, "truthText", "Детский рисунок углём: большая семья гоблинов у костра.");
            SetString(drawItem, "narrationKey", "z1.item_drawing");
            ItemGlint(drawing);
        }

        /// <summary>Слабый тёплый огонёк над предметом, чтобы его было видно в темноте.</summary>
        static void ItemGlint(Transform item)
        {
            var l = PointLight(item, "Glint", item.position + Vector3.up * 1.2f, new Color(1f, 0.8f, 0.45f), 3.5f, 1.4f);
            l.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- клетка Сияющего

        static Vector3 BuildCage(Transform world)
        {
            var root = new GameObject("ShiningCage").transform;
            root.SetParent(world, false);
            var basePos = Ground(CagePos, -0.1f);
            root.position = basePos;
            root.rotation = Quaternion.LookRotation(Flat(BridgeHall.c - CagePos));

            // Каменный постамент с пологими краями
            var lp = new LowPolyMesh(1);
            lp.Lathe(0, Matrix4x4.identity, new[] { -0.6f, 0.2f, 0.6f }, new[] { 5f, 4.2f, 3.6f }, 9, new System.Random(61), 0.05f);
            var ped = MeshObject("Pedestal", root, Register(lp.ToMesh("Z1_Pedestal")), new[] { mRockDark }, true);
            GameObjectUtility.SetStaticEditorFlags(ped, StaticEditorFlags.BatchingStatic);

            // Клетка
            const float barR = 1.3f, top = 3.6f, floorY = 0.6f;
            const int bars = 14;
            float doorAngle = 90f; // +z локально — к мосту
            lp = new LowPolyMesh(1);
            lp.Lathe(0, Matrix4x4.identity, new[] { floorY, floorY + 0.25f }, new[] { 1.55f, 1.5f }, 14, null, 0f, true);
            lp.Lathe(0, Matrix4x4.identity, new[] { top, top + 0.15f, top + 0.7f, top + 1f }, new[] { 1.5f, 1.45f, 0.7f, 0f }, 14);
            lp.Lathe(0, Matrix4x4.TRS(new Vector3(0f, top + 1f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new[] { -0.03f, 0.03f }, new[] { 0.25f, 0.25f }, 8);
            var doorBars = new List<float>();
            for (int k = 0; k < bars; k++)
            {
                float a = doorAngle + k * 360f / bars;
                float delta = Mathf.DeltaAngle(a, doorAngle);
                if (Mathf.Abs(delta) < 30f) { doorBars.Add(a); continue; }
                var p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * barR, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * barR);
                lp.Lathe(0, Matrix4x4.TRS(p, Quaternion.identity, Vector3.one), new[] { floorY + 0.2f, top + 0.05f }, new[] { 0.05f, 0.05f }, 4);
            }
            MeshObject("Bars", root, Register(lp.ToMesh("Z1_Cage")), new[] { mIron }, false);

            // Дверь на петле
            float hingeA = (doorAngle - 360f / bars * 1.5f) * Mathf.Deg2Rad;
            var hinge = new Vector3(Mathf.Cos(hingeA) * barR, 0f, Mathf.Sin(hingeA) * barR);
            var door = new GameObject("Door").transform;
            door.SetParent(root, false);
            door.localPosition = hinge;
            lp = new LowPolyMesh(1);
            foreach (var a in doorBars)
            {
                var p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * barR, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * barR) - hinge;
                lp.Lathe(0, Matrix4x4.TRS(p, Quaternion.identity, Vector3.one), new[] { floorY + 0.2f, top + 0.05f }, new[] { 0.05f, 0.05f }, 4);
            }
            // Поперечины двери
            var first = new Vector3(Mathf.Cos(doorBars[0] * Mathf.Deg2Rad) * barR, 0f, Mathf.Sin(doorBars[0] * Mathf.Deg2Rad) * barR) - hinge;
            var last = new Vector3(Mathf.Cos(doorBars[doorBars.Count - 1] * Mathf.Deg2Rad) * barR, 0f, Mathf.Sin(doorBars[doorBars.Count - 1] * Mathf.Deg2Rad) * barR) - hinge;
            foreach (float y in new[] { floorY + 0.6f, top - 0.4f })
            {
                var mid = (first + last) * 0.5f;
                var m = Matrix4x4.TRS(new Vector3(mid.x, y, mid.z), Quaternion.LookRotation(last - first), Vector3.one);
                lp.Box(0, m, Vector3.zero, new Vector3(0.08f, 0.08f, Vector3.Distance(first, last) + 0.2f));
            }
            MeshObject("Mesh", door, Register(lp.ToMesh("Z1_CageDoor")), new[] { mIron }, false);

            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 2.2f, 0f);
            col.radius = 1.5f;
            col.height = 3.6f;

            // Сияющий
            var shining = new GameObject("Shining").transform;
            shining.SetParent(root, false);
            shining.localPosition = new Vector3(0f, floorY + 0.5f, 0f);
            shining.localRotation = Quaternion.Euler(0f, 0f, 0f);
            MeshObject("Mesh", shining, Register(ShiningMesh()), new[] { mShining, mShiningWing }, false);
            var hover = shining.gameObject.AddComponent<Hover>();
            SetFloat(hover, "amplitude", 0.12f);
            SetFloat(hover, "speed", 1.3f);
            var glow = PointLight(shining, "Glow", shining.position + Vector3.up * 1.4f, new Color(1f, 0.78f, 0.45f), 14f, 18f);
            ShadowLight(glow);

            // Золотые пылинки вокруг клетки
            var motes = new GameObject("Motes");
            motes.transform.SetParent(shining, false);
            motes.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var mps = Particles(motes, pDust, 14f, 2.5f, 4f, 0.05f, 0.25f, 0.03f, 0.07f,
                new Color(1f, 0.85f, 0.5f), new Color(1f, 0.7f, 0.35f), -0.02f, 80, FadeCurve(), 0.3f);
            ShapeBox(mps, new Vector3(2.4f, 3f, 2.4f));

            var cage = root.gameObject.AddComponent<ShiningCage>();
            SetRef(cage, "door", door);
            SetRef(cage, "shining", shining);
            SetRef(cage, "shiningLight", glow);
            SetFloat(cage, "flyHeight", 9f);
            return basePos;
        }

        static Mesh ShiningMesh()
        {
            var lp = new LowPolyMesh(2);
            // Ниспадающее одеяние, руки сложены
            lp.Lathe(0, Matrix4x4.identity, new[] { 0f, 0.7f, 1.25f, 1.45f }, new[] { 0.42f, 0.3f, 0.2f, 0.12f }, 8, null, 0f, true, 0.5f);
            lp.Lathe(0, Matrix4x4.TRS(new Vector3(0f, 1.45f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.12f, 0.27f, 0.36f }, new[] { 0.09f, 0.14f, 0.12f, 0f }, 7);
            // Нимб
            lp.Lathe(1, Matrix4x4.TRS(new Vector3(0f, 1.95f, -0.08f), Quaternion.Euler(80f, 0f, 0f), Vector3.one), new[] { -0.01f, 0.01f }, new[] { 0.22f, 0.22f }, 10);
            // Крылья
            for (int s = -1; s <= 1; s += 2)
            {
                for (int f = 0; f < 3; f++)
                {
                    var m = Matrix4x4.TRS(new Vector3(s * (0.25f + f * 0.05f), 1.15f - f * 0.12f, -0.15f),
                        Quaternion.Euler(0f, s * (35f + f * 8f), s * (-55f + f * 22f)), Vector3.one);
                    lp.Box(1, m, new Vector3(0f, 0.45f, 0f), new Vector3(0.22f - f * 0.03f, 0.95f - f * 0.18f, 0.03f));
                }
            }
            return lp.ToMesh("Z1_Shining");
        }

        // ---------------------------------------------------------------- выход к вратам

        static void BuildExit(Transform world)
        {
            var root = new GameObject("ExitToGate").transform;
            root.SetParent(world, false);
            var face = Flat(ExitRoom.c - ExitArchPos);
            root.position = Ground(ExitArchPos, -0.3f);
            root.rotation = Quaternion.LookRotation(face);
            root.localScale = Vector3.one * 0.75f; // арка ниже свода прохода

            var r = new System.Random(71);
            var lp = new LowPolyMesh(2);
            for (int s = -1; s <= 1; s += 2)
            {
                var m = Matrix4x4.TRS(new Vector3(s * 3f, 0f, 0f), Quaternion.identity, Vector3.one);
                lp.Lathe(0, m, new[] { 0f, 1f, 5f, 7.2f }, new[] { 1.2f, 1f, 0.85f, 0.75f }, 6, r, 0.05f);
                for (int k = 0; k < 4; k++)
                    lp.Box(1, Matrix4x4.TRS(new Vector3(s * 3f, 1.6f + k * 1.3f, 0.86f), Quaternion.Euler(0f, 0f, 45f), Vector3.one), Vector3.zero, new Vector3(0.22f, 0.22f, 0.06f));
            }
            lp.Box(0, Matrix4x4.TRS(new Vector3(0f, 7.6f, 0f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(8.6f, 1.2f, 1.8f));
            lp.Box(1, Matrix4x4.TRS(new Vector3(0f, 7.6f, 0.92f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(3f, 0.12f, 0.04f));
            MeshObject("Arch", root, Register(lp.ToMesh("Z1_ExitArch")), new[] { mRock, mRune }, true);

            // Свечение за аркой: бирюзовый свет эльфийских врат
            var veil = new GameObject("Veil");
            veil.transform.SetParent(root, false);
            veil.transform.localPosition = new Vector3(0f, 0f, -0.4f);
            veil.AddComponent<MeshFilter>().sharedMesh = Register(GlowQuad("Z1_Veil", 5f, 7f, new Color(0.3f, 1f, 0.9f, 0.9f), new Color(0.3f, 1f, 0.9f, 0f)));
            veil.AddComponent<MeshRenderer>().sharedMaterial = gVeil;
            var block = veil.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, 3.5f, -0.5f);
            block.size = new Vector3(5f, 7f, 1f);

            var l = PointLight(root, "GateLight", root.position + root.forward * 2f + Vector3.up * 3f, new Color(0.3f, 1f, 0.9f), 26f, 5f);
            l.shadows = LightShadows.None;
        }

        static Mesh GlowQuad(string name, float w, float h, Color bottom, Color top)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3> { new Vector3(-w / 2f, 0f, 0f), new Vector3(w / 2f, 0f, 0f), new Vector3(w / 2f, h, 0f), new Vector3(-w / 2f, h, 0f) });
            mesh.SetColors(new List<Color> { bottom, bottom, top, top });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- гоблины

        static void BuildGoblins(Transform world)
        {
            var root = new GameObject("Enemies").transform;
            root.SetParent(world, false);
            var lie = Register(GoblinMesh(false));
            var truth = Register(GoblinMesh(true));
            var lieMats = new[] { mGobSkin, mGobEyes, mBone, mIron };
            var trueMats = new[] { mGobSkinTrue, mGobEyesTrue, mTunic, mIron };

            Goblin(root, "Goblin_01", "goblin_01", Goblin1Pos, Entrance.c, "z1.goblin_first_seen", lie, lieMats, truth, trueMats);
            Goblin(root, "Goblin_02", "goblin_02", Goblin2Pos, FirstFight.c, "", lie, lieMats, truth, trueMats);
            Goblin(root, "Goblin_03", "goblin_03", Goblin3Pos, BridgeHall.c, "", lie, lieMats, truth, trueMats);
        }

        static void Goblin(Transform parent, string name, string id, Vector2 pos, Vector2 lookAt, string seenKey,
            Mesh lie, Material[] lieMats, Mesh truth, Material[] trueMats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Ground(pos);
            go.transform.rotation = Quaternion.LookRotation(Flat(lookAt - pos));

            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.height = 1.6f;
            col.radius = 0.45f;

            var health = go.AddComponent<Health>();
            SetFloat(health, "max", 100f);
            var enemy = go.AddComponent<EnemyController>();
            SetString(enemy, "enemyId", id);
            SetString(enemy, "narrationKeyOnSeen", seenKey);
            go.AddComponent<HitFlash>();
            var iv = go.AddComponent<IllusionVisual>();

            var lieGo = MeshObject("Illusion", go.transform, lie, lieMats, false);
            var trueGo = MeshObject("Truth", go.transform, truth, trueMats, false);
            trueGo.SetActive(false);
            SetRef(iv, "illusionModel", lieGo);
            SetRef(iv, "truthModel", trueGo);

            // Красные глаза светятся в темноте
            var eyes = PointLight(lieGo.transform, "EyeGlow", go.transform.position + go.transform.forward * 0.5f + Vector3.up * 1.1f, new Color(1f, 0.08f, 0.04f), 1.6f, 0.5f);
            eyes.shadows = LightShadows.None;
        }

        /// <summary>Гоблин ~1.5 м, вперёд по +z. Иллюзия: тёмная тварь с шипами и красными глазами. Правда: подросток в рубахе.</summary>
        static Mesh GoblinMesh(bool truth)
        {
            var lp = new LowPolyMesh(4); // 0 кожа, 1 глаза, 2 кость/рубаха, 3 нож
            const int skin = 0, eye = 1, extra = 2, knife = 3;
            // Ноги и ступни
            for (int s = -1; s <= 1; s += 2)
            {
                lp.Lathe(skin, Matrix4x4.TRS(new Vector3(s * 0.14f, 0.05f, 0f), Quaternion.Euler(0f, 0f, s * 4f), Vector3.one), new[] { 0f, 0.25f, 0.5f }, new[] { 0.07f, 0.09f, 0.11f }, 5);
                lp.Box(skin, Matrix4x4.TRS(new Vector3(s * 0.15f, 0.04f, 0.06f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.13f, 0.08f, 0.24f));
            }
            // Сгорбленное тело
            var torso = Matrix4x4.TRS(new Vector3(0f, 0.45f, 0f), Quaternion.Euler(truth ? 10f : 24f, 0f, 0f), Vector3.one);
            lp.Lathe(skin, torso, new[] { 0f, 0.18f, 0.45f, 0.58f }, new[] { 0.2f, 0.25f, 0.24f, 0.13f }, 7, null, 0f, true);
            if (truth)
            {
                // Рубаха и пояс
                lp.Lathe(extra, torso, new[] { -0.02f, 0.2f, 0.47f }, new[] { 0.23f, 0.27f, 0.26f }, 7, null, 0f, true);
                lp.Lathe(knife, torso, new[] { 0.06f, 0.1f }, new[] { 0.235f, 0.235f }, 7);
            }
            // Голова
            float headTilt = truth ? -5f : 12f;
            var head = Matrix4x4.TRS(new Vector3(0f, 1.08f, truth ? 0.1f : 0.2f), Quaternion.Euler(headTilt, 0f, 0f), new Vector3(1f, 1f, 1.1f));
            lp.Lathe(skin, head, new[] { -0.17f, -0.05f, 0.1f, 0.2f }, new[] { 0.1f, 0.19f, 0.17f, 0.07f }, 7, null, 0f, true);
            // Нос-крюк
            lp.Lathe(skin, head * Matrix4x4.TRS(new Vector3(0f, -0.02f, 0.17f), Quaternion.Euler(70f, 0f, 0f), Vector3.one), new[] { 0f, 0.13f }, new[] { 0.045f, 0f }, 4);
            // Уши: у иллюзии торчат вверх, у правды — опущены
            for (int s = -1; s <= 1; s += 2)
            {
                var ear = head * Matrix4x4.TRS(new Vector3(s * 0.16f, 0.03f, -0.02f), Quaternion.Euler(0f, 0f, -s * (truth ? 100f : 62f)), new Vector3(1f, 1f, 0.4f));
                lp.Lathe(skin, ear, new[] { 0f, 0.36f }, new[] { 0.08f, 0f }, 4);
                lp.Box(eye, head, new Vector3(s * 0.065f, 0.03f, 0.16f), truth ? new Vector3(0.045f, 0.05f, 0.03f) : new Vector3(0.06f, 0.03f, 0.03f));
            }
            // Руки: одна сжимает нож
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = Matrix4x4.TRS(new Vector3(s * 0.27f, 0.96f, 0.1f), Quaternion.Euler(truth ? 150f : 140f, 0f, s * 12f), Vector3.one);
                lp.Lathe(skin, arm, new[] { 0f, 0.3f, 0.55f }, new[] { 0.065f, 0.055f, 0.06f }, 5, null, 0f, true);
                if (!truth)
                    for (int c = -1; c <= 1; c++)
                        lp.Lathe(extra, arm * Matrix4x4.TRS(new Vector3(c * 0.03f, 0.55f, 0.02f), Quaternion.Euler(15f, 0f, 0f), Vector3.one), new[] { 0f, 0.12f }, new[] { 0.02f, 0f }, 3);
                if (s == 1)
                    lp.Box(knife, arm * Matrix4x4.TRS(new Vector3(0f, 0.62f, 0.12f), Quaternion.Euler(-80f, 0f, 0f), Vector3.one), new Vector3(0f, 0.12f, 0f), new Vector3(0.03f, truth ? 0.26f : 0.36f, 0.06f));
            }
            if (!truth)
            {
                // Костяные шипы на спине и наплечники
                for (int k = 0; k < 4; k++)
                {
                    var m = torso * Matrix4x4.TRS(new Vector3(0f, 0.15f + k * 0.12f, -0.2f), Quaternion.Euler(-55f, 0f, 0f), Vector3.one);
                    lp.Lathe(extra, m, new[] { 0f, 0.18f + k * 0.03f }, new[] { 0.045f, 0f }, 4);
                }
                for (int s = -1; s <= 1; s += 2)
                    lp.Lathe(extra, Matrix4x4.TRS(new Vector3(s * 0.26f, 1f, 0.05f), Quaternion.Euler(0f, 0f, -s * 50f), Vector3.one), new[] { 0f, 0.2f }, new[] { 0.06f, 0f }, 4);
            }
            return lp.ToMesh(truth ? "Z1_GoblinTruth" : "Z1_GoblinLie");
        }

        // ---------------------------------------------------------------- игрок и атмосфера

        static Camera BuildPlayer(Transform[] safePoints)
        {
            var cam = CreatePlayer(Ground(SpawnPos, 1.2f));
            var player = GameObject.FindWithTag("Player");
            player.transform.rotation = Quaternion.LookRotation(Flat(new Vector2(-5f, 22f) - SpawnPos));

            // Око выдаёт старейшина у врат (N14): в зоне 1 Q не работает
            var eyeSo = new SerializedObject(player.GetComponent<EyeOfTruth>());
            eyeSo.FindProperty("unlocked").boolValue = false;
            eyeSo.ApplyModifiedPropertiesWithoutUndo();

            var fall = player.AddComponent<FallRespawn>();
            var so = new SerializedObject(fall);
            var arr = so.FindProperty("safePoints");
            arr.arraySize = safePoints.Length;
            for (int i = 0; i < safePoints.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = safePoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Слабый тёплый свет вокруг героя, чтобы между факелами не было чёрных провалов
            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(cam.transform, false);
            lantern.transform.localPosition = new Vector3(0.3f, -0.2f, 0.2f);
            lantern.type = LightType.Point;
            lantern.range = 7f;
            lantern.intensity = 0.6f;
            lantern.color = new Color(1f, 0.75f, 0.5f);
            lantern.shadows = LightShadows.None;

            // Пылинки в воздухе вокруг игрока
            var dust = new GameObject("Dust");
            dust.transform.SetParent(player.transform, false);
            dust.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var ps = Particles(dust, pDust, 30f, 6f, 10f, 0.02f, 0.12f, 0.015f, 0.035f,
                new Color(1f, 0.85f, 0.6f, 0.7f), new Color(1f, 0.7f, 0.45f, 0.4f), -0.003f, 400, FadeCurve(), 0.25f);
            ShapeBox(ps, new Vector3(22f, 6f, 22f));

            new GameObject("HUD").AddComponent<GameHUD>();
            return cam;
        }

        static void BuildAtmosphere(Camera cam)
        {
            // Тени в референсе не чёрные, а холодные сине-серые: сверху холод, снизу тёплый отсвет пола
            var illusionFog = new Color(0.075f, 0.06f, 0.05f);
            var illusionAmbient = new Color(0.15f, 0.16f, 0.21f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = illusionAmbient;
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.13f, 0.11f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.095f, 0.075f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = illusionFog;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = illusionFog;
            cam.farClipPlane = 250f;

            // В пещере нет солнца: убираем направленный свет сцены
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);

            // Мягкая «подсветка от стен» в каждом зале: имитация отражённого света факелов
            var fillRoot = new GameObject("BounceLights").transform;
            foreach (var room in Rooms)
            {
                var f = PointLight(fillRoot, "Bounce", Ground(room.c, Mathf.Min(6f, room.r * 0.35f)), new Color(1f, 0.72f, 0.48f), room.r * 1.4f + 8f, room.r * 0.9f);
                f.shadows = LightShadows.None;
            }

            // Бледный свет из расщелины за спиной — оттуда пришли друзья
            var crack = PointLight(null, "LightFromCrack", Ground(SpawnPos + new Vector2(0f, -8f), 4f), new Color(0.75f, 0.82f, 0.95f), 18f, 10f);
            crack.shadows = LightShadows.None;

            // Настроение: иллюзия — огненно-оранжевая, правда — золотистая и светлее
            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var so = new SerializedObject(mood);
            so.FindProperty("illusionAmbient").colorValue = illusionAmbient;
            so.FindProperty("illusionFog").colorValue = illusionFog;
            so.FindProperty("illusionTorch").colorValue = TorchColor;
            so.FindProperty("truthAmbient").colorValue = new Color(0.24f, 0.22f, 0.2f);
            so.FindProperty("truthFog").colorValue = new Color(0.12f, 0.09f, 0.06f);
            so.FindProperty("truthTorch").colorValue = new Color(1f, 0.8f, 0.5f);
            var list = so.FindProperty("torches");
            list.arraySize = moodLights.Count;
            for (int i = 0; i < moodLights.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = moodLights[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
            DynamicGI.UpdateEnvironment();
        }

        static VolumeProfile PostProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostPath);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.7f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.3f);
            color.contrast.Override(12f);
            color.saturation.Override(-4f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(2f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static void BuildNarration(Transform world, Vector3 cagePos)
        {
            var root = new GameObject("Narration").transform;
            root.SetParent(world, false);
            NarrationZone(root, "z1.enter", Ground(SpawnPos, 1f), 3f, true);
            NarrationZone(root, "z1.cage_shining", cagePos + Vector3.up, 12f, false);
            NarrationZone(root, "z1.exit", Ground(ExitRoom.c, 1f), 6f, false);
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

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material[] mats, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>Объект с двумя обликами: «Illusion» видно всегда, «Truth» — только с Оком.</summary>
        static GameObject DualObject(string name, Transform parent, Vector3 pos, Quaternion rot,
            Mesh lie, Material[] lieMats, Mesh truth, Material[] trueMats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var lieGo = MeshObject("Illusion", go.transform, lie, lieMats, false);
            var trueGo = MeshObject("Truth", go.transform, truth, trueMats, false);
            trueGo.SetActive(false);
            var iv = go.AddComponent<IllusionVisual>();
            SetRef(iv, "illusionModel", lieGo);
            SetRef(iv, "truthModel", trueGo);
            return go;
        }

        static Light PointLight(Transform parent, string name, Vector3 worldPos, Color color, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            if (parent != null) l.transform.SetParent(parent, true);
            l.transform.position = worldPos;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            return l;
        }

        static AnimationCurve FadeCurve() => new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));

        static AnimationCurve GrowCurve() => new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 2.6f));

        static AnimationCurve ShrinkCurve() => new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.75f), new Keyframe(1f, 0.1f));

        static readonly Color TorchColor = new Color(1f, 0.68f, 0.42f);

        static ParticleSystem Particles(GameObject go, Material mat, float rate, float lifeMin, float lifeMax,
            float speedMin, float speedMax, float sizeMin, float sizeMax, Color a, Color b, float gravity,
            int maxParticles, AnimationCurve sizeCurve, float noiseStrength)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = maxParticles;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorLife.color = new ParticleSystem.MinMaxGradient(g);

            var noise = ps.noise;
            noise.enabled = noiseStrength > 0f;
            noise.strength = noiseStrength;
            noise.frequency = 0.4f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static void ShapeCone(ParticleSystem ps, float angle, float radius)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        static void ShapeBox(ParticleSystem ps, Vector3 size)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
        }
    }
}
