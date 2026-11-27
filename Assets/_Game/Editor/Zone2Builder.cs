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
    /// Меню False Paradise → Build Zone 2 (Ogre Village). Каньон под открытым небом на закате:
    /// деревня огров (5 хижин, кузница с кузнецом, огороды, колодец, костёр, 2 стража),
    /// дорога в гору к крепости вождя (стены из кладки, башни, двор, тронный зал с фресками),
    /// лестница к свету (появляется после выбора).
    ///
    /// Иллюзия и правда (TASKS.md, раздел 4): Глас — кровавое небо, кости, шипы, красные знамёна;
    /// Око — мягкий закат, огороды, бельё на верёвках, цветы, игрушки. Переключают IllusionVisual
    /// (двойные объекты, небо и солнце) и TruthMood (свет солнца, туман, ambient).
    ///
    /// Схема (метры, x — восток, z — север): вход (-28,3) → деревня (27,0) r30 → дорога (44,24..56)
    /// → крепость (24..64, 58..96), тронный зал (32..56, 74..94) → лестница к свету на восток.
    /// docs/levels/LEVEL_MAP.png.
    /// </summary>
    public static class Zone2Builder
    {
        const string ScenePath = "Assets/_Game/Scenes/Zone2_OgreVillage.unity";
        const string ArtDir = "Assets/_Game/Art/Zone2";
        const string MatDir = ArtDir + "/Materials";
        const string MeshPath = ArtDir + "/Zone2_Meshes.asset";
        const string PostPath = ArtDir + "/Zone2_Post.asset";
        const int Seed = 2002;

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

        static readonly Room Entry = new Room(-24f, 3f, 6f, 0.3f);
        static readonly Room Village = new Room(27f, 0f, 30f, 0f);
        static readonly Room RoadMid = new Room(44f, 40f, 5f, 3.5f);
        static readonly Room Fortress = new Room(44f, 77f, 26f, FortH);
        static readonly Room[] Rooms = { Entry, Village, RoadMid, Fortress };

        static readonly Corr[] Corridors =
        {
            new Corr(-36f, 3f, 0f, 2f, 4.5f),
            new Corr(44f, 22f, 44f, 58f, 4.5f),
            new Corr(62f, 88f, 98f, 88f, 3.5f), // ущелье, по которому поднимается лестница к свету
        };

        const float FortH = 7f;
        const float FortMinX = 24f, FortMaxX = 64f, FortMinZ = 58f, FortMaxZ = 96f;
        const float HallMinX = 32f, HallMaxX = 56f, HallMinZ = 74f, HallMaxZ = 94f;

        static readonly Vector2 SpawnPos = new Vector2(-30f, 3f);
        static readonly Vector2[] Huts = { new Vector2(6f, 23f), new Vector2(31f, 24f), new Vector2(39f, -22f), new Vector2(10f, -21f), new Vector2(51f, -1f) };
        const float HutR = 4.2f;
        static readonly Vector2 ForgePos = new Vector2(14f, -6f);
        static readonly Vector2 GardenPos = new Vector2(27f, -10f);
        static readonly Vector2 Garden2Pos = new Vector2(38f, 13f);
        static readonly Vector2 WellPos = new Vector2(22f, 5f);
        static readonly Vector2 FirePos = new Vector2(30f, 3f);
        static readonly Vector2 Guard1Pos = new Vector2(28f, 10f);
        static readonly Vector2 Guard2Pos = new Vector2(42f, 16f);
        static readonly Vector2 SmithPos = new Vector2(16f, -8.5f);
        static readonly Vector2 ChiefPos = new Vector2(44f, 87f);
        static readonly Vector2 SonPos = new Vector2(45.5f, 90.5f);
        static readonly Quaternion SunRot = Quaternion.Euler(15f, 250f, 0f); // солнце садится на северо-востоке, по ходу игрока

        const float MinX = -44f, MaxX = 104f, MinZ = -48f, MaxZ = 116f;
        const float Cell = 2f;
        const int ChunkCells = 20;

        // ---------------------------------------------------------------- поля высот

        static float Noise(float x, float z, float freq, float offset) =>
            Mathf.PerlinNoise(x * freq + offset, z * freq + offset * 1.37f + 63.1f);

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        static float Sdf(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            foreach (var r in Rooms) d = Mathf.Min(d, Vector2.Distance(p, r.c) - r.r);
            foreach (var c in Corridors) d = Mathf.Min(d, SegDist(p, c.a, c.b) - c.r);
            // Двор крепости — прямоугольник с запасом
            float bx = Mathf.Max(FortMinX - 3f - x, x - FortMaxX - 3f), bz = Mathf.Max(FortMinZ - 3f - z, z - FortMaxZ - 3f);
            d = Mathf.Min(d, Mathf.Max(bx, bz));
            d += (Noise(x, z, 0.07f, 110f) - 0.5f) * 4f;
            d += (Noise(x, z, 0.23f, 230f) - 0.5f) * 1.2f;
            return d;
        }

        static float PathDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            foreach (var c in Corridors) d = Mathf.Min(d, SegDist(p, c.a, c.b));
            return d;
        }

        static bool InFort(float x, float z, float pad = 0f) =>
            x > FortMinX - pad && x < FortMaxX + pad && z > FortMinZ - pad && z < FortMaxZ + pad;

        static float FloorH(float x, float z)
        {
            var p = new Vector2(x, z);
            float sw = 0f, sh = 0f;
            foreach (var r in Rooms)
            {
                float d = Mathf.Max(0f, Vector2.Distance(p, r.c) - r.r);
                float w = 1f / (d * d + 20f);
                sw += w;
                sh += w * r.h;
            }
            float h = sh / sw + (Noise(x, z, 0.05f, 540f) - 0.5f) * 1.6f;
            // Двор крепости ровный
            float fx = Mathf.Max(FortMinX - x, x - FortMaxX), fz = Mathf.Max(FortMinZ - z, z - FortMaxZ);
            float k = 1f - Smooth(-1f, 4f, Mathf.Max(fx, fz));
            return Mathf.Lerp(h, FortH, k);
        }

        internal static float Height(float x, float z)
        {
            float h = FloorH(x, z) + (Noise(x, z, 0.4f, 780f) - 0.5f) * 0.25f;
            float d = Sdf(x, z);
            if (d > 0f)
            {
                float n = Noise(x, z, 0.1f, 960f);
                // Между деревней и крепостью — невысокий скальный гребень, чтобы крепость была видна с деревни
                float ridge = 1f - Smooth(7f, 15f, Mathf.Abs(z - 42f));
                ridge *= Smooth(4f, 12f, x) * (1f - Smooth(80f, 92f, x));
                float scale = Mathf.Lerp(1f, 0.16f, ridge);
                h += (Smooth(0f, 4f, d) * (6f + 5f * n) + Smooth(2f, 26f, d) * (24f + 18f * n)) * scale;
                h += (Noise(x, z, 0.21f, 1210f) - 0.5f) * 2.4f * Smooth(0f, 3f, d);
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

        // ---------------------------------------------------------------- состояние

        static readonly List<Mesh> generated = new List<Mesh>();
        static readonly List<Light> shadowLights = new List<Light>();
        static readonly List<Light> warmLights = new List<Light>();
        static readonly List<Vector2> reserved = new List<Vector2>();
        static System.Random rng;

        static Material mGround, mPath, mCliff, mCliffHigh, mFlag, mRock, mRockDark, mBlock;
        static Material mWood, mWoodDark, mPlank, mThatch, mIron, mDark, mWindow, mFlame;
        static Material mClothRed, mClothBone, mBone, mClothGreen, mClothOchre, mClothBlue, mClothWhite;
        static Material mLeaf, mLeafDark, mBark, mGrass, mCabbage, mPumpkin, mFlower, mThorn, mSoil, mGold;
        static Material mOgreSkin, mOgreSkinTrue, mOgreEyes, mOgreEyesTrue, mHide, mTunic, mApron;
        static Material mSkin, mSleeve, mSteel, mSkyLie, mSkyTrue, gSunLie, gSunTrue, gStair;
        static Material pFire, pSmoke, pEmbers, pDust, pHaze, pGold;

        [MenuItem("False Paradise/Build Zone 2 (Ogre Village)")]
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
            warmLights.Clear();
            reserved.Clear();
            rng = new System.Random(Seed);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            try
            {
                EditorUtility.DisplayProgressBar("Zone 2", "Материалы", 0.05f);
                CreateMaterials();
                var world = new GameObject("Zone2_OgreVillage").transform;
                ReserveSpots();

                EditorUtility.DisplayProgressBar("Zone 2", "Рельеф каньона", 0.15f);
                BuildTerrain(world);

                EditorUtility.DisplayProgressBar("Zone 2", "Хижины, кузница, огороды", 0.35f);
                BuildHuts(world);
                BuildForge(world);
                BuildGardens(world);
                BuildVillageProps(world);

                EditorUtility.DisplayProgressBar("Zone 2", "Крепость вождя", 0.55f);
                var choiceStairs = BuildFortress(world);

                EditorUtility.DisplayProgressBar("Zone 2", "Камни, деревья, трава", 0.65f);
                BuildNature(world);

                EditorUtility.DisplayProgressBar("Zone 2", "Огры и предметы", 0.75f);
                BuildOgres(world, choiceStairs);
                BuildItems(world);

                EditorUtility.DisplayProgressBar("Zone 2", "Небо, свет, атмосфера", 0.88f);
                var cam = BuildPlayer();
                var sun = BuildSky(world);
                BuildAtmosphere(cam, sun);
                BuildNarration(world);

                EditorUtility.DisplayProgressBar("Zone 2", "Сохранение", 0.96f);
                SaveMeshes();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
                Debug.Log($"[False Paradise] Зона 2 собрана: {ScenePath}. Мешей: {generated.Count}. Нажмите Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- материалы

        static void CreateMaterials()
        {
            TextureKit.BeginBuild();
            var rock = TextureKit.Rock();
            var ground = TextureKit.Ground();
            var planks = TextureKit.Planks();

            mGround = Lit("Z2_Ground", new Color(0.92f, 0.86f, 0.8f));
            mPath = Lit("Z2_Path", new Color(0.66f, 0.55f, 0.45f));
            mCliff = Lit("Z2_Cliff", new Color(0.68f, 0.56f, 0.46f));
            mCliffHigh = Lit("Z2_CliffHigh", new Color(0.58f, 0.48f, 0.42f));
            mFlag = Lit("Z2_Courtyard", new Color(0.66f, 0.6f, 0.54f), 0f, 0.12f);
            mRock = Lit("Z2_Rock", new Color(0.66f, 0.58f, 0.5f));
            mRockDark = Lit("Z2_RockDark", new Color(0.44f, 0.38f, 0.34f));
            mBlock = Lit("Z2_Block", new Color(0.7f, 0.62f, 0.54f));
            TextureKit.Apply(mGround, ground, 0.55f, 1f);
            TextureKit.Apply(mPath, rock, 2.5f, 0.7f);
            TextureKit.Apply(mCliff, rock, 0.8f, 1.3f);
            TextureKit.Apply(mCliffHigh, rock, 0.8f, 1.3f);
            TextureKit.Apply(mFlag, TextureKit.Flagstone(), 0.5f, 0.8f);
            TextureKit.Apply(mRock, rock, 1.8f, 1.2f);
            TextureKit.Apply(mRockDark, rock, 1.8f, 1.2f);
            TextureKit.Apply(mBlock, rock, 2.2f, 1.4f);

            mWood = Lit("Z2_Wood", new Color(0.5f, 0.36f, 0.24f));
            mWoodDark = Lit("Z2_WoodDark", new Color(0.3f, 0.21f, 0.14f));
            mPlank = Lit("Z2_Plank", new Color(0.62f, 0.48f, 0.34f));
            mThatch = Lit("Z2_Thatch", new Color(0.78f, 0.64f, 0.42f));
            TextureKit.Apply(mWood, planks, 1.6f, 0.8f);
            TextureKit.Apply(mWoodDark, planks, 1.6f, 0.8f);
            TextureKit.Apply(mPlank, planks, 1.2f, 0.9f);
            TextureKit.Apply(mThatch, TextureKit.Thatch(), 1.4f, 1.2f);
            mIron = Lit("Z2_Iron", new Color(0.22f, 0.21f, 0.22f), 0f, 0.5f, 0.6f);
            RuinKit.Reflective(mIron);
            mDark = Lit("Z2_Dark", new Color(0.03f, 0.025f, 0.02f));
            mWindow = Lit("Z2_Window", new Color(1f, 0.55f, 0.22f), 1.15f);
            mFlame = Emissive("Z2_Flame", new Color(1f, 0.5f, 0.12f), 1.8f);
            mGold = Lit("Z2_StairGold", new Color(1f, 0.82f, 0.5f), 2.2f, 0.6f, 0.3f);

            mClothRed = Lit("Z2_ClothRed", new Color(0.55f, 0.06f, 0.05f));
            mClothBone = Lit("Z2_ClothBone", new Color(0.7f, 0.64f, 0.52f));
            mBone = Lit("Z2_Bone", new Color(0.76f, 0.7f, 0.58f));
            mClothGreen = Lit("Z2_ClothGreen", new Color(0.32f, 0.5f, 0.3f));
            mClothOchre = Lit("Z2_ClothOchre", new Color(0.82f, 0.6f, 0.26f));
            mClothBlue = Lit("Z2_ClothBlue", new Color(0.3f, 0.42f, 0.62f));
            mClothWhite = Lit("Z2_ClothWhite", new Color(0.88f, 0.85f, 0.78f));
            mLeaf = Lit("Z2_Leaf", new Color(0.36f, 0.46f, 0.24f), 0f, 0.2f);
            mLeafDark = Lit("Z2_LeafDark", new Color(0.24f, 0.32f, 0.18f), 0f, 0.2f);
            mBark = Lit("Z2_Bark", new Color(0.4f, 0.32f, 0.24f));
            TextureKit.Apply(mBark, rock, 3f, 0.9f);
            mGrass = Lit("Z2_Grass", new Color(0.52f, 0.6f, 0.34f), 0f, 0.2f);
            mGrass.SetFloat("_Cull", (float)CullMode.Off);
            mCabbage = Lit("Z2_Cabbage", new Color(0.46f, 0.66f, 0.34f), 0f, 0.35f);
            mPumpkin = Lit("Z2_Pumpkin", new Color(0.9f, 0.5f, 0.15f), 0f, 0.3f);
            mFlower = Lit("Z2_Flower", new Color(0.95f, 0.75f, 0.85f), 0.2f, 0.2f);
            mThorn = Lit("Z2_Thorn", new Color(0.22f, 0.05f, 0.05f));
            mSoil = Lit("Z2_Soil", new Color(0.36f, 0.26f, 0.2f));
            TextureKit.Apply(mSoil, ground, 1.2f, 1.2f);

            mOgreSkin = Lit("Z2_OgreSkin", new Color(0.2f, 0.22f, 0.18f));
            mOgreSkinTrue = Lit("Z2_OgreSkinTrue", new Color(0.56f, 0.6f, 0.44f));
            mOgreEyes = Lit("Z2_OgreEyes", new Color(1f, 0.08f, 0.03f), 7f);
            mOgreEyesTrue = Lit("Z2_OgreEyesTrue", new Color(0.08f, 0.06f, 0.05f));
            mHide = Lit("Z2_Hide", new Color(0.22f, 0.15f, 0.11f));
            mTunic = Lit("Z2_Tunic", new Color(0.55f, 0.42f, 0.28f));
            mApron = Lit("Z2_Apron", new Color(0.36f, 0.26f, 0.18f));

            mSkin = Lit("Z2_HeroSkin", new Color(0.78f, 0.58f, 0.46f));
            mSleeve = Lit("Z2_HeroSleeve", new Color(0.3f, 0.26f, 0.32f));
            mSteel = Lit("Z2_Steel", new Color(0.42f, 0.44f, 0.48f), 0f, 0.45f, 0.7f);

            mSkyLie = SkyMat("Z2_SkyIllusion");
            mSkyTrue = SkyMat("Z2_SkyTruth");
            gSunLie = Glow("Z2_SunIllusion", Color.white, 1.2f);
            gSunTrue = Glow("Z2_SunTruth", Color.white, 1.4f);
            gStair = Glow("Z2_StairBeam", new Color(1f, 0.85f, 0.55f), 0.5f);

            pFire = Soft("Z2_Fire", new Color(1f, 0.45f, 0.12f), 2.2f, true, 1.4f);
            pSmoke = Soft("Z2_Smoke", new Color(0.4f, 0.36f, 0.34f, 0.12f), 1f, false, 2.6f);
            pEmbers = Soft("Z2_Embers", new Color(1f, 0.55f, 0.18f), 4f, true, 2f);
            pDust = Soft("Z2_Dust", new Color(1f, 0.85f, 0.65f), 1.2f, true, 2f);
            pHaze = Soft("Z2_Haze", Color.white, 1f, false, 2.2f);
            pGold = Soft("Z2_GoldMotes", new Color(1f, 0.8f, 0.45f), 3f, true, 2f);
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
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_EnvironmentReflections", 0f);
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
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            var hdr = new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f);
            m.color = hdr;
            m.SetColor("_BaseColor", hdr);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Glow(string name, Color c, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("FalseParadise/UnlitGlow") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SkyMat(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("FalseParadise/SkyDome") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Soft(string name, Color c, float intensity, bool additive, float softness)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("FalseParadise/ParticleSoft") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
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

        // ---------------------------------------------------------------- рельеф

        static void ReserveSpots()
        {
            foreach (var h in Huts) reserved.Add(h);
            reserved.Add(ForgePos);
            reserved.Add(GardenPos);
            reserved.Add(Garden2Pos);
            reserved.Add(WellPos);
            reserved.Add(FirePos);
        }

        static bool Reserved(float x, float z, float pad)
        {
            var p = new Vector2(x, z);
            foreach (var r in reserved) if (Vector2.Distance(p, r) < 7f + pad) return true;
            if (InFort(x, z, 4f)) return true;
            if (Vector2.Distance(p, SpawnPos) < 4f) return true;
            return false;
        }

        /// <summary>Утоптанные тропы деревни: от входа к костру, к хижинам, к кузнице и к дороге в гору.</summary>
        static float TrailDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = SegDist(p, new Vector2(-30f, 3f), FirePos);
            d = Mathf.Min(d, SegDist(p, FirePos, new Vector2(44f, 30f)));
            foreach (var h in Huts) d = Mathf.Min(d, SegDist(p, FirePos, h));
            d = Mathf.Min(d, SegDist(p, FirePos, ForgePos));
            return d + (Noise(x, z, 0.3f, 1500f) - 0.5f) * 1.2f;
        }

        static void BuildTerrain(Transform world)
        {
            var root = new GameObject("Terrain").transform;
            root.SetParent(world, false);
            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell) + 1;
            int nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell) + 1;
            var pos = new Vector3[nx, nz];
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = MinX + ix * Cell + (Hash(ix, iz, 1) - 0.5f) * Cell * 0.45f;
                float z = MinZ + iz * Cell + (Hash(ix, iz, 2) - 0.5f) * Cell * 0.45f;
                pos[ix, iz] = new Vector3(x, Height(x, z), z);
            }
            // 0 земля с травой, 1 тропа, 2 скала, 3 верх скал, 4 мощёный двор
            var mats = new[] { mGround, mPath, mCliff, mCliffHigh, mFlag };
            var chunks = new Dictionary<Vector2Int, LowPolyMesh>();
            for (int ix = 0; ix < nx - 1; ix++)
            for (int iz = 0; iz < nz - 1; iz++)
            {
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
                var go = Z.MeshObject($"Chunk_{kv.Key.x}_{kv.Key.y}", root, Register(kv.Value.ToMesh($"Z2_Terrain_{kv.Key.x}_{kv.Key.y}")), mats, true);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static Vector3 HeightNormal(float x, float z)
        {
            const float e = 1.5f;
            float dx = Height(x + e, z) - Height(x - e, z);
            float dz = Height(x, z + e) - Height(x, z - e);
            var n = new Vector3(-dx, 2f * e, -dz).normalized;
            return n.y < 0.3f ? Vector3.up : n;
        }

        static void TerrainTri(LowPolyMesh lp, Vector3 a, Vector3 b, Vector3 c)
        {
            var center = (a + b + c) / 3f;
            var n = Vector3.Cross(b - a, c - a);
            if (n.y < 0f) n = -n;
            n.Normalize();
            float rel = center.y - FloorH(center.x, center.z);
            int sub;
            if (n.y > 0.78f && rel < 1.5f)
            {
                if (InFort(center.x, center.z, -0.5f)) sub = 4;
                else if (TrailDist(center.x, center.z) < 1.6f || PathDist(center.x, center.z) < 2.2f) sub = 1;
                else sub = 0;
            }
            else sub = rel > 14f ? 3 : 2;
            if (sub <= 1 || sub == 4)
            {
                lp.TriSmooth(sub, a, b, c, HeightNormal(a.x, a.z), HeightNormal(b.x, b.z), HeightNormal(c.x, c.z));
                return;
            }
            // Скалы: нормали наполовину сглажены — меньше «граней», больше камня
            var f = Vector3.Cross(b - a, c - a).normalized;
            if (f.y < -0.2f) f = -f;
            Vector3 N(Vector3 v) => Vector3.Lerp(f, HeightNormalRaw(v.x, v.z), 0.7f).normalized;
            lp.TriSmooth(sub, a, b, c, N(a), N(b), N(c));
        }

        static Vector3 HeightNormalRaw(float x, float z)
        {
            const float e = 1.5f;
            float dx = Height(x + e, z) - Height(x - e, z);
            float dz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        // ---------------------------------------------------------------- хижины огров

        static void BuildHuts(Transform world)
        {
            var root = new GameObject("Huts").transform;
            root.SetParent(world, false);
            var hutMats = new[] { mRock, mPlank, mThatch, mDark, mWindow, mWood };
            for (int i = 0; i < Huts.Length; i++)
            {
                var c = Huts[i];
                var t = new GameObject($"Hut_{i + 1}").transform;
                t.SetParent(root, false);
                t.position = Ground(c, -0.3f);
                t.rotation = Quaternion.LookRotation(Flat(FirePos - c));
                var r = new System.Random(100 + i);
                Z.MeshObject("Hut", t, Register(HutMesh(r, i)), hutMats, true);

                // Окна светятся изнутри, из трубы — дым
                var l = Z.PointLight(t, "WindowLight", t.TransformPoint(new Vector3(0f, 2.2f, HutR + 0.8f)), new Color(1f, 0.62f, 0.32f), 7f, 3.5f);
                l.shadows = LightShadows.None;
                warmLights.Add(l);
                var smoke = new GameObject("ChimneySmoke");
                smoke.transform.SetParent(t, false);
                smoke.transform.localPosition = new Vector3(-1.2f, 7.2f, -0.6f);
                var ps = Z.Particles(smoke, pSmoke, 3f, 6f, 9f, 0.5f, 0.9f, 1f, 2f,
                    new Color(1f, 1f, 1f, 0.7f), new Color(0.85f, 0.85f, 0.85f, 0.5f), -0.01f, 40, Z.GrowCurve(), 0.5f);
                Z.ShapeCone(ps, 6f, 0.2f);

                // Иллюзия: черепа на кольях и красная шкура у двери. Правда: цветы, скамья, бельё
                var lie = new LowPolyMesh(4); // 0 дерево, 1 кость, 2 красная ткань, 3 шкура
                var tru = new LowPolyMesh(6); // 0 дерево, 1 белая ткань, 2 охра, 3 синяя, 4 цветы, 5 зелень
                for (int s = -1; s <= 1; s += 2)
                {
                    var stake = Matrix4x4.TRS(new Vector3(s * 2.2f, 0f, HutR + 1.6f), Quaternion.Euler(R(-6f, 6f), 0f, R(-6f, 6f)), Vector3.one);
                    lie.Lathe(0, stake, new[] { 0f, 2.6f, 3f }, new[] { 0.07f, 0.06f, 0f }, 5);
                    SkullAt(lie, 1, stake.MultiplyPoint3x4(new Vector3(0f, 2.75f, 0f)), 0.32f);
                    // Правда: глиняные горшки с цветами
                    var pot = Matrix4x4.TRS(new Vector3(s * 1.9f, 0f, HutR + 0.9f), Quaternion.identity, Vector3.one);
                    tru.Lathe(2, pot, new[] { 0f, 0.35f, 0.45f }, new[] { 0.22f, 0.28f, 0.26f }, 7, null, 0f, true);
                    for (int k = 0; k < 5; k++)
                        tru.Lathe(k % 2 == 0 ? 4 : 5, pot * Matrix4x4.TRS(new Vector3(R(-0.15f, 0.15f), 0.45f, R(-0.15f, 0.15f)), Quaternion.Euler(R(-20f, 20f), 0f, R(-20f, 20f)), Vector3.one),
                            new[] { 0f, 0.3f, 0.42f }, new[] { 0.02f, 0.03f, k % 2 == 0 ? 0.09f : 0.05f }, 5, null, 0f, false);
                }
                lie.Box(3, Matrix4x4.TRS(new Vector3(0f, 3.4f, HutR + 0.5f), Quaternion.Euler(-8f, 0f, R(-4f, 4f)), Vector3.one), Vector3.zero, new Vector3(2.6f, 1.6f, 0.05f));
                lie.Box(2, Matrix4x4.TRS(new Vector3(1.6f, 2.2f, HutR + 0.45f), Quaternion.Euler(0f, 0f, 5f), Vector3.one), Vector3.zero, new Vector3(0.6f, 1.8f, 0.04f));
                // Скамья и бельё на верёвке между шестами
                tru.Box(0, Matrix4x4.TRS(new Vector3(-2.8f, 0.5f, HutR + 0.6f), Quaternion.Euler(0f, 90f, 0f), Vector3.one), Vector3.zero, new Vector3(2f, 0.12f, 0.5f));
                for (int s = -1; s <= 1; s += 2)
                    tru.Box(0, Matrix4x4.TRS(new Vector3(-2.8f, 0.25f, HutR + 0.6f + s * 0.8f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.4f, 0.5f, 0.12f));
                var lineA = new Vector3(HutR + 1.2f, 0f, 1.5f);
                var lineB = new Vector3(HutR + 1.2f, 0f, -3.5f);
                foreach (var pp in new[] { lineA, lineB })
                    tru.Lathe(0, Matrix4x4.TRS(pp, Quaternion.identity, Vector3.one), new[] { 0f, 2.7f }, new[] { 0.06f, 0.05f }, 5);
                tru.Box(0, Matrix4x4.identity, new Vector3(HutR + 1.2f, 2.55f, -1f), new Vector3(0.02f, 0.02f, 5f));
                for (int k = 0; k < 4; k++)
                    tru.Box(1 + k % 3, Matrix4x4.TRS(new Vector3(HutR + 1.2f, 2.1f, 0.8f - k * 1.15f), Quaternion.Euler(R(-4f, 4f), 0f, R(-4f, 4f)), Vector3.one), Vector3.zero, new Vector3(0.04f, 0.9f, 0.8f));
                var dual = DualObject("Dressing", t, t.position, t.rotation,
                    Register(lie.ToMesh($"Z2_HutLie_{i}")), new[] { mWoodDark, mBone, mClothRed, mHide },
                    Register(tru.ToMesh($"Z2_HutTrue_{i}")), new[] { mWood, mClothWhite, mClothOchre, mClothBlue, mFlower, mLeaf });
                dual.transform.localPosition = Vector3.zero;
                dual.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Хижина огра (вперёд по +z, ~9 м в диаметре): каменный цоколь, стены из досок, конус соломенной
        /// крыши со свесом, большая дверь, два окна с тёплым светом, балки, труба.
        /// Подмеши: 0 камень, 1 доски, 2 солома, 3 тёмный проём, 4 окно, 5 балки.
        /// </summary>
        static Mesh HutMesh(System.Random r, int i)
        {
            var lp = new LowPolyMesh(6);
            float R0 = HutR * (0.92f + (float)r.NextDouble() * 0.16f);
            lp.Lathe(0, Matrix4x4.identity, new[] { -0.4f, 0.6f, 1.2f }, new[] { R0 + 0.35f, R0 + 0.25f, R0 + 0.1f }, 12, r, 0.08f, true);
            lp.Lathe(1, Matrix4x4.identity, new[] { 1.2f, 3.6f }, new[] { R0, R0 - 0.1f }, 12, r, 0.02f);
            // Вертикальные столбы каркаса
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f) * Mathf.PI / 3f;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R0 + 0.02f);
                lp.Box(5, Matrix4x4.TRS(p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one), new Vector3(0f, 2.4f, 0f), new Vector3(0.28f, 2.6f, 0.28f));
            }
            // Крыша: свес, излом, острие
            float H = 7.4f + (float)r.NextDouble() * 0.8f;
            lp.Lathe(2, Matrix4x4.TRS(new Vector3(0f, 3.3f, 0f), Quaternion.Euler(0f, 15f, 0f), Vector3.one),
                new[] { 0f, 0.45f, 1.6f, H - 3.3f - 0.4f, H - 3.3f }, new[] { R0 + 1.4f, R0 + 1.1f, R0 * 0.62f, 0.35f, 0f }, 12, r, 0.06f, true);
            lp.Lathe(5, Matrix4x4.TRS(new Vector3(0f, H - 0.5f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 1.1f }, new[] { 0.12f, 0.06f }, 5);
            // Дверь (огры большие) с рамой
            lp.Box(3, Matrix4x4.identity, new Vector3(0f, 1.95f, R0 - 0.02f), new Vector3(2f, 2.9f, 0.3f));
            lp.Box(5, Matrix4x4.identity, new Vector3(0f, 3.45f, R0 + 0.12f), new Vector3(2.6f, 0.3f, 0.3f));
            for (int s = -1; s <= 1; s += 2)
            {
                lp.Box(5, Matrix4x4.identity, new Vector3(s * 1.15f, 1.95f, R0 + 0.12f), new Vector3(0.26f, 3f, 0.3f));
                // Окна по бокам
                float a = Mathf.PI * 0.5f + s * 0.75f;
                var wp = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R0 + 0.01f);
                var wm = Matrix4x4.TRS(wp, Quaternion.LookRotation(new Vector3(wp.x, 0f, wp.z)), Vector3.one);
                lp.Box(4, wm, new Vector3(0f, 2.4f, 0f), new Vector3(0.8f, 0.8f, 0.12f));
                lp.Box(5, wm, new Vector3(0f, 2.4f, 0.08f), new Vector3(0.08f, 0.85f, 0.06f));
                lp.Box(5, wm, new Vector3(0f, 2.4f, 0.08f), new Vector3(0.85f, 0.08f, 0.06f));
            }
            // Труба
            lp.Box(0, Matrix4x4.TRS(new Vector3(-1.2f, 5.4f, -0.6f), Quaternion.identity, Vector3.one), Vector3.zero, new Vector3(0.8f, 3f, 0.8f));
            return lp.ToMesh($"Z2_Hut_{i}");
        }

        static void SkullAt(LowPolyMesh lp, int sub, Vector3 p, float s)
        {
            var m = Matrix4x4.TRS(p, Quaternion.Euler(R(-10f, 10f), R(-30f, 30f), 0f), Vector3.one * s);
            lp.Lathe(sub, m, new[] { -0.4f, 0f, 0.45f, 0.75f }, new[] { 0.3f, 0.5f, 0.48f, 0.15f }, 7, null, 0f, true);
            lp.Box(sub, m, new Vector3(0f, -0.45f, 0.18f), new Vector3(0.55f, 0.3f, 0.5f));
            lp.Box(0, m, new Vector3(-0.17f, 0.05f, 0.45f), new Vector3(0.18f, 0.18f, 0.08f));
            lp.Box(0, m, new Vector3(0.17f, 0.05f, 0.45f), new Vector3(0.18f, 0.18f, 0.08f));
        }

        // ---------------------------------------------------------------- кузница

        static void BuildForge(Transform world)
        {
            var t = new GameObject("Forge").transform;
            t.SetParent(world, false);
            t.position = Ground(ForgePos, -0.1f);
            t.rotation = Quaternion.LookRotation(Flat(FirePos - ForgePos));
            var lp = new LowPolyMesh(5); // 0 камень, 1 дерево, 2 доски крыши, 3 железо, 4 тёмный
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                lp.Box(1, Matrix4x4.identity, new Vector3(sx * 4f, 2.2f + (sz < 0 ? 0.6f : 0f), sz * 3.5f), new Vector3(0.4f, 4.4f + (sz < 0 ? 1.2f : 0f), 0.4f));
            for (int sz = -1; sz <= 1; sz += 2)
                lp.Box(1, Matrix4x4.identity, new Vector3(0f, sz < 0 ? 5.1f : 4.5f, sz * 3.5f), new Vector3(8.6f, 0.35f, 0.4f));
            // Односкатная крыша
            lp.Box(2, Matrix4x4.TRS(new Vector3(0f, 5.1f, 0f), Quaternion.Euler(-9f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(9.6f, 0.2f, 8.6f));
            // Горн из камня с огнём, кирпичная труба
            lp.Box(0, Matrix4x4.identity, new Vector3(-2.2f, 0.6f, -2.2f), new Vector3(2.8f, 1.2f, 2.4f));
            lp.Box(4, Matrix4x4.identity, new Vector3(-2.2f, 1.22f, -2.2f), new Vector3(1.8f, 0.06f, 1.5f));
            lp.Box(0, Matrix4x4.identity, new Vector3(-2.2f, 4f, -3.1f), new Vector3(1.4f, 5.6f, 0.9f));
            // Наковальня на пне
            lp.Lathe(1, Matrix4x4.TRS(new Vector3(0.8f, 0f, 0.2f), Quaternion.identity, Vector3.one), new[] { 0f, 0.75f }, new[] { 0.55f, 0.5f }, 8, null, 0f, false);
            lp.Box(3, Matrix4x4.identity, new Vector3(0.8f, 0.95f, 0.2f), new Vector3(1.1f, 0.4f, 0.45f));
            lp.Lathe(3, Matrix4x4.TRS(new Vector3(1.35f, 1.05f, 0.2f), Quaternion.Euler(0f, 0f, -90f), Vector3.one), new[] { 0f, 0.4f }, new[] { 0.16f, 0f }, 6);
            // Бочка с водой, стойка с инструментом, мех
            lp.Lathe(1, Matrix4x4.TRS(new Vector3(3f, 0f, -2.4f), Quaternion.identity, Vector3.one), new[] { 0f, 0.5f, 1f }, new[] { 0.45f, 0.52f, 0.45f }, 10, null, 0f, false);
            lp.Lathe(4, Matrix4x4.TRS(new Vector3(3f, 0.92f, -2.4f), Quaternion.identity, Vector3.one), new[] { 0f, 0.02f }, new[] { 0.42f, 0.42f }, 10);
            lp.Box(1, Matrix4x4.identity, new Vector3(3.7f, 1.4f, 1.8f), new Vector3(0.12f, 2.6f, 2.4f));
            for (int k = 0; k < 4; k++)
            {
                lp.Box(1, Matrix4x4.identity, new Vector3(3.55f, 1.1f, 0.9f + k * 0.6f), new Vector3(0.06f, 1.3f, 0.06f));
                lp.Box(3, Matrix4x4.identity, new Vector3(3.55f, 1.85f, 0.9f + k * 0.6f), new Vector3(0.12f, 0.3f, 0.2f));
            }
            lp.Box(4, Matrix4x4.TRS(new Vector3(-3.7f, 0.9f, -1f), Quaternion.Euler(0f, 0f, 12f), Vector3.one), Vector3.zero, new Vector3(1.2f, 0.5f, 1f));
            // Ограда, которую чинит кузнец
            for (int k = 0; k < 4; k++)
                lp.Box(1, Matrix4x4.TRS(new Vector3(-1.5f + k * 1.2f, 0.7f, 5.2f), Quaternion.Euler(0f, 0f, R(-6f, 6f)), Vector3.one), Vector3.zero, new Vector3(0.14f, 1.4f, 0.14f));
            lp.Box(1, Matrix4x4.TRS(new Vector3(0.3f, 1.05f, 5.25f), Quaternion.Euler(0f, 0f, 2f), Vector3.one), Vector3.zero, new Vector3(4f, 0.14f, 0.06f));
            lp.Box(1, Matrix4x4.TRS(new Vector3(1.1f, 0.15f, 6f), Quaternion.Euler(0f, 30f, 0f), Vector3.one), Vector3.zero, new Vector3(2.2f, 0.12f, 0.12f));
            Z.MeshObject("Forge", t, Register(lp.ToMesh("Z2_Forge")), new[] { mRock, mWood, mPlank, mIron, mDark }, true);

            var hearth = t.TransformPoint(new Vector3(-2.2f, 1.4f, -2.2f));
            var l = Z.PointLight(t, "ForgeFire", hearth + Vector3.up * 0.8f, new Color(1f, 0.55f, 0.25f), 14f, 18f);
            ShadowLight(l);
            var fl = l.gameObject.AddComponent<TorchFlicker>();
            SetFloat(fl, "baseIntensity", 18f);
            SetFloat(fl, "flickerAmount", 3f);
            warmLights.Add(l);
            Fire(t, hearth, 0.6f);
        }

        static void Fire(Transform parent, Vector3 pos, float scale)
        {
            var fire = new GameObject("Fire");
            fire.transform.SetParent(parent, false);
            fire.transform.position = pos;
            var ps = Z.Particles(fire, pFire, 45f * scale + 10f, 0.4f, 0.8f, 0.6f, 1.4f, 0.4f * scale + 0.2f, 0.8f * scale + 0.3f,
                new Color(1f, 0.8f, 0.45f), new Color(1f, 0.45f, 0.12f), -0.1f, 70, Z.ShrinkCurve(), 0.5f);
            Z.ShapeCone(ps, 10f, 0.4f * scale);
            var embers = new GameObject("Embers");
            embers.transform.SetParent(parent, false);
            embers.transform.position = pos + Vector3.up * 0.4f;
            ps = Z.Particles(embers, pEmbers, 16f, 1.2f, 3f, 0.8f, 2f, 0.02f, 0.05f,
                new Color(1f, 0.75f, 0.35f), new Color(1f, 0.4f, 0.1f), -0.05f, 40, Z.FadeCurve(), 0.9f);
            Z.ShapeCone(ps, 18f, 0.3f);
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(parent, false);
            smoke.transform.position = pos + Vector3.up * 1f;
            ps = Z.Particles(smoke, pSmoke, 6f, 4f, 6f, 0.5f, 0.9f, 0.9f, 1.6f,
                new Color(1f, 1f, 1f, 0.8f), new Color(0.85f, 0.85f, 0.85f, 0.6f), -0.012f, 40, Z.GrowCurve(), 0.4f);
            Z.ShapeCone(ps, 8f, 0.3f);
        }

        // ---------------------------------------------------------------- огороды

        static void BuildGardens(Transform world)
        {
            var root = new GameObject("Gardens").transform;
            root.SetParent(world, false);
            Garden(root, GardenPos, 10f, 8f, 0);
            Garden(root, Garden2Pos, 7f, 5f, 1);
        }

        /// <summary>Иллюзия: голая земля, кости, колючие кусты, частокол. Правда: грядки с капустой и тыквами, ограда.</summary>
        static void Garden(Transform root, Vector2 c, float w, float d, int idx)
        {
            var lie = new LowPolyMesh(4); // 0 почва, 1 кость, 2 колючки, 3 тёмное дерево
            var tru = new LowPolyMesh(5); // 0 почва, 1 капуста, 2 тыква, 3 дерево, 4 листья
            int rows = Mathf.RoundToInt(d / 1.6f);
            for (int k = 0; k < rows; k++)
            {
                float z = -d * 0.5f + 0.8f + k * 1.6f;
                lie.Box(0, Matrix4x4.identity, new Vector3(0f, 0.05f, z), new Vector3(w - 1f, 0.25f, 1f));
                tru.Box(0, Matrix4x4.identity, new Vector3(0f, 0.08f, z), new Vector3(w - 1f, 0.3f, 1f));
                for (float x = -w * 0.5f + 1f; x < w * 0.5f - 0.6f; x += 0.75f)
                {
                    var p = new Vector3(x + R(-0.1f, 0.1f), 0.22f, z + R(-0.1f, 0.1f));
                    if (k % 3 == 2)
                    {
                        if (R01() < 0.5f)
                            tru.Lathe(2, Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(1f, 0.7f, 1f)), new[] { -0.05f, 0.15f, 0.36f, 0.42f }, new[] { 0.2f, 0.36f, 0.3f, 0.06f }, 9, null, 0f, true);
                    }
                    else
                    {
                        var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(0.8f, 1.1f));
                        tru.Lathe(1, m, new[] { 0f, 0.15f, 0.3f, 0.38f }, new[] { 0.18f, 0.27f, 0.22f, 0.05f }, 7, rng, 0.15f, true);
                        for (int l = 0; l < 3; l++)
                            tru.Box(4, m * Matrix4x4.TRS(new Vector3(0f, 0.12f, 0f), Quaternion.Euler(-35f, l * 120f, 0f), Vector3.one), new Vector3(0f, 0f, 0.22f), new Vector3(0.22f, 0.02f, 0.3f));
                    }
                    if (R01() < 0.12f) BonesAt(lie, 1, p);
                    if (R01() < 0.18f)
                        for (int s = 0; s < 4; s++)
                            lie.Lathe(2, Matrix4x4.TRS(p, Quaternion.Euler(R(-40f, 40f), R(0f, 360f), R(-40f, 40f)), Vector3.one), new[] { 0f, R(0.4f, 0.9f) }, new[] { 0.04f, 0f }, 4);
                }
            }
            // Ограда (правда) / частокол с остриями (иллюзия)
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float len = alongX ? w : d;
                float off = (side % 2 == 0 ? -1f : 1f) * (alongX ? d : w) * 0.5f;
                for (float t = -len * 0.5f; t <= len * 0.5f + 0.01f; t += 1.25f)
                {
                    if (side == 0 && Mathf.Abs(t) < 0.8f) continue; // калитка
                    var p = alongX ? new Vector3(t, 0f, off) : new Vector3(off, 0f, t);
                    tru.Box(3, Matrix4x4.TRS(p, Quaternion.Euler(0f, 0f, R(-4f, 4f)), Vector3.one), new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.1f, 0.12f));
                    lie.Lathe(3, Matrix4x4.TRS(p, Quaternion.Euler(R(-12f, 12f), 0f, R(-12f, 12f)), Vector3.one), new[] { 0f, 1.6f, 2.1f }, new[] { 0.1f, 0.09f, 0f }, 5);
                }
                var a = alongX ? new Vector3(0f, 0.75f, off) : new Vector3(off, 0.75f, 0f);
                var size = alongX ? new Vector3(len, 0.08f, 0.05f) : new Vector3(0.05f, 0.08f, len);
                tru.Box(3, Matrix4x4.identity, a, size);
                tru.Box(3, Matrix4x4.identity, a + Vector3.down * 0.4f, size);
            }
            var rot = Quaternion.LookRotation(Flat(FirePos - c));
            DualObject($"Garden_{idx}", root, Ground(c, -0.05f), rot,
                Register(lie.ToMesh($"Z2_GardenLie_{idx}")), new[] { mSoil, mBone, mThorn, mWoodDark },
                Register(tru.ToMesh($"Z2_GardenTrue_{idx}")), new[] { mSoil, mCabbage, mPumpkin, mWood, mLeaf });
        }

        static void BonesAt(LowPolyMesh lp, int sub, Vector3 p)
        {
            for (int i = 0; i < 3; i++)
            {
                var m = Matrix4x4.TRS(p + new Vector3(R(-0.3f, 0.3f), 0.04f, R(-0.3f, 0.3f)), Quaternion.Euler(90f, R(0f, 360f), 0f), Vector3.one);
                float len = R(0.4f, 0.8f);
                lp.Lathe(sub, m, new[] { -len * 0.5f, len * 0.5f }, new[] { 0.035f, 0.035f }, 5);
                lp.Lathe(sub, m * Matrix4x4.Translate(new Vector3(0f, -len * 0.5f, 0f)), new[] { -0.05f, 0.05f }, new[] { 0.07f, 0.07f }, 5);
                lp.Lathe(sub, m * Matrix4x4.Translate(new Vector3(0f, len * 0.5f, 0f)), new[] { -0.05f, 0.05f }, new[] { 0.07f, 0.07f }, 5);
            }
        }

        // ---------------------------------------------------------------- колодец, костёр, фонари, дрова

        static void BuildVillageProps(Transform world)
        {
            var root = new GameObject("VillageProps").transform;
            root.SetParent(world, false);
            var lp = new LowPolyMesh(4); // 0 камень, 1 дерево, 2 доски, 3 тёмный
            // Колодец
            var w = Ground(WellPos);
            var wm = Matrix4x4.TRS(w, Quaternion.identity, Vector3.one);
            lp.Lathe(0, wm, new[] { -0.3f, 0.9f, 1f }, new[] { 1.2f, 1.15f, 1.25f }, 12, rng, 0.06f, false);
            lp.Lathe(3, wm, new[] { 0.95f, 0.97f }, new[] { 0.95f, 0.95f }, 12);
            for (int s = -1; s <= 1; s += 2) lp.Box(1, wm, new Vector3(s * 1.1f, 1.7f, 0f), new Vector3(0.18f, 2.2f, 0.18f));
            lp.Box(1, wm, new Vector3(0f, 2.3f, 0f), new Vector3(2.4f, 0.12f, 0.12f));
            for (int s = -1; s <= 1; s += 2)
                lp.Box(2, wm * Matrix4x4.TRS(new Vector3(0f, 3f, s * 0.6f), Quaternion.Euler(s * 38f, 0f, 0f), Vector3.one), Vector3.zero, new Vector3(2.8f, 0.1f, 1.6f));
            lp.Lathe(1, wm * Matrix4x4.Translate(new Vector3(0.3f, 1.6f, 0f)), new[] { 0f, 0.4f }, new[] { 0.2f, 0.24f }, 8, null, 0f, true);
            // Костёр общины с бревнами-скамьями
            var f = Ground(FirePos);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                lp.Lathe(0, Matrix4x4.TRS(f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.1f, Quaternion.Euler(R(-10f, 10f), R(0f, 360f), 0f), Vector3.one * R(0.3f, 0.42f)),
                    new[] { -0.3f, 0.3f, 0.7f }, new[] { 0.8f, 1f, 0.4f }, 6, rng, 0.2f);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + 0.4f;
                var p = f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4f;
                p.y = Height(p.x, p.z) + 0.3f;
                lp.Lathe(1, Matrix4x4.TRS(p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 90f), Vector3.one), new[] { -1.4f, 1.4f }, new[] { 0.32f, 0.3f }, 8, null, 0f, true);
            }
            for (int i = 0; i < 4; i++)
                lp.Lathe(1, Matrix4x4.TRS(f + Vector3.up * 0.25f, Quaternion.Euler(70f, i * 90f + 20f, 0f), Vector3.one), new[] { -0.6f, 0.6f }, new[] { 0.12f, 0.1f }, 6);
            // Поленницы у хижин
            foreach (var h in Huts)
            {
                var dir = Flat(FirePos - h);
                var side = Vector3.Cross(Vector3.up, dir);
                var p = Ground(h) - side * (HutR + 1.2f) - dir * 1f;
                var m = Matrix4x4.TRS(p, Quaternion.LookRotation(dir), Vector3.one);
                for (int row = 0; row < 3; row++)
                for (int k = 0; k < 5 - row; k++)
                    lp.Lathe(1, m * Matrix4x4.TRS(new Vector3((k - (4 - row) * 0.5f) * 0.32f, 0.16f + row * 0.28f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new[] { -0.6f, 0.6f }, new[] { 0.15f, 0.15f }, 6, null, 0f, true);
            }
            var go = Z.MeshObject("Props", root, Register(lp.ToMesh("Z2_VillageProps")), new[] { mRock, mWood, mPlank, mDark }, true);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            var fl = Z.PointLight(root, "CampFire", f + Vector3.up * 1.4f, new Color(1f, 0.6f, 0.3f), 16f, 16f);
            ShadowLight(fl);
            var flick = fl.gameObject.AddComponent<TorchFlicker>();
            SetFloat(flick, "baseIntensity", 16f);
            SetFloat(flick, "flickerAmount", 3f);
            warmLights.Add(fl);
            Fire(root, f + Vector3.up * 0.2f, 1f);

            // Фонари на столбах вдоль троп
            var lanternSpots = new[] { new Vector2(-8f, 5f), new Vector2(8f, 9f), new Vector2(18f, -14f), new Vector2(36f, -6f), new Vector2(44f, 6f), new Vector2(40f, 24f), new Vector2(20f, 18f) };
            foreach (var s in lanternSpots) Lantern(root, Ground(s));

            // Иллюзия: красные военные знамёна на въезде; правда: гирлянда флажков
            var lie = new LowPolyMesh(3);
            var tru = new LowPolyMesh(4);
            foreach (var bp in new[] { new Vector2(0f, 7f), new Vector2(0f, -3f) })
            {
                var p = Ground(bp) - Ground(new Vector2(0f, 2f));
                var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, 90f, 0f), Vector3.one);
                lie.Lathe(0, m, new[] { 0f, 5.5f }, new[] { 0.08f, 0.06f }, 5);
                lie.Box(1, m, new Vector3(0f, 3.8f, 0.08f), new Vector3(1.3f, 2.8f, 0.04f));
                SkullAt(lie, 2, m.MultiplyPoint3x4(new Vector3(0f, 5.7f, 0f)), 0.3f);
                tru.Lathe(0, m, new[] { 0f, 5.5f }, new[] { 0.08f, 0.06f }, 5);
            }
            var a0 = new Vector3(0f, 5f, 5f);
            var b0 = new Vector3(0f, 5f, -5f);
            for (int k = 0; k < 12; k++)
            {
                float t = (k + 0.5f) / 12f;
                var p = Vector3.Lerp(a0, b0, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.9f;
                tru.Lathe(1 + k % 3, Matrix4x4.TRS(p, Quaternion.Euler(180f, 0f, 0f), new Vector3(1f, 1f, 0.15f)), new[] { 0f, 0.5f }, new[] { 0.22f, 0f }, 3);
            }
            DualObject("EntryBanners", root, Ground(new Vector2(0f, 2f)), Quaternion.identity,
                Register(lie.ToMesh("Z2_EntryLie")), new[] { mWoodDark, mClothRed, mBone },
                Register(tru.ToMesh("Z2_EntryTrue")), new[] { mWood, mClothOchre, mClothGreen, mClothBlue });
        }

        static Mesh lanternMesh;

        static void Lantern(Transform parent, Vector3 pos)
        {
            if (lanternMesh == null || !generated.Contains(lanternMesh))
            {
                var lp = new LowPolyMesh(3);
                lp.Lathe(0, Matrix4x4.identity, new[] { -0.3f, 3f }, new[] { 0.08f, 0.06f }, 5);
                lp.Box(0, Matrix4x4.identity, new Vector3(0.35f, 2.85f, 0f), new Vector3(0.8f, 0.06f, 0.06f));
                lp.Box(1, Matrix4x4.identity, new Vector3(0.65f, 2.35f, 0f), new Vector3(0.34f, 0.46f, 0.34f));
                lp.Box(2, Matrix4x4.identity, new Vector3(0.65f, 2.35f, 0f), new Vector3(0.26f, 0.36f, 0.36f));
                lp.Box(2, Matrix4x4.identity, new Vector3(0.65f, 2.35f, 0f), new Vector3(0.36f, 0.36f, 0.26f));
                lanternMesh = Register(lp.ToMesh("Z2_Lantern"));
            }
            var t = new GameObject("Lantern").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            Z.MeshObject("Mesh", t, lanternMesh, new[] { mWood, mIron, mWindow }, false);
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.4f, 0f);
            col.radius = 0.12f;
            col.height = 3f;
            var l = Z.PointLight(t, "Light", t.TransformPoint(new Vector3(0.65f, 2.3f, 0f)), new Color(1f, 0.66f, 0.36f), 9f, 4f);
            l.shadows = LightShadows.None;
            warmLights.Add(l);
        }

        // ---------------------------------------------------------------- крепость вождя

        /// <summary>Крепость: стены из блоков с зубцами, 4 башни, ворота, двор, тронный зал. Возвращает лестницу к свету (выключена).</summary>
        static GameObject BuildFortress(Transform world)
        {
            var root = new GameObject("Fortress").transform;
            root.SetParent(world, false);
            var r = new System.Random(909);
            var stone = new LowPolyMesh(1);
            var wood = new LowPolyMesh(3); // 0 дерево, 1 доски, 2 железо
            float y = FortH;
            Vector3 P(float x, float z) => new Vector3(x, y, z);

            // Стены по периметру (проёмы: ворота на юге, выход к лестнице на востоке)
            void WallLine(Vector3 a, Vector3 b, Vector3 inward, float h)
            {
                RuinKit.Wall(stone, 0, a, b, inward, h, 1.6f, r, false);
                // Зубцы и боевой ход
                var dir = (b - a);
                dir.y = 0f;
                float len = dir.magnitude;
                dir /= len;
                var rot = Quaternion.LookRotation(inward);
                for (float t = 0.6f; t < len - 0.4f; t += 1.6f)
                {
                    var p = a + dir * t;
                    stone.Box(0, Matrix4x4.TRS(new Vector3(p.x, y + h + 0.95f, p.z), rot, Vector3.one), Vector3.zero, new Vector3(0.9f, 0.9f, 1.4f));
                }
            }
            float wallH = 6.5f;
            WallLine(P(FortMinX, FortMinZ), P(40f, FortMinZ), Vector3.forward, wallH);
            WallLine(P(48f, FortMinZ), P(FortMaxX, FortMinZ), Vector3.forward, wallH);
            WallLine(P(FortMinX, FortMaxZ), P(FortMaxX, FortMaxZ), Vector3.back, wallH);
            WallLine(P(FortMinX, FortMinZ), P(FortMinX, FortMaxZ), Vector3.right, wallH);
            WallLine(P(FortMaxX, FortMinZ), P(FortMaxX, 84.5f), Vector3.left, wallH);
            WallLine(P(FortMaxX, 91.5f), P(FortMaxX, FortMaxZ), Vector3.left, wallH);

            // Башни по углам и у ворот
            foreach (var tp in new[] { new Vector2(FortMinX, FortMinZ), new Vector2(FortMaxX, FortMinZ), new Vector2(FortMinX, FortMaxZ), new Vector2(FortMaxX, FortMaxZ), new Vector2(39f, FortMinZ - 0.4f), new Vector2(49f, FortMinZ - 0.4f) })
            {
                bool gate = tp.y < FortMinZ;
                Tower(stone, wood, new Vector3(tp.x, y, tp.y), gate ? 2.2f : 3.2f, gate ? 10f : 12f, r);
            }
            // Арка ворот и открытые створки
            stone.Box(0, Matrix4x4.identity, new Vector3(44f, y + 7.2f, FortMinZ), new Vector3(10f, 2f, 2.2f));
            for (int s = -1; s <= 1; s += 2)
            {
                var hinge = new Vector3(44f + s * 3.9f, y, FortMinZ + 0.3f);
                var m = Matrix4x4.TRS(hinge, Quaternion.Euler(0f, s * 100f, 0f), Vector3.one);
                wood.Box(1, m, new Vector3(-s * 1.95f, 3.1f, 0f), new Vector3(3.9f, 6.2f, 0.3f));
                for (int k = 0; k < 4; k++) wood.Box(2, m, new Vector3(-s * 1.95f, 0.8f + k * 1.6f, 0.18f), new Vector3(3.9f, 0.18f, 0.06f));
            }

            // Тронный зал: стены, вход на юге, двускатная крыша на балках
            float hallH = 8f;
            float hy = y;
            Vector3 H(float x, float z) => new Vector3(x, hy, z);
            RuinKit.Wall(stone, 0, H(HallMinX, HallMinZ), H(42f, HallMinZ), Vector3.forward, hallH, 1.2f, r, false);
            RuinKit.Wall(stone, 0, H(46f, HallMinZ), H(HallMaxX, HallMinZ), Vector3.forward, hallH, 1.2f, r, false);
            RuinKit.Wall(stone, 0, H(HallMinX, HallMaxZ), H(HallMaxX, HallMaxZ), Vector3.back, hallH, 1.2f, r, false);
            RuinKit.Wall(stone, 0, H(HallMinX, HallMinZ), H(HallMinX, HallMaxZ), Vector3.right, hallH, 1.2f, r, false);
            RuinKit.Wall(stone, 0, H(HallMaxX, HallMinZ), H(HallMaxX, HallMaxZ), Vector3.left, hallH, 1.2f, r, false);
            stone.Box(0, Matrix4x4.identity, new Vector3(44f, hy + 5.6f, HallMinZ), new Vector3(5.2f, 1.2f, 1.6f));
            float top = hy + hallH + 0.7f;
            float midX = (HallMinX + HallMaxX) * 0.5f, halfW = (HallMaxX - HallMinX) * 0.5f + 1f;
            float hallLen = HallMaxZ - HallMinZ + 2f, midZ = (HallMinZ + HallMaxZ) * 0.5f;
            float rise = 4.5f;
            float slope = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
            float slant = Mathf.Sqrt(rise * rise + halfW * halfW);
            for (int s = -1; s <= 1; s += 2)
                wood.Box(1, Matrix4x4.TRS(new Vector3(midX + s * halfW * 0.5f, top + rise * 0.5f, midZ), Quaternion.Euler(0f, 0f, -s * slope), Vector3.one), Vector3.zero, new Vector3(slant + 0.6f, 0.3f, hallLen));
            for (float z = HallMinZ + 1f; z < HallMaxZ; z += 3.2f)
                wood.Box(0, Matrix4x4.identity, new Vector3(midX, top - 0.3f, z), new Vector3(HallMaxX - HallMinX, 0.4f, 0.4f));
            wood.Box(0, Matrix4x4.identity, new Vector3(midX, top + rise - 0.1f, midZ), new Vector3(0.5f, 0.5f, hallLen));
            // Фронтоны: треугольники кладки под скатами на обоих торцах (двусторонние)
            foreach (var gz in new[] { HallMinZ, HallMaxZ })
            {
                var g0 = new Vector3(HallMinX - 0.6f, top - 0.8f, gz);
                var g1 = new Vector3(HallMaxX + 0.6f, top - 0.8f, gz);
                var g2 = new Vector3(midX, top + rise - 0.2f, gz);
                stone.Tri(0, g0, g1, g2, Vector3.forward);
                stone.Tri(0, g0, g1, g2, Vector3.back);
            }
            // Колонны зала
            for (int s = -1; s <= 1; s += 2)
            for (int k = 0; k < 3; k++)
                RuinKit.Pilaster(stone, 0, new Vector3(midX + s * 6f, hy + 0.4f, HallMinZ + 4.5f + k * 5.5f), Vector3.back, hallH + 0.6f, 1f, r);
            // Трон
            var throne = Matrix4x4.TRS(new Vector3(44f, hy, 91f), Quaternion.identity, Vector3.one);
            stone.Box(0, throne, new Vector3(0f, 0.4f, 0f), new Vector3(5f, 0.8f, 3.6f));
            stone.Box(0, throne, new Vector3(0f, 1.5f, 0.3f), new Vector3(2.6f, 1.4f, 1.8f));
            stone.Box(0, throne, new Vector3(0f, 3.4f, 1.1f), new Vector3(2.8f, 4.2f, 0.6f));

            var sgo = Z.MeshObject("Masonry", root, Register(stone.ToMesh("Z2_FortressStone")), new[] { mBlock }, true);
            GameObjectUtility.SetStaticEditorFlags(sgo, StaticEditorFlags.BatchingStatic);
            var wgo = Z.MeshObject("Timber", root, Register(wood.ToMesh("Z2_FortressTimber")), new[] { mWood, mPlank, mIron }, true);
            GameObjectUtility.SetStaticEditorFlags(wgo, StaticEditorFlags.BatchingStatic);

            // Фрески истории народа на стенах зала (видны всегда; рисунки — часть правды вождя)
            var panel = Register(RuinKit.Panel("Z2_MuralPanel", 1f, 1f));
            int mi = 0;
            foreach (var (x, inward) in new[] { (HallMinX + 0.62f, Vector3.right), (HallMaxX - 0.62f, Vector3.left) })
            foreach (var z in new[] { 79.5f, 87.5f })
            {
                var tex = TextureKit.Mural(TextureKit.MuralTheme.OgreHistory, 31 + mi % 2);
                var mat = RuinKit.MuralMaterial(MatDir + "/Z2_Mural_" + (mi % 2) + ".mat", tex, new Color(0.95f, 0.86f, 0.74f));
                var go = new GameObject("Mural_" + mi);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x, hy + 1.6f, z);
                go.transform.rotation = Quaternion.LookRotation(inward);
                go.transform.localScale = new Vector3(5.6f, 2.8f, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = panel;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mi++;
            }

            // Жаровни: две у ворот, две во дворе, четыре в тронном зале
            foreach (var b in new[] {
                new Vector3(38f, y, 56.5f), new Vector3(50f, y, 56.5f),
                new Vector3(38f, y, 66f), new Vector3(50f, y, 66f),
                new Vector3(39f, y, 77.5f), new Vector3(49f, y, 77.5f),
                new Vector3(39f, y, 86.5f), new Vector3(49f, y, 86.5f) })
                Brazier(root, b, b.z > 82f);

            // Мягкая верхняя и контровая подсветка тронного зала + фресок истории огров
            var hallFill = Z.PointLight(root, "ThroneHallFill", new Vector3(44f, hy + 6.2f, 83.5f), new Color(1f, 0.72f, 0.48f), 26f, 20f);
            hallFill.shadows = LightShadows.None;
            warmLights.Add(hallFill);
            var throneRim = Z.PointLight(root, "ThroneRimLight", new Vector3(44f, hy + 5.2f, 90.2f), new Color(1f, 0.78f, 0.55f), 16f, 16f);
            throneRim.shadows = LightShadows.None;
            warmLights.Add(throneRim);
            foreach (var mx in new[] { HallMinX + 3.2f, HallMaxX - 3.2f })
            {
                var ml = Z.PointLight(root, "MuralFill", new Vector3(mx, hy + 3.5f, 83.5f), new Color(1f, 0.74f, 0.50f), 14f, 10f);
                ml.shadows = LightShadows.None;
                warmLights.Add(ml);
            }

            // Знамёна: иллюзия — красные с черепом, правда — охра с очагом
            var lie = new LowPolyMesh(3);
            var tru = new LowPolyMesh(3);
            foreach (var bp in new[] { new Vector3(30f, y, FortMinZ - 0.9f), new Vector3(58f, y, FortMinZ - 0.9f), new Vector3(40f, y, HallMinZ - 0.7f), new Vector3(48f, y, HallMinZ - 0.7f) })
            {
                var m = Matrix4x4.TRS(bp, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
                lie.Box(0, m, new Vector3(0f, 4.6f, 0f), new Vector3(1.8f, 3.6f, 0.04f));
                lie.Box(2, m, new Vector3(0f, 6.5f, 0f), new Vector3(2.2f, 0.12f, 0.12f));
                SkullAt(lie, 1, m.MultiplyPoint3x4(new Vector3(0f, 5.2f, 0.1f)), 0.35f);
                tru.Box(0, m, new Vector3(0f, 4.6f, 0f), new Vector3(1.8f, 3.6f, 0.04f));
                tru.Box(2, m, new Vector3(0f, 6.5f, 0f), new Vector3(2.2f, 0.12f, 0.12f));
                tru.Lathe(1, m * Matrix4x4.TRS(new Vector3(0f, 4.6f, 0.06f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new[] { 0f, 0.02f }, new[] { 0.55f, 0.55f }, 10);
            }
            DualObject("Banners", root, Vector3.zero, Quaternion.identity,
                Register(lie.ToMesh("Z2_BannersLie")), new[] { mClothRed, mBone, mWoodDark },
                Register(tru.ToMesh("Z2_BannersTrue")), new[] { mClothOchre, mClothGreen, mWood });

            return BuildStairs(root);
        }

        static void Tower(LowPolyMesh stone, LowPolyMesh wood, Vector3 p, float rad, float h, System.Random r)
        {
            // Внутреннее ядро башни, чтобы небо не просвечивало сквозь горизонтальные швы кладки
            stone.Lathe(0, Matrix4x4.TRS(p, Quaternion.identity, Vector3.one), new[] { -0.8f, h + 0.1f }, new[] { rad - 0.12f, rad - 0.14f }, 12, null, 0f, false);
            float yy = -0.6f;
            while (yy < h)
            {
                float rowH = 0.6f;
                stone.Lathe(0, Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)r.NextDouble() * 30f, 0f), Vector3.one), new[] { yy, yy + rowH - 0.02f }, new[] { rad, rad - 0.02f }, 12, r, 0.015f, false);
                yy += rowH;
            }
            stone.Lathe(0, Matrix4x4.TRS(p, Quaternion.identity, Vector3.one), new[] { h, h + 0.5f }, new[] { rad + 0.4f, rad + 0.4f }, 12, null, 0f, true);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                stone.Box(0, Matrix4x4.TRS(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (rad + 0.15f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one),
                    new Vector3(0f, h + 1.1f, 0f), new Vector3(0.7f, 1.1f, 0.9f));
            }
            wood.Lathe(1, Matrix4x4.TRS(p + Vector3.up * (h + 1.6f), Quaternion.identity, Vector3.one), new[] { 0f, 0.3f, 3.2f }, new[] { rad + 0.8f, rad + 0.6f, 0f }, 10, r, 0.04f, true);
        }

        static void Brazier(Transform parent, Vector3 p, bool shadows)
        {
            var t = new GameObject("Brazier").transform;
            t.SetParent(parent, false);
            t.position = p;
            var lp = new LowPolyMesh(1);
            lp.Lathe(0, Matrix4x4.identity, new[] { 0f, 0.2f, 1f, 1.2f, 1.5f }, new[] { 0.45f, 0.2f, 0.16f, 0.55f, 0.62f }, 8, null, 0f, true);
            Z.MeshObject("Mesh", t, Register(lp.ToMesh("Z2_Brazier_" + parent.childCount)), new[] { mIron }, true);
            var l = Z.PointLight(t, "Light", p + Vector3.up * 2.1f, new Color(1f, 0.62f, 0.32f), 17f, 16f);
            if (shadows) ShadowLight(l); else l.shadows = LightShadows.None;
            var flick = l.gameObject.AddComponent<TorchFlicker>();
            SetFloat(flick, "baseIntensity", 16f);
            SetFloat(flick, "flickerAmount", 2.4f);
            warmLights.Add(l);
            Fire(t, p + Vector3.up * 1.45f, 0.5f);
        }

        /// <summary>Лестница к свету: золотые ступени над обрывом на восток, луч и искры. Включается после выбора.</summary>
        static GameObject BuildStairs(Transform parent)
        {
            var root = new GameObject("StairsToLight");
            root.transform.SetParent(parent, false);
            var lp = new LowPolyMesh(1);
            int steps = 34;
            for (int i = 0; i < steps; i++)
            {
                float x = FortMaxX - 0.5f + i * 0.9f;
                float yy = FortH + i * 0.62f;
                lp.Box(0, Matrix4x4.identity, new Vector3(x, yy + 0.15f, 88f), new Vector3(0.9f, 0.3f, 4f - i * 0.04f));
                lp.Box(0, Matrix4x4.identity, new Vector3(x, yy - 0.6f, 88f), new Vector3(0.6f, 1.2f, 3.4f - i * 0.04f));
            }
            var go = Z.MeshObject("Steps", root.transform, Register(lp.ToMesh("Z2_StairsToLight")), new[] { mGold }, true);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var topP = new Vector3(FortMaxX + steps * 0.9f, FortH + steps * 0.62f, 88f);
            var l = Z.PointLight(root.transform, "Light", topP + Vector3.up * 2f, new Color(1f, 0.86f, 0.6f), 40f, 30f);
            l.shadows = LightShadows.None;
            var beam = new GameObject("Beam");
            beam.transform.SetParent(root.transform, false);
            beam.transform.position = topP + Vector3.up * 60f;
            beam.AddComponent<MeshFilter>().sharedMesh = Register(Z.ShaftMesh("Z2_StairBeam", 3f, 5f, 62f, 6, 0.5f));
            var mr = beam.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gStair;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var motes = new GameObject("Motes");
            motes.transform.SetParent(root.transform, false);
            motes.transform.position = Vector3.Lerp(new Vector3(FortMaxX, FortH, 88f), topP, 0.5f);
            var ps = Z.Particles(motes, pGold, 30f, 3f, 6f, 0.1f, 0.4f, 0.04f, 0.09f,
                new Color(1f, 0.9f, 0.6f), new Color(1f, 0.75f, 0.4f), -0.02f, 150, Z.FadeCurve(), 0.5f);
            Z.ShapeBox(ps, new Vector3(32f, 24f, 6f));
            root.SetActive(false);
            return root;
        }

        // ---------------------------------------------------------------- природа

        static void BuildNature(Transform world)
        {
            var root = new GameObject("Nature").transform;
            root.SetParent(world, false);
            var rocks = new LowPolyMesh(2);
            var trees = new LowPolyMesh(3); // 0 кора, 1 листва, 2 тёмная листва
            var grass = new LowPolyMesh(1);
            const float step = 2.5f;
            for (float gx = MinX; gx < MaxX; gx += step)
            for (float gz = MinZ; gz < MaxZ; gz += step)
            {
                float x = gx + R(-0.5f, 0.5f) * step, z = gz + R(-0.5f, 0.5f) * step;
                float d = Sdf(x, z);
                if (d > 10f) continue;
                var p = new Vector3(x, Height(x, z), z);
                bool res = Reserved(x, z, 0f);
                float trail = TrailDist(x, z);
                // У проходов валуны только у самой стены и мельче — путь не загораживают
                bool nearPath = PathDist(x, z) < 9f;
                if (d > (nearPath ? -0.3f : -2f) && d < 4f && R01() < 0.45f && !InFort(x, z, 6f))
                    Boulder(rocks, p, nearPath ? R(0.8f, 2f) : R(1.2f, 3.6f));
                if (d < -2f && d > -9f && !res && trail > 3f && PathDist(x, z) > 4f && R01() < 0.07f && !InFort(x, z, 8f))
                    Tree(trees, p, R(5f, 9f));
                if (d < -0.5f && !res && trail > 1.4f && !InFort(x, z, 1f) && R01() < 0.55f)
                    Tuft(grass, p, R(0.25f, 0.6f));
            }
            var rgo = Z.MeshObject("Rocks", root, Register(rocks.ToMesh("Z2_Rocks")), new[] { mRock, mRockDark }, true);
            GameObjectUtility.SetStaticEditorFlags(rgo, StaticEditorFlags.BatchingStatic);
            var tgo = Z.MeshObject("Trees", root, Register(trees.ToMesh("Z2_Trees")), new[] { mBark, mLeaf, mLeafDark }, true);
            GameObjectUtility.SetStaticEditorFlags(tgo, StaticEditorFlags.BatchingStatic);
            var ggo = Z.MeshObject("Grass", root, Register(grass.ToMesh("Z2_Grass")), new[] { mGrass }, false);
            ggo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(ggo, StaticEditorFlags.BatchingStatic);
        }

        static void Boulder(LowPolyMesh lp, Vector3 p, float s)
        {
            var m = Matrix4x4.TRS(p + Vector3.down * s * 0.25f, Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)),
                new Vector3(s * R(0.8f, 1.3f), s * R(0.5f, 0.9f), s * R(0.8f, 1.3f)));
            lp.Lathe(R01() < 0.35f ? 1 : 0, m, new[] { -0.3f, 0.25f, 0.7f, 1f }, new[] { 0.85f, 1f, 0.7f, 0.25f }, rng.Next(6, 9), rng, 0.25f);
        }

        static void Tree(LowPolyMesh lp, Vector3 p, float h)
        {
            var m = Matrix4x4.TRS(p + Vector3.down * 0.3f, Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)), Vector3.one);
            lp.Lathe(0, m, new[] { 0f, h * 0.35f, h * 0.75f }, new[] { h * 0.055f, h * 0.04f, h * 0.025f }, 7, rng, 0.1f, false, 0.3f);
            int blobs = rng.Next(4, 7);
            for (int i = 0; i < blobs; i++)
            {
                float s = h * R(0.17f, 0.26f);
                var c = m.MultiplyPoint3x4(new Vector3(R(-0.18f, 0.18f) * h, h * R(0.55f, 0.92f), R(-0.18f, 0.18f) * h));
                lp.Lathe(R01() < 0.5f ? 1 : 2, Matrix4x4.TRS(c, Quaternion.Euler(R(0f, 30f), R(0f, 360f), 0f), new Vector3(s, s * 0.8f, s)),
                    new[] { -0.7f, -0.3f, 0.3f, 0.75f }, new[] { 0.3f, 0.95f, 0.85f, 0.2f }, 7, rng, 0.22f, true);
            }
        }

        static void Tuft(LowPolyMesh lp, Vector3 p, float h)
        {
            int n = rng.Next(4, 8);
            for (int i = 0; i < n; i++)
            {
                float yaw = R(0f, 360f) * Mathf.Deg2Rad;
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var fwd = new Vector3(-side.z, 0f, side.x);
                var bp = p + new Vector3(R(-0.2f, 0.2f), -0.03f, R(-0.2f, 0.2f));
                float bh = h * R(0.6f, 1.2f), w = R(0.03f, 0.06f);
                var tip = bp + Vector3.up * bh + fwd * R(-0.25f, 0.3f) * bh;
                var n0 = (Vector3.up + fwd * 0.35f).normalized;
                lp.TriSmooth(0, bp - side * w, bp + side * w, tip, n0, n0, n0);
            }
        }

        // ---------------------------------------------------------------- огры

        static void BuildOgres(Transform world, GameObject stairs)
        {
            var root = new GameObject("Ogres").transform;
            root.SetParent(world, false);
            var lieMats = new[] { mOgreSkin, mHide, mBone, mOgreEyes, mIron };
            var truMats = new[] { mOgreSkinTrue, mTunic, mWood, mOgreEyesTrue, mIron };

            // Стражи: можно пощадить
            int gi = 0;
            foreach (var gp in new[] { Guard1Pos, Guard2Pos })
            {
                gi++;
                var go = OgreBody(root, $"OgreGuard_{gi}", gp, FirePos + new Vector2(-30f, 0f), 2.9f, "guard", lieMats, truMats);
                var health = go.AddComponent<Health>();
                SetFloat(health, "max", 160f);
                var enemy = go.AddComponent<EnemyController>();
                SetString(enemy, "enemyId", $"ogre_guard_{gi}");
                var so = new SerializedObject(enemy);
                so.FindProperty("detectRange").floatValue = 11f;
                so.FindProperty("attackRange").floatValue = 2.6f;
                so.FindProperty("moveSpeed").floatValue = 2.4f;
                so.FindProperty("attackDamage").floatValue = 14f;
                so.FindProperty("attackWindup").floatValue = 0.6f;
                so.ApplyModifiedPropertiesWithoutUndo();
                go.AddComponent<HitFlash>();
            }

            // Кузнец: говорит только через Око
            var smith = OgreBody(root, "OgreSmith", SmithPos, ForgePos + new Vector2(0f, 3f), 2.7f, "smith", lieMats, truMats);
            smith.AddComponent<TalkNpc>();

            // Вождь и сын (сын виден только через Око)
            var chief = OgreBody(root, "OgreChief", ChiefPos, new Vector2(44f, 70f), 3.4f, "chief", lieMats, truMats);
            var ch = chief.AddComponent<Health>();
            SetFloat(ch, "max", 220f);
            var ce = chief.AddComponent<EnemyController>();
            SetString(ce, "enemyId", MercyCounter.OgreChiefId);
            var cso = new SerializedObject(ce);
            cso.FindProperty("detectRange").floatValue = 0.5f; // не нападает первым
            cso.FindProperty("moveSpeed").floatValue = 0f;
            cso.ApplyModifiedPropertiesWithoutUndo();
            chief.AddComponent<HitFlash>();

            var son = new GameObject("OgreSon");
            son.transform.SetParent(root, false);
            son.transform.position = Ground(SonPos);
            son.transform.rotation = Quaternion.LookRotation(Flat(new Vector2(44f, 70f) - SonPos));
            var sonModel = Z.MeshObject("Truth", son.transform, Register(OgreMesh(true, "child", 1.5f)), truMats, false);
            sonModel.SetActive(false);
            var siv = son.AddComponent<IllusionVisual>();
            SetRef(siv, "truthModel", sonModel);

            // Триггер речи вождя у входа в зал
            var trig = new GameObject("ChiefSpeechTrigger");
            trig.transform.SetParent(root, false);
            trig.transform.position = new Vector3(44f, FortH + 1.5f, HallMinZ + 3f);
            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 4f, 4f);
            var choice = trig.AddComponent<ChiefChoice>();
            SetRef(choice, "chief", ce);
            SetRef(choice, "stairs", stairs);
        }

        static GameObject OgreBody(Transform parent, string name, Vector2 pos, Vector2 lookAt, float height, string kind, Material[] lieMats, Material[] truMats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Ground(pos);
            go.transform.rotation = Quaternion.LookRotation(Flat(lookAt - pos));
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.height = height;
            col.radius = height * 0.24f;
            var lie = Z.MeshObject("Illusion", go.transform, Register(OgreMesh(false, kind, height)), lieMats, false);
            var tru = Z.MeshObject("Truth", go.transform, Register(OgreMesh(true, kind, height)), truMats, false);
            tru.SetActive(false);
            var iv = go.AddComponent<IllusionVisual>();
            SetRef(iv, "illusionModel", lie);
            SetRef(iv, "truthModel", tru);
            var eyes = Z.PointLight(lie.transform, "EyeGlow", go.transform.TransformPoint(new Vector3(0f, height * 0.88f, height * 0.2f)), new Color(1f, 0.1f, 0.05f), 2.2f, 0.8f);
            eyes.shadows = LightShadows.None;
            return go;
        }

        /// <summary>
        /// Огр (вперёд по +z). Иллюзия: сгорбленный, тёмный, с клыками, рогами, шипами и дубиной.
        /// Правда: прямой, в рубахе (кузнец — в фартуке с молотом, страж — с копьём, вождь — с посохом
        /// и деревянной короной, ребёнок — без оружия). Подмеши: 0 кожа, 1 одежда/шкура, 2 кость/дерево, 3 глаза, 4 железо.
        /// </summary>
        static Mesh OgreMesh(bool truth, string kind, float H)
        {
            var lp = new LowPolyMesh(5);
            float s = H / 2.9f;
            Matrix4x4 T(float x, float y, float z, float ex = 0f, float ey = 0f, float ez = 0f) =>
                Matrix4x4.TRS(new Vector3(x, y, z) * s, Quaternion.Euler(ex, ey, ez), Vector3.one * s);
            bool child = kind == "child";
            float hunch = truth ? 6f : 22f;
            // Ноги
            for (int side = -1; side <= 1; side += 2)
            {
                lp.Lathe(0, T(side * 0.3f, 0f, 0f), new[] { 0f, 0.55f, 1.15f }, new[] { 0.2f, 0.24f, 0.3f }, 7, null, 0f, true);
                lp.Box(0, T(side * 0.32f, 0.06f, 0.12f), Vector3.zero, new Vector3(0.36f, 0.14f, 0.55f));
            }
            // Торс-бочка и живот
            var torso = T(0f, 1.05f, 0f, hunch);
            lp.Lathe(0, torso, new[] { 0f, 0.35f, 0.85f, 1.25f, 1.45f }, new[] { 0.55f, 0.66f, 0.7f, 0.6f, 0.3f }, 9, null, 0f, true);
            if (truth)
            {
                lp.Lathe(1, torso, new[] { -0.05f, 0.4f, 0.95f }, new[] { 0.58f, 0.69f, 0.72f }, 9, null, 0f, true);
                lp.Lathe(2, torso, new[] { 0.32f, 0.42f }, new[] { 0.7f, 0.7f }, 9);
                if (kind == "smith") lp.Box(1, torso, new Vector3(0f, 0.55f, 0.66f), new Vector3(0.8f, 1.1f, 0.08f));
            }
            else
            {
                lp.Box(1, torso, new Vector3(0f, 0.25f, 0f), new Vector3(1.3f, 0.4f, 1.2f)); // набедренная шкура
                for (int k = 0; k < 4; k++)
                    lp.Lathe(2, torso * Matrix4x4.TRS(new Vector3(0f, 0.6f + k * 0.22f, -0.6f), Quaternion.Euler(-60f, 0f, 0f), Vector3.one), new[] { 0f, 0.35f }, new[] { 0.08f, 0f }, 4);
            }
            // Голова
            var neck = torso.MultiplyPoint3x4(new Vector3(0f, 1.42f, truth ? 0.05f : 0.25f));
            var head = Matrix4x4.TRS(neck + new Vector3(0f, 0.12f * s, 0.05f * s), Quaternion.Euler(truth ? 0f : 10f, 0f, 0f), Vector3.one * s);
            lp.Lathe(0, head, new[] { -0.05f, 0.15f, 0.42f, 0.55f }, new[] { 0.3f, 0.36f, 0.3f, 0.12f }, 8, null, 0f, true);
            lp.Box(0, head, new Vector3(0f, 0.02f, 0.18f), new Vector3(0.5f, 0.2f, 0.3f)); // челюсть
            lp.Box(0, head, new Vector3(0f, 0.25f, 0.33f), new Vector3(0.14f, 0.16f, 0.12f)); // нос
            for (int side = -1; side <= 1; side += 2)
            {
                lp.Box(3, head, new Vector3(side * 0.13f, 0.32f, 0.29f), truth ? new Vector3(0.07f, 0.07f, 0.03f) : new Vector3(0.1f, 0.04f, 0.03f));
                float tusk = truth ? (child ? 0f : 0.09f) : 0.24f;
                if (tusk > 0f)
                    lp.Lathe(2, head * Matrix4x4.TRS(new Vector3(side * 0.16f, 0.08f, 0.3f), Quaternion.Euler(-20f, 0f, side * -12f), Vector3.one), new[] { 0f, tusk }, new[] { 0.035f, 0f }, 4);
                lp.Lathe(0, head * Matrix4x4.TRS(new Vector3(side * 0.32f, 0.3f, 0f), Quaternion.Euler(0f, 0f, -side * 70f), new Vector3(1f, 1f, 0.4f)), new[] { 0f, 0.2f }, new[] { 0.09f, 0f }, 4);
                if (!truth)
                    lp.Lathe(2, head * Matrix4x4.TRS(new Vector3(side * 0.2f, 0.45f, -0.05f), Quaternion.Euler(-30f, 0f, -side * 35f), Vector3.one), new[] { 0f, 0.25f, 0.42f }, new[] { 0.08f, 0.05f, 0f }, 5);
            }
            if (kind == "chief")
            {
                // Корона: иллюзия — из костей, правда — деревянный обруч с перьями
                for (int k = 0; k < 7; k++)
                {
                    float a = k * Mathf.PI * 2f / 7f;
                    lp.Lathe(2, head * Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.27f, 0.5f, Mathf.Sin(a) * 0.27f), Quaternion.Euler(Mathf.Sin(a) * 15f, 0f, -Mathf.Cos(a) * 15f), Vector3.one), new[] { 0f, truth ? 0.22f : 0.32f }, new[] { 0.045f, 0f }, 4);
                }
                lp.Lathe(truth ? 2 : 4, head, new[] { 0.46f, 0.53f }, new[] { 0.31f, 0.31f }, 8);
            }
            // Руки
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = torso.MultiplyPoint3x4(new Vector3(side * 0.72f, 1.2f, 0f));
                var arm = Matrix4x4.TRS(sh, Quaternion.Euler(truth ? 175f : 160f, 0f, side * (truth ? 8f : 16f)), Vector3.one * s);
                lp.Lathe(0, arm, new[] { -0.1f, 0.45f, 0.95f, 1.15f }, new[] { 0.2f, 0.17f, 0.16f, 0.2f }, 7, null, 0f, true);
                lp.Box(0, arm, new Vector3(0f, 1.22f, 0f), new Vector3(0.28f, 0.22f, 0.3f));
                if (!truth && side == 1 && !child)
                {
                    // Дубина с шипами
                    var club = arm * Matrix4x4.TRS(new Vector3(0f, 1.25f, 0.15f), Quaternion.Euler(-70f, 0f, 0f), Vector3.one);
                    lp.Lathe(2, club, new[] { -0.2f, 0.6f, 1.3f }, new[] { 0.06f, 0.12f, 0.2f }, 6);
                    for (int k = 0; k < 6; k++)
                        lp.Lathe(4, club * Matrix4x4.TRS(new Vector3(0f, 0.8f + k * 0.08f, 0f), Quaternion.Euler(90f, k * 60f, 0f), Vector3.one), new[] { 0.15f, 0.32f }, new[] { 0.03f, 0f }, 3);
                }
                if (truth && side == 1)
                {
                    var hand = arm * Matrix4x4.Translate(new Vector3(0f, 1.25f, 0.1f));
                    if (kind == "smith")
                    {
                        lp.Lathe(2, hand * Matrix4x4.Rotate(Quaternion.Euler(-80f, 0f, 0f)), new[] { -0.1f, 0.7f }, new[] { 0.035f, 0.035f }, 5);
                        lp.Box(4, hand * Matrix4x4.Rotate(Quaternion.Euler(-80f, 0f, 0f)), new Vector3(0f, 0.72f, 0f), new Vector3(0.28f, 0.16f, 0.16f));
                    }
                    else if (kind == "guard")
                    {
                        lp.Lathe(2, hand, new[] { -1.2f, 1.4f }, new[] { 0.035f, 0.035f }, 5);
                        lp.Lathe(4, hand * Matrix4x4.Translate(new Vector3(0f, 1.4f, 0f)), new[] { 0f, 0.35f }, new[] { 0.07f, 0f }, 4);
                    }
                    else if (kind == "chief")
                    {
                        lp.Lathe(2, hand, new[] { -1.3f, 1.2f }, new[] { 0.05f, 0.045f }, 5);
                        lp.Lathe(1, hand * Matrix4x4.Translate(new Vector3(0f, 1.25f, 0f)), new[] { 0f, 0.15f, 0.3f }, new[] { 0.06f, 0.12f, 0f }, 6);
                    }
                }
            }
            return lp.ToMesh($"Z2_Ogre_{kind}_{(truth ? "True" : "Lie")}");
        }

        // ---------------------------------------------------------------- предметы

        static void BuildItems(Transform world)
        {
            var root = new GameObject("Items").transform;
            root.SetParent(world, false);
            // Письмо на столике у хижины 3
            var h3 = Huts[2];
            var dir = Flat(FirePos - h3);
            var side = Vector3.Cross(Vector3.up, dir);
            var letter = new GameObject("Item_Letter").transform;
            letter.SetParent(root, false);
            letter.position = Ground(h3) + dir * (HutR + 2.2f) + side * 2.4f;
            letter.position = new Vector3(letter.position.x, Height(letter.position.x, letter.position.z), letter.position.z);
            letter.rotation = Quaternion.LookRotation(dir);
            var lp = new LowPolyMesh(3);
            lp.Lathe(0, Matrix4x4.identity, new[] { 0f, 0.7f }, new[] { 0.45f, 0.42f }, 8, null, 0f, false);
            lp.Lathe(0, Matrix4x4.identity, new[] { 0.7f, 0.8f }, new[] { 0.48f, 0.48f }, 8, null, 0f, true);
            lp.Box(1, Matrix4x4.TRS(new Vector3(0.05f, 0.81f, 0f), Quaternion.Euler(0f, 15f, 0f), Vector3.one), Vector3.zero, new Vector3(0.3f, 0.01f, 0.38f));
            lp.Box(2, Matrix4x4.TRS(new Vector3(-0.2f, 0.82f, 0.1f), Quaternion.Euler(0f, 50f, 90f), Vector3.one), Vector3.zero, new Vector3(0.02f, 0.18f, 0.02f));
            Z.MeshObject("Mesh", letter, Register(lp.ToMesh("Z2_LetterTable")), new[] { mWood, mClothWhite, mDark }, false);
            ItemSetup(letter, "z2_letter", "Письмо", "Список жертв для следующего набега. Не читай их проклятые знаки.",
                "Письмо сыну: «Не ходи к старой дороге. Там снова видели сияние. Мама ждёт к ужину».", "z2.item_letter", new Vector3(1f, 1f, 1f));

            // Колыбель у хижины 1
            var h1 = Huts[0];
            dir = Flat(FirePos - h1);
            side = Vector3.Cross(Vector3.up, dir);
            var cradle = new GameObject("Item_Cradle").transform;
            cradle.SetParent(root, false);
            var cp = Ground(h1) + dir * (HutR + 1.5f) - side * 2.6f;
            cradle.position = new Vector3(cp.x, Height(cp.x, cp.z), cp.z);
            cradle.rotation = Quaternion.LookRotation(side);
            lp = new LowPolyMesh(3);
            lp.Box(0, Matrix4x4.identity, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.35f, 1.4f));
            lp.Box(1, Matrix4x4.identity, new Vector3(0f, 0.64f, 0f), new Vector3(0.75f, 0.06f, 1.25f));
            for (int s = -1; s <= 1; s += 2)
                lp.Lathe(0, Matrix4x4.TRS(new Vector3(0f, 0.2f, s * 0.6f), Quaternion.Euler(0f, 0f, 90f), new Vector3(1f, 1f, 0.12f)), new[] { -0.5f, 0.5f }, new[] { 0.3f, 0.3f }, 10, null, 0f, true);
            lp.Lathe(2, Matrix4x4.TRS(new Vector3(0.1f, 0.72f, -0.3f), Quaternion.identity, Vector3.one), new[] { 0f, 0.1f, 0.18f }, new[] { 0.08f, 0.1f, 0.04f }, 6);
            Z.MeshObject("Mesh", cradle, Register(lp.ToMesh("Z2_Cradle")), new[] { mWood, mClothBlue, mClothOchre }, false);
            ItemSetup(cradle, "z2_cradle", "Колыбель", "Ловушка. Они кладут туда приманку для пленников.",
                "Деревянная колыбель, вырезанная вручную. Под одеялом — тряпичная кукла.", "z2.item_cradle", new Vector3(1.2f, 1f, 1.6f));
        }

        static void ItemSetup(Transform item, string id, string title, string lie, string truth, string key, Vector3 box)
        {
            var bc = item.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, box.y * 0.5f, 0f);
            bc.size = box;
            var it = item.gameObject.AddComponent<InspectItem>();
            SetString(it, "itemId", id);
            SetString(it, "title", title);
            SetString(it, "illusionText", lie);
            SetString(it, "truthText", truth);
            SetString(it, "narrationKey", key);
            var l = Z.PointLight(item, "Glint", item.position + Vector3.up * 1.4f, new Color(1f, 0.85f, 0.55f), 3.5f, 2f);
            l.shadows = LightShadows.None;
            var sparkle = new GameObject("Sparkle");
            sparkle.transform.SetParent(item, false);
            sparkle.transform.position = item.position + Vector3.up * 0.8f;
            var ps = Z.Particles(sparkle, pDust, 5f, 1.2f, 2f, 0.05f, 0.2f, 0.03f, 0.06f,
                new Color(1f, 0.92f, 0.6f), new Color(1f, 0.75f, 0.4f), -0.03f, 20, Z.FadeCurve(), 0.3f);
            Z.ShapeSphere(ps, 0.4f);
        }

        // ---------------------------------------------------------------- игрок, небо, атмосфера

        static Camera BuildPlayer()
        {
            var cam = CreatePlayer(Ground(SpawnPos, 1.2f));
            var player = GameObject.FindWithTag("Player");
            player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            // Око уже у героя (получено у врат)
            var eyeSo = new SerializedObject(player.GetComponent<EyeOfTruth>());
            eyeSo.FindProperty("unlocked").boolValue = true;
            eyeSo.ApplyModifiedPropertiesWithoutUndo();

            var cp3 = new GameObject("Checkpoint_CP3").transform;
            cp3.position = Ground(SpawnPos, 1.2f);
            cp3.rotation = player.transform.rotation;
            var cp4 = new GameObject("Checkpoint_CP4").transform;
            cp4.position = Ground(new Vector2(44f, 56f), 1.2f);
            cp4.rotation = Quaternion.identity;
            var fall = player.AddComponent<FallRespawn>();
            var so = new SerializedObject(fall);
            so.FindProperty("killY").floatValue = -10f;
            var arr = so.FindProperty("safePoints");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = cp3;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = cp4;
            so.ApplyModifiedPropertiesWithoutUndo();

            var dust = new GameObject("Dust");
            dust.transform.SetParent(player.transform, false);
            dust.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var ps = Z.Particles(dust, pDust, 20f, 6f, 10f, 0.02f, 0.12f, 0.015f, 0.035f,
                new Color(1f, 0.85f, 0.65f, 0.6f), new Color(1f, 0.75f, 0.55f, 0.4f), -0.003f, 260, Z.FadeCurve(), 0.25f);
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

        /// <summary>Небо (купол с градиентом и облаками) и солнце: два облика, переключает Око.</summary>
        static Light BuildSky(Transform world)
        {
            var root = new GameObject("Sky").transform;
            var sunDir = -(SunRot * Vector3.forward);
            var lieSky = new GameObject("Illusion");
            lieSky.transform.SetParent(root, false);
            var truSky = new GameObject("Truth");
            truSky.transform.SetParent(root, false);
            SkyObjects(lieSky.transform, false, sunDir);
            SkyObjects(truSky.transform, true, sunDir);
            truSky.SetActive(false);
            var iv = root.gameObject.AddComponent<IllusionVisual>();
            SetRef(iv, "illusionModel", lieSky);
            SetRef(iv, "truthModel", truSky);

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = SunRot;
            sun.color = new Color(1f, 0.32f, 0.2f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.5f;
            RenderSettings.sun = sun;
            return sun;
        }

        static void SkyObjects(Transform parent, bool truth, Vector3 sunDir)
        {
            var dome = new GameObject("Dome");
            dome.transform.SetParent(parent, false);
            dome.transform.position = new Vector3(30f, 0f, 30f);
            dome.AddComponent<MeshFilter>().sharedMesh = Register(SkyMesh(truth, sunDir));
            var mr = dome.AddComponent<MeshRenderer>();
            mr.sharedMaterial = truth ? mSkyTrue : mSkyLie;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var sun = new GameObject("SunDisc");
            sun.transform.SetParent(parent, false);
            sun.transform.position = new Vector3(30f, 0f, 30f) + sunDir * 420f;
            sun.transform.rotation = Quaternion.LookRotation(-sunDir);
            sun.AddComponent<MeshFilter>().sharedMesh = Register(SunMesh(truth));
            var sr = sun.AddComponent<MeshRenderer>();
            sr.sharedMaterial = truth ? gSunTrue : gSunLie;
            sr.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Купол неба: цвет по высоте и по углу к солнцу, слоистые облака подсвечены со стороны солнца.</summary>
        static Mesh SkyMesh(bool truth, Vector3 sunDir)
        {
            const int seg = 64, rings = 24;
            const float R = 460f;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            Color horizonSun, horizon, mid, zenith, cloudLit, cloudDark;
            if (truth)
            {
                horizonSun = new Color(1f, 0.62f, 0.32f);
                horizon = new Color(0.86f, 0.56f, 0.52f);
                mid = new Color(0.52f, 0.42f, 0.58f);
                zenith = new Color(0.16f, 0.2f, 0.4f);
                cloudLit = new Color(1f, 0.7f, 0.52f);
                cloudDark = new Color(0.46f, 0.34f, 0.46f);
            }
            else
            {
                horizonSun = new Color(0.82f, 0.24f, 0.1f);
                horizon = new Color(0.38f, 0.11f, 0.08f);
                mid = new Color(0.19f, 0.07f, 0.07f);
                zenith = new Color(0.05f, 0.025f, 0.035f);
                cloudLit = new Color(0.75f, 0.14f, 0.06f);
                cloudDark = new Color(0.1f, 0.02f, 0.02f);
            }
            for (int ri = 0; ri <= rings; ri++)
            {
                float el = Mathf.Lerp(-0.15f, 1f, (float)ri / rings); // синус высоты
                float elA = Mathf.Asin(Mathf.Clamp(el, -1f, 1f));
                for (int si = 0; si <= seg; si++)
                {
                    float az = si * Mathf.PI * 2f / seg;
                    var dir = new Vector3(Mathf.Cos(az) * Mathf.Cos(elA), Mathf.Sin(elA), Mathf.Sin(az) * Mathf.Cos(elA));
                    v.Add(dir * R);
                    float toSun = Mathf.Max(0f, Vector3.Dot(dir, sunDir));
                    float e = Mathf.Clamp01(el);
                    var hor = Color.Lerp(horizon, horizonSun, Mathf.Pow(toSun, 3f));
                    var col = e < 0.25f ? Color.Lerp(hor, mid, e / 0.25f) : Color.Lerp(mid, zenith, Mathf.Pow((e - 0.25f) / 0.75f, 0.7f));
                    // Облака: полосы шума у горизонта и средней высоты
                    // Шум по кругу (cos/sin азимута) — без шва на стыке купола
                    float cx = Mathf.Cos(az), cz = Mathf.Sin(az);
                    float n = Mathf.PerlinNoise(cx * 2.2f + 11f + e * 3f, cz * 2.2f + 5f + e * 9f) * 0.6f
                            + Mathf.PerlinNoise(cx * 6f + 3f + e * 7f, cz * 6f + 17f + e * 22f) * 0.4f;
                    float band = Smooth(0.02f, 0.1f, e) * (1f - Smooth(0.35f, 0.6f, e));
                    float cloud = Smooth(0.5f, 0.68f, n) * band;
                    var cc = Color.Lerp(cloudDark, cloudLit, Mathf.Pow(toSun, 2f) * 0.8f + 0.2f * (1f - e));
                    col = Color.Lerp(col, cc, cloud * 0.85f);
                    // Свечение вокруг солнца
                    col += (truth ? new Color(1f, 0.75f, 0.45f) : new Color(0.9f, 0.2f, 0.08f)) * Mathf.Pow(toSun, 24f) * 0.9f;
                    if (el < 0f) col = Color.Lerp(col, hor * 0.6f, Smooth(0f, -0.15f, el));
                    c.Add(col);
                }
            }
            for (int ri = 0; ri < rings; ri++)
            for (int si = 0; si < seg; si++)
            {
                int a = ri * (seg + 1) + si, b = a + 1, d = a + seg + 1, e2 = d + 1;
                t.Add(a); t.Add(d); t.Add(b);
                t.Add(b); t.Add(d); t.Add(e2);
            }
            var mesh = new Mesh { name = truth ? "Z2_SkyTruth" : "Z2_SkyIllusion" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh SunMesh(bool truth)
        {
            var v = new List<Vector3> { Vector3.zero };
            var core = truth ? new Color(1f, 0.92f, 0.72f, 1f) : new Color(0.95f, 0.25f, 0.1f, 1f);
            var halo = truth ? new Color(1f, 0.7f, 0.4f, 0.4f) : new Color(0.7f, 0.1f, 0.04f, 0.35f);
            var c = new List<Color> { core };
            var t = new List<int>();
            const int seg = 32;
            float r = truth ? 14f : 18f;
            float[] radii = { r, r * 1.08f, r * 3.5f };
            Color[] cols = { core, halo, new Color(halo.r, halo.g, halo.b, 0f) };
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radii[k]);
                    c.Add(cols[k]);
                }
            for (int i = 0; i < seg; i++)
            {
                int i1 = (i + 1) % seg;
                t.Add(0); t.Add(1 + i1); t.Add(1 + i);
                for (int k = 0; k < 2; k++)
                {
                    int a0 = 1 + k * seg + i, a1 = 1 + k * seg + i1, b0 = a0 + seg, b1 = a1 + seg;
                    t.Add(a0); t.Add(a1); t.Add(b1);
                    t.Add(a0); t.Add(b1); t.Add(b0);
                }
            }
            var mesh = new Mesh { name = truth ? "Z2_SunTruth" : "Z2_SunIllusion" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void BuildAtmosphere(Camera cam, Light sun)
        {
            var lieFog = new Color(0.24f, 0.1f, 0.09f);
            var lieAmbient = new Color(0.27f, 0.17f, 0.17f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = lieAmbient;
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.17f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.08f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = lieFog;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = lieFog;
            cam.farClipPlane = 1000f;

            // Иллюзия: кровавый закат и багровые факелы. Правда: мягкий золотой закат
            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var so = new SerializedObject(mood);
            so.FindProperty("illusionAmbient").colorValue = lieAmbient;
            so.FindProperty("illusionFog").colorValue = lieFog;
            so.FindProperty("illusionTorch").colorValue = new Color(1f, 0.3f, 0.15f);
            so.FindProperty("truthAmbient").colorValue = new Color(0.4f, 0.34f, 0.4f);
            so.FindProperty("truthFog").colorValue = new Color(0.72f, 0.52f, 0.46f);
            so.FindProperty("truthTorch").colorValue = new Color(1f, 0.72f, 0.42f);
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("illusionSun").colorValue = new Color(1f, 0.42f, 0.3f);
            so.FindProperty("truthSun").colorValue = new Color(1f, 0.74f, 0.5f);
            so.FindProperty("illusionSunIntensity").floatValue = 2.35f;
            so.FindProperty("truthSunIntensity").floatValue = 2.75f;
            var list = so.FindProperty("torches");
            list.arraySize = warmLights.Count;
            for (int i = 0; i < warmLights.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = warmLights[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var haze = new GameObject("Haze").transform;
            RuinKit.Haze(haze, "ValleyHaze", Ground(Village.c, 3f), new Vector3(56f, 5f, 56f), pHaze, new Color(0.62f, 0.46f, 0.42f, 0.035f), 26, 10f, 16f);
            RuinKit.Probe(haze, Ground(Village.c, 4f), new Vector3(64f, 24f, 64f), 1f);
            RuinKit.Probe(haze, new Vector3(44f, FortH + 4f, 84f), new Vector3(26f, 12f, 22f), 1f);

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
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.75f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.55f);
            color.contrast.Override(18f);
            color.saturation.Override(-4f);
            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.97f, 0.98f, 1.04f, -0.01f));
            lgg.gain.Override(new Vector4(1.03f, 1f, 0.96f, 0f));
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.18f);
            grain.response.Override(0.85f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static void BuildNarration(Transform world)
        {
            var root = new GameObject("Narration").transform;
            root.SetParent(world, false);
            NarrationZone(root, "z2.enter", Ground(SpawnPos, 1f), 3f, true);
            NarrationZone(root, "z2.smith_seen", Ground(ForgePos, 1f), 10f, false);
            NarrationZone(root, "z2.fortress_enter", new Vector3(44f, FortH + 1f, FortMinZ - 2f), 5f, false);
            // Конец зоны — SceneFlow (N11) повесит сюда переход в рай
            var exit = new GameObject("ZoneExit_ToParadise");
            exit.transform.SetParent(root, false);
            exit.transform.position = new Vector3(FortMaxX + 30f, FortH + 21f, 88f);
            var bc = exit.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(4f, 5f, 5f);
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
        static Vector3 Flat(Vector2 v) => v.sqrMagnitude > 1e-6f ? new Vector3(v.x, 0f, v.y).normalized : Vector3.forward;
        static Vector3 Flat(Vector3 v) => Flat(new Vector2(v.x, v.z));

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

        static GameObject DualObject(string name, Transform parent, Vector3 pos, Quaternion rot, Mesh lie, Material[] lieMats, Mesh truth, Material[] trueMats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var lieGo = Z.MeshObject("Illusion", go.transform, lie, lieMats, false);
            var trueGo = Z.MeshObject("Truth", go.transform, truth, trueMats, false);
            trueGo.SetActive(false);
            var iv = go.AddComponent<IllusionVisual>();
            SetRef(iv, "illusionModel", lieGo);
            SetRef(iv, "truthModel", trueGo);
            return go;
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
