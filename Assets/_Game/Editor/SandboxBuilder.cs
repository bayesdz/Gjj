using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static FalseParadise.EditorTools.EnvironmentProps;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Меню False Paradise → Build Sandbox Scene: собирает детализированную сцену подземелья
    /// (игрок, HUD, два гоблина, Огр-вождь, предметы, сводчатый зал, факелы, частицы, двойной декор).
    /// Повторный запуск пересоздаёт сцену с нуля.
    /// </summary>
    public static class SandboxBuilder
    {
        const string SceneDir = "Assets/_Game/Scenes";
        const string ScenePath = SceneDir + "/Sandbox_Nariman.unity";
        const string MatDir = "Assets/_Game/Art/Sandbox";

        [MenuItem("False Paradise/Build Sandbox Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(SceneDir);
            EnsureFolder(MatDir);
            ProceduralTextures.EnsureAllTextures();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---------- PBR-материалы окружения ----------
            var floorMat = ProceduralTextures.GetPbrMaterial("Dungeon_Floor", "DungeonFloor", new Color(0.75f, 0.72f, 0.68f), smoothness: 0.36f, bumpScale: 1.25f, tiling: new Vector2(10f, 10f));
            var runnerMat = ProceduralTextures.GetPbrMaterial("Dungeon_Runner", "StoneBrick", new Color(0.55f, 0.50f, 0.48f), smoothness: 0.25f, bumpScale: 1.0f, tiling: new Vector2(2f, 12f));
            var wallMat = ProceduralTextures.GetPbrMaterial("Dungeon_Wall", "StoneBrick", new Color(0.68f, 0.66f, 0.72f), smoothness: 0.22f, bumpScale: 1.4f, tiling: new Vector2(8f, 2.2f));
            var wallShortMat = ProceduralTextures.GetPbrMaterial("Dungeon_WallTrim", "StoneBrick", new Color(0.55f, 0.53f, 0.58f), smoothness: 0.26f, bumpScale: 1.2f, tiling: new Vector2(2f, 2f));
            var rockMat = ProceduralTextures.GetPbrMaterial("Dungeon_Rock", "CaveRock", new Color(0.55f, 0.52f, 0.58f), smoothness: 0.18f, bumpScale: 1.5f, tiling: new Vector2(2f, 2f));
            var woodMat = ProceduralTextures.GetPbrMaterial("Dungeon_Wood", "WoodPlanks", new Color(0.78f, 0.68f, 0.58f), smoothness: 0.22f, bumpScale: 1.15f, tiling: new Vector2(2f, 2f));
            var darkWoodMat = ProceduralTextures.GetPbrMaterial("Dungeon_DarkWood", "WoodPlanks", new Color(0.42f, 0.35f, 0.28f), smoothness: 0.18f, bumpScale: 1.1f, tiling: new Vector2(1.5f, 1.5f));
            var ironMat = ProceduralTextures.GetPbrMaterial("Dungeon_Iron", "MetalIron", new Color(0.7f, 0.72f, 0.75f), smoothness: 0.52f, metallic: 0.85f, bumpScale: 0.9f);
            var paintingMat = ProceduralTextures.GetPbrMaterial("Dungeon_Painting", "CavePainting", Color.white, smoothness: 0.12f, bumpScale: 1.0f);
            var cozyMat = ProceduralTextures.GetPbrMaterial("Dungeon_CozyRug", "FoliageBlossom", new Color(0.75f, 0.45f, 0.32f), smoothness: 0.08f, bumpScale: 0.4f);

            var torchMat = EmissiveMat("TorchFlame", new Color(1f, 0.52f, 0.12f), 6.5f);
            var emberMat = EmissiveMat("DungeonEmber", new Color(1f, 0.32f, 0.05f), 5f);
            var spikeCrystalMat = EmissiveMat("IllusionSpike", new Color(0.85f, 0.08f, 0.12f), 2.5f);
            var truthLanternMat = EmissiveMat("TruthWarmGlow", new Color(1f, 0.82f, 0.45f), 3.5f);
            var sparkMat = ProceduralTextures.GetParticleMaterial("FX_FireSpark", new Color(1f, 0.72f, 0.32f, 0.95f), additive: true);
            var dustMat = ProceduralTextures.GetParticleMaterial("FX_DungeonDust", new Color(0.85f, 0.80f, 0.75f, 0.28f), additive: false);

            // Материалы персонажей (не трогаем дизайн персонажей)
            var illusionMat = Mat("Illusion", new Color(0.55f, 0.05f, 0.05f));
            var truthMat = Mat("Truth", new Color(0.35f, 0.6f, 1f));
            var itemMat = Mat("Item", new Color(0.95f, 0.8f, 0.3f));
            var faceMat = Mat("Face", new Color(0.1f, 0.1f, 0.1f));

            // ---------- Архитектура зала 40×40 ----------
            var env = new GameObject("Environment").transform;
            var walls = new GameObject("Walls").transform;
            walls.SetParent(env, false);

            float ceilingH = 7.5f;

            // Основной каменный пол + центральная дорожка + подиум вождя
            var ground = Prim(PrimitiveType.Plane, env, "Ground", Vector3.zero, new Vector3(4.2f, 1f, 4.2f), floorMat, true);
            Prim(PrimitiveType.Cube, env, "CenterPath", new Vector3(0f, 0.03f, -2f), new Vector3(4.2f, 0.06f, 26f), runnerMat, false);
            Prim(PrimitiveType.Cube, env, "BossDais_Step1", new Vector3(0f, 0.12f, 13.5f), new Vector3(11f, 0.24f, 8.5f), wallShortMat, true);
            Prim(PrimitiveType.Cube, env, "BossDais_Step2", new Vector3(0f, 0.26f, 14.2f), new Vector3(8.5f, 0.24f, 6.2f), runnerMat, true);

            // Стены с цоколем и верхним карнизом
            BuildWallSegment(walls, "Wall_N", new Vector3(0f, 0f, 20f), 40f, ceilingH, 1.2f, 0f, wallMat, wallShortMat);
            BuildWallSegment(walls, "Wall_S", new Vector3(0f, 0f, -20f), 40f, ceilingH, 1.2f, 0f, wallMat, wallShortMat);
            BuildWallSegment(walls, "Wall_E", new Vector3(20f, 0f, 0f), 40f, ceilingH, 1.2f, 90f, wallMat, wallShortMat);
            BuildWallSegment(walls, "Wall_W", new Vector3(-20f, 0f, 0f), 40f, ceilingH, 1.2f, 90f, wallMat, wallShortMat);

            // Потолок с каменными рёбрами и деревянными балками
            var ceiling = new GameObject("Ceiling").transform;
            ceiling.SetParent(env, false);
            Prim(PrimitiveType.Cube, ceiling, "RoofSlab", new Vector3(0f, ceilingH + 0.4f, 0f), new Vector3(42f, 0.8f, 42f), rockMat, false);
            for (int z = -14; z <= 14; z += 7)
            {
                Prim(PrimitiveType.Cube, ceiling, $"RibBeam_{z}", new Vector3(0f, ceilingH - 0.35f, z), new Vector3(40f, 0.7f, 0.9f), wallShortMat, false);
                Prim(PrimitiveType.Cube, ceiling, $"WoodBeam_{z}", new Vector3(0f, ceilingH - 0.85f, z), new Vector3(39f, 0.35f, 0.35f), darkWoodMat, false);
            }

            // Колонны (основные 2 на прежних позициях + 4 дополнительных для ритма зала)
            Vector3[] pillarCoords =
            {
                new Vector3(-7f, 0f, 5f), new Vector3(7f, 0f, 5f),
                new Vector3(-7f, 0f, -7f), new Vector3(7f, 0f, -7f),
                new Vector3(-13f, 0f, 13f), new Vector3(13f, 0f, 13f)
            };
            for (int p = 0; p < pillarCoords.Length; p++)
                BuildDungeonPillar(walls, $"Pillar_{p + 1}", pillarCoords[p], ceilingH, 1.5f, wallShortMat, rockMat, collider: true);

            // Декоративные каменные арки у северной и южной стен
            BuildStoneArch(walls, "NorthArch", new Vector3(0f, 0f, 18.8f), 7.5f, 6.2f, 1.0f, wallShortMat, rockMat, 0f);
            BuildStoneArch(walls, "SouthArch", new Vector3(0f, 0f, -18.8f), 5.5f, 5.2f, 0.9f, wallShortMat, rockMat, 0f);

            // Укрытие Cover (на прежней позиции, но теперь выглядит как каменная баррикада с ящиками)
            var cover = new GameObject("Cover").transform;
            cover.SetParent(walls, false);
            cover.position = new Vector3(0f, 0f, -4f);
            Prim(PrimitiveType.Cube, cover, "WallLow", new Vector3(0f, 0.65f, 0f), new Vector3(4.2f, 1.3f, 0.85f), wallShortMat, true, local: true);
            Prim(PrimitiveType.Cube, cover, "Cap", new Vector3(0f, 1.34f, 0f), new Vector3(4.4f, 0.14f, 0.98f), rockMat, false, local: true);
            BuildCrate(cover, "CoverCrate", new Vector3(-2.6f, 0f, -4.1f), 0.95f, woodMat, darkWoodMat, 14f);
            BuildBarrel(cover, "CoverBarrel", new Vector3(2.55f, 0f, -3.9f), woodMat, ironMat, 0.95f);

            // Декор: камни, сталактиты, ящики, бочки, цепи
            var rng = new System.Random(42);
            var props = new GameObject("Props").transform;
            props.SetParent(env, false);

            Vector3[] rockSpots =
            {
                new Vector3(-18f, 0f, 18f), new Vector3(18f, 0f, 18f),
                new Vector3(-18f, 0f, -18f), new Vector3(18f, 0f, -18f),
                new Vector3(-17.5f, 0f, 1f), new Vector3(17.5f, 0f, -2f),
                new Vector3(-14f, 0f, -14f), new Vector3(14f, 0f, 8f)
            };
            for (int r = 0; r < rockSpots.Length; r++)
                BuildRockCluster(props, $"RockCluster_{r}", rockSpots[r], rockMat, rng, count: 3, baseScale: 1.5f, collider: true);

            // Сталактиты с потолка по периметру
            for (int s = 0; s < 16; s++)
            {
                float sx = (float)(rng.NextDouble() * 34 - 17);
                float sz = (float)(rng.NextDouble() * 34 - 17);
                if (Mathf.Abs(sx) < 5f && Mathf.Abs(sz) < 10f) continue;
                BuildStalactite(ceiling, new Vector3(sx, ceilingH - 0.1f, sz), (float)(1.1 + rng.NextDouble() * 1.4), (float)(0.25 + rng.NextDouble() * 0.25), rockMat);
            }

            // Бочки и ящики по углам
            BuildCrate(props, "Crate_1", new Vector3(-15f, 0f, -8f), 1.15f, woodMat, darkWoodMat, 22f);
            BuildCrate(props, "Crate_2", new Vector3(-13.8f, 0f, -9.1f), 0.85f, woodMat, darkWoodMat, -12f);
            BuildBarrel(props, "Barrel_1", new Vector3(-15.5f, 0f, -9.6f), woodMat, ironMat, 1.0f);
            BuildBarrel(props, "Barrel_2", new Vector3(15.2f, 0f, -7.5f), woodMat, ironMat, 1.05f);
            BuildBarrel(props, "Barrel_3", new Vector3(16.1f, 0f, -8.3f), woodMat, ironMat, 0.9f);
            BuildCrate(props, "Crate_3", new Vector3(14.5f, 0f, -9.2f), 1.0f, woodMat, darkWoodMat, 8f);

            // Свисающие цепи
            BuildHangingChain(props, "Chain_1", new Vector3(-4.5f, ceilingH, 10f), 3.2f, ironMat);
            BuildHangingChain(props, "Chain_2", new Vector3(4.5f, ceilingH, 10f), 3.6f, ironMat);
            BuildHangingChain(props, "Chain_3", new Vector3(-10f, ceilingH, -1f), 2.8f, ironMat);
            BuildHangingChain(props, "Chain_4", new Vector3(10f, ceilingH, -1f), 3.0f, ironMat);

            // Двойной декор окружения (Иллюзия vs Правда при нажатии Q!)
            var dualDecor = new GameObject("EnvironmentDualDecor");
            dualDecor.transform.SetParent(env, false);
            var dualVis = dualDecor.AddComponent<IllusionVisual>();

            var illuGroup = new GameObject("IllusionDecor").transform;
            illuGroup.SetParent(dualDecor.transform, false);
            var coneMesh = GetOrCreateCone("Cone8", 8);
            Vector3[] spikeSpots = { new Vector3(-12f, 0f, 3f), new Vector3(12f, 0f, 3f), new Vector3(-9f, 0f, -11f), new Vector3(9f, 0f, -11f) };
            foreach (var sp in spikeSpots)
                CreateMeshObj(illuGroup, "DarkSpike", coneMesh, sp, new Vector3(0.9f, 2.6f, 0.9f), spikeCrystalMat, false);

            var truthGroup = new GameObject("TruthDecor").transform;
            truthGroup.SetParent(dualDecor.transform, false);
            // В правде — уютные коврики и наскальный рисунок семьи на стене!
            Prim(PrimitiveType.Cube, truthGroup, "CozyRug_L", new Vector3(-10f, 0.04f, -8f), new Vector3(3.2f, 0.04f, 2.2f), cozyMat, false);
            Prim(PrimitiveType.Cube, truthGroup, "CozyRug_R", new Vector3(10f, 0.04f, -8f), new Vector3(3.2f, 0.04f, 2.2f), cozyMat, false);
            Prim(PrimitiveType.Cube, truthGroup, "FamilyMural", new Vector3(0f, 3.0f, 19.32f), new Vector3(4.5f, 3.0f, 0.08f), paintingMat, false);
            Prim(PrimitiveType.Sphere, truthGroup, "WarmLantern_L", new Vector3(-10f, 0.4f, -6.5f), Vector3.one * 0.35f, truthLanternMat, false);
            Prim(PrimitiveType.Sphere, truthGroup, "WarmLantern_R", new Vector3(10f, 0.4f, -6.5f), Vector3.one * 0.35f, truthLanternMat, false);
            truthGroup.gameObject.SetActive(false);

            SetRef(dualVis, "illusionModel", illuGroup.gameObject);
            SetRef(dualVis, "truthModel", truthGroup.gameObject);

            // Игрок
            var cam = CreatePlayer(new Vector3(0f, 1f, -14f));

            // HUD
            new GameObject("HUD").AddComponent<GameHUD>();

            // Атмосфера подземелья (свет, факелы, жаровни, пыль, постобработка)
            BuildAtmosphere(cam, walls, ironMat, woodMat, wallShortMat, torchMat, emberMat, sparkMat, dustMat);

            // Враги (не трогаем их дизайн и логику!)
            Enemy("Goblin_01", "goblin_01", new Vector3(-4f, 0f, 2f), 1f, 100f, illusionMat, truthMat, faceMat);
            Enemy("Goblin_02", "goblin_02", new Vector3(4f, 0f, 4f), 1f, 100f, illusionMat, truthMat, faceMat);
            Enemy("OgreChief", MercyCounter.OgreChiefId, new Vector3(0f, 0.25f, 13f), 1.6f, 200f, illusionMat, truthMat, faceMat);

            // Предметы
            Item("Toy", "floor1_toy", "Детская игрушка", new Vector3(-10f, 0.25f, -8f), itemMat,
                "Трофей их кровавых обрядов. Не трогайте.",
                "Деревянная лошадка. На боку неровно вырезано имя ребёнка.");
            Item("Drawing", "floor1_drawing", "Рисунок на камне", new Vector3(10f, 0.25f, -8f), itemMat,
                "Знаки тёмного культа. Они призывают смерть.",
                "Детский рисунок углём: большая семья гоблинов у костра.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[False Paradise] Улучшенная сцена подземелья собрана: {ScenePath}. Нажмите Play.");
        }

        static void BuildWallSegment(Transform parent, string name, Vector3 baseCenter, float length, float height, float thickness, float yRot, Material wallMat, Material trimMat)
        {
            var w = new GameObject(name).transform;
            w.SetParent(parent, false);
            w.position = baseCenter;
            w.rotation = Quaternion.Euler(0f, yRot, 0f);

            Prim(PrimitiveType.Cube, w, "Main", new Vector3(0f, height * 0.5f, 0f), new Vector3(length, height, thickness), wallMat, true, local: true);
            Prim(PrimitiveType.Cube, w, "Baseboard", new Vector3(0f, 0.45f, 0f), new Vector3(length, 0.9f, thickness + 0.35f), trimMat, false, local: true);
            Prim(PrimitiveType.Cube, w, "Cornice", new Vector3(0f, height - 0.35f, 0f), new Vector3(length, 0.7f, thickness + 0.45f), trimMat, false, local: true);

            // Пилястры вдоль стены каждые 8 м
            for (float x = -length * 0.5f + 4f; x < length * 0.5f - 2f; x += 8f)
                Prim(PrimitiveType.Cube, w, $"Pilaster_{x:0}", new Vector3(x, height * 0.5f, 0f), new Vector3(0.95f, height, thickness + 0.5f), trimMat, false, local: true);
        }

        /// <summary>Игрок со всеми компонентами и камерой. Возвращает камеру.</summary>
        internal static Camera CreatePlayer(Vector3 position)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = position;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = Vector3.zero;
            player.AddComponent<Health>();
            var fpc = player.AddComponent<FirstPersonController>();
            player.AddComponent<PlayerMelee>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<EyeOfTruth>();
            player.AddComponent<PlayerDeath>();

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 0.7f, 0f);

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.transform.SetParent(pivot, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f;
            cam.gameObject.AddComponent<CameraShake>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            SetRef(fpc, "cameraPivot", pivot);
            return cam;
        }

        static void BuildAtmosphere(Camera cam, Transform walls, Material ironMat, Material woodMat, Material stoneMat, Material torchMat, Material emberMat, Material sparkMat, Material dustMat)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.11f, 0.09f, 0.13f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.06f, 0.07f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.03f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.042f;
            RenderSettings.fogColor = new Color(0.06f, 0.03f, 0.04f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // Солнца в подземелье нет: приглушённый холодный верхний свет
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.intensity = 0.08f;
                l.color = new Color(0.45f, 0.52f, 0.78f);
                l.shadows = LightShadows.None;
            }

            // Фонарь игрока: тёплый, мягкий
            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(cam.transform, false);
            lantern.transform.localPosition = new Vector3(0.32f, -0.18f, 0.25f);
            lantern.type = LightType.Point;
            lantern.range = 8.5f;
            lantern.intensity = 1.4f;
            lantern.color = new Color(1f, 0.80f, 0.56f);
            lantern.shadows = LightShadows.None;

            // Кованые факелы на колоннах и стенах
            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var torchList = new List<Light>();
            Color torchCol = new Color(1f, 0.38f, 0.14f);

            (Vector3 pos, Vector3 normal, bool shadow)[] spots =
            {
                (new Vector3(-7f, 2.8f, 4.18f), Vector3.back, true),
                (new Vector3(7f, 2.8f, 4.18f), Vector3.back, true),
                (new Vector3(-7f, 2.8f, -7.82f), Vector3.back, false),
                (new Vector3(7f, 2.8f, -7.82f), Vector3.back, false),
                (new Vector3(-19.25f, 3.0f, -10f), Vector3.right, false),
                (new Vector3(19.25f, 3.0f, -10f), Vector3.left, false),
                (new Vector3(-19.25f, 3.0f, 8f), Vector3.right, false),
                (new Vector3(19.25f, 3.0f, 8f), Vector3.left, false),
                (new Vector3(-5.5f, 3.2f, 19.25f), Vector3.back, false),
                (new Vector3(5.5f, 3.2f, 19.25f), Vector3.back, false),
                (new Vector3(0f, 3.0f, -19.25f), Vector3.forward, false)
            };

            for (int i = 0; i < spots.Length; i++)
            {
                var l = BuildTorch(walls, $"Torch_{i + 1}", spots[i].pos, spots[i].normal, ironMat, woodMat, torchMat, sparkMat, torchCol, range: 11f, intensity: 3.4f, castShadows: spots[i].shadow);
                torchList.Add(l);
            }

            // Две напольные жаровни по бокам подиума Огра-вождя
            torchList.Add(BuildBrazier(walls, "Brazier_L", new Vector3(-5.2f, 0.12f, 11.2f), ironMat, stoneMat, emberMat, sparkMat, torchCol, 1.05f, castShadows: false));
            torchList.Add(BuildBrazier(walls, "Brazier_R", new Vector3(5.2f, 0.12f, 11.2f), ironMat, stoneMat, emberMat, sparkMat, torchCol, 1.05f, castShadows: false));

            var so = new SerializedObject(mood);
            var list = so.FindProperty("torches");
            list.arraySize = torchList.Count;
            for (int k = 0; k < torchList.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = torchList[k];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Пылинки в воздухе по всему залу
            BuildAmbientParticles(walls, "DungeonDustFX", new Vector3(0f, 3.2f, 0f), new Vector3(34f, 5.5f, 34f), dustMat,
                new Color(0.85f, 0.78f, 0.70f, 0.25f), new Color(1f, 0.65f, 0.4f, 0.22f), rate: 26f, minSize: 0.05f, maxSize: 0.14f, gravity: -0.01f, speed: 0.25f);

            // Постобработка
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
        }

        static VolumeProfile PostProfile()
        {
            string path = $"{MatDir}/DungeonPost.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.95f);
            bloom.scatter.Override(0.7f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.55f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(22f);
            color.saturation.Override(-8f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        internal static Material EmissiveMat(string name, Color color, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            m.color = color;
            m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Enemy(string name, string id, Vector3 pos, float scale, float hp,
            Material illusionMat, Material truthMat, Material faceMat)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            root.transform.localScale = Vector3.one * scale;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.5f;

            var health = root.AddComponent<Health>();
            SetFloat(health, "max", hp);
            var enemy = root.AddComponent<EnemyController>();
            SetString(enemy, "enemyId", id);
            root.AddComponent<HitFlash>();
            var illusion = root.AddComponent<IllusionVisual>();

            var illusionModel = Model(root.transform, "Illusion", illusionMat, faceMat);
            var truthModel = Model(root.transform, "Truth", truthMat, faceMat);
            truthModel.SetActive(false);

            SetRef(illusion, "illusionModel", illusionModel);
            SetRef(illusion, "truthModel", truthModel);
        }

        static GameObject Model(Transform parent, string name, Material body, Material face)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = body;

            var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = "Face";
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
            eye.transform.localScale = new Vector3(0.6f, 0.15f, 0.1f);
            eye.GetComponent<Renderer>().sharedMaterial = face;
            return go;
        }

        static void Item(string name, string id, string title, Vector3 pos, Material mat, string lie, string truth)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.5f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var item = go.AddComponent<InspectItem>();
            SetString(item, "itemId", id);
            SetString(item, "title", title);
            SetString(item, "illusionText", lie);
            SetString(item, "truthText", truth);
        }

        internal static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        internal static Material Mat(string name, Color color)
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
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Нет поля {field} у {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Нет поля {field} у {target.GetType().Name}"); return; }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Нет поля {field} у {target.GetType().Name}"); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        internal static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
