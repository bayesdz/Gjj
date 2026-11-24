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
    /// Собирает зону 1 целиком из кода в стиле low-poly: рельеф огромной пещеры, скалы,
    /// сталагмиты, колонны, кристаллы, «небо» из светлячков вместо потолка, пропасть с мостом,
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

        static float Height(float x, float z)
        {
            float h = FloorH(x, z) + (Noise(x, z, 0.4f, 700f) - 0.5f) * 0.35f;
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
        static System.Random rng;

        // Материалы
        static Material mFloor, mFloorAlt, mWall, mWallHigh, mChasm;
        static Material mRock, mRockDark, mCrystalRed, mCrystalViolet, mMushStem, mMushCap;
        static Material mWood, mIron, mFlame, mBone, mHide, mHideWarm, mClothRed, mClothBlue, mClothYellow;
        static Material mCharcoal, mOchre, mBlack, mRune;
        static Material mGobSkin, mGobEyes, mGobSkinTrue, mGobEyesTrue, mTunic;
        static Material mShining, mShiningWing;
        static Material gStars, gSpores, gEmbers, gVeil;

        [MenuItem("False Paradise/Build Zone 1 (Goblin Lair)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/_Game/Scenes");
            EnsureFolder(MatDir);
            if (AssetDatabase.LoadAssetAtPath<Object>(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
            if (AssetDatabase.LoadAssetAtPath<Object>(PostPath) != null) AssetDatabase.DeleteAsset(PostPath);

            generated.Clear();
            moodLights.Clear();
            rng = new System.Random(Seed);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            try
            {
                EditorUtility.DisplayProgressBar("Zone 1", "Материалы", 0.05f);
                CreateMaterials();

                var world = new GameObject("Zone1_Lair").transform;

                EditorUtility.DisplayProgressBar("Zone 1", "Рельеф пещеры", 0.15f);
                BuildTerrain(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Скалы, сталагмиты, кристаллы", 0.35f);
                BuildDecor(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Небо из светлячков", 0.5f);
                BuildSky(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Пропасть и мост", 0.6f);
                var safePoints = BuildChasmAndBridge(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Лагерь гоблинов", 0.68f);
                BuildCamp(world);
                BuildTorches(world);

                EditorUtility.DisplayProgressBar("Zone 1", "Предметы, клетка, выход", 0.75f);
                BuildItems(world);
                var cagePos = BuildCage(world);
                BuildExit(world);

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
                Debug.Log($"[False Paradise] Зона 1 собрана: {ScenePath}. Мешей: {generated.Count}. Нажмите Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- материалы

        static void CreateMaterials()
        {
            // Камень пещеры: холодный, с фиолетовым оттенком
            mFloor = Lit("Z1_Floor", new Color(0.24f, 0.22f, 0.27f));
            mFloorAlt = Lit("Z1_FloorGravel", new Color(0.19f, 0.18f, 0.22f));
            mWall = Lit("Z1_Wall", new Color(0.2f, 0.19f, 0.24f));
            mWallHigh = Lit("Z1_WallHigh", new Color(0.12f, 0.11f, 0.16f));
            mChasm = Lit("Z1_ChasmRock", new Color(0.22f, 0.08f, 0.08f));
            mRock = Lit("Z1_Rock", new Color(0.29f, 0.27f, 0.31f));
            mRockDark = Lit("Z1_RockDark", new Color(0.15f, 0.14f, 0.18f));

            // Свечение: багровые кристаллы (иллюзия зоны 1), фиолетовые, бирюзовые грибы
            mCrystalRed = Lit("Z1_CrystalRed", new Color(0.85f, 0.08f, 0.16f), 3.5f, 0.6f);
            mCrystalViolet = Lit("Z1_CrystalViolet", new Color(0.5f, 0.18f, 0.95f), 3f, 0.6f);
            mMushStem = Lit("Z1_MushStem", new Color(0.62f, 0.6f, 0.55f));
            mMushCap = Lit("Z1_MushCap", new Color(0.15f, 0.85f, 0.8f), 2.2f);

            mWood = Lit("Z1_Wood", new Color(0.33f, 0.22f, 0.14f));
            mIron = Lit("Z1_Iron", new Color(0.22f, 0.22f, 0.25f), 0f, 0.45f, 0.6f);
            mFlame = Lit("Z1_Flame", new Color(1f, 0.45f, 0.1f), 6f);
            mBone = Lit("Z1_Bone", new Color(0.78f, 0.72f, 0.6f));
            mHide = Lit("Z1_HideDark", new Color(0.2f, 0.13f, 0.11f));
            mHideWarm = Lit("Z1_HideWarm", new Color(0.62f, 0.42f, 0.26f));
            mClothRed = Lit("Z1_ClothRed", new Color(0.72f, 0.28f, 0.22f));
            mClothBlue = Lit("Z1_ClothBlue", new Color(0.28f, 0.44f, 0.7f));
            mClothYellow = Lit("Z1_ClothYellow", new Color(0.88f, 0.72f, 0.35f));
            mCharcoal = Lit("Z1_Charcoal", new Color(0.04f, 0.04f, 0.05f));
            mOchre = Lit("Z1_Ochre", new Color(0.85f, 0.5f, 0.2f), 0.4f);
            mBlack = Lit("Z1_Void", new Color(0.005f, 0.005f, 0.008f));
            mRune = Lit("Z1_ElvenRune", new Color(0.3f, 1f, 0.9f), 4f);

            // Гоблины: иллюзия — тёмные, с красными глазами; правда — тёплые, в одежде
            mGobSkin = Lit("Z1_GoblinSkin", new Color(0.17f, 0.2f, 0.15f));
            mGobEyes = Lit("Z1_GoblinEyes", new Color(1f, 0.05f, 0.02f), 8f);
            mGobSkinTrue = Lit("Z1_GoblinSkinTrue", new Color(0.46f, 0.62f, 0.36f));
            mGobEyesTrue = Lit("Z1_GoblinEyesTrue", new Color(0.06f, 0.05f, 0.04f));
            mTunic = Lit("Z1_Tunic", new Color(0.52f, 0.36f, 0.22f));

            mShining = Lit("Z1_Shining", new Color(1f, 0.93f, 0.78f), 3f);
            mShiningWing = Lit("Z1_ShiningWing", new Color(1f, 0.85f, 0.55f), 4.5f);

            gStars = Glow("Z1_GlowStars", new Color(1f, 1f, 1f), 3.5f);
            gSpores = Glow("Z1_GlowSpores", new Color(1f, 1f, 1f), 2.5f);
            gEmbers = Glow("Z1_GlowEmbers", new Color(1f, 1f, 1f), 4f);
            gVeil = Glow("Z1_GlowVeil", new Color(0.3f, 1f, 0.9f), 1.6f);
        }

        static Material Lit(string name, Color c, float emission = 0f, float smooth = 0.08f, float metallic = 0f)
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

        // ---------------------------------------------------------------- декор

        static void BuildDecor(Transform world)
        {
            var root = new GameObject("Decor").transform;
            root.SetParent(world, false);

            // Твёрдое (с коллайдером): 0 камень, 1 тёмный камень, 2 кристалл красный, 3 кристалл фиолетовый
            var solidMats = new[] { mRock, mRockDark, mCrystalRed, mCrystalViolet };
            // Мелочь (без коллайдера): 0 камень, 1 ножка гриба, 2 шляпка гриба, 3 тёмный камень
            var detailMats = new[] { mRock, mMushStem, mMushCap, mRockDark };
            var solid = new Dictionary<Vector2Int, LowPolyMesh>();
            var detail = new Dictionary<Vector2Int, LowPolyMesh>();
            var crystalLights = new List<Vector3>();
            var lightsRoot = new GameObject("CrystalLights").transform;
            lightsRoot.SetParent(world, false);

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
                if (d > 6f) continue;
                float dc = ChasmSdf(x, z);
                float path = PathDist(x, z);
                var p = new Vector3(x, Height(x, z), z);
                var inward = -Gradient(x, z); // от стены в пещеру

                // Валуны у стен
                if (d > -1.5f && d < 3.5f && dc > 2f && R01() < 0.5f)
                    Boulder(Chunk(solid, 4, x, z), p, R(1.2f, 3.4f));

                // Сталагмиты (с маленькими соседями)
                if (d > -4f && d < 1f && dc > 2f && path > 3.5f && R01() < 0.17f)
                {
                    var lp = Chunk(solid, 4, x, z);
                    Stalagmite(lp, p, R(1.6f, 6f));
                    int kids = rng.Next(0, 3);
                    for (int k = 0; k < kids; k++)
                    {
                        var q = p + new Vector3(R(-1.6f, 1.6f), 0f, R(-1.6f, 1.6f));
                        if (PathDist(q.x, q.z) < 3f) continue;
                        q.y = Height(q.x, q.z);
                        Stalagmite(lp, q, R(0.6f, 2f));
                    }
                }

                // Кристаллы
                if (d > -1f && d < 2f && dc > 2f && R01() < 0.06f)
                {
                    bool violet = R01() < 0.25f;
                    CrystalCluster(Chunk(solid, 4, x, z), p, inward, violet ? 3 : 2);
                    bool far = true;
                    foreach (var l in crystalLights) if (Vector3.Distance(l, p) < 16f) far = false;
                    if (far && crystalLights.Count < 16)
                    {
                        crystalLights.Add(p);
                        var light = PointLight(lightsRoot, "CrystalLight", p + Vector3.up * 1.2f + new Vector3(inward.x, 0f, inward.z) * 1.2f,
                            violet ? new Color(0.6f, 0.25f, 1f) : new Color(1f, 0.12f, 0.2f), 9f, 2.6f);
                        light.shadows = LightShadows.None;
                    }
                }

                // Светящиеся грибы: больше всего у норы и в тупике
                float mushChance = Vector2.Distance(new Vector2(x, z), Warren.c) < 28f || Vector2.Distance(new Vector2(x, z), Nook.c) < 14f ? 0.08f : 0.015f;
                if (d > -3f && d < 0.5f && dc > 2f && R01() < mushChance)
                    MushroomCluster(Chunk(detail, 4, x, z), p);

                // Галька на полу
                if (d < -1f && dc > 1f && path > 1f && R01() < 0.09f)
                    Pebble(Chunk(detail, 4, x, z), p);
            }

            // Огромные колонны: снизу сталагмит, сверху расширяются и уходят в темноту — там потолок, которого не видно
            Vector2[] pillars =
            {
                new Vector2(22f, 44f), new Vector2(-6f, 52f), new Vector2(16f, 64f), new Vector2(20f, 34f),
                new Vector2(-34f, 90f), new Vector2(-4f, 94f),
                new Vector2(44f, 144f), new Vector2(16f, 168f), new Vector2(50f, 168f), new Vector2(10f, 150f),
            };
            foreach (var pp in pillars)
                Pillar(Chunk(solid, 4, pp.x, pp.y), Ground(pp, -0.5f), R(0.8f, 1.25f));

            foreach (var kv in solid)
            {
                var mesh = Register(kv.Value.ToMesh($"Z1_Decor_{kv.Key.x}_{kv.Key.y}"));
                var go = MeshObject($"Solid_{kv.Key.x}_{kv.Key.y}", root, mesh, solidMats, true);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            foreach (var kv in detail)
            {
                var mesh = Register(kv.Value.ToMesh($"Z1_Detail_{kv.Key.x}_{kv.Key.y}"));
                var go = MeshObject($"Detail_{kv.Key.x}_{kv.Key.y}", root, mesh, detailMats, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
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
            float w = h * R(0.16f, 0.24f);
            var m = Matrix4x4.TRS(p + Vector3.down * 0.3f, Quaternion.Euler(R(-7f, 7f), R(0f, 360f), R(-7f, 7f)), Vector3.one);
            lp.Lathe(R01() < 0.5f ? 0 : 1, m,
                new[] { 0f, h * 0.3f, h * 0.65f, h },
                new[] { w, w * 0.7f, w * 0.38f, 0f },
                rng.Next(5, 8), rng, 0.18f, false, 0.3f);
        }

        static void Pillar(LowPolyMesh lp, Vector3 p, float s)
        {
            var m = Matrix4x4.TRS(p, Quaternion.Euler(R(-3f, 3f), R(0f, 360f), R(-3f, 3f)), Vector3.one * s);
            lp.Lathe(1, m,
                new[] { 0f, 3f, 9f, 18f, 27f, 36f, 48f, 60f },
                new[] { 5f, 3.6f, 2.3f, 1.8f, 2.1f, 3f, 5f, 8f },
                8, rng, 0.14f, false, 0.25f);
            // Сколы у основания
            for (int i = 0; i < 4; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.5f * s;
                q.y = Height(q.x, q.z);
                Boulder(lp, q, R(1f, 2.2f));
            }
        }

        static void CrystalCluster(LowPolyMesh lp, Vector3 p, Vector3 inward, int sub)
        {
            int n = rng.Next(3, 8);
            for (int i = 0; i < n; i++)
            {
                var dir = (Vector3.up * R(0.8f, 1.6f) + inward * R(0.2f, 0.9f) + new Vector3(R(-0.5f, 0.5f), 0f, R(-0.5f, 0.5f))).normalized;
                float len = R(0.6f, 2.6f) * (i == 0 ? 1.4f : 1f);
                float r = len * R(0.12f, 0.2f);
                var m = Matrix4x4.TRS(p + new Vector3(R(-0.6f, 0.6f), -0.2f, R(-0.6f, 0.6f)),
                    Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                lp.Lathe(sub, m, new[] { 0f, len * 0.78f, len }, new[] { r, r * 0.95f, 0f }, 6, rng, 0.06f);
            }
        }

        static void MushroomCluster(LowPolyMesh lp, Vector3 p)
        {
            int n = rng.Next(3, 8);
            for (int i = 0; i < n; i++)
            {
                float k = R(0.5f, 1.6f) * (i == 0 ? 1.5f : 1f);
                var q = p + new Vector3(R(-0.9f, 0.9f), 0f, R(-0.9f, 0.9f));
                q.y = Height(q.x, q.z) - 0.05f;
                float h = 0.35f * k;
                var m = Matrix4x4.TRS(q, Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)), Vector3.one);
                lp.Lathe(1, m, new[] { 0f, h }, new[] { 0.05f * k, 0.035f * k }, 5);
                lp.Lathe(2, m, new[] { h - 0.04f * k, h + 0.06f * k, h + 0.16f * k }, new[] { 0.22f * k, 0.18f * k, 0f }, 7, null, 0f, true);
            }
        }

        static void Pebble(LowPolyMesh lp, Vector3 p)
        {
            float s = R(0.15f, 0.45f);
            var m = Matrix4x4.TRS(p + Vector3.down * s * 0.2f, Quaternion.Euler(0f, R(0f, 360f), 0f),
                new Vector3(s * R(0.8f, 1.4f), s * R(0.4f, 0.8f), s));
            lp.Lathe(R01() < 0.5f ? 0 : 3, m, new[] { 0f, 0.6f, 1f }, new[] { 1f, 0.8f, 0.2f }, 5, rng, 0.2f);
        }

        // ---------------------------------------------------------------- небо пещеры

        /// <summary>
        /// Потолка не видно: вместо него тёмная бездна, в которой светятся тысячи пещерных
        /// светлячков, а с невидимого свода свисают их светящиеся нити.
        /// </summary>
        static void BuildSky(Transform world)
        {
            var root = new GameObject("CaveSky").transform;
            root.SetParent(world, false);

            var stars = new GlowPointsMesh();
            for (int i = 0; i < 3200; i++)
            {
                float x = R(MinX - 20f, MaxX + 20f);
                float z = R(MinZ - 20f, MaxZ + 20f);
                float y = R(42f, 95f);
                float t = R01();
                Color c = t < 0.7f ? new Color(0.35f, 0.9f, 1f) : t < 0.9f ? new Color(0.6f, 0.45f, 1f) : new Color(1f, 0.75f, 0.9f);
                c *= R(0.35f, 1f);
                c.a = 1f;
                stars.Point(new Vector3(x, y, z), R(0.12f, 0.42f) * (y / 60f), c, R(0f, 360f));
            }
            MeshObject("Glowworms", root, Register(stars.ToMesh("Z1_Glowworms")), new[] { gStars }, false);

            // Светящиеся нити, свисающие из темноты над пещерой
            var threads = new GlowPointsMesh();
            int made = 0;
            for (int tries = 0; tries < 4000 && made < 220; tries++)
            {
                float x = R(MinX, MaxX);
                float z = R(MinZ, MaxZ);
                float d = Sdf(x, z);
                if (d > 8f) continue;
                made++;
                float top = R(44f, 60f);
                float bottom = Mathf.Max(Height(x, z) + 9f, top - R(8f, 30f));
                for (float y = top; y > bottom; y -= R(0.3f, 0.6f))
                {
                    float k = Mathf.InverseLerp(top, bottom, y);
                    var c = new Color(0.4f, 0.95f, 1f) * Mathf.Lerp(0.25f, 1f, k);
                    c.a = 1f;
                    threads.Point(new Vector3(x + R(-0.03f, 0.03f), y, z + R(-0.03f, 0.03f)), 0.05f + 0.06f * k, c, R(0f, 360f));
                }
                // Капля-приманка на конце нити
                var drop = new Color(0.6f, 1f, 1f);
                threads.Point(new Vector3(x, bottom, z), 0.16f, drop, R(0f, 360f));
            }
            MeshObject("GlowThreads", root, Register(threads.ToMesh("Z1_GlowThreads")), new[] { gStars }, false);
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
            Particles(embers, gEmbers, new Vector3(6f, 2f, 34f), 60f, 7f, 12f, 0.05f, 0.14f,
                new Color(1f, 0.45f, 0.1f), new Color(1f, 0.15f, 0.05f), -0.12f, 600);

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
            var fl = PointLight(fire, "FireLight", fire.position + Vector3.up * 1.4f, new Color(1f, 0.45f, 0.15f), 16f, 4.5f);
            fl.shadows = LightShadows.Soft;
            var flick = fl.gameObject.AddComponent<TorchFlicker>();
            SetFloat(flick, "baseIntensity", 4.5f);
            SetFloat(flick, "flickerAmount", 1f);
            moodLights.Add(fl);

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

        static void BuildTorches(Transform world)
        {
            var root = new GameObject("Torches").transform;
            root.SetParent(world, false);
            Vector2[] spots =
            {
                new Vector2(-6f, 14f), new Vector2(4f, 30f),                         // путь к первому бою
                new Vector2(0f, 40f), new Vector2(17f, 55f), new Vector2(-1f, 64f),  // зал первого боя
                new Vector2(-14f, 82f),                                               // спуск к логову
                new Vector2(-22f, 90f), new Vector2(-33f, 107f), new Vector2(-20f, 116f), new Vector2(-10f, 101f), // лагерь
                new Vector2(-44f, 106f),                                              // к тупику
                new Vector2(22f, 140f), new Vector2(31f, 165f), new Vector2(46f, 151f), // зал с клеткой
                new Vector2(44f, 178f), new Vector2(54f, 192f),                       // к выходу
            };
            foreach (var s in spots) Torch(root, Ground(s));
        }

        static void Torch(Transform parent, Vector3 pos)
        {
            if (torchMesh == null || !generated.Contains(torchMesh))
            {
                var lp = new LowPolyMesh(3);
                lp.Lathe(0, Matrix4x4.identity, new[] { -0.2f, 2.2f }, new[] { 0.07f, 0.05f }, 5);
                lp.Lathe(1, Matrix4x4.TRS(new Vector3(0f, 2.15f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.15f, 0.3f }, new[] { 0.08f, 0.2f, 0.24f }, 6);
                lp.Lathe(2, Matrix4x4.TRS(new Vector3(0f, 2.4f, 0f), Quaternion.identity, Vector3.one), new[] { 0f, 0.25f, 0.65f }, new[] { 0.17f, 0.13f, 0f }, 5);
                torchMesh = Register(lp.ToMesh("Z1_Torch"));
            }
            var t = new GameObject("Torch").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f));
            MeshObject("Mesh", t, torchMesh, new[] { mWood, mIron, mFlame }, false);
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.2f, 0f);
            col.radius = 0.15f;
            col.height = 2.6f;
            var l = PointLight(t, "Light", pos + Vector3.up * 2.9f, new Color(1f, 0.35f, 0.15f), 11f, 3f);
            l.shadows = LightShadows.None;
            l.gameObject.AddComponent<TorchFlicker>();
            moodLights.Add(l);
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
            var glow = PointLight(shining, "Glow", shining.position + Vector3.up * 1.4f, new Color(1f, 0.85f, 0.55f), 13f, 4f);
            glow.shadows = LightShadows.Soft;

            var cage = root.gameObject.AddComponent<ShiningCage>();
            SetRef(cage, "door", door);
            SetRef(cage, "shining", shining);
            SetRef(cage, "shiningLight", glow);
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
            var eyes = PointLight(lieGo.transform, "EyeGlow", go.transform.position + go.transform.forward * 0.5f + Vector3.up * 1.1f, new Color(1f, 0.08f, 0.04f), 2.2f, 1.2f);
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

            // Тёплый фонарь героя
            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(cam.transform, false);
            lantern.transform.localPosition = new Vector3(0.3f, -0.2f, 0.2f);
            lantern.type = LightType.Point;
            lantern.range = 8f;
            lantern.intensity = 0.9f;
            lantern.color = new Color(1f, 0.82f, 0.6f);
            lantern.shadows = LightShadows.None;

            // Летающие споры вокруг игрока
            var spores = new GameObject("Spores");
            spores.transform.SetParent(player.transform, false);
            spores.transform.localPosition = new Vector3(0f, 3f, 0f);
            Particles(spores, gSpores, new Vector3(36f, 10f, 36f), 40f, 6f, 11f, 0.035f, 0.08f,
                new Color(0.4f, 1f, 0.95f), new Color(0.8f, 0.6f, 1f), -0.005f, 500);

            new GameObject("HUD").AddComponent<GameHUD>();
            return cam;
        }

        static void BuildAtmosphere(Camera cam)
        {
            var illusionFog = new Color(0.03f, 0.026f, 0.05f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.09f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = illusionFog;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = illusionFog;
            cam.farClipPlane = 350f;

            // Холодный «лунный» свет светлячков сверху: без него грани low-poly не читаются
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.name = "Glowworm Skylight";
                l.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
                l.color = new Color(0.45f, 0.55f, 0.95f);
                l.intensity = 0.32f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.7f;
            }

            // Бледный дневной свет из расщелины за спиной — оттуда пришли друзья
            var crack = PointLight(null, "LightFromCrack", Ground(SpawnPos + new Vector2(0f, -9f), 5f), new Color(0.65f, 0.78f, 1f), 20f, 3f);
            crack.shadows = LightShadows.Soft;

            // Настроение: иллюзия холодная и багровая, правда тёплая
            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var so = new SerializedObject(mood);
            so.FindProperty("illusionAmbient").colorValue = new Color(0.1f, 0.09f, 0.14f);
            so.FindProperty("illusionFog").colorValue = illusionFog;
            so.FindProperty("illusionTorch").colorValue = new Color(1f, 0.35f, 0.15f);
            so.FindProperty("truthAmbient").colorValue = new Color(0.2f, 0.17f, 0.13f);
            so.FindProperty("truthFog").colorValue = new Color(0.09f, 0.07f, 0.05f);
            so.FindProperty("truthTorch").colorValue = new Color(1f, 0.78f, 0.45f);
            var list = so.FindProperty("torches");
            list.arraySize = moodLights.Count;
            for (int i = 0; i < moodLights.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = moodLights[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
        }

        static VolumeProfile PostProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostPath);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.3f);
            bloom.scatter.Override(0.72f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.5f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.55f);
            color.contrast.Override(14f);
            color.saturation.Override(6f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.9f, 0.92f, 1.1f, 0f));

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

        static void Particles(GameObject go, Material mat, Vector3 box, float rate, float lifeMin, float lifeMax,
            float sizeMin, float sizeMax, Color a, Color b, float gravity, int maxParticles)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = maxParticles;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
    }
}
