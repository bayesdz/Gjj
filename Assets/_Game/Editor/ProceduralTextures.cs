using System.IO;
using UnityEditor;
using UnityEngine;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Генерирует тайловые PBR-текстуры (Albedo, Normal, Emission) и настраивает URP/Lit материалы.
    /// Текстуры сохраняются как PNG в Assets/_Game/Art/GeneratedTextures/, чтобы работать и в редакторе, и в билде.
    /// </summary>
    public static class ProceduralTextures
    {
        public const string TexDir = "Assets/_Game/Art/GeneratedTextures";
        public const string MatDir = "Assets/_Game/Art/GeneratedMaterials";
        const int Size = 256; // Быстрая генерация + чёткие тайлы с нормалями

        static bool generatedThisSession;

        public static void EnsureAllTextures(bool force = false)
        {
            if (generatedThisSession && !force) return;
            SandboxBuilder.EnsureFolder(TexDir);
            SandboxBuilder.EnsureFolder(MatDir);

            bool anyCreated = false;

            anyCreated |= GenStoneBrick("StoneBrick", new Color(0.24f, 0.23f, 0.26f), new Color(0.11f, 0.10f, 0.12f), moss: 0.15f, force: force);
            anyCreated |= GenStoneBrick("DungeonFloor", new Color(0.26f, 0.24f, 0.22f), new Color(0.10f, 0.09f, 0.09f), moss: 0.06f, bigTiles: true, force: force);
            anyCreated |= GenCaveRock("CaveRock", new Color(0.20f, 0.18f, 0.21f), new Color(0.10f, 0.09f, 0.11f), force);
            anyCreated |= GenWoodPlanks("WoodPlanks", new Color(0.36f, 0.23f, 0.13f), new Color(0.18f, 0.10f, 0.05f), force);
            anyCreated |= GenTreeBark("TreeBark", new Color(0.35f, 0.24f, 0.16f), new Color(0.18f, 0.11f, 0.07f), force);
            anyCreated |= GenTreeBark("CharredBark", new Color(0.10f, 0.07f, 0.06f), new Color(0.03f, 0.02f, 0.02f), force);
            anyCreated |= GenFoliage("FoliageGreen", new Color(0.34f, 0.62f, 0.24f), new Color(0.18f, 0.42f, 0.14f), force);
            anyCreated |= GenFoliage("FoliageBlossom", new Color(0.98f, 0.72f, 0.82f), new Color(0.86f, 0.48f, 0.64f), force);
            anyCreated |= GenParadiseGrass("ParadiseGrass", force);
            anyCreated |= GenMarble("WhiteMarble", new Color(0.94f, 0.92f, 0.88f), new Color(0.76f, 0.72f, 0.65f), force);
            anyCreated |= GenGoldOrnate("GoldOrnate", force);
            anyCreated |= GenWaterRipple("WaterRipple", force);
            anyCreated |= GenHellGround("HellGround", force);
            anyCreated |= GenCaveRock("ObsidianRock", new Color(0.09f, 0.07f, 0.08f), new Color(0.03f, 0.02f, 0.03f), force);
            anyCreated |= GenLava("LavaSurface", force);
            anyCreated |= GenElvenStone("ElvenStone", force);
            anyCreated |= GenWoodPlanks("OgreWood", new Color(0.28f, 0.19f, 0.12f), new Color(0.12f, 0.08f, 0.05f), force);
            anyCreated |= GenSoilGarden("SoilGarden", force);
            anyCreated |= GenMetalIron("MetalIron", force);
            anyCreated |= GenSoftParticle("SoftParticle", force);
            anyCreated |= GenCavePainting("CavePainting", force);

            if (anyCreated) AssetDatabase.Refresh();
            generatedThisSession = true;
        }

        // ==================== МАТЕРИАЛЫ ====================

        public static Material GetPbrMaterial(
            string matName,
            string texBaseName,
            Color tint,
            float smoothness = 0.25f,
            float metallic = 0f,
            float bumpScale = 1.0f,
            Vector2? tiling = null,
            bool hasEmissionMap = false,
            Color? emissionColor = null)
        {
            EnsureAllTextures();
            string path = $"{MatDir}/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Normal.png");

            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", tint);
            m.color = tint;

            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", bumpScale);
                m.EnableKeyword("_NORMALMAP");
            }

            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);

            Vector2 t = tiling ?? Vector2.one;
            m.SetTextureScale("_BaseMap", t);
            m.mainTextureScale = t;

            if (hasEmissionMap || emissionColor.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                if (hasEmissionMap)
                {
                    var em = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Emission.png");
                    if (em != null) m.SetTexture("_EmissionMap", em);
                }
                m.SetColor("_EmissionColor", emissionColor ?? Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material GetWaterMaterial(string matName, Color shallowColor, float smoothness = 0.92f, float tiling = 4f)
        {
            EnsureAllTextures();
            string path = $"{MatDir}/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/WaterRipple_Normal.png");
            m.SetColor("_BaseColor", shallowColor);
            m.color = shallowColor;
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 0.45f);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Metallic", 0.1f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", shallowColor * 0.22f);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material GetParticleMaterial(string matName, Color color, bool additive = true)
        {
            EnsureAllTextures();
            string path = $"{MatDir}/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/SoftParticle_Albedo.png");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);

            // Transparent surface settings for URP Particles/Unlit
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f); // 0=Alpha, 2=Additive
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;

            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material GetProceduralSkybox(string matName, Color skyTint, Color groundColor, float atmosphereThickness, float exposure, float sunSize = 0.04f)
        {
            EnsureAllTextures();
            string path = $"{MatDir}/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return null;
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            if (m.HasProperty("_SunSize")) m.SetFloat("_SunSize", sunSize);
            if (m.HasProperty("_SunSizeConvergence")) m.SetFloat("_SunSizeConvergence", 5f);
            if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", atmosphereThickness);
            if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", skyTint);
            if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", groundColor);
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", exposure);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ==================== ГЕНЕРАТОРЫ ТЕКСТУР ====================

        static bool Exists(string name, bool force, bool needEmission = false)
        {
            if (force) return false;
            bool ok = File.Exists($"{TexDir}/{name}_Albedo.png") && File.Exists($"{TexDir}/{name}_Normal.png");
            if (needEmission) ok &= File.Exists($"{TexDir}/{name}_Emission.png");
            return ok;
        }

        static bool GenStoneBrick(string name, Color stoneCol, Color mortarCol, float moss, bool bigTiles = false, bool force = false)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];
            int rows = bigTiles ? 4 : 6;
            int cols = bigTiles ? 4 : 3;

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                int row = Mathf.FloorToInt(fy * rows);
                float rowY = (fy * rows) - row;
                float xOff = (row % 2 == 1) ? 0.5f : 0f;

                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float sx = (fx * cols + xOff);
                    int col = Mathf.FloorToInt(sx) % cols;
                    float colX = sx - Mathf.Floor(sx);

                    float n1 = TileNoise(fx * 6f, fy * 6f, 6, 11);
                    float n2 = TileNoise(fx * 18f, fy * 18f, 18, 37);
                    float edgeWarp = (n1 - 0.5f) * 0.045f;

                    float mortarW = bigTiles ? 0.045f : 0.06f;
                    bool isMortar = rowY + edgeWarp < mortarW || rowY + edgeWarp > 1f - mortarW ||
                                    colX + edgeWarp < mortarW || colX + edgeWarp > 1f - mortarW;

                    float bevelX = Mathf.Min(colX, 1f - colX) / 0.14f;
                    float bevelY = Mathf.Min(rowY, 1f - rowY) / 0.14f;
                    float bevel = Mathf.Clamp01(Mathf.Min(bevelX, bevelY));

                    float brickVar = Hash2(col + row * 17, row * 31) * 0.18f - 0.09f;
                    Color c;
                    float h;
                    if (isMortar)
                    {
                        c = mortarCol * (0.85f + n2 * 0.3f);
                        h = 0.12f + n2 * 0.08f;
                    }
                    else
                    {
                        c = stoneCol * (0.88f + brickVar + (n1 - 0.5f) * 0.22f + (n2 - 0.5f) * 0.12f);
                        h = 0.45f + bevel * 0.42f + (n1 - 0.5f) * 0.14f + (n2 - 0.5f) * 0.06f;
                    }

                    if (moss > 0f)
                    {
                        float mMask = Mathf.Clamp01((TileNoise(fx * 4f, fy * 4f, 4, 91) - (1f - moss * 2.2f)) * 3.5f);
                        if (isMortar) mMask *= 1.4f;
                        c = Color.Lerp(c, new Color(0.14f, 0.22f, 0.10f), Mathf.Clamp01(mMask) * 0.7f);
                    }

                    albedo.SetPixel(x, y, c);
                    height[y * Size + x] = Mathf.Clamp01(h);
                }
            }

            SavePair(name, albedo, height, 3.2f);
            return true;
        }

        static bool GenCaveRock(string name, Color hi, Color lo, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float n = TileFbm(fx, fy, 4, 17);
                    float ridge = 1f - Mathf.Abs(TileNoise(fx * 8f, fy * 8f, 8, 53) * 2f - 1f);
                    float h = Mathf.Clamp01(n * 0.65f + ridge * ridge * 0.35f);
                    Color c = Color.Lerp(lo, hi, h);
                    albedo.SetPixel(x, y, c);
                    height[y * Size + x] = h;
                }
            }

            SavePair(name, albedo, height, 4.0f);
            return true;
        }

        static bool GenWoodPlanks(string name, Color light, Color dark, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];
            int planks = 6;

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                int p = Mathf.FloorToInt(fy * planks);
                float py = fy * planks - p;
                float pVar = Hash2(p, 7) * 0.22f - 0.11f;

                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float grain = TileNoise(fx * 4f, fy * 28f, 28, 23 + p * 5);
                    float fine = TileNoise(fx * 12f, fy * 48f, 48, 71);
                    bool seam = py < 0.045f || py > 0.955f;

                    float h = seam ? 0.1f : 0.65f + grain * 0.22f + fine * 0.1f;
                    Color c = seam ? dark * 0.45f : Color.Lerp(dark, light, Mathf.Clamp01(grain * 0.8f + fine * 0.2f + pVar));
                    albedo.SetPixel(x, y, c);
                    height[y * Size + x] = Mathf.Clamp01(h);
                }
            }

            SavePair(name, albedo, height, 2.8f);
            return true;
        }

        static bool GenTreeBark(string name, Color light, Color dark, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float warp = TileNoise(fx * 6f, fy * 6f, 6, 13) * 0.08f;
                    float furrow = TileNoise((fx + warp) * 18f, fy * 4f, 18, 41);
                    float detail = TileNoise(fx * 24f, fy * 16f, 24, 83);
                    float h = Mathf.Clamp01(furrow * 0.72f + detail * 0.28f);
                    albedo.SetPixel(x, y, Color.Lerp(dark, light, h));
                    height[y * Size + x] = h;
                }
            }

            SavePair(name, albedo, height, 4.2f);
            return true;
        }

        static bool GenFoliage(string name, Color hi, Color lo, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float n1 = TileNoise(fx * 12f, fy * 12f, 12, 29);
                    float n2 = TileNoise(fx * 28f, fy * 28f, 28, 61);
                    float h = Mathf.Clamp01(n1 * 0.55f + n2 * 0.45f);
                    albedo.SetPixel(x, y, Color.Lerp(lo, hi, h));
                    height[y * Size + x] = h;
                }
            }

            SavePair(name, albedo, height, 2.2f);
            return true;
        }

        static bool GenParadiseGrass(string name, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float patch = TileFbm(fx, fy, 4, 19);
                    float blades = TileNoise(fx * 32f, fy * 32f, 32, 47);
                    float flower = TileNoise(fx * 24f, fy * 24f, 24, 97);

                    Color c = Color.Lerp(new Color(0.24f, 0.48f, 0.16f), new Color(0.46f, 0.72f, 0.25f), patch * 0.7f + blades * 0.3f);
                    if (flower > 0.89f)
                        c = Color.Lerp(c, new Color(1f, 0.94f, 0.65f), (flower - 0.89f) * 8f);
                    else if (flower < 0.07f)
                        c = Color.Lerp(c, new Color(0.98f, 0.75f, 0.85f), (0.07f - flower) * 10f);

                    albedo.SetPixel(x, y, c);
                    height[y * Size + x] = Mathf.Clamp01(patch * 0.4f + blades * 0.6f);
                }
            }

            SavePair(name, albedo, height, 2.0f);
            return true;
        }

        static bool GenMarble(string name, Color baseCol, Color veinCol, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float turb = TileFbm(fx, fy, 4, 31);
                    float v = Mathf.Sin((fx * 2f + fy + turb * 1.4f) * Mathf.PI * 2f);
                    float vein = Mathf.Pow(1f - Mathf.Abs(v), 7f);
                    Color c = Color.Lerp(baseCol, veinCol, vein * 0.65f + (turb - 0.5f) * 0.08f);
                    albedo.SetPixel(x, y, c);
                    height[y * Size + x] = 0.7f - vein * 0.15f + turb * 0.1f;
                }
            }

            SavePair(name, albedo, height, 1.1f);
            return true;
        }

        static bool GenGoldOrnate(string name, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float dx = Mathf.Abs(Mathf.Repeat(fx * 4f, 1f) - 0.5f);
                    float dy = Mathf.Abs(Mathf.Repeat(fy * 4f, 1f) - 0.5f);
                    float diamond = 1f - Mathf.Clamp01((dx + dy) * 2.2f);
                    float n = TileNoise(fx * 16f, fy * 16f, 16, 43);
                    float h = Mathf.Clamp01(0.45f + diamond * 0.45f + (n - 0.5f) * 0.12f);
                    Color c = Color.Lerp(new Color(0.76f, 0.55f, 0.18f), new Color(1f, 0.86f, 0.42f), h);
                    albedo.SetPixel(x, y, c);
                    height[y * Size + x] = h;
                }
            }

            SavePair(name, albedo, height, 2.4f);
            return true;
        }

        static bool GenWaterRipple(string name, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float w1 = Mathf.Sin((fx * 4f + TileNoise(fx * 4f, fy * 4f, 4, 15) * 0.6f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float w2 = Mathf.Sin((fy * 6f - fx * 2f + TileNoise(fx * 8f, fy * 8f, 8, 67) * 0.5f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float h = w1 * 0.55f + w2 * 0.45f;
                    albedo.SetPixel(x, y, new Color(0.25f, 0.62f, 0.85f));
                    height[y * Size + x] = h;
                }
            }

            SavePair(name, albedo, height, 1.8f);
            return true;
        }

        static bool GenHellGround(string name, bool force)
        {
            if (Exists(name, force, needEmission: true)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var emission = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float n = TileFbm(fx, fy, 4, 27);
                    float crackRaw = Mathf.Abs(TileNoise(fx * 6f, fy * 6f, 6, 73) * 2f - 1f);
                    float crack = Mathf.Clamp01(1f - crackRaw * 11f);
                    float h = Mathf.Clamp01(n * 0.85f - crack * 0.6f + 0.15f);

                    Color rock = Color.Lerp(new Color(0.06f, 0.03f, 0.03f), new Color(0.18f, 0.09f, 0.07f), n);
                    Color glow = new Color(1f, 0.28f, 0.03f) * crack;
                    albedo.SetPixel(x, y, Color.Lerp(rock, new Color(0.95f, 0.25f, 0.04f), crack));
                    emission.SetPixel(x, y, glow);
                    height[y * Size + x] = h;
                }
            }

            SavePair(name, albedo, height, 3.8f, emission);
            return true;
        }

        static bool GenLava(string name, bool force)
        {
            if (Exists(name, force, needEmission: true)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var emission = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float n = TileFbm(fx, fy, 4, 89);
                    float heat = Mathf.Clamp01((n - 0.32f) * 2.1f);
                    Color crust = new Color(0.08f, 0.03f, 0.02f);
                    Color mid = new Color(0.95f, 0.22f, 0.02f);
                    Color hot = new Color(1f, 0.78f, 0.18f);
                    Color c = heat < 0.5f ? Color.Lerp(crust, mid, heat * 2f) : Color.Lerp(mid, hot, (heat - 0.5f) * 2f);

                    albedo.SetPixel(x, y, c);
                    emission.SetPixel(x, y, c * (heat * heat * 1.4f));
                    height[y * Size + x] = 1f - heat * 0.6f;
                }
            }

            SavePair(name, albedo, height, 2.5f, emission);
            return true;
        }

        static bool GenElvenStone(string name, bool force)
        {
            if (Exists(name, force, needEmission: true)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var emission = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float n = TileFbm(fx, fy, 4, 55);
                    // Эльфийские тонкие бирюзовые рунические линии
                    float lineX = Mathf.Abs(Mathf.Repeat(fx * 2f, 1f) - 0.5f);
                    float lineY = Mathf.Abs(Mathf.Repeat(fy * 2f, 1f) - 0.5f);
                    float rune = (lineX < 0.025f || lineY < 0.025f) ? 1f : 0f;
                    float ring = Mathf.Abs(Mathf.Sqrt((lineX - 0.25f) * (lineX - 0.25f) + (lineY - 0.25f) * (lineY - 0.25f)) - 0.14f) < 0.02f ? 1f : 0f;
                    float glowMask = Mathf.Clamp01(rune + ring);

                    Color stone = Color.Lerp(new Color(0.58f, 0.66f, 0.70f), new Color(0.80f, 0.86f, 0.90f), n);
                    Color cyan = new Color(0.25f, 0.88f, 0.85f);
                    albedo.SetPixel(x, y, Color.Lerp(stone, cyan, glowMask * 0.7f));
                    emission.SetPixel(x, y, cyan * glowMask * 0.8f);
                    height[y * Size + x] = 0.65f + (n - 0.5f) * 0.2f - glowMask * 0.25f;
                }
            }

            SavePair(name, albedo, height, 2.2f, emission);
            return true;
        }

        static bool GenSoilGarden(string name, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float furrow = Mathf.Sin(fx * 8f * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float clumps = TileNoise(fx * 24f, fy * 24f, 24, 63);
                    float h = furrow * 0.65f + clumps * 0.35f;
                    albedo.SetPixel(x, y, Color.Lerp(new Color(0.14f, 0.09f, 0.05f), new Color(0.28f, 0.18f, 0.10f), h));
                    height[y * Size + x] = h;
                }
            }

            SavePair(name, albedo, height, 3.2f);
            return true;
        }

        static bool GenMetalIron(string name, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float hammer = TileNoise(fx * 14f, fy * 14f, 14, 77);
                    float rust = Mathf.Clamp01((TileNoise(fx * 6f, fy * 6f, 6, 33) - 0.62f) * 3.5f);
                    Color iron = Color.Lerp(new Color(0.14f, 0.14f, 0.16f), new Color(0.28f, 0.29f, 0.32f), hammer);
                    Color c = Color.Lerp(iron, new Color(0.38f, 0.18f, 0.09f), rust * 0.65f);
                    albedo.SetPixel(x, y, c);
                    height[y * Size + x] = 0.5f + hammer * 0.35f;
                }
            }

            SavePair(name, albedo, height, 2.2f);
            return true;
        }

        static bool GenSoftParticle(string name, bool force)
        {
            string path = $"{TexDir}/{name}_Albedo.png";
            if (!force && File.Exists(path)) return false;
            int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x / (float)(s - 1)) * 2f - 1f;
                    float dy = (y / (float)(s - 1)) * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float a = d * d * (3f - 2f * d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return true;
        }

        static bool GenCavePainting(string name, bool force)
        {
            if (Exists(name, force)) return false;
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var height = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float fy = (float)y / Size;
                for (int x = 0; x < Size; x++)
                {
                    float fx = (float)x / Size;
                    float n = TileFbm(fx, fy, 4, 101);
                    Color rock = Color.Lerp(new Color(0.22f, 0.18f, 0.15f), new Color(0.34f, 0.28f, 0.23f), n);

                    // Примитивный наскальный рисунок: солнце, костёр, 3 фигурки семьи
                    float paint = 0f;
                    // Солнце
                    float ds = Vector2.Distance(new Vector2(fx, fy), new Vector2(0.5f, 0.78f));
                    if (ds < 0.08f) paint = 1f;
                    // Костёр в центре внизу
                    float df = Vector2.Distance(new Vector2(fx, fy), new Vector2(0.5f, 0.24f));
                    if (df < 0.07f && fy > 0.20f) paint = 1f;
                    // Три фигурки (отец, мать, ребёнок)
                    paint = Mathf.Max(paint, StickFigure(fx, fy, 0.26f, 0.22f, 1.0f));
                    paint = Mathf.Max(paint, StickFigure(fx, fy, 0.74f, 0.22f, 0.9f));
                    paint = Mathf.Max(paint, StickFigure(fx, fy, 0.62f, 0.22f, 0.58f));

                    Color ochre = new Color(0.84f, 0.45f, 0.18f);
                    albedo.SetPixel(x, y, Color.Lerp(rock, ochre, paint * (0.72f + n * 0.28f)));
                    height[y * Size + x] = n * 0.8f;
                }
            }

            SavePair(name, albedo, height, 2.5f);
            return true;
        }

        static float StickFigure(float fx, float fy, float cx, float by, float scale)
        {
            float headY = by + 0.30f * scale;
            if (Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, headY)) < 0.042f * scale) return 1f;
            if (Mathf.Abs(fx - cx) < 0.02f * scale && fy > by + 0.09f * scale && fy < headY - 0.03f * scale) return 1f;
            if (Mathf.Abs(fy - (by + 0.21f * scale)) < 0.018f * scale && Mathf.Abs(fx - cx) < 0.08f * scale) return 1f;
            float legY = (fy - by) / (0.10f * scale);
            if (legY >= 0f && legY <= 1f && Mathf.Abs(Mathf.Abs(fx - cx) - (1f - legY) * 0.05f * scale) < 0.018f * scale) return 1f;
            return 0f;
        }

        // ==================== УТИЛИТЫ СОХРАНЕНИЯ И ШУМА ====================

        static void SavePair(string name, Texture2D albedo, float[] height, float normalStrength, Texture2D emission = null)
        {
            albedo.Apply();
            string albedoPath = $"{TexDir}/{name}_Albedo.png";
            File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
            Object.DestroyImmediate(albedo);

            var normal = BuildNormalMap(height, Size, Size, normalStrength);
            string normalPath = $"{TexDir}/{name}_Normal.png";
            File.WriteAllBytes(normalPath, normal.EncodeToPNG());
            Object.DestroyImmediate(normal);

            if (emission != null)
            {
                emission.Apply();
                string emPath = $"{TexDir}/{name}_Emission.png";
                File.WriteAllBytes(emPath, emission.EncodeToPNG());
                Object.DestroyImmediate(emission);
            }

            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(albedoPath, isNormal: false);

            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(normalPath, isNormal: true);

            if (emission != null)
            {
                string emPath = $"{TexDir}/{name}_Emission.png";
                AssetDatabase.ImportAsset(emPath, ImportAssetOptions.ForceUpdate);
                ConfigureImporter(emPath, isNormal: false);
            }
        }

        static void ConfigureImporter(string path, bool isNormal)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
        }

        static Texture2D BuildNormalMap(float[] h, int w, int hh, float strength)
        {
            var n = new Texture2D(w, hh, TextureFormat.RGBA32, false);
            for (int y = 0; y < hh; y++)
            {
                int ym = (y - 1 + hh) % hh;
                int yp = (y + 1) % hh;
                for (int x = 0; x < w; x++)
                {
                    int xm = (x - 1 + w) % w;
                    int xp = (x + 1) % w;
                    float dx = (h[y * w + xp] - h[y * w + xm]) * strength;
                    float dy = (h[yp * w + x] - h[ym * w + x]) * strength;
                    Vector3 v = new Vector3(-dx, -dy, 1f).normalized;
                    n.SetPixel(x, y, new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f));
                }
            }
            n.Apply();
            return n;
        }

        static float Hash2(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        static float TileNoise(float x, float y, int period, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            int x0 = ((ix % period) + period) % period;
            int y0 = ((iy % period) + period) % period;
            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;

            float a = Hash2(x0 + seed * 131, y0 + seed * 977);
            float b = Hash2(x1 + seed * 131, y0 + seed * 977);
            float c = Hash2(x0 + seed * 131, y1 + seed * 977);
            float d = Hash2(x1 + seed * 131, y1 + seed * 977);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float TileFbm(float fx, float fy, int basePeriod, int seed)
        {
            float sum = 0f, amp = 0.55f, norm = 0f;
            int p = basePeriod;
            for (int i = 0; i < 4; i++)
            {
                sum += TileNoise(fx * p, fy * p, p, seed + i * 19) * amp;
                norm += amp;
                amp *= 0.5f;
                p *= 2;
            }
            return sum / norm;
        }
    }
}
