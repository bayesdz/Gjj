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
    /// Сборщики всех сюжетных карт мира False Paradise по схеме docs/levels/LEVEL_MAP.png:
    /// - Zone1_GoblinLair (Логово гоблинов)
    /// - Zone2_ElvenGate (Врата эльфов)
    /// - Zone3_OgreVillage (Деревня и крепость огров + лестница к свету)
    /// Плюс пакетная сборка всех 5 карт одной командой.
    /// </summary>
    public static class WorldMapsBuilder
    {
        const string SceneDir = "Assets/_Game/Scenes";
        const string MatDir = "Assets/_Game/Art/Sandbox";

        [MenuItem("False Paradise/Build All Maps")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildAllBatch();
        }

        /// <summary>Точка входа для Unity -batchmode -executeMethod FalseParadise.EditorTools.WorldMapsBuilder.BuildAllBatch</summary>
        public static void BuildAllBatch()
        {
            ProceduralTextures.EnsureAllTextures(force: true);
            SandboxBuilder.Build();
            ParadiseBuilder.Build();
            BuildZone1GoblinLair(askSave: false);
            BuildZone2ElvenGate(askSave: false);
            BuildZone3OgreVillage(askSave: false);
            AssetDatabase.SaveAssets();
            Debug.Log("[False Paradise] Все 5 карт успешно собраны и сохранены!");
        }

        // =========================================================================
        // ЗОНА 1 — ЛОГОВО ГОБЛИНОВ (4 зала + тупик с рисунком + зал с клеткой)
        // =========================================================================

        [MenuItem("False Paradise/Build Zone 1 - Goblin Lair")]
        public static void MenuZone1() => BuildZone1GoblinLair(askSave: true);

        public static void BuildZone1GoblinLair(bool askSave = true)
        {
            if (askSave && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(SceneDir);
            EnsureFolder(MatDir);
            ProceduralTextures.EnsureAllTextures();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            string path = $"{SceneDir}/Zone1_GoblinLair.unity";

            var floorMat = ProceduralTextures.GetPbrMaterial("Z1_Floor", "DungeonFloor", new Color(0.68f, 0.65f, 0.63f), 0.34f, bumpScale: 1.3f, tiling: new Vector2(4f, 4f));
            var wallMat = ProceduralTextures.GetPbrMaterial("Z1_Wall", "StoneBrick", new Color(0.58f, 0.56f, 0.64f), 0.20f, bumpScale: 1.45f, tiling: new Vector2(4f, 2f));
            var rockMat = ProceduralTextures.GetPbrMaterial("Z1_Rock", "CaveRock", new Color(0.45f, 0.43f, 0.50f), 0.16f, bumpScale: 1.5f, tiling: new Vector2(2f, 2f));
            var woodMat = ProceduralTextures.GetPbrMaterial("Z1_Wood", "WoodPlanks", new Color(0.72f, 0.62f, 0.50f), 0.20f, bumpScale: 1.15f);
            var ironMat = ProceduralTextures.GetPbrMaterial("Z1_Iron", "MetalIron", new Color(0.65f, 0.68f, 0.72f), 0.55f, metallic: 0.85f);
            var paintingMat = ProceduralTextures.GetPbrMaterial("Z1_Painting", "CavePainting", Color.white, 0.12f, bumpScale: 1.0f);
            var torchMat = EmissiveMat("TorchFlame", new Color(1f, 0.50f, 0.12f), 6.5f);
            var emberMat = EmissiveMat("DungeonEmber", new Color(1f, 0.30f, 0.05f), 5f);
            var cageGlowMat = EmissiveMat("Z1_CageGlow", new Color(1f, 0.94f, 0.72f), 3.5f);
            var sparkMat = ProceduralTextures.GetParticleMaterial("FX_FireSpark", new Color(1f, 0.72f, 0.32f, 0.95f), additive: true);
            var dustMat = ProceduralTextures.GetParticleMaterial("FX_DungeonDust", new Color(0.8f, 0.76f, 0.72f, 0.25f), additive: false);

            var env = new GameObject("GoblinLair_Environment").transform;
            var torchList = new List<Light>();
            var rng = new System.Random(1001);
            Color torchCol = new Color(1f, 0.36f, 0.14f);

            // R1: Вход (0,0) 18×20
            BuildDungeonRoom(env, "R1_Entrance", new Vector3(0f, 0f, 0f), 18f, 20f, 7f, floorMat, wallMat, rockMat, openN: false, openS: false, openE: true, openW: false, doorW: 6f);
            BuildCampfire(env, "R1_Campfire", new Vector3(-4f, 0f, 4f), rockMat, woodMat, emberMat, sparkMat, torchCol);
            torchList.Add(BuildTorch(env, "T_R1_1", new Vector3(8.4f, 3f, 4.5f), Vector3.left, ironMat, woodMat, torchMat, sparkMat, torchCol, castShadows: true));
            torchList.Add(BuildTorch(env, "T_R1_2", new Vector3(8.4f, 3f, -4.5f), Vector3.left, ironMat, woodMat, torchMat, sparkMat, torchCol));

            // C1: Коридор (17, 0) 16×6
            BuildCorridor(env, "C1", new Vector3(17f, 0f, 0f), 16f, 6f, 5.2f, true, floorMat, wallMat, rockMat);
            BuildStoneArch(env, "Arch_C1_A", new Vector3(12f, 0f, 0f), 5.6f, 4.8f, 0.8f, wallMat, rockMat, 90f);
            BuildStoneArch(env, "Arch_C1_B", new Vector3(22f, 0f, 0f), 5.6f, 4.8f, 0.8f, wallMat, rockMat, 90f);

            // R2: Первый бой (36, 0) 22×24
            BuildDungeonRoom(env, "R2_FirstFight", new Vector3(36f, 0f, 0f), 22f, 24f, 7.5f, floorMat, wallMat, rockMat, openN: true, openS: false, openE: false, openW: true, doorW: 6f);
            BuildDungeonPillar(env, "R2_Pillar1", new Vector3(31f, 0f, 5f), 7.5f, 1.4f, wallMat, rockMat);
            BuildDungeonPillar(env, "R2_Pillar2", new Vector3(41f, 0f, 5f), 7.5f, 1.4f, wallMat, rockMat);
            BuildDungeonPillar(env, "R2_Pillar3", new Vector3(31f, 0f, -5f), 7.5f, 1.4f, wallMat, rockMat);
            BuildDungeonPillar(env, "R2_Pillar4", new Vector3(41f, 0f, -5f), 7.5f, 1.4f, wallMat, rockMat);
            BuildCrate(env, "R2_Crate1", new Vector3(35f, 0f, -4f), 1.1f, woodMat, rockMat, 18f);
            BuildBarrel(env, "R2_Barrel1", new Vector3(36.3f, 0f, -4.3f), woodMat, ironMat);
            torchList.Add(BuildTorch(env, "T_R2_1", new Vector3(31f, 3f, 4.1f), Vector3.back, ironMat, woodMat, torchMat, sparkMat, torchCol, castShadows: true));
            torchList.Add(BuildTorch(env, "T_R2_2", new Vector3(41f, 3f, 4.1f), Vector3.back, ironMat, woodMat, torchMat, sparkMat, torchCol));

            // C2: Коридор на север (36, 19) 6×14
            BuildCorridor(env, "C2", new Vector3(36f, 0f, 19f), 14f, 6f, 5.2f, false, floorMat, wallMat, rockMat);
            BuildStoneArch(env, "Arch_C2", new Vector3(36f, 0f, 19f), 5.6f, 4.8f, 0.8f, wallMat, rockMat, 0f);

            // R3: Нора (36, 37) 22×22 + Тупик на западе (18, 37) 14×10
            BuildDungeonRoom(env, "R3_Burrow", new Vector3(36f, 0f, 37f), 22f, 22f, 7.5f, floorMat, wallMat, rockMat, openN: false, openS: true, openE: true, openW: true, doorW: 6f);
            BuildDungeonRoom(env, "R3_DeadEnd", new Vector3(18f, 0f, 37f), 14f, 10f, 5.5f, floorMat, wallMat, rockMat, openN: false, openS: false, openE: true, openW: false, doorW: 6f);
            Prim(PrimitiveType.Cube, env, "R3_Mural", new Vector3(11.6f, 2.5f, 37f), new Vector3(0.08f, 2.8f, 4.2f), paintingMat, false);
            BuildCampfire(env, "R3_Campfire", new Vector3(32f, 0f, 41f), rockMat, woodMat, emberMat, sparkMat, torchCol);
            torchList.Add(BuildTorch(env, "T_R3_1", new Vector3(14f, 2.6f, 32.5f), Vector3.forward, ironMat, woodMat, torchMat, sparkMat, torchCol));

            // C3: Коридор на восток (54, 37) 14×6
            BuildCorridor(env, "C3", new Vector3(54f, 0f, 37f), 14f, 6f, 5.2f, true, floorMat, wallMat, rockMat);
            BuildStoneArch(env, "Arch_C3", new Vector3(54f, 0f, 37f), 5.6f, 4.8f, 0.8f, wallMat, rockMat, 90f);

            // R4: Зал с клеткой Сияющего (74, 37) 26×30
            BuildDungeonRoom(env, "R4_CageHall", new Vector3(74f, 0f, 37f), 26f, 30f, 9f, floorMat, wallMat, rockMat, openN: false, openS: false, openE: true, openW: true, doorW: 6f);
            BuildCage(env, new Vector3(76f, 0.25f, 37f), 9f, ironMat, wallMat, cageGlowMat);
            torchList.Add(BuildBrazier(env, "R4_Brazier1", new Vector3(70f, 0f, 31f), ironMat, rockMat, emberMat, sparkMat, torchCol, 1.1f));
            torchList.Add(BuildBrazier(env, "R4_Brazier2", new Vector3(70f, 0f, 43f), ironMat, rockMat, emberMat, sparkMat, torchCol, 1.1f));
            torchList.Add(BuildBrazier(env, "R4_Brazier3", new Vector3(82f, 0f, 31f), ironMat, rockMat, emberMat, sparkMat, torchCol, 1.1f));
            torchList.Add(BuildBrazier(env, "R4_Brazier4", new Vector3(82f, 0f, 43f), ironMat, rockMat, emberMat, sparkMat, torchCol, 1.1f));

            // Выходной тоннель к Вратам Эльфов
            BuildCorridor(env, "ExitTunnel", new Vector3(95f, 0f, 37f), 16f, 6f, 5.5f, true, floorMat, wallMat, rockMat);

            // Камни и сталактиты по залам
            Vector3[] rocks = { new Vector3(-7f, 0f, -7f), new Vector3(28f, 0f, 9f), new Vector3(44f, 0f, -9f), new Vector3(43f, 0f, 45f), new Vector3(64f, 0f, 25f), new Vector3(84f, 0f, 49f) };
            for (int r = 0; r < rocks.Length; r++) BuildRockCluster(env, $"Rock_{r}", rocks[r], rockMat, rng, 3, 1.4f);

            // Пыль в воздухе
            BuildAmbientParticles(env, "LairDustFX", new Vector3(40f, 3.5f, 20f), new Vector3(95f, 6f, 65f), dustMat,
                new Color(0.85f, 0.78f, 0.72f, 0.24f), new Color(1f, 0.65f, 0.4f, 0.20f), 45f, 0.05f, 0.14f, -0.01f, 0.25f);

            // Освещение и игрок
            var cam = CreatePlayer(new Vector3(-5f, 1.1f, -5f));
            cam.transform.parent.parent.rotation = Quaternion.Euler(0f, 90f, 0f);
            new GameObject("HUD").AddComponent<GameHUD>();
            SetupDungeonLighting(cam, torchList, new Color(0.06f, 0.03f, 0.05f), 0.038f);

            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            Debug.Log($"[False Paradise] Карта Зоны 1 собрана: {path}");
        }

        // =========================================================================
        // ПЕРЕХОД — ВРАТА ЭЛЬФОВ (лунный свет, серебро, бирюза, руины, лагерь)
        // =========================================================================

        [MenuItem("False Paradise/Build Zone 2 - Elven Gate")]
        public static void MenuZone2() => BuildZone2ElvenGate(askSave: true);

        public static void BuildZone2ElvenGate(bool askSave = true)
        {
            if (askSave && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(SceneDir);
            EnsureFolder(MatDir);
            ProceduralTextures.EnsureAllTextures();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            string path = $"{SceneDir}/Zone2_ElvenGate.unity";

            var groundMat = ProceduralTextures.GetPbrMaterial("Elven_Ground", "ParadiseGrass", new Color(0.32f, 0.48f, 0.52f), 0.18f, bumpScale: 0.9f, tiling: new Vector2(12f, 12f));
            var stoneMat = ProceduralTextures.GetPbrMaterial("Elven_Stone", "ElvenStone", Color.white, 0.55f, bumpScale: 1.0f, tiling: new Vector2(2f, 2f), hasEmissionMap: true, emissionColor: new Color(0.25f, 0.92f, 0.88f) * 1.4f);
            var marbleMat = ProceduralTextures.GetPbrMaterial("Elven_Marble", "WhiteMarble", new Color(0.82f, 0.92f, 0.95f), 0.62f, bumpScale: 0.6f, tiling: new Vector2(2f, 2f));
            var rockMat = ProceduralTextures.GetPbrMaterial("Elven_Rock", "CaveRock", new Color(0.40f, 0.48f, 0.55f), 0.25f, bumpScale: 1.3f);
            var barkMat = ProceduralTextures.GetPbrMaterial("Elven_Bark", "TreeBark", new Color(0.65f, 0.72f, 0.75f), 0.16f, bumpScale: 1.2f);
            var leafMat = ProceduralTextures.GetPbrMaterial("Elven_Leaves", "FoliageGreen", new Color(0.45f, 0.85f, 0.88f), 0.22f, bumpScale: 0.6f);
            var woodMat = ProceduralTextures.GetPbrMaterial("Elven_Wood", "WoodPlanks", new Color(0.70f, 0.75f, 0.78f), 0.25f);
            var crystalMat = EmissiveMat("Elven_Crystal", new Color(0.30f, 0.95f, 0.92f), 4.2f);
            var gatePortalMat = EmissiveMat("Elven_GateSeal", new Color(0.20f, 0.82f, 0.90f), 2.2f);
            var emberMat = EmissiveMat("Elven_CampEmber", new Color(0.45f, 0.95f, 0.95f), 4f);
            var fireflyMat = ProceduralTextures.GetParticleMaterial("FX_MoonFirefly", new Color(0.45f, 0.98f, 0.95f, 0.9f), additive: true);
            var skyMat = ProceduralTextures.GetProceduralSkybox("Sky_ElvenMoon", new Color(0.12f, 0.24f, 0.38f), new Color(0.05f, 0.10f, 0.16f), atmosphereThickness: 0.9f, exposure: 0.72f, sunSize: 0.045f);

            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.28f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.20f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.09f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.09f, 0.18f, 0.24f);

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(32f, 140f, 0f);
                l.color = new Color(0.62f, 0.86f, 1f);
                l.intensity = 1.15f;
                l.shadows = LightShadows.Soft;
            }

            var env = new GameObject("ElvenGate_Environment").transform;
            var rng = new System.Random(2002);

            // Площадка 48×48 + каменная дорога
            Prim(PrimitiveType.Plane, env, "MeadowGround", Vector3.zero, new Vector3(12f, 1f, 12f), groundMat, true);
            Prim(PrimitiveType.Cube, env, "MoonRoad", new Vector3(0f, 0.05f, 0f), new Vector3(46f, 0.1f, 6.2f), marbleMat, true);

            // Монументальные Врата Эльфов на восточной стороне (x = 18)
            var gate = new GameObject("AncientElvenGate").transform;
            gate.SetParent(env, false);
            gate.position = new Vector3(18f, 0f, 0f);
            var cone = GetOrCreateCone("Cone12", 12);

            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cylinder, gate, $"GateTower_{s}", new Vector3(0f, 7.5f, s * 6.5f), new Vector3(3.4f, 7.5f, 3.4f), stoneMat, true, local: true);
                CreateMeshObj(gate, $"GateSpire_{s}", cone, new Vector3(0f, 15f, s * 6.5f), new Vector3(4.2f, 6.5f, 4.2f), marbleMat, false, local: true);
                // Парящий рунический кристалл
                var cr = CreateMeshObj(gate, $"Crystal_{s}", cone, new Vector3(-2.4f, 6.2f, s * 6.5f), new Vector3(0.9f, 2.0f, 0.9f), crystalMat, false, local: true);
                cr.AddComponent<Hover>();
            }
            BuildStoneArch(gate, "GateArch", new Vector3(18f, 0f, 0f), 11f, 11.5f, 1.6f, stoneMat, marbleMat, 90f);
            Prim(PrimitiveType.Cube, gate, "GateSeal", new Vector3(0.2f, 5.2f, 0f), new Vector3(0.35f, 10.4f, 9.6f), gatePortalMat, true, local: true);

            var gateLight = new GameObject("GateLight").AddComponent<Light>();
            gateLight.transform.SetParent(gate, false);
            gateLight.transform.localPosition = new Vector3(-3f, 6f, 0f);
            gateLight.type = LightType.Point;
            gateLight.range = 24f;
            gateLight.intensity = 5f;
            gateLight.color = new Color(0.32f, 0.95f, 0.92f);

            // Колоннада по периметру арены боя со Стражем
            for (int x = -14; x <= 10; x += 8)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    BuildDungeonPillar(env, $"ElvenCol_{x}_{s}", new Vector3(x, 0f, s * 13f), 6.5f, 1.1f, marbleMat, stoneMat);
                    Prim(PrimitiveType.Sphere, env, $"Orb_{x}_{s}", new Vector3(x, 7.0f, s * 13f), Vector3.one * 0.55f, crystalMat, false);
                }
            }

            // Лагерь эльфов (на севере площадки)
            var camp = new GameObject("ElvenCamp").transform;
            camp.SetParent(env, false);
            camp.position = new Vector3(-8f, 0f, 17f);
            BuildElvenTent(camp, "Tent_1", new Vector3(-4f, 0f, 0f), marbleMat, woodMat, 15f);
            BuildElvenTent(camp, "Tent_2", new Vector3(4f, 0f, 1f), marbleMat, woodMat, -20f);
            BuildCampfire(camp, "ElvenFire", new Vector3(0f, 0f, -3.5f), rockMat, woodMat, emberMat, fireflyMat, new Color(0.45f, 0.92f, 1f));

            // Скальное кольцо и серебристые деревья вокруг площадки
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                float dist = 28f + (float)(rng.NextDouble() * 14);
                Vector3 rp = new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist);
                if (Mathf.Abs(rp.z) < 5f && rp.x < 0f) continue; // вход из Логова
                BuildRockCluster(env, $"BorderRock_{i}", rp, rockMat, rng, 3, (float)(2.4 + rng.NextDouble() * 2.2));
                if (i % 2 == 0)
                    BuildRealisticTree(env, $"ElvenTree_{i}", rp + new Vector3(Mathf.Cos(a) * -4f, 0f, Mathf.Sin(a) * -4f), (float)(1.0 + rng.NextDouble() * 0.45), barkMat, leafMat, rng, dead: false);
            }

            // Лунные светлячки
            BuildAmbientParticles(env, "ElvenFireflies", new Vector3(0f, 4f, 0f), new Vector3(44f, 8f, 44f), fireflyMat,
                new Color(0.35f, 0.98f, 0.95f, 0.85f), new Color(0.75f, 0.95f, 1f, 0.75f), 32f, 0.08f, 0.22f, -0.015f, 0.45f);

            var cam = CreatePlayer(new Vector3(-18f, 1.1f, 0f));
            cam.transform.parent.parent.rotation = Quaternion.Euler(0f, 90f, 0f);
            cam.clearFlags = skyMat != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            new GameObject("HUD").AddComponent<GameHUD>();

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = CreateZoneProfile("ElvenGatePost", 0.25f, 16f, 10f, new Color(0.88f, 0.96f, 1f), 1.15f, 0.22f);

            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            Debug.Log($"[False Paradise] Карта Врат Эльфов собрана: {path}");
        }

        // =========================================================================
        // ЗОНА 2 — ДЕРЕВНЯ И КРЕПОСТЬ ОГРОВ + ЛЕСТНИЦА К СВЕТУ
        // =========================================================================

        [MenuItem("False Paradise/Build Zone 3 - Ogre Village & Fortress")]
        public static void MenuZone3() => BuildZone3OgreVillage(askSave: true);

        public static void BuildZone3OgreVillage(bool askSave = true)
        {
            if (askSave && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(SceneDir);
            EnsureFolder(MatDir);
            ProceduralTextures.EnsureAllTextures();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            string path = $"{SceneDir}/Zone3_OgreVillage.unity";

            var groundMat = ProceduralTextures.GetPbrMaterial("Ogre_Ground", "CaveRock", new Color(0.42f, 0.32f, 0.26f), 0.16f, bumpScale: 1.2f, tiling: new Vector2(18f, 18f));
            var stoneMat = ProceduralTextures.GetPbrMaterial("Ogre_Stone", "StoneBrick", new Color(0.54f, 0.44f, 0.40f), 0.22f, bumpScale: 1.4f, tiling: new Vector2(3f, 2f));
            var rockMat = ProceduralTextures.GetPbrMaterial("Ogre_Rock", "CaveRock", new Color(0.36f, 0.28f, 0.24f), 0.18f, bumpScale: 1.5f);
            var woodMat = ProceduralTextures.GetPbrMaterial("Ogre_Wood", "OgreWood", Color.white, 0.18f, bumpScale: 1.25f, tiling: new Vector2(2f, 2f));
            var roofMat = ProceduralTextures.GetPbrMaterial("Ogre_Roof", "TreeBark", new Color(0.45f, 0.30f, 0.20f), 0.12f, bumpScale: 1.3f);
            var soilMat = ProceduralTextures.GetPbrMaterial("Ogre_Soil", "SoilGarden", Color.white, 0.10f, bumpScale: 1.4f, tiling: new Vector2(3f, 3f));
            var cropMat = ProceduralTextures.GetPbrMaterial("Ogre_Crops", "FoliageGreen", new Color(0.52f, 0.76f, 0.32f), 0.20f);
            var ironMat = ProceduralTextures.GetPbrMaterial("Ogre_Iron", "MetalIron", new Color(0.55f, 0.56f, 0.60f), 0.52f, metallic: 0.85f);
            var paintingMat = ProceduralTextures.GetPbrMaterial("Ogre_Mural", "CavePainting", Color.white, 0.12f);
            var marbleMat = ProceduralTextures.GetPbrMaterial("Ogre_StairsLight", "WhiteMarble", new Color(1f, 0.96f, 0.85f), 0.65f);
            var goldLightMat = EmissiveMat("Ogre_StairPortal", new Color(1f, 0.92f, 0.60f), 4.5f);
            var emberMat = EmissiveMat("Ogre_ForgeFire", new Color(1f, 0.38f, 0.06f), 6.0f);
            var spikeMat = EmissiveMat("Ogre_IllusionSpike", new Color(0.72f, 0.10f, 0.08f), 1.8f);
            var sparkMat = ProceduralTextures.GetParticleMaterial("FX_FireSpark", new Color(1f, 0.68f, 0.25f, 0.95f), additive: true);
            var skyMat = ProceduralTextures.GetProceduralSkybox("Sky_OgreSunset", new Color(0.72f, 0.38f, 0.22f), new Color(0.18f, 0.10f, 0.08f), atmosphereThickness: 1.5f, exposure: 0.85f, sunSize: 0.07f);

            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.22f, 0.18f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.16f, 0.14f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.07f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.26f, 0.16f, 0.13f);

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(22f, -45f, 0f);
                l.color = new Color(1f, 0.68f, 0.42f);
                l.intensity = 1.35f;
                l.shadows = LightShadows.Soft;
            }

            var env = new GameObject("OgreVillage_Environment").transform;
            var torchList = new List<Light>();
            var rng = new System.Random(3003);
            Color fireCol = new Color(1f, 0.48f, 0.16f);

            // Общая земля деревни и крепости
            Prim(PrimitiveType.Plane, env, "ValleyGround", new Vector3(15f, 0f, 35f), new Vector3(16f, 1f, 18f), groundMat, true);

            // ---- ДЕРЕВНЯ ОГРОВ (центр ~ 0, 0, 0) ----
            Vector3[] hutSpots =
            {
                new Vector3(-16f, 0f, 14f), new Vector3(4f, 0f, 16f),
                new Vector3(12f, 0f, -14f), new Vector3(-12f, 0f, -14f),
                new Vector3(22f, 0f, 2f)
            };
            float[] hutYaws = { 130f, 195f, -20f, 25f, -90f };
            for (int i = 0; i < hutSpots.Length; i++)
                BuildOgreHut(env, $"OgreHut_{i + 1}", hutSpots[i], hutYaws[i], woodMat, roofMat, stoneMat);

            // Кузница Огра-кузнеца
            BuildForge(env, new Vector3(-4f, 0f, -5f), woodMat, stoneMat, ironMat, emberMat, sparkMat, fireCol, torchList);

            // Огороды (с двойным визуалом: в иллюзии — мрачные шипы, в правде — грядки и зелень!)
            var gardenDual = new GameObject("VillageDualGarden");
            gardenDual.transform.SetParent(env, false);
            var gVis = gardenDual.AddComponent<IllusionVisual>();

            var gIllu = new GameObject("IllusionSpikes").transform;
            gIllu.SetParent(gardenDual.transform, false);
            var cone = GetOrCreateCone("Cone8", 8);
            for (int s = 0; s < 6; s++)
                CreateMeshObj(gIllu, $"BoneSpike_{s}", cone, new Vector3(6f + (s % 3) * 3.5f, 0f, -6f + (s / 3) * 4f), new Vector3(0.8f, 2.5f, 0.8f), spikeMat, false);

            var gTruth = new GameObject("TruthGardens").transform;
            gTruth.SetParent(gardenDual.transform, false);
            BuildGarden(gTruth, new Vector3(9.5f, 0f, -4f), soilMat, woodMat, cropMat);
            gTruth.gameObject.SetActive(false);
            SetRef(gVis, "illusionModel", gIllu.gameObject);
            SetRef(gVis, "truthModel", gTruth.gameObject);

            // Дорога от деревни к крепости (z = 22..44, x = 14)
            Prim(PrimitiveType.Cube, env, "FortressRoad", new Vector3(14f, 0.06f, 32f), new Vector3(8f, 0.12f, 24f), stoneMat, true);
            BuildStoneArch(env, "RoadArch_1", new Vector3(14f, 0f, 24f), 7.8f, 6.2f, 1.1f, stoneMat, rockMat, 0f);
            BuildStoneArch(env, "RoadArch_2", new Vector3(14f, 0f, 38f), 7.8f, 6.2f, 1.1f, stoneMat, rockMat, 0f);

            // ---- КРЕПОСТЬ ОГРА-ВОЖДЯ (центр ~ 14, 0, 66) ----
            var fort = new GameObject("OgreFortress").transform;
            fort.SetParent(env, false);
            fort.position = new Vector3(14f, 0f, 66f);

            // Внешние стены крепости 44×44
            BuildFortressWall(fort, "FortWall_W", new Vector3(-22f, 0f, 0f), 44f, 9f, 2f, 90f, stoneMat, rockMat);
            BuildFortressWall(fort, "FortWall_N", new Vector3(0f, 0f, 22f), 44f, 9f, 2f, 0f, stoneMat, rockMat);
            // Южная стена с воротами
            BuildFortressWall(fort, "FortWall_SL", new Vector3(-13.5f, 0f, -22f), 17f, 9f, 2f, 0f, stoneMat, rockMat);
            BuildFortressWall(fort, "FortWall_SR", new Vector3(13.5f, 0f, -22f), 17f, 9f, 2f, 0f, stoneMat, rockMat);
            // Восточная стена с проёмом к Лестнице к свету
            BuildFortressWall(fort, "FortWall_EN", new Vector3(22f, 0f, 14f), 16f, 9f, 2f, 90f, stoneMat, rockMat);
            BuildFortressWall(fort, "FortWall_ES", new Vector3(22f, 0f, -14f), 16f, 9f, 2f, 90f, stoneMat, rockMat);

            // Тронный зал и каменный трон
            Prim(PrimitiveType.Cube, fort, "ThroneFloor", new Vector3(0f, 0.1f, 4f), new Vector3(26f, 0.2f, 22f), stoneMat, true, local: true);
            Prim(PrimitiveType.Cube, fort, "ThroneDais", new Vector3(0f, 0.45f, 11f), new Vector3(8f, 0.7f, 5f), rockMat, true, local: true);
            Prim(PrimitiveType.Cube, fort, "ThroneSeat", new Vector3(0f, 1.3f, 11.8f), new Vector3(2.8f, 1.2f, 2.2f), rockMat, true, local: true);
            Prim(PrimitiveType.Cube, fort, "ThroneBack", new Vector3(0f, 3.0f, 12.7f), new Vector3(2.8f, 4.2f, 0.6f), rockMat, true, local: true);

            // Колонны и наскальные фрески истории народа огров на боковых стенах тронного зала
            for (int z = -4; z <= 12; z += 8)
            {
                BuildDungeonPillar(fort, $"ThronePillar_L_{z}", fort.TransformPoint(new Vector3(-11f, 0f, z)), 8.5f, 1.5f, stoneMat, rockMat);
                BuildDungeonPillar(fort, $"ThronePillar_R_{z}", fort.TransformPoint(new Vector3(11f, 0f, z)), 8.5f, 1.5f, stoneMat, rockMat);
            }
            Prim(PrimitiveType.Cube, fort, "HistoryMural_L", new Vector3(-20.8f, 3.8f, 4f), new Vector3(0.15f, 4.2f, 11f), paintingMat, false, local: true);
            Prim(PrimitiveType.Cube, fort, "HistoryMural_R", new Vector3(20.8f, 3.8f, 4f), new Vector3(0.15f, 4.2f, 11f), paintingMat, false, local: true);

            torchList.Add(BuildBrazier(fort, "ThroneBrazier_L", fort.TransformPoint(new Vector3(-5.5f, 0.2f, 8f)), ironMat, rockMat, emberMat, sparkMat, fireCol, 1.2f, true));
            torchList.Add(BuildBrazier(fort, "ThroneBrazier_R", fort.TransformPoint(new Vector3(5.5f, 0.2f, 8f)), ironMat, rockMat, emberMat, sparkMat, fireCol, 1.2f, true));

            // ---- ЛЕСТНИЦА К СВЕТУ (ведёт на восток вверх из крепости) ----
            var stairs = new GameObject("StairwayToLight").transform;
            stairs.SetParent(env, false);
            stairs.position = new Vector3(36f, 0f, 66f);
            for (int st = 0; st < 28; st++)
            {
                float sx = st * 1.15f;
                float sy = st * 0.42f;
                Prim(PrimitiveType.Cube, stairs, $"Step_{st}", new Vector3(sx, sy, 0f), new Vector3(1.25f, 0.45f, 7.6f), marbleMat, true, local: true);
                if (st % 5 == 0)
                {
                    for (int s = -1; s <= 1; s += 2)
                        Prim(PrimitiveType.Cylinder, stairs, $"StairPost_{st}_{s}", new Vector3(sx, sy + 1.4f, s * 4.0f), new Vector3(0.55f, 1.4f, 0.55f), marbleMat, false, local: true);
                }
            }
            // Сияющий портал Рая на вершине лестницы
            Prim(PrimitiveType.Cube, stairs, "ParadiseGateLight", new Vector3(33.5f, 15.5f, 0f), new Vector3(0.6f, 9f, 8.5f), goldLightMat, false, local: true);
            var stairLight = new GameObject("StairLight").AddComponent<Light>();
            stairLight.transform.SetParent(stairs, false);
            stairLight.transform.localPosition = new Vector3(31f, 16f, 0f);
            stairLight.type = LightType.Point;
            stairLight.range = 30f;
            stairLight.intensity = 8f;
            stairLight.color = new Color(1f, 0.90f, 0.60f);

            // Скалы по краям долины
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                Vector3 rp = new Vector3(Mathf.Cos(a) * 44f, 0f, Mathf.Sin(a) * 40f);
                if (rp.z > 20f && Mathf.Abs(rp.x - 14f) < 28f) continue;
                BuildRockCluster(env, $"ValleyRock_{i}", rp, rockMat, rng, 3, 2.8f);
            }

            // Игрок, HUD, настроение
            var cam = CreatePlayer(new Vector3(-24f, 1.1f, 0f));
            cam.transform.parent.parent.rotation = Quaternion.Euler(0f, 90f, 0f);
            cam.clearFlags = skyMat != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            new GameObject("HUD").AddComponent<GameHUD>();

            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var so = new SerializedObject(mood);
            var list = so.FindProperty("torches");
            list.arraySize = torchList.Count;
            for (int k = 0; k < torchList.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = torchList[k];
            so.ApplyModifiedPropertiesWithoutUndo();

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = CreateZoneProfile("OgreVillagePost", 0.2f, 20f, 5f, new Color(1f, 0.92f, 0.85f), 1.0f, 0.28f);

            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            Debug.Log($"[False Paradise] Карта Деревни и Крепости Огров собрана: {path}");
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ ПОСТРОИТЕЛИ ====================

        static void BuildDungeonRoom(Transform parent, string name, Vector3 center, float w, float d, float h, Material floorMat, Material wallMat, Material trimMat, bool openN, bool openS, bool openE, bool openW, float doorW)
        {
            var r = new GameObject(name).transform;
            r.SetParent(parent, false);
            r.position = center;

            Prim(PrimitiveType.Cube, r, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(w, 0.5f, d), floorMat, true, local: true);
            Prim(PrimitiveType.Cube, r, "Ceiling", new Vector3(0f, h + 0.25f, 0f), new Vector3(w, 0.5f, d), trimMat, false, local: true);

            BuildWallWithOptionalDoor(r, "Wall_N", new Vector3(0f, 0f, d * 0.5f), w, h, 0f, openN, doorW, wallMat);
            BuildWallWithOptionalDoor(r, "Wall_S", new Vector3(0f, 0f, -d * 0.5f), w, h, 0f, openS, doorW, wallMat);
            BuildWallWithOptionalDoor(r, "Wall_E", new Vector3(w * 0.5f, 0f, 0f), d, h, 90f, openE, doorW, wallMat);
            BuildWallWithOptionalDoor(r, "Wall_W", new Vector3(-w * 0.5f, 0f, 0f), d, h, 90f, openW, doorW, wallMat);
        }

        static void BuildWallWithOptionalDoor(Transform parent, string name, Vector3 localPos, float length, float height, float yRot, bool hasDoor, float doorW, Material mat)
        {
            var w = new GameObject(name).transform;
            w.SetParent(parent, false);
            w.localPosition = localPos;
            w.localRotation = Quaternion.Euler(0f, yRot, 0f);

            if (!hasDoor)
            {
                Prim(PrimitiveType.Cube, w, "Solid", new Vector3(0f, height * 0.5f, 0f), new Vector3(length, height, 1f), mat, true, local: true);
                return;
            }
            float sideLen = Mathf.Max(1f, (length - doorW) * 0.5f);
            float offset = doorW * 0.5f + sideLen * 0.5f;
            Prim(PrimitiveType.Cube, w, "Left", new Vector3(-offset, height * 0.5f, 0f), new Vector3(sideLen, height, 1f), mat, true, local: true);
            Prim(PrimitiveType.Cube, w, "Right", new Vector3(offset, height * 0.5f, 0f), new Vector3(sideLen, height, 1f), mat, true, local: true);
            float lintelH = Mathf.Max(0.8f, height - 4.6f);
            Prim(PrimitiveType.Cube, w, "Lintel", new Vector3(0f, height - lintelH * 0.5f, 0f), new Vector3(doorW, lintelH, 1f), mat, true, local: true);
        }

        static void BuildCorridor(Transform parent, string name, Vector3 center, float len, float wid, float h, bool alongX, Material floorMat, Material wallMat, Material trimMat)
        {
            var c = new GameObject(name).transform;
            c.SetParent(parent, false);
            c.position = center;
            if (!alongX) c.rotation = Quaternion.Euler(0f, 90f, 0f);

            Prim(PrimitiveType.Cube, c, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(len, 0.5f, wid), floorMat, true, local: true);
            Prim(PrimitiveType.Cube, c, "Ceiling", new Vector3(0f, h + 0.25f, 0f), new Vector3(len, 0.5f, wid), trimMat, false, local: true);
            Prim(PrimitiveType.Cube, c, "SideL", new Vector3(0f, h * 0.5f, wid * 0.5f), new Vector3(len, h, 0.8f), wallMat, true, local: true);
            Prim(PrimitiveType.Cube, c, "SideR", new Vector3(0f, h * 0.5f, -wid * 0.5f), new Vector3(len, h, 0.8f), wallMat, true, local: true);
        }

        static void BuildCage(Transform parent, Vector3 pos, float ceilingH, Material ironMat, Material stoneMat, Material glowMat)
        {
            var cage = new GameObject("ShiningCage").transform;
            cage.SetParent(parent, false);
            cage.position = pos;

            Prim(PrimitiveType.Cube, cage, "Dais", new Vector3(0f, -0.1f, 0f), new Vector3(5.5f, 0.35f, 5.5f), stoneMat, true, local: true);
            Prim(PrimitiveType.Cube, cage, "Roof", new Vector3(0f, 3.6f, 0f), new Vector3(3.6f, 0.22f, 3.6f), ironMat, false, local: true);
            for (int i = -3; i <= 3; i++)
            {
                float o = i * 0.52f;
                Prim(PrimitiveType.Cylinder, cage, $"BarN_{i}", new Vector3(o, 1.8f, 1.65f), new Vector3(0.08f, 1.8f, 0.08f), ironMat, false, local: true);
                Prim(PrimitiveType.Cylinder, cage, $"BarS_{i}", new Vector3(o, 1.8f, -1.65f), new Vector3(0.08f, 1.8f, 0.08f), ironMat, false, local: true);
                Prim(PrimitiveType.Cylinder, cage, $"BarE_{i}", new Vector3(1.65f, 1.8f, o), new Vector3(0.08f, 1.8f, 0.08f), ironMat, false, local: true);
                Prim(PrimitiveType.Cylinder, cage, $"BarW_{i}", new Vector3(-1.65f, 1.8f, o), new Vector3(0.08f, 1.8f, 0.08f), ironMat, false, local: true);
            }
            BuildHangingChain(cage, "CageChain", pos + new Vector3(0f, ceilingH - pos.y, 0f), ceilingH - pos.y - 3.6f, ironMat);
            // Мягкое золотое свечение внутри клетки
            var l = new GameObject("CageLight").AddComponent<Light>();
            l.transform.SetParent(cage, false);
            l.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            l.type = LightType.Point;
            l.range = 12f;
            l.intensity = 3.5f;
            l.color = new Color(1f, 0.92f, 0.65f);
            Prim(PrimitiveType.Sphere, cage, "LightOrb", new Vector3(0f, 1.6f, 0f), Vector3.one * 0.45f, glowMat, false, local: true);
        }

        static void BuildElvenTent(Transform parent, string name, Vector3 localPos, Material clothMat, Material woodMat, float yRot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(0f, yRot, 0f);
            var cone = GetOrCreateCone("Cone8", 8);
            CreateMeshObj(t, "Canopy", cone, Vector3.zero, new Vector3(5.2f, 4.2f, 5.2f), clothMat, true, local: true);
            Prim(PrimitiveType.Cylinder, t, "Pole", new Vector3(0f, 2.3f, 0f), new Vector3(0.18f, 2.3f, 0.18f), woodMat, false, local: true);
        }

        static void BuildOgreHut(Transform parent, string name, Vector3 pos, float yRot, Material woodMat, Material roofMat, Material stoneMat)
        {
            var h = new GameObject(name).transform;
            h.SetParent(parent, false);
            h.position = pos;
            h.rotation = Quaternion.Euler(0f, yRot, 0f);

            Prim(PrimitiveType.Cube, h, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(7.6f, 0.6f, 6.6f), stoneMat, true, local: true);
            Prim(PrimitiveType.Cube, h, "Walls", new Vector3(0f, 2.4f, 0f), new Vector3(7.0f, 3.8f, 6.0f), woodMat, true, local: true);
            // Двускатная крыша
            Prim(PrimitiveType.Cube, h, "RoofL", new Vector3(-2.1f, 5.2f, 0f), new Vector3(5.0f, 0.35f, 7.0f), roofMat, false, local: true, rot: Quaternion.Euler(0f, 0f, 32f));
            Prim(PrimitiveType.Cube, h, "RoofR", new Vector3(2.1f, 5.2f, 0f), new Vector3(5.0f, 0.35f, 7.0f), roofMat, false, local: true, rot: Quaternion.Euler(0f, 0f, -32f));
            // Дверной проём-накладка
            Prim(PrimitiveType.Cube, h, "Door", new Vector3(0f, 1.6f, 3.05f), new Vector3(1.8f, 2.8f, 0.15f), roofMat, false, local: true);
        }

        static void BuildForge(Transform parent, Vector3 pos, Material woodMat, Material stoneMat, Material ironMat, Material emberMat, Material sparkMat, Color fireCol, List<Light> torchList)
        {
            var f = new GameObject("OgreForge").transform;
            f.SetParent(parent, false);
            f.position = pos;

            // Навес на 4 столбах
            Prim(PrimitiveType.Cube, f, "Floor", new Vector3(0f, 0.15f, 0f), new Vector3(9.5f, 0.3f, 8.5f), stoneMat, true, local: true);
            Prim(PrimitiveType.Cube, f, "Roof", new Vector3(0f, 4.8f, 0f), new Vector3(10.2f, 0.4f, 9.2f), woodMat, false, local: true, rot: Quaternion.Euler(6f, 0f, 0f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Prim(PrimitiveType.Cylinder, f, "Post", new Vector3(sx * 4.2f, 2.4f, sz * 3.6f), new Vector3(0.4f, 2.4f, 0.4f), woodMat, true, local: true);

            // Каменный горн с трубой и раскалёнными углями
            Prim(PrimitiveType.Cube, f, "Furnace", new Vector3(-2.6f, 1.4f, 0f), new Vector3(2.4f, 2.8f, 2.8f), stoneMat, true, local: true);
            Prim(PrimitiveType.Cube, f, "Chimney", new Vector3(-2.6f, 4.5f, 0f), new Vector3(1.4f, 4.2f, 1.4f), stoneMat, false, local: true);
            Prim(PrimitiveType.Sphere, f, "ForgeCoals", new Vector3(-1.4f, 1.4f, 0f), new Vector3(0.9f, 0.55f, 1.3f), emberMat, false, local: true);
            AddFireParticles(f, new Vector3(-1.4f, 1.7f, 0f), sparkMat, 0.8f, 26f);

            var l = new GameObject("ForgeLight").AddComponent<Light>();
            l.transform.SetParent(f, false);
            l.transform.localPosition = new Vector3(-0.6f, 2.2f, 0f);
            l.type = LightType.Point;
            l.range = 16f;
            l.intensity = 5.5f;
            l.color = fireCol;
            l.shadows = LightShadows.Soft;
            l.gameObject.AddComponent<TorchFlicker>();
            torchList.Add(l);

            // Наковальня
            Prim(PrimitiveType.Cube, f, "AnvilBase", new Vector3(1.5f, 0.5f, 0.5f), new Vector3(0.9f, 0.8f, 0.7f), woodMat, true, local: true);
            Prim(PrimitiveType.Cube, f, "AnvilTop", new Vector3(1.5f, 1.05f, 0.5f), new Vector3(1.3f, 0.38f, 0.65f), ironMat, true, local: true);
        }

        static void BuildGarden(Transform parent, Vector3 pos, Material soilMat, Material woodMat, Material cropMat)
        {
            var g = new GameObject("OgreGarden").transform;
            g.SetParent(parent, false);
            g.position = pos;

            for (int r = -1; r <= 1; r++)
            {
                Prim(PrimitiveType.Cube, g, $"Bed_{r}", new Vector3(r * 2.8f, 0.12f, 0f), new Vector3(2.0f, 0.24f, 6.5f), soilMat, false, local: true);
                for (int c = -2; c <= 2; c++)
                    Prim(PrimitiveType.Sphere, g, $"Plant_{r}_{c}", new Vector3(r * 2.8f, 0.42f, c * 1.25f), new Vector3(0.75f, 0.55f, 0.75f), cropMat, false, local: true);
            }
            // Деревянный заборчик
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, g, $"FenceX_{s}", new Vector3(0f, 0.6f, s * 4.2f), new Vector3(9.6f, 0.16f, 0.16f), woodMat, false, local: true);
                Prim(PrimitiveType.Cube, g, $"FenceZ_{s}", new Vector3(s * 4.8f, 0.6f, 0f), new Vector3(0.16f, 0.16f, 8.4f), woodMat, false, local: true);
            }
        }

        static void BuildFortressWall(Transform parent, string name, Vector3 localPos, float len, float h, float thick, float yRot, Material wallMat, Material trimMat)
        {
            var w = new GameObject(name).transform;
            w.SetParent(parent, false);
            w.localPosition = localPos;
            w.localRotation = Quaternion.Euler(0f, yRot, 0f);
            Prim(PrimitiveType.Cube, w, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(len, h, thick), wallMat, true, local: true);
            Prim(PrimitiveType.Cube, w, "Top", new Vector3(0f, h + 0.3f, 0f), new Vector3(len, 0.6f, thick + 0.4f), trimMat, false, local: true);
            for (float x = -len * 0.5f + 1.5f; x < len * 0.5f - 1f; x += 3.5f)
                Prim(PrimitiveType.Cube, w, $"Merlon_{x:0}", new Vector3(x, h + 1.0f, 0f), new Vector3(1.5f, 1.0f, thick), trimMat, false, local: true);
        }

        static void SetupDungeonLighting(Camera cam, List<Light> torchList, Color fogColor, float fogDensity)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.08f, 0.12f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.05f, 0.07f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.03f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fogColor;

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.intensity = 0.06f;
                l.shadows = LightShadows.None;
            }

            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(cam.transform, false);
            lantern.transform.localPosition = new Vector3(0.32f, -0.18f, 0.25f);
            lantern.type = LightType.Point;
            lantern.range = 8.5f;
            lantern.intensity = 1.4f;
            lantern.color = new Color(1f, 0.80f, 0.56f);

            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var so = new SerializedObject(mood);
            var list = so.FindProperty("torches");
            list.arraySize = torchList.Count;
            for (int k = 0; k < torchList.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = torchList[k];
            so.ApplyModifiedPropertiesWithoutUndo();

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = CreateZoneProfile("Zone1Post", 0.3f, 22f, -10f, Color.white, 0.95f, 0.38f);
        }

        static VolumeProfile CreateZoneProfile(string name, float exposure, float contrast, float sat, Color filter, float bloomIntensity, float vignette)
        {
            string path = $"{MatDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var b = p.Add<Bloom>(true);
            b.threshold.Override(0.85f);
            b.intensity.Override(bloomIntensity);
            b.scatter.Override(0.70f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(exposure);
            ca.contrast.Override(contrast);
            ca.saturation.Override(sat);
            ca.colorFilter.Override(filter);
            var v = p.Add<Vignette>(true);
            v.intensity.Override(vignette);
            v.smoothness.Override(0.55f);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            AssetDatabase.SaveAssets();
            return p;
        }
    }
}
