using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static FalseParadise.EditorTools.SandboxBuilder;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Меню False Paradise → Build Paradise Test Scene: ложный рай и его ад (задача N10).
    /// Подойдите к богине — она произнесёт речь, затем рай рухнет. F9 — запустить/сбросить вручную.
    /// </summary>
    public static class ParadiseBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Paradise_Test.unity";
        const string MatDir = "Assets/_Game/Art/Sandbox";

        // Одинаковые позиции для обеих версий мира
        static readonly Vector3 GoddessPos = new Vector3(0f, 0f, 22f);
        static readonly Vector3 CastlePos = new Vector3(0f, 0f, 55f);
        static readonly Vector3 PondPos = new Vector3(-16f, 0f, 14f);

        [MenuItem("False Paradise/Build Paradise Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/_Game/Scenes");
            EnsureFolder(MatDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---------- материалы ----------
            var pGrass = Mat("P_Grass", new Color(0.45f, 0.75f, 0.3f));
            var pTrunk = Mat("P_Trunk", new Color(0.5f, 0.35f, 0.22f));
            var pLeaves = Mat("P_Leaves", new Color(0.55f, 0.85f, 0.4f));
            var pBlossom = Mat("P_Blossom", new Color(1f, 0.75f, 0.85f));
            var pWhite = Mat("P_White", new Color(0.96f, 0.95f, 0.92f));
            var pGold = EmissiveMat("P_Gold", new Color(1f, 0.8f, 0.35f), 1.5f);
            var pWater = EmissiveMat("P_Water", new Color(0.4f, 0.75f, 1f), 0.6f);
            var pAngel = EmissiveMat("P_Angel", new Color(1f, 0.97f, 0.9f), 0.8f);
            var pGoddess = EmissiveMat("P_Goddess", new Color(1f, 0.92f, 0.75f), 2.5f);

            var hGround = Mat("H_Ground", new Color(0.12f, 0.06f, 0.05f));
            var hBlack = Mat("H_Black", new Color(0.05f, 0.04f, 0.045f));
            var hRock = Mat("H_Rock", new Color(0.2f, 0.1f, 0.08f));
            var hLava = EmissiveMat("H_Lava", new Color(1f, 0.25f, 0.02f), 5f);
            var hEyes = EmissiveMat("H_Eyes", new Color(1f, 0.05f, 0.02f), 8f);
            var hDemon = Mat("H_Demon", new Color(0.35f, 0.04f, 0.04f));

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
                sun.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
                sun.shadows = LightShadows.Soft;
            }

            var cam = CreatePlayer(new Vector3(0f, 1f, -12f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 400f;
            new GameObject("HUD").AddComponent<GameHUD>();
            new GameObject("GlitchOverlay").AddComponent<GlitchOverlay>();

            // ---------- РАЙ ----------
            var paradise = new GameObject("Paradise").transform;
            Prim(PrimitiveType.Plane, paradise, "Ground", Vector3.zero, new Vector3(20f, 1f, 20f), pGrass, true);
            // Дорога к богине
            for (int i = 0; i < 9; i++)
                Prim(PrimitiveType.Cube, paradise, $"Path_{i}", new Vector3(0f, 0.05f, -8f + i * 3.2f), new Vector3(2.2f, 0.1f, 2.8f), pWhite, true);
            // Пруд
            Prim(PrimitiveType.Cylinder, paradise, "Pond", PondPos + Vector3.up * 0.02f, new Vector3(10f, 0.02f, 7f), pWater, false);
            // Деревья
            var rng = new System.Random(7);
            var treeSpots = TreeSpots(rng);
            foreach (var p in treeSpots)
            {
                var tree = new GameObject("Tree").transform;
                tree.SetParent(paradise, false);
                tree.position = p;
                Prim(PrimitiveType.Cylinder, tree, "Trunk", new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 1.5f, 0.5f), pTrunk, true, local: true);
                Prim(PrimitiveType.Sphere, tree, "Crown", new Vector3(0f, 4f, 0f), Vector3.one * 3.2f, rng.NextDouble() < 0.35 ? pBlossom : pLeaves, false, local: true);
            }
            // Белый замок
            Castle(paradise, pWhite, pGold, pGold, false);
            // Ангелы
            for (int i = 0; i < 7; i++)
            {
                var pos = AngelPos(i);
                var angel = Figure(paradise, $"Angel_{i}", pos, 1f, pAngel, pWhite, null, null);
                angel.gameObject.AddComponent<Hover>();
            }
            // Богиня
            var goddess = Figure(paradise, "Goddess", GoddessPos + Vector3.up * 1.5f, 1.6f, pGoddess, pGold, null, null);
            goddess.gameObject.AddComponent<Hover>();
            var halo = new GameObject("Halo").AddComponent<Light>();
            halo.transform.SetParent(goddess, false);
            halo.transform.localPosition = new Vector3(0f, 2f, -1f);
            halo.type = LightType.Point;
            halo.range = 14f;
            halo.intensity = 4f;
            halo.color = new Color(1f, 0.85f, 0.5f);

            // ---------- АД (те же места) ----------
            var hell = new GameObject("Hell").transform;
            Prim(PrimitiveType.Plane, hell, "Ground", Vector3.zero, new Vector3(20f, 1f, 20f), hGround, true);
            for (int i = 0; i < 9; i++)
            {
                var stone = Prim(PrimitiveType.Cube, hell, $"Path_{i}", new Vector3(Random.Range(-0.3f, 0.3f), 0.05f, -8f + i * 3.2f), new Vector3(2f, 0.15f, 2.4f), hRock, true);
                stone.transform.rotation = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-15f, 15f), Random.Range(-4f, 4f));
            }
            Prim(PrimitiveType.Cylinder, hell, "LavaPool", PondPos + Vector3.up * 0.02f, new Vector3(10f, 0.02f, 7f), hLava, false);
            var lavaLight = new GameObject("LavaLight").AddComponent<Light>();
            lavaLight.transform.SetParent(hell, false);
            lavaLight.transform.position = PondPos + Vector3.up * 2f;
            lavaLight.type = LightType.Point;
            lavaLight.range = 16f;
            lavaLight.intensity = 5f;
            lavaLight.color = new Color(1f, 0.3f, 0.05f);
            lavaLight.gameObject.AddComponent<TorchFlicker>();

            foreach (var p in treeSpots)
            {
                var tree = new GameObject("DeadTree").transform;
                tree.SetParent(hell, false);
                tree.position = p;
                Prim(PrimitiveType.Cylinder, tree, "Trunk", new Vector3(0f, 2f, 0f), new Vector3(0.35f, 2f, 0.35f), hBlack, true, local: true);
                for (int b = 0; b < 3; b++)
                {
                    var branch = Prim(PrimitiveType.Cube, tree, "Branch", new Vector3(0f, 3f + b * 0.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), hBlack, false, local: true);
                    branch.transform.localRotation = Quaternion.Euler(Random.Range(25f, 60f), b * 120f + Random.Range(-20f, 20f), 0f);
                }
            }
            Castle(hell, hBlack, hRock, hLava, true);
            for (int i = 0; i < 7; i++)
            {
                var demon = Figure(hell, $"Demon_{i}", AngelPos(i), 1.15f, hDemon, hBlack, hEyes, hRock);
                demon.gameObject.AddComponent<Hover>();
            }
            var demonQueen = Figure(hell, "HighDemon", GoddessPos + Vector3.up * 1.5f, 2.4f, hBlack, hDemon, hEyes, hRock);
            demonQueen.gameObject.AddComponent<Hover>();
            var redGlow = new GameObject("RedGlow").AddComponent<Light>();
            redGlow.transform.SetParent(demonQueen, false);
            redGlow.transform.localPosition = new Vector3(0f, 2f, -1.5f);
            redGlow.type = LightType.Point;
            redGlow.range = 18f;
            redGlow.intensity = 6f;
            redGlow.color = new Color(1f, 0.1f, 0.05f);
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

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[False Paradise] Сцена рая собрана: {ScenePath}. Play → идите к богине (или F9).");
        }

        // ---------- построители ----------

        static Vector3[] TreeSpots(System.Random rng)
        {
            var list = new System.Collections.Generic.List<Vector3>();
            while (list.Count < 26)
            {
                var p = new Vector3((float)(rng.NextDouble() * 80 - 40), 0f, (float)(rng.NextDouble() * 70 - 15));
                if (Mathf.Abs(p.x) < 5f) continue;                          // не на дороге
                if (Vector3.Distance(p, PondPos) < 8f) continue;            // не в пруду
                if (Vector3.Distance(p, CastlePos) < 16f) continue;         // не в замке
                list.Add(p);
            }
            return list.ToArray();
        }

        static Vector3 AngelPos(int i)
        {
            float a = i / 7f * Mathf.PI * 1.4f - Mathf.PI * 0.7f;
            return GoddessPos + new Vector3(Mathf.Sin(a) * 12f, 6f + (i % 3) * 2.5f, Mathf.Cos(a) * 6f + 6f);
        }

        static void Castle(Transform parent, Material walls, Material roofs, Material windows, bool hellish)
        {
            var c = new GameObject(hellish ? "DemonFortress" : "WhiteCastle").transform;
            c.SetParent(parent, false);
            c.position = CastlePos;
            float h = hellish ? 1.4f : 1f;
            Prim(PrimitiveType.Cube, c, "Keep", new Vector3(0f, 7f * h, 0f), new Vector3(16f, 14f * h, 10f), walls, true, local: true);
            Prim(PrimitiveType.Cube, c, "Gate", new Vector3(0f, 2.5f, -5.1f), new Vector3(4f, 5f, 0.3f), roofs, false, local: true);
            Vector3[] towers = { new Vector3(-10f, 0f, -6f), new Vector3(10f, 0f, -6f), new Vector3(-10f, 0f, 6f), new Vector3(10f, 0f, 6f) };
            foreach (var t in towers)
            {
                float th = (hellish ? 30f : 20f);
                Prim(PrimitiveType.Cylinder, c, "Tower", t + Vector3.up * th / 2f, new Vector3(4f, th / 2f, 4f), walls, true, local: true);
                var roof = Prim(PrimitiveType.Cube, c, "Roof", t + Vector3.up * (th + 1.5f), new Vector3(3f, 3f, 3f), roofs, false, local: true);
                roof.transform.localRotation = hellish ? Quaternion.Euler(0f, 45f, 0f) : Quaternion.Euler(45f, 0f, 45f);
                if (hellish)
                {
                    roof.transform.localScale = new Vector3(1.2f, 7f, 1.2f); // шпиль
                    roof.transform.localPosition = t + Vector3.up * (th + 3.5f);
                }
                // Окна
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Cube, c, "Window", t + new Vector3(0f, 6f + k * 5f, -2.05f), new Vector3(0.8f, 1.6f, 0.1f), windows, false, local: true);
            }
        }

        /// <summary>Фигура: тело-капсула, крылья, (для демонов) рога и горящие глаза.</summary>
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

        // ---------- профили постобработки ----------

        static VolumeProfile NewProfile(string name, out bool created)
        {
            string path = $"{MatDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            created = existing == null;
            if (existing != null) return existing;
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
            var p = NewProfile("ParadisePost", out bool created);
            if (!created) return p;
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.75f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.6f);
            ca.saturation.Override(15f);
            ca.colorFilter.Override(new Color(1f, 0.95f, 0.85f));
            p.Add<Vignette>(true).intensity.Override(0.15f);
            Finish(p);
            return p;
        }

        static VolumeProfile HellProfile()
        {
            var p = NewProfile("HellPost", out bool created);
            if (!created) return p;
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.2f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.2f);
            ca.contrast.Override(35f);
            ca.saturation.Override(-15f);
            ca.colorFilter.Override(new Color(1f, 0.6f, 0.55f));
            var v = p.Add<Vignette>(true);
            v.intensity.Override(0.5f);
            v.color.Override(new Color(0.25f, 0f, 0f));
            Finish(p);
            return p;
        }

        static VolumeProfile GlitchProfile()
        {
            var p = NewProfile("GlitchPost", out bool created);
            if (!created) return p;
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
