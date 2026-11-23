using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Меню False Paradise → Build Sandbox Scene: собирает тестовую сцену для проверки 3D
    /// (игрок, HUD, два гоблина, Огр-вождь, предметы, стены) из примитивов.
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
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(SceneDir);
            EnsureFolder(MatDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floorMat = Mat("Floor", new Color(0.30f, 0.27f, 0.24f));
            var wallMat = Mat("Wall", new Color(0.22f, 0.21f, 0.24f));
            var illusionMat = Mat("Illusion", new Color(0.55f, 0.05f, 0.05f));
            var truthMat = Mat("Truth", new Color(0.35f, 0.6f, 1f));
            var itemMat = Mat("Item", new Color(0.95f, 0.8f, 0.3f));
            var faceMat = Mat("Face", new Color(0.1f, 0.1f, 0.1f));

            // Пол и стены: комната 40×40
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = floorMat;

            var walls = new GameObject("Walls").transform;
            Box(walls, "Wall_N", new Vector3(0, 2, 20), new Vector3(40, 4, 1), wallMat);
            Box(walls, "Wall_S", new Vector3(0, 2, -20), new Vector3(40, 4, 1), wallMat);
            Box(walls, "Wall_E", new Vector3(20, 2, 0), new Vector3(1, 4, 40), wallMat);
            Box(walls, "Wall_W", new Vector3(-20, 2, 0), new Vector3(1, 4, 40), wallMat);
            Box(walls, "Pillar_1", new Vector3(-7, 2, 5), new Vector3(1.5f, 4, 1.5f), wallMat);
            Box(walls, "Pillar_2", new Vector3(7, 2, 5), new Vector3(1.5f, 4, 1.5f), wallMat);
            Box(walls, "Cover", new Vector3(0, 0.75f, -4), new Vector3(4, 1.5f, 0.8f), wallMat);

            // Игрок
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0f, 1f, -14f);
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

            SetRef(fpc, "cameraPivot", pivot);

            // HUD
            new GameObject("HUD").AddComponent<GameHUD>();

            // Атмосфера подземелья
            BuildAtmosphere(cam, walls);

            // Враги
            Enemy("Goblin_01", "goblin_01", new Vector3(-4f, 0f, 2f), 1f, 100f, illusionMat, truthMat, faceMat);
            Enemy("Goblin_02", "goblin_02", new Vector3(4f, 0f, 4f), 1f, 100f, illusionMat, truthMat, faceMat);
            Enemy("OgreChief", MercyCounter.OgreChiefId, new Vector3(0f, 0f, 13f), 1.6f, 200f, illusionMat, truthMat, faceMat);

            // Предметы
            Item("Toy", "floor1_toy", "Детская игрушка", new Vector3(-10f, 0.25f, -8f), itemMat,
                "Трофей их кровавых обрядов. Не трогайте.",
                "Деревянная лошадка. На боку неровно вырезано имя ребёнка.");
            Item("Drawing", "floor1_drawing", "Рисунок на камне", new Vector3(10f, 0.25f, -8f), itemMat,
                "Знаки тёмного культа. Они призывают смерть.",
                "Детский рисунок углём: большая семья гоблинов у костра.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[False Paradise] Сцена собрана: {ScenePath}. Нажмите Play.");
        }

        static void BuildAtmosphere(Camera cam, Transform walls)
        {
            // Без неба: тёмный фон и туман, дальние стены растворяются в темноте
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.05f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.06f;
            RenderSettings.fogColor = new Color(0.08f, 0.03f, 0.04f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // Солнца в подземелье нет: почти выключаем
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.intensity = 0.05f;
                l.color = new Color(0.5f, 0.55f, 0.8f);
                l.shadows = LightShadows.None;
            }

            // Фонарь игрока: тёплый, мягкий, без теней
            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(cam.transform, false);
            lantern.transform.localPosition = new Vector3(0.3f, -0.2f, 0.2f);
            lantern.type = LightType.Point;
            lantern.range = 7f;
            lantern.intensity = 1.2f;
            lantern.color = new Color(1f, 0.82f, 0.6f);
            lantern.shadows = LightShadows.None;

            // Факелы
            var torchMat = EmissiveMat("TorchFlame", new Color(1f, 0.45f, 0.1f), 6f);
            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var torchList = new System.Collections.Generic.List<Light>();
            Vector3[] spots =
            {
                new Vector3(-7f, 2.6f, 4.1f), new Vector3(7f, 2.6f, 4.1f),
                new Vector3(-19.3f, 2.6f, -10f), new Vector3(19.3f, 2.6f, -10f),
                new Vector3(-19.3f, 2.6f, 10f), new Vector3(19.3f, 2.6f, 10f),
                new Vector3(-6f, 2.6f, 19.3f), new Vector3(6f, 2.6f, 19.3f),
                new Vector3(0f, 2.6f, -19.3f)
            };
            int i = 0;
            foreach (var p in spots)
            {
                var torch = new GameObject($"Torch_{++i}").transform;
                torch.SetParent(walls, false);
                torch.position = p;

                var flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flame.name = "Flame";
                Object.DestroyImmediate(flame.GetComponent<Collider>());
                flame.transform.SetParent(torch, false);
                flame.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
                flame.GetComponent<Renderer>().sharedMaterial = torchMat;

                var l = new GameObject("Light").AddComponent<Light>();
                l.transform.SetParent(torch, false);
                l.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = 3f;
                l.color = new Color(1f, 0.35f, 0.15f);
                l.shadows = i <= 2 ? LightShadows.Soft : LightShadows.None;
                l.gameObject.AddComponent<TorchFlicker>();
                torchList.Add(l);
            }
            var so = new SerializedObject(mood);
            var list = so.FindProperty("torches");
            list.arraySize = torchList.Count;
            for (int k = 0; k < torchList.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = torchList[k];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Постобработка: виньетка, свечение, кинематографичный тонмаппинг
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
        }

        static VolumeProfile PostProfile()
        {
            string path = $"{MatDir}/DungeonPost.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.5f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.3f);
            color.contrast.Override(18f);
            color.saturation.Override(-12f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Material EmissiveMat(string name, Color color, float intensity)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(m, path);
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

            // «Лицо», чтобы было видно, куда смотрит враг
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

        static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Material Mat(string name, Color color)
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

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Нет поля {field} у {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Нет поля {field} у {target.GetType().Name}"); return; }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Нет поля {field} у {target.GetType().Name}"); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
