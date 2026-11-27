using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static FalseParadise.EditorTools.SandboxBuilder;
using static FalseParadise.EditorTools.EnvironmentProps;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Меню False Paradise → Build Paradise Test Scene: детализированный ложный рай и его ад (задача N10).
    /// Рельефная долина с холмами, процедурное небо, анимированный пруд/лава, ветвистые деревья,
    /// многоуровневый Белый замок / Обсидиановая цитадель, арки, колоннада и атмосферные частицы.
    /// Персонажи (ангелы, богиня, демоны) не изменены.
    /// </summary>
    public static class ParadiseBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Paradise_Test.unity";
        const string MatDir = "Assets/_Game/Art/Sandbox";

        // Одинаковые позиции для обеих версий мира
        static readonly Vector3 GoddessPos = new Vector3(0f, 0f, 22f);
        static readonly Vector3 CastlePos = new Vector3(0f, 0f, 56f);
        static readonly Vector3 PondPos = new Vector3(-16f, 0f, 14f);

        [MenuItem("False Paradise/Build Paradise Test Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/_Game/Scenes");
            EnsureFolder(MatDir);
            ProceduralTextures.EnsureAllTextures();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---------- PBR-материалы Рая ----------
            var pGrass = ProceduralTextures.GetPbrMaterial("P_Grass_PBR", "ParadiseGrass", Color.white, smoothness: 0.14f, bumpScale: 0.85f, tiling: Vector2.one);
            var pTrunk = ProceduralTextures.GetPbrMaterial("P_Trunk_PBR", "TreeBark", new Color(0.85f, 0.72f, 0.60f), smoothness: 0.12f, bumpScale: 1.35f, tiling: new Vector2(2f, 3f));
            var pLeaves = ProceduralTextures.GetPbrMaterial("P_Leaves_PBR", "FoliageGreen", new Color(0.95f, 1f, 0.90f), smoothness: 0.18f, bumpScale: 0.7f, tiling: new Vector2(2f, 2f));
            var pBlossom = ProceduralTextures.GetPbrMaterial("P_Blossom_PBR", "FoliageBlossom", Color.white, smoothness: 0.22f, bumpScale: 0.6f, tiling: new Vector2(2f, 2f));
            var pWhite = ProceduralTextures.GetPbrMaterial("P_White_PBR", "WhiteMarble", Color.white, smoothness: 0.58f, bumpScale: 0.65f, tiling: new Vector2(2f, 2f));
            var pPathMarble = ProceduralTextures.GetPbrMaterial("P_PathMarble", "WhiteMarble", new Color(0.98f, 0.96f, 0.90f), smoothness: 0.62f, bumpScale: 0.55f, tiling: new Vector2(1.5f, 1.5f));
            var pGold = ProceduralTextures.GetPbrMaterial("P_Gold_PBR", "GoldOrnate", new Color(1f, 0.88f, 0.48f), smoothness: 0.78f, metallic: 0.88f, bumpScale: 0.9f, emissionColor: new Color(1f, 0.76f, 0.25f) * 0.45f);
            var pWindowGold = EmissiveMat("P_WindowGold", new Color(1f, 0.90f, 0.55f), 2.8f);
            var pWater = ProceduralTextures.GetWaterMaterial("P_Water_PBR", new Color(0.20f, 0.68f, 0.88f), smoothness: 0.94f, tiling: 4f);
            var pRock = ProceduralTextures.GetPbrMaterial("P_Rock_PBR", "CaveRock", new Color(0.76f, 0.74f, 0.70f), smoothness: 0.22f, bumpScale: 1.1f);
            var pWood = ProceduralTextures.GetPbrMaterial("P_Wood_PBR", "WoodPlanks", new Color(0.85f, 0.72f, 0.55f), smoothness: 0.24f, bumpScale: 1.0f);
            var pCloud = EmissiveMat("P_Cloud", new Color(1f, 0.97f, 0.92f), 0.45f);

            // Материалы персонажей Рая (не трогаем)
            var pAngel = EmissiveMat("P_Angel", new Color(1f, 0.97f, 0.9f), 0.8f);
            var pGoddess = EmissiveMat("P_Goddess", new Color(1f, 0.92f, 0.75f), 2.5f);

            // ---------- PBR-материалы Ада ----------
            var hGround = ProceduralTextures.GetPbrMaterial("H_Ground_PBR", "HellGround", Color.white, smoothness: 0.20f, bumpScale: 1.4f, tiling: Vector2.one, hasEmissionMap: true, emissionColor: new Color(1f, 0.30f, 0.04f) * 1.8f);
            var hBlack = ProceduralTextures.GetPbrMaterial("H_Obsidian_PBR", "ObsidianRock", new Color(0.55f, 0.48f, 0.50f), smoothness: 0.48f, metallic: 0.25f, bumpScale: 1.4f, tiling: new Vector2(2f, 2f));
            var hCharred = ProceduralTextures.GetPbrMaterial("H_Charred_PBR", "CharredBark", Color.white, smoothness: 0.14f, bumpScale: 1.5f, tiling: new Vector2(2f, 3f));
            var hRock = ProceduralTextures.GetPbrMaterial("H_Rock_PBR", "CaveRock", new Color(0.32f, 0.18f, 0.15f), smoothness: 0.22f, bumpScale: 1.4f);
            var hLava = ProceduralTextures.GetPbrMaterial("H_Lava_PBR", "LavaSurface", Color.white, smoothness: 0.35f, bumpScale: 1.1f, tiling: new Vector2(3f, 3f), hasEmissionMap: true, emissionColor: new Color(1f, 0.38f, 0.06f) * 3.8f);
            var hIron = ProceduralTextures.GetPbrMaterial("H_Iron_PBR", "MetalIron", new Color(0.35f, 0.22f, 0.20f), smoothness: 0.45f, metallic: 0.8f);
            var hCloud = Mat("H_DarkCloud", new Color(0.14f, 0.04f, 0.04f));

            // Материалы персонажей Ада (не трогаем)
            var hEyes = EmissiveMat("H_Eyes", new Color(1f, 0.05f, 0.02f), 8f);
            var hDemon = Mat("H_Demon", new Color(0.35f, 0.04f, 0.04f));

            // Частицы
            var petalMat = ProceduralTextures.GetParticleMaterial("FX_Petal", new Color(1f, 0.78f, 0.88f, 0.85f), additive: false);
            var goldDustMat = ProceduralTextures.GetParticleMaterial("FX_GoldDust", new Color(1f, 0.90f, 0.55f, 0.8f), additive: true);
            var ashMat = ProceduralTextures.GetParticleMaterial("FX_HellAsh", new Color(0.95f, 0.35f, 0.08f, 0.9f), additive: true);
            var fireSparkMat = ProceduralTextures.GetParticleMaterial("FX_FireSpark", new Color(1f, 0.65f, 0.22f, 0.95f), additive: true);

            // Процедурные скайбоксы
            var paradiseSkyMat = ProceduralTextures.GetProceduralSkybox("Sky_Paradise", new Color(0.55f, 0.78f, 1f), new Color(0.85f, 0.82f, 0.68f), atmosphereThickness: 1.05f, exposure: 1.25f, sunSize: 0.055f);
            var hellSkyMat = ProceduralTextures.GetProceduralSkybox("Sky_Hell", new Color(0.75f, 0.12f, 0.06f), new Color(0.12f, 0.02f, 0.02f), atmosphereThickness: 2.2f, exposure: 0.55f, sunSize: 0.09f);

            // ---------- окружение ----------
            RenderSettings.skybox = paradiseSkyMat;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) sun = l;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(28f, -24f, 0f);
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
            }

            var cam = CreatePlayer(new Vector3(0f, 1.1f, -12f));
            cam.clearFlags = paradiseSkyMat != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.farClipPlane = 450f;
            new GameObject("HUD").AddComponent<GameHUD>();
            new GameObject("GlitchOverlay").AddComponent<GlitchOverlay>();

            // Единый рельефный меш долины (180×180 м)
            var terrainMesh = GetOrCreateParadiseTerrain("ParadiseValleyTerrain", 180f, 72, PondPos, CastlePos, GoddessPos);
            var rng = new System.Random(7);
            var treeSpots = TreeSpots(rng);

            // ==================== РАЙ ====================
            var paradise = new GameObject("Paradise").transform;

            // Рельефная земля с холмами
            CreateMeshObj(paradise, "Ground", terrainMesh, Vector3.zero, Vector3.one, pGrass, collider: true);

            // Парадная мраморная аллея с золотыми бордюрами, колоннами и триумфальными арками
            BuildParadisePath(paradise, pPathMarble, pWhite, pGold, pWindowGold, hellish: false);

            // Подиум под Богиней
            Prim(PrimitiveType.Cylinder, paradise, "GoddessDias1", GoddessPos + Vector3.up * 0.15f, new Vector3(9.5f, 0.15f, 9.5f), pWhite, true);
            Prim(PrimitiveType.Cylinder, paradise, "GoddessDias2", GoddessPos + Vector3.up * 0.35f, new Vector3(7.2f, 0.15f, 7.2f), pGold, true);
            Prim(PrimitiveType.Cylinder, paradise, "GoddessDias3", GoddessPos + Vector3.up * 0.45f, new Vector3(6.6f, 0.12f, 6.6f), pWhite, true);

            // Пруд с живой водой, валунами, кувшинками и деревянным мостиком
            var pond = Prim(PrimitiveType.Cylinder, paradise, "Pond", PondPos + Vector3.up * -0.18f, new Vector3(13.5f, 0.04f, 9.5f), pWater, false);
            pond.AddComponent<SurfaceAnimator>();
            BuildPondShore(paradise, PondPos, pRock, pLeaves, pBlossom, pWood, hellish: false);

            // Ветвистые деревья и кусты с учётом высоты рельефа
            var treeRng = new System.Random(7);
            foreach (var p in treeSpots)
            {
                float y = SampleParadiseHeight(p.x, p.z, PondPos, CastlePos, GoddessPos);
                float sc = (float)(0.85 + treeRng.NextDouble() * 0.55);
                var leafMat = treeRng.NextDouble() < 0.38 ? pBlossom : pLeaves;
                BuildRealisticTree(paradise, "Tree", new Vector3(p.x, y - 0.15f, p.z), sc, pTrunk, leafMat, treeRng, dead: false);
            }
            for (int b = 0; b < 24; b++)
            {
                float bx = (float)(treeRng.NextDouble() * 72 - 36);
                float bz = (float)(treeRng.NextDouble() * 64 - 16);
                if (Mathf.Abs(bx) < 5.2f) continue;
                if (Vector2.Distance(new Vector2(bx, bz), new Vector2(PondPos.x, PondPos.z)) < 7.5f) continue;
                if (Vector2.Distance(new Vector2(bx, bz), new Vector2(CastlePos.x, CastlePos.z)) < 18f) continue;
                float by = SampleParadiseHeight(bx, bz, PondPos, CastlePos, GoddessPos);
                BuildBush(paradise, new Vector3(bx, by, bz), (float)(0.7 + treeRng.NextDouble() * 0.6), treeRng.NextDouble() < 0.25 ? pBlossom : pLeaves, treeRng);
            }

            // Плывущие облака в небе Рая
            BuildClouds(paradise, pCloud, height: 42f, count: 10);

            // Величественный Белый замок
            BuildDetailedCastle(paradise, pWhite, pGold, pWindowGold, hellish: false);

            // Падающие лепестки и золотая пыльца
            BuildAmbientParticles(paradise, "BlossomPetalsFX", new Vector3(0f, 8f, 12f), new Vector3(48f, 10f, 52f), petalMat,
                new Color(1f, 0.80f, 0.90f, 0.9f), new Color(1f, 0.95f, 0.98f, 0.8f), rate: 28f, minSize: 0.12f, maxSize: 0.24f, gravity: 0.04f, speed: 0.6f);
            BuildAmbientParticles(paradise, "GoldPollenFX", new Vector3(0f, 4f, 16f), new Vector3(36f, 6f, 42f), goldDustMat,
                new Color(1f, 0.92f, 0.55f, 0.7f), new Color(1f, 0.82f, 0.35f, 0.6f), rate: 22f, minSize: 0.08f, maxSize: 0.18f, gravity: -0.02f, speed: 0.35f);

            // Ангелы и Богиня (персонажей не трогаем!)
            for (int i = 0; i < 7; i++)
            {
                var pos = AngelPos(i);
                var angel = Figure(paradise, $"Angel_{i}", pos, 1f, pAngel, pWhite, null, null);
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

            // ==================== АД (те же места) ====================
            var hell = new GameObject("Hell").transform;

            // Рельефная вулканическая земля с трещинами магмы
            CreateMeshObj(hell, "Ground", terrainMesh, Vector3.zero, Vector3.one, hGround, collider: true);

            // Расколотая обсидиановая дорога с пылающими жаровнями
            BuildParadisePath(hell, hRock, hBlack, hIron, hLava, hellish: true, sparkMat: fireSparkMat);

            // Алтарь Высшего демона
            Prim(PrimitiveType.Cylinder, hell, "DemonAltar1", GoddessPos + Vector3.up * 0.18f, new Vector3(9.5f, 0.18f, 9.5f), hBlack, true);
            Prim(PrimitiveType.Cylinder, hell, "DemonAltar2", GoddessPos + Vector3.up * 0.38f, new Vector3(7.0f, 0.16f, 7.0f), hLava, false);
            Prim(PrimitiveType.Cylinder, hell, "DemonAltar3", GoddessPos + Vector3.up * 0.48f, new Vector3(6.2f, 0.14f, 6.2f), hBlack, true);

            // Бурлящее лавовое озеро
            var lava = Prim(PrimitiveType.Cylinder, hell, "LavaPool", PondPos + Vector3.up * -0.18f, new Vector3(13.5f, 0.04f, 9.5f), hLava, false);
            var lavaAnim = lava.AddComponent<SurfaceAnimator>();
            SetFloat(lavaAnim, "pulseSpeed", 2.2f);
            SetFloat(lavaAnim, "pulseAmount", 0.25f);
            BuildPondShore(hell, PondPos, hBlack, hRock, hLava, hCharred, hellish: true);

            var lavaLight = new GameObject("LavaLight").AddComponent<Light>();
            lavaLight.transform.SetParent(hell, false);
            lavaLight.transform.position = PondPos + Vector3.up * 2.5f;
            lavaLight.type = LightType.Point;
            lavaLight.range = 20f;
            lavaLight.intensity = 6.5f;
            lavaLight.color = new Color(1f, 0.28f, 0.04f);
            lavaLight.gameObject.AddComponent<TorchFlicker>();

            // Обугленные мёртвые деревья на тех же позициях
            var deadRng = new System.Random(7);
            foreach (var p in treeSpots)
            {
                float y = SampleParadiseHeight(p.x, p.z, PondPos, CastlePos, GoddessPos);
                float sc = (float)(0.85 + deadRng.NextDouble() * 0.55);
                deadRng.NextDouble(); // синхронизация random
                BuildRealisticTree(hell, "DeadTree", new Vector3(p.x, y - 0.15f, p.z), sc, hCharred, hBlack, deadRng, dead: true);
            }

            // Тёмные пепельные тучи
            BuildClouds(hell, hCloud, height: 38f, count: 10);

            // Демоническая цитадель
            BuildDetailedCastle(hell, hBlack, hRock, hLava, hellish: true);

            // Пепел и искры в воздухе Ада
            BuildAmbientParticles(hell, "HellEmbersFX", new Vector3(0f, 3f, 12f), new Vector3(50f, 8f, 54f), ashMat,
                new Color(1f, 0.42f, 0.08f, 0.9f), new Color(0.95f, 0.15f, 0.03f, 0.85f), rate: 40f, minSize: 0.08f, maxSize: 0.20f, gravity: -0.06f, speed: 0.8f);

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
            redGlow.transform.localPosition = new Vector3(0f, 2f, -1.5f);
            redGlow.type = LightType.Point;
            redGlow.range = 22f;
            redGlow.intensity = 7f;
            redGlow.color = new Color(1f, 0.1f, 0.04f);
            hell.gameObject.SetActive(false);

            // ---------- постобработка ----------
            var pVol = GlobalVolume("Volume_Paradise", ParadiseProfile(), 1f, 10);
            var hVol = GlobalVolume("Volume_Hell", HellProfile(), 0f, 11);
            var gVol = GlobalVolume("Volume_Glitch", GlitchProfile(), 0f, 12);

            // ---------- контроллер падения ----------
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
            SetRef(fall, "paradiseSkybox", paradiseSkyMat);
            SetRef(fall, "hellSkybox", hellSkyMat);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[False Paradise] Улучшенная сцена рая и ада собрана: {ScenePath}. Play → идите к богине (или F9).");
        }

        // ==================== ПОСТРОИТЕЛИ ОКРУЖЕНИЯ ====================

        static void BuildParadisePath(Transform parent, Material tileMat, Material stoneMat, Material accentMat, Material glowMat, bool hellish, Material sparkMat = null)
        {
            var path = new GameObject(hellish ? "HellPath" : "MarbleAvenue").transform;
            path.SetParent(parent, false);
            var rng = new System.Random(101);

            for (int i = 0; i < 10; i++)
            {
                float z = -10f + i * 3.1f;
                Vector3 pos = new Vector3(hellish ? (float)(rng.NextDouble() * 0.4 - 0.2) : 0f, 0.06f, z);
                Quaternion rot = hellish ? Quaternion.Euler((float)(rng.NextDouble() * 6 - 3), (float)(rng.NextDouble() * 14 - 7), (float)(rng.NextDouble() * 6 - 3)) : Quaternion.identity;
                Prim(PrimitiveType.Cube, path, $"Slab_{i}", pos, new Vector3(3.4f, 0.14f, 2.75f), tileMat, true, rot: rot);

                // Бордюры по бокам
                for (int s = -1; s <= 1; s += 2)
                {
                    if (hellish && rng.NextDouble() < 0.3) continue;
                    Prim(PrimitiveType.Cube, path, $"Curb_{i}_{s}", new Vector3(s * 1.9f, 0.18f, z), new Vector3(0.32f, 0.26f, 2.6f), accentMat, false, rot: rot);
                }
            }

            // Колоннада и триумфальные арки вдоль аллеи
            float[] archZ = { -4f, 6f, 15f };
            for (int a = 0; a < archZ.Length; a++)
            {
                float z = archZ[a];
                if (!hellish)
                {
                    BuildStoneArch(path, $"AvenueArch_{a}", new Vector3(0f, 0f, z), 5.6f, 5.4f, 0.72f, stoneMat, accentMat, 0f);
                    // Светящиеся фонари на колоннах
                    for (int s = -1; s <= 1; s += 2)
                        Prim(PrimitiveType.Sphere, path, $"Lantern_{a}_{s}", new Vector3(s * 2.8f, 3.6f, z - 0.45f), Vector3.one * 0.32f, glowMat, false);
                }
                else
                {
                    // В Аду — сломанные колонны и пылающие жаровни
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float h = (a == 1 && s == 1) ? 2.4f : 4.2f;
                        var col = BuildDungeonPillar(path, $"BrokenPillar_{a}_{s}", new Vector3(s * 2.9f, 0f, z), h, 0.75f, stoneMat, accentMat, collider: true);
                        col.rotation = Quaternion.Euler(s * 5f, 0f, s * -6f);
                        if (sparkMat != null && a != 1)
                            BuildBrazier(path, $"HellBrazier_{a}_{s}", new Vector3(s * 2.4f, 0f, z - 1.1f), accentMat, stoneMat, glowMat, sparkMat, new Color(1f, 0.25f, 0.04f), 0.75f);
                    }
                }
            }
        }

        static void BuildPondShore(Transform parent, Vector3 pondCenter, Material rockMat, Material foliageMat, Material accentMat, Material woodMat, bool hellish)
        {
            var shore = new GameObject("PondShore").transform;
            shore.SetParent(parent, false);
            var rng = new System.Random(202);

            for (int i = 0; i < 16; i++)
            {
                float ang = i / 16f * Mathf.PI * 2f;
                float rx = Mathf.Cos(ang) * 6.8f;
                float rz = Mathf.Sin(ang) * 4.8f;
                Vector3 wp = new Vector3(pondCenter.x + rx, 0f, pondCenter.z + rz);
                wp.y = SampleParadiseHeight(wp.x, wp.z, PondPos, CastlePos, GoddessPos);
                BuildRockCluster(shore, $"ShoreRock_{i}", wp, rockMat, rng, count: 2, baseScale: (float)(0.75 + rng.NextDouble() * 0.5), collider: false);
                if (!hellish && i % 3 == 0)
                    BuildBush(shore, wp + new Vector3(Mathf.Cos(ang) * 1.1f, 0f, Mathf.Sin(ang) * 1.1f), 0.85f, i % 6 == 0 ? accentMat : foliageMat, rng);
            }

            if (!hellish)
            {
                // Деревянный мостик-причал у восточного берега пруда
                Vector3 dockPos = new Vector3(pondCenter.x + 5.8f, 0.1f, pondCenter.z);
                Prim(PrimitiveType.Cube, shore, "DockDeck", dockPos, new Vector3(3.2f, 0.18f, 1.8f), woodMat, true);
                for (int px = -1; px <= 1; px += 2)
                    for (int pz = -1; pz <= 1; pz += 2)
                        Prim(PrimitiveType.Cylinder, shore, "DockPost", dockPos + new Vector3(px * 1.3f, -0.3f, pz * 0.75f), new Vector3(0.16f, 0.65f, 0.16f), woodMat, false);

                // Кувшинки на воде
                for (int l = 0; l < 8; l++)
                {
                    float lx = pondCenter.x + (float)(rng.NextDouble() * 8 - 4);
                    float lz = pondCenter.z + (float)(rng.NextDouble() * 5 - 2.5);
                    Prim(PrimitiveType.Cylinder, shore, $"Lily_{l}", new Vector3(lx, -0.13f, lz), new Vector3(0.65f, 0.02f, 0.65f), foliageMat, false);
                }
            }
        }

        static void BuildClouds(Transform parent, Material cloudMat, float height, int count)
        {
            var group = new GameObject("Clouds").transform;
            group.SetParent(parent, false);
            var rng = new System.Random(303);
            for (int i = 0; i < count; i++)
            {
                var c = new GameObject($"Cloud_{i}").transform;
                c.SetParent(group, false);
                c.position = new Vector3((float)(rng.NextDouble() * 160 - 80), height + (float)(rng.NextDouble() * 10), (float)(rng.NextDouble() * 140 - 30));
                c.gameObject.AddComponent<CloudDrift>();
                int puffs = 3 + rng.Next(0, 3);
                for (int p = 0; p < puffs; p++)
                {
                    Vector3 off = new Vector3((p - puffs * 0.5f) * 4.5f, (float)(rng.NextDouble() * 1.8), (float)(rng.NextDouble() * 3 - 1.5));
                    Vector3 sc = new Vector3((float)(7 + rng.NextDouble() * 5), (float)(2.4 + rng.NextDouble() * 1.6), (float)(5 + rng.NextDouble() * 4));
                    Prim(PrimitiveType.Sphere, c, $"Puff_{p}", off, sc, cloudMat, false, local: true);
                }
            }
        }

        static Vector3[] TreeSpots(System.Random rng)
        {
            var list = new List<Vector3>();
            while (list.Count < 30)
            {
                var p = new Vector3((float)(rng.NextDouble() * 86 - 43), 0f, (float)(rng.NextDouble() * 76 - 18));
                if (Mathf.Abs(p.x) < 6.2f) continue;                          // не на аллее
                if (Vector3.Distance(p, PondPos) < 9.5f) continue;            // не в пруду
                if (Vector3.Distance(p, CastlePos) < 20f) continue;           // не в замке
                if (Vector3.Distance(p, GoddessPos) < 8.5f) continue;         // не на подиуме богини
                list.Add(p);
            }
            return list.ToArray();
        }

        static Vector3 AngelPos(int i)
        {
            float a = i / 7f * Mathf.PI * 1.4f - Mathf.PI * 0.7f;
            return GoddessPos + new Vector3(Mathf.Sin(a) * 12f, 6f + (i % 3) * 2.5f, Mathf.Cos(a) * 6f + 6f);
        }

        static void BuildDetailedCastle(Transform parent, Material walls, Material roofs, Material windows, bool hellish)
        {
            var c = new GameObject(hellish ? "DemonFortress" : "WhiteCastle").transform;
            c.SetParent(parent, false);
            c.position = CastlePos;
            var cone = GetOrCreateCone("Cone12", 12);

            float h = hellish ? 1.35f : 1f;

            // Массивный фундамент и парадная лестница
            Prim(PrimitiveType.Cube, c, "Foundation", new Vector3(0f, 1.2f, 0f), new Vector3(28f, 2.4f, 18f), walls, true, local: true);
            for (int st = 0; st < 5; st++)
                Prim(PrimitiveType.Cube, c, $"Step_{st}", new Vector3(0f, 0.25f + st * 0.45f, -9.5f - (4 - st) * 0.9f), new Vector3(7.5f, 0.45f, 1.1f), walls, true, local: true);

            // Главный донжон (Keep)
            Prim(PrimitiveType.Cube, c, "Keep", new Vector3(0f, 2.4f + 8f * h, 0f), new Vector3(17f, 16f * h, 11f), walls, true, local: true);

            // Центральная высокая башня со шпилем
            float spireBaseY = 2.4f + 16f * h;
            Prim(PrimitiveType.Cylinder, c, "CentralTower", new Vector3(0f, spireBaseY + 5f * h, 0f), new Vector3(6.2f, 5f * h, 6.2f), walls, false, local: true);
            CreateMeshObj(c, "CentralSpire", cone, new Vector3(0f, spireBaseY + 10f * h, 0f), new Vector3(7.4f, hellish ? 15f : 10f, 7.4f), roofs, false, local: true);

            // Портик и арочные ворота
            Prim(PrimitiveType.Cube, c, "GateFrame", new Vector3(0f, 5.4f, -5.8f), new Vector3(6.2f, 6.4f, 1.2f), walls, false, local: true);
            Prim(PrimitiveType.Cube, c, "Gate", new Vector3(0f, 4.9f, -6.2f), new Vector3(4.2f, 5.2f, 0.4f), roofs, false, local: true);
            // Витражное окно-роза над воротами
            Prim(PrimitiveType.Cylinder, c, "RoseWindow", new Vector3(0f, 11.5f * h, -5.6f), new Vector3(3.2f, 0.15f, 3.2f), windows, false, local: true, rot: Quaternion.Euler(90f, 0f, 0f));

            // Зубцы крепостной стены по фасаду
            for (int m = -7; m <= 7; m += 2)
                Prim(PrimitiveType.Cube, c, $"Merlon_{m}", new Vector3(m, spireBaseY + 0.7f, -5.3f), new Vector3(1.1f, 1.4f, 0.7f), walls, false, local: true);

            // 4 угловые башни + 2 передние сторожевые башни
            Vector3[] towers =
            {
                new Vector3(-11f, 2.4f, -6.5f), new Vector3(11f, 2.4f, -6.5f),
                new Vector3(-11f, 2.4f, 6.5f), new Vector3(11f, 2.4f, 6.5f),
                new Vector3(-6.2f, 2.4f, -7.8f), new Vector3(6.2f, 2.4f, -7.8f)
            };

            for (int ti = 0; ti < towers.Length; ti++)
            {
                var t = towers[ti];
                bool frontSmall = ti >= 4;
                float th = frontSmall ? (hellish ? 19f : 14f) : (hellish ? 28f : 21f);
                float tr = frontSmall ? 3.0f : 4.4f;

                Prim(PrimitiveType.Cylinder, c, $"Tower_{ti}", t + Vector3.up * th * 0.5f, new Vector3(tr, th * 0.5f, tr), walls, true, local: true);
                // Кольцо-карниз под крышей
                Prim(PrimitiveType.Cylinder, c, $"TowerRing_{ti}", t + Vector3.up * (th - 0.3f), new Vector3(tr * 1.18f, 0.45f, tr * 1.18f), roofs, false, local: true);
                // Настоящая коническая крыша / шпиль
                float roofH = hellish ? (frontSmall ? 9f : 13f) : (frontSmall ? 5.5f : 7.5f);
                CreateMeshObj(c, $"Roof_{ti}", cone, t + Vector3.up * th, new Vector3(tr * 1.32f, roofH, tr * 1.32f), roofs, false, local: true);

                // Светящиеся окна башни
                int winCount = frontSmall ? 2 : 3;
                for (int k = 0; k < winCount; k++)
                    Prim(PrimitiveType.Cube, c, $"Window_{ti}_{k}", t + new Vector3(0f, 5f + k * 4.8f, -tr * 0.51f), new Vector3(0.85f, 1.85f, 0.14f), windows, false, local: true);
            }

            if (hellish)
            {
                // Дополнительные демонические шипы по бокам цитадели
                for (int s = -1; s <= 1; s += 2)
                {
                    CreateMeshObj(c, $"Spike_{s}_1", cone, new Vector3(s * 14.5f, 2f, -3f), new Vector3(2.8f, 11f, 2.8f), roofs, false, local: true, rot: Quaternion.Euler(0f, 0f, -s * 18f));
                    CreateMeshObj(c, $"Spike_{s}_2", cone, new Vector3(s * 15.5f, 2f, 3f), new Vector3(2.4f, 9f, 2.4f), roofs, false, local: true, rot: Quaternion.Euler(0f, 0f, -s * 24f));
                }
            }
        }

        /// <summary>Фигура: тело-капсула, крылья, (для демонов) рога и горящие глаза. Не трогаем по просьбе пользователя.</summary>
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
            if (eyes == null) // нимб
                Prim(PrimitiveType.Cylinder, f, "Halo", new Vector3(0f, 2.2f, 0f), new Vector3(0.7f, 0.02f, 0.7f), wings, false, local: true);
            return f;
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

        // ---------- профили постобработки ----------

        static VolumeProfile ResetProfile(string name)
        {
            string path = $"{MatDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null)
                AssetDatabase.DeleteAsset(path);
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
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.72f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(12f);
            ca.saturation.Override(18f);
            ca.colorFilter.Override(new Color(1f, 0.96f, 0.88f));
            var v = p.Add<Vignette>(true);
            v.intensity.Override(0.14f);
            v.smoothness.Override(0.55f);
            Finish(p);
            return p;
        }

        static VolumeProfile HellProfile()
        {
            var p = ResetProfile("HellPost");
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.35f);
            bloom.scatter.Override(0.70f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.15f);
            ca.contrast.Override(38f);
            ca.saturation.Override(-10f);
            ca.colorFilter.Override(new Color(1f, 0.62f, 0.56f));
            var v = p.Add<Vignette>(true);
            v.intensity.Override(0.48f);
            v.smoothness.Override(0.55f);
            v.color.Override(new Color(0.28f, 0f, 0f));
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
