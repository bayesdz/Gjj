using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Меню False Paradise → Build Floor 1 (Goblin Lair): собирает сцену первого этажа из
    /// Art/Levels/GoblinLair/GoblinLair.fbx (генерируется Tools/Blender/lair).
    /// Повторный запуск пересоздаёт сцену.
    /// </summary>
    public class LairBuilder : AssetPostprocessor
    {
        const string Dir = "Assets/_Game/Art/Levels/GoblinLair";
        const string FbxPath = Dir + "/GoblinLair.fbx";
        const string TexDir = Dir + "/Textures";
        const string MatDir = Dir + "/Materials";
        const string ScenePath = "Assets/_Game/Scenes/Floor1_GoblinLair.unity";

        static bool Ours(string p) => p.Replace('\\', '/').StartsWith(Dir);

        // ------------------------------------------------------------ import settings
        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.indexFormat = ModelImporterIndexFormat.Auto;
        }

        void OnPreprocessTexture()
        {
            if (!Ours(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            if (n.EndsWith("_nor")) ti.textureType = TextureImporterType.NormalMap;
            if (n.EndsWith("_mask")) ti.sRGBTexture = false;
            if (n.StartsWith("Lair_Drawing")) ti.alphaIsTransparency = true;
        }

        // ------------------------------------------------------------ manifest
        [Serializable] class MatEntry
        {
            public string name, diff, nor, mask;
            public float[] tint, color, emission;
            public float smooth_mul = 0.8f, smoothness = 0.2f, emission_strength;
            public bool cutout;
        }
        [Serializable] class Manifest { public List<MatEntry> items = new List<MatEntry>(); }

        static Dictionary<string, Material> BuildMaterials()
        {
            SandboxBuilder.EnsureFolder(MatDir);
            var json = File.ReadAllText(Dir + "/materials.json");
            var man = JsonUtility.FromJson<Manifest>(json);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<string, Material>();
            foreach (var e in man.items)
            {
                string path = $"{MatDir}/{e.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                Texture2D T(string f) => string.IsNullOrEmpty(f) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{f}");

                m.SetTexture("_BaseMap", T(e.diff));
                var c = Color.white;
                if (e.tint != null && e.tint.Length >= 3) c = new Color(e.tint[0], e.tint[1], e.tint[2]);
                if (e.color != null && e.color.Length >= 3) c = new Color(e.color[0], e.color[1], e.color[2]);
                m.SetColor("_BaseColor", c);

                var nor = T(e.nor);
                m.SetTexture("_BumpMap", nor);
                Toggle(m, "_NORMALMAP", nor != null);

                var mask = T(e.mask);
                m.SetTexture("_MetallicGlossMap", mask);
                m.SetTexture("_OcclusionMap", mask);       // G = AO
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.SetFloat("_Smoothness", mask != null ? Mathf.Clamp01(e.smooth_mul) : e.smoothness);
                Toggle(m, "_METALLICSPECGLOSSMAP", mask != null);
                Toggle(m, "_OCCLUSIONMAP", mask != null);

                bool glow = e.emission != null && e.emission.Length >= 3 && e.emission_strength > 0f;
                Toggle(m, "_EMISSION", glow);
                if (glow)
                {
                    m.SetColor("_EmissionColor", new Color(e.emission[0], e.emission[1], e.emission[2]) * e.emission_strength);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                if (e.cutout)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.35f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                EditorUtility.SetDirty(m);
                result[e.name] = m;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        static void Toggle(Material m, string k, bool on) { if (on) m.EnableKeyword(k); else m.DisableKeyword(k); }

        // ------------------------------------------------------------ build
        [MenuItem("False Paradise/Build Floor 1 (Goblin Lair)")]
        public static void Build()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { EditorUtility.DisplayDialog("Goblin Lair", "Не найден " + FbxPath, "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var mats = BuildMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var level = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(level, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            level.name = "GoblinLair";

            // materials
            foreach (var r in level.GetComponentsInChildren<Renderer>(true))
            {
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] == null) continue;
                    string key = arr[i].name.Replace(" (Instance)", "").Replace(".", "_");
                    if (mats.TryGetValue(key, out var m)) arr[i] = m;
                }
                r.sharedMaterials = arr;
            }

            // decor: two looks under one IllusionVisual
            var decor = new GameObject("Decor");
            var gIll = new GameObject("Illusion"); gIll.transform.SetParent(decor.transform, false);
            var gTru = new GameObject("Truth"); gTru.transform.SetParent(decor.transform, false);
            var children = new List<Transform>();
            foreach (Transform t in level.transform) children.Add(t);

            Transform spawn = null, look = null, door = null, shining = null, cageAnchor = null, exit = null;
            var goblins = new List<Transform>();
            var items = new Dictionary<string, Transform>();
            var lights = new List<Transform>();
            var zones = new List<Transform>();

            foreach (var t in children)
            {
                string n = t.name;
                if (n.StartsWith("I_")) t.SetParent(gIll.transform, true);
                else if (n.StartsWith("T_")) t.SetParent(gTru.transform, true);

                if (n == "Spawn_Player") spawn = t;
                else if (n == "Look_Player") look = t;
                else if (n == "Cage_Door") door = t;
                else if (n == "Shining_One") shining = t;
                else if (n == "Item_Cage") cageAnchor = t;
                else if (n == "Exit") exit = t;
                else if (n.StartsWith("Goblin_")) goblins.Add(t);
                else if (n.StartsWith("Item_")) items[n] = t;
                else if (n.StartsWith("Light_")) lights.Add(t);
                else if (n.StartsWith("Zone__")) zones.Add(t);

                if (n.Length > 8 && n.Substring(1, 3) == "_P_")
                    SpawnProp(t, n.Substring(4, n.Length - 8), mats);   // S_P_<model>_NNN

                bool isMesh = t.GetComponent<MeshFilter>() != null;
                if (isMesh && NeedsCollider(n)) t.gameObject.AddComponent<MeshCollider>();
                if (isMesh && n != "Cage_Door" && n != "Shining_One")
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.ReflectionProbeStatic);
            }
            var iv = decor.AddComponent<IllusionVisual>();
            SandboxBuilder.SetRef(iv, "illusionModel", gIll);
            SandboxBuilder.SetRef(iv, "truthModel", gTru);
            gTru.SetActive(false);

            // player
            var cam = SandboxBuilder.CreatePlayer(spawn != null ? spawn.position : Vector3.up);
            var player = cam.GetComponentInParent<FirstPersonController>().transform;
            if (spawn != null && look != null) player.rotation = Flat(look.position - spawn.position);
            new GameObject("HUD").AddComponent<GameHUD>();

            // atmosphere, lights, fire
            var torchLights = Atmosphere(cam, lights, out Light shiningLight);

            // enemies (real goblin model if Goblin_Visual.prefab exists)
            var illusionMat = SandboxBuilder.Mat("Illusion", new Color(0.55f, 0.05f, 0.05f));
            var truthMat = SandboxBuilder.Mat("Truth", new Color(0.35f, 0.6f, 1f));
            var faceMat = SandboxBuilder.Mat("Face", new Color(0.1f, 0.1f, 0.1f));
            foreach (var g in goblins)
            {
                string id = g.name.ToLower();               // goblin_01
                var e = SandboxBuilder.Enemy(g.name, id, g.position, 1f, 100f, illusionMat, truthMat, faceMat);
                if (spawn != null) e.transform.rotation = Flat(spawn.position - g.position);
                var agent = e.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 1.2f;
                agent.angularSpeed = 540f;
                agent.stoppingDistance = 1.2f;
                if (id == "goblin_01")
                    SandboxBuilder.SetString(e.GetComponent<EnemyController>(), "narrationKeyOnSeen", "floor1.goblin_first_seen");
            }

            // items
            if (items.TryGetValue("Item_Toy", out var toy))
                Item(toy, "floor1_toy", "Детская игрушка", "floor1.toy",
                    "Трофей их кровавых обрядов. Не трогайте.",
                    "Деревянный слонёнок. На боку неровно вырезано имя ребёнка.");
            if (items.TryGetValue("Item_Drawing", out var drawing))
                Item(drawing, "floor1_drawing", "Рисунок на стене", "floor1.drawing",
                    "Знаки тёмного культа. Они призывают смерть.",
                    "Детский рисунок углём: большая семья гоблинов у костра.");

            // cage with the Shining One
            if (cageAnchor != null)
            {
                var col = cageAnchor.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(1.4f, 2f, 1.4f);
                var cr = cageAnchor.gameObject.AddComponent<CageRelease>();
                SandboxBuilder.SetRef(cr, "door", door);
                SandboxBuilder.SetRef(cr, "shining", shining);
                SandboxBuilder.SetRef(cr, "shiningLight", shiningLight);
            }

            // Glas narration zones: Zone__<key>__<sx>x<sy>x<sz> (Blender axes: x, y, up)
            foreach (var z in zones)
            {
                var parts = z.name.Split(new[] { "__" }, StringSplitOptions.None);
                var s = parts.Length > 2 ? parts[2].Split('x') : new[] { "3", "3", "3" };
                var box = z.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(F(s[0]), F(s[2]), F(s[1]));
                var nz = z.gameObject.AddComponent<NarrationZone>();
                SandboxBuilder.SetString(nz, "narrationKey", parts[1]);
            }
            if (exit != null)
            {
                var box = exit.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(4f, 3f, 4f);
                exit.gameObject.AddComponent<LevelExit>();
            }

            // TruthMood for this level's palette
            var mood = new GameObject("TruthMood").AddComponent<TruthMood>();
            var so = new SerializedObject(mood);
            so.FindProperty("illusionAmbient").colorValue = new Color(0.030f, 0.030f, 0.034f);
            so.FindProperty("illusionFog").colorValue = new Color(0.020f, 0.019f, 0.020f);
            so.FindProperty("illusionTorch").colorValue = new Color(1f, 0.42f, 0.18f);
            so.FindProperty("truthAmbient").colorValue = new Color(0.085f, 0.07f, 0.05f);
            so.FindProperty("truthFog").colorValue = new Color(0.06f, 0.048f, 0.035f);
            so.FindProperty("truthTorch").colorValue = new Color(1f, 0.72f, 0.45f);
            var list = so.FindProperty("torches");
            list.arraySize = torchLights.Count;
            for (int i = 0; i < torchLights.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = torchLights[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // navmesh for goblins
            var navGo = new GameObject("NavMesh");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string navPath = Dir + "/GoblinLair_NavMesh.asset";
                AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            }

            SandboxBuilder.EnsureFolder("Assets/_Game/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            SandboxBuilder.AddToBuildSettings(ScenePath);
            Debug.Log($"[False Paradise] Этаж 1 собран: {ScenePath}");
        }

        /// <summary>Скан-модель из Props/&lt;model&gt;.fbx в точке, заданной пустышкой из Blender.</summary>
        static void SpawnProp(Transform anchor, string model, Dictionary<string, Material> mats)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/Props/{model}.fbx");
            if (src == null) { Debug.LogWarning("Goblin Lair: нет пропа " + model); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, anchor);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] == null) continue;
                    string key = arr[i].name.Replace(" (Instance)", "").Replace(".", "_");
                    if (mats.TryGetValue(key, out var m)) arr[i] = m;
                }
                r.sharedMaterials = arr;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                if (NeedsCollider(model)) r.gameObject.AddComponent<MeshCollider>();
            }
        }

        static bool NeedsCollider(string n)
        {
            if (n == "Lair_Cave" || n == "Cage_Door" || n.StartsWith("S_Cage") || n.StartsWith("S_Support")) return true;
            string[] solid = { "barrel", "crate", "boulder", "stump", "chest", "fire_pit", "rock_moss", "spinning" };
            foreach (var k in solid) if (n.Contains(k)) return true;
            return false;
        }

        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        static Quaternion Flat(Vector3 d)
        {
            d.y = 0f;
            return d.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        static void Item(Transform anchor, string id, string title, string narration, string lie, string truth)
        {
            var col = anchor.gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.45f;
            var it = anchor.gameObject.AddComponent<InspectItem>();
            SandboxBuilder.SetString(it, "itemId", id);
            SandboxBuilder.SetString(it, "title", title);
            SandboxBuilder.SetString(it, "illusionText", lie);
            SandboxBuilder.SetString(it, "truthText", truth);
            SandboxBuilder.SetString(it, "narrationKey", narration);
        }

        // ------------------------------------------------------------ atmosphere
        static List<Light> Atmosphere(Camera cam, List<Transform> anchors, out Light shiningLight)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.030f, 0.030f, 0.034f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.fogColor = new Color(0.020f, 0.019f, 0.020f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.farClipPlane = 80f;

            // hero's hand lantern
            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(cam.transform, false);
            lantern.transform.localPosition = new Vector3(0.28f, -0.3f, 0.25f);
            lantern.type = LightType.Point;
            lantern.range = 6.5f;
            lantern.intensity = 1.1f;
            lantern.color = new Color(1f, 0.78f, 0.52f);
            lantern.shadows = LightShadows.None;
            var lf = lantern.gameObject.AddComponent<TorchFlicker>();
            SandboxBuilder.SetFloat(lf, "baseIntensity", 1.1f);
            SandboxBuilder.SetFloat(lf, "flickerAmount", 0.08f);

            Dust(cam.transform);

            var flameMat = ParticleMat("Lair_Flame", true);
            var smokeMat = ParticleMat("Lair_Smoke", false);
            var torches = new List<Light>();
            shiningLight = null;
            int shadowBudget = 6;
            foreach (var a in anchors)
            {
                string kind = a.name.Split('_')[1];
                var l = a.gameObject.AddComponent<Light>();
                l.type = LightType.Point;
                switch (kind)
                {
                    case "Torch":
                        l.range = 9f; l.intensity = 2.6f; l.color = new Color(1f, 0.42f, 0.18f);
                        l.shadows = shadowBudget-- > 0 ? LightShadows.Soft : LightShadows.None;
                        Flicker(l, 2.6f, 0.5f);
                        Fire(a, flameMat, null, 0.12f);
                        torches.Add(l);
                        break;
                    case "Fire":
                        l.range = 14f; l.intensity = 4.5f; l.color = new Color(1f, 0.48f, 0.2f);
                        l.shadows = LightShadows.Soft;
                        Flicker(l, 4.5f, 1.0f);
                        Fire(a, flameMat, smokeMat, 0.45f);
                        torches.Add(l);
                        break;
                    case "Lantern":
                        l.range = 5f; l.intensity = 1.4f; l.color = new Color(1f, 0.62f, 0.32f);
                        Flicker(l, 1.4f, 0.15f);
                        break;
                    case "Shining":
                        l.range = 6f; l.intensity = 2.2f; l.color = new Color(1f, 0.9f, 0.7f);
                        l.shadows = LightShadows.Soft;
                        shiningLight = l;
                        break;
                }
            }

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
            return torches;
        }

        static void Flicker(Light l, float baseIntensity, float amount)
        {
            var f = l.gameObject.AddComponent<TorchFlicker>();
            SandboxBuilder.SetFloat(f, "baseIntensity", baseIntensity);
            SandboxBuilder.SetFloat(f, "flickerAmount", amount);
        }

        static Material ParticleMat(string name, bool additive)
        {
            SandboxBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Fire(Transform at, Material flame, Material smoke, float size)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(at, false);
            go.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.6f);
            main.startColor = new Color(1f, 0.55f, 0.2f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 40f + size * 60f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = size * 0.35f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.4f), new GradientColorKey(new Color(0.6f, 0.1f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 1.5f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = flame;

            if (smoke == null) return;
            var sgo = new GameObject("Smoke");
            sgo.transform.SetParent(at, false);
            sgo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            sgo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var sp = sgo.AddComponent<ParticleSystem>();
            var sm = sp.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            sm.startColor = new Color(0.15f, 0.14f, 0.13f, 0.25f);
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            var sem = sp.emission; sem.rateOverTime = 6f;
            var ssh = sp.shape; ssh.shapeType = ParticleSystemShapeType.Cone; ssh.angle = 15f; ssh.radius = 0.3f;
            var scol = sp.colorOverLifetime; scol.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            scol.color = sg;
            var ssz = sp.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.5f));
            sgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = smoke;
        }

        /// <summary>Пылинки в луче фонаря вокруг игрока (как в метро).</summary>
        static void Dust(Transform cam)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, 0f, 2f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 8f;
            main.startSpeed = 0.02f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
            main.startColor = new Color(0.9f, 0.85f, 0.75f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 45f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(5f, 3f, 5f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.05f; noise.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Lair_Dust", true);
        }

        static VolumeProfile PostProfile()
        {
            string path = Dir + "/LairPost.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.72f);
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.42f);
            vig.smoothness.Override(0.45f);
            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.75f);
            p.Add<ChromaticAberration>(true).intensity.Override(0.12f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.7f);
            ca.contrast.Override(18f);
            ca.saturation.Override(-28f);
            ca.colorFilter.Override(new Color(1f, 0.96f, 0.88f));
            var split = p.Add<SplitToning>(true);   // зелёно-холодные тени, тёплый свет: палитра «метро»
            split.shadows.Override(new Color(0.36f, 0.45f, 0.40f));
            split.highlights.Override(new Color(0.92f, 0.76f, 0.55f));
            split.balance.Override(-25f);

            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            AssetDatabase.SaveAssets();
            return p;
        }
    }
}
