using System.IO;
using UnityEditor;
using UnityEngine;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Импорт гоблина из Tools/Blender/goblin (Blender + MakeHuman CC0).
    /// 1) Настройки импорта: Humanoid-риг (подходят анимации Mixamo), нормал-карты, linear-маски.
    /// 2) Меню False Paradise → Art → Build Goblin Prefab: URP-материалы для двух обликов
    ///    и префаб Goblin_Visual с детьми Illusion и Truth (для IllusionVisual).
    /// </summary>
    public class GoblinArtBuilder : AssetPostprocessor
    {
        public const string Dir = "Assets/_Game/Art/Characters/Goblin";
        public const string FbxPath = Dir + "/Goblin.fbx";
        public const string PrefabPath = Dir + "/Goblin_Visual.prefab";
        const string TexDir = Dir + "/Textures";
        const string MatDir = Dir + "/Materials";

        static bool Ours(string path) => path.Replace('\\', '/').StartsWith(Dir);

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.bakeAxisConversion = true;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.optimizeGameObjects = false;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
        }

        void OnPreprocessTexture()
        {
            if (!Ours(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (n.EndsWith("_Normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else if (n.EndsWith("_MetallicSmoothness") || n.EndsWith("_AO"))
            {
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            if (n.Contains("Detail")) ti.wrapMode = TextureWrapMode.Repeat;
            else ti.wrapMode = TextureWrapMode.Clamp;
        }

        // ------------------------------------------------------------------ prefab

        [MenuItem("False Paradise/Art/Build Goblin Prefab")]
        public static void BuildPrefab()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                EditorUtility.DisplayDialog("Goblin", "Не найден " + FbxPath, "OK");
                return;
            }
            EnsureFolder(MatDir);

            var skinI = Lit("Goblin_Body_Illusion", "Goblin_Body_Illusion", "Goblin_Body_Normal", "Goblin_Body_AO", true);
            var skinT = Lit("Goblin_Body_Truth", "Goblin_Body_Truth", "Goblin_Body_Normal", "Goblin_Body_AO", true);
            var clothI = Lit("Goblin_Clothes_Illusion", "Goblin_Clothes_Illusion", "Goblin_Clothes_Illusion_Normal", null, false);
            var clothT = Lit("Goblin_Clothes_Truth", "Goblin_Clothes_Truth", "Goblin_Clothes_Truth_Normal", null, false);
            var eyeI = Eye("Goblin_Eye_Illusion", true);
            var eyeT = Eye("Goblin_Eye_Truth", false);
            var teethI = Plain("Goblin_Teeth_Illusion", new Color(0.30f, 0.22f, 0.10f), 0.55f);
            var teethT = Plain("Goblin_Teeth_Truth", new Color(0.62f, 0.56f, 0.42f), 0.6f);
            var tongue = Plain("Goblin_Tongue", new Color(0.32f, 0.08f, 0.08f), 0.7f);

            var root = new GameObject("Goblin_Visual");
            var illusion = Look(root.transform, fbx, "Illusion", skinI, clothI, eyeI, teethI, tongue, true);
            var truth = Look(root.transform, fbx, "Truth", skinT, clothT, eyeT, teethT, tongue, false);
            truth.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"Goblin: префаб собран → {PrefabPath}. Назначьте Animator Controller на Illusion и Truth.");
        }

        static GameObject Look(Transform parent, GameObject fbx, string name, Material skin, Material cloth,
            Material eye, Material teeth, Material tongue, bool illusion)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetParent(parent, false);

            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                switch (r.name)
                {
                    case "Goblin_Body": r.sharedMaterial = skin; break;
                    case "Goblin_Eyes": r.sharedMaterial = eye; break;
                    case "Goblin_Teeth": r.sharedMaterial = teeth; break;
                    case "Goblin_Tusks": r.sharedMaterial = teeth; r.gameObject.SetActive(illusion); break;
                    case "Goblin_Tongue": r.sharedMaterial = tongue; break;
                    case "Goblin_Clothes_Illusion": r.sharedMaterial = cloth; r.gameObject.SetActive(illusion); break;
                    case "Goblin_Clothes_Truth": r.sharedMaterial = cloth; r.gameObject.SetActive(!illusion); break;
                }
                r.updateWhenOffscreen = false;
            }

            // Модель должна смотреть по +Z (так двигается EnemyController).
            // Проверяем по стопе: носок впереди пятки.
            var anim = go.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                var toes = anim.GetBoneTransform(HumanBodyBones.LeftToes);
                if (foot != null && toes != null)
                {
                    var d = go.transform.InverseTransformPoint(toes.position) -
                            go.transform.InverseTransformPoint(foot.position);
                    if (d.z < 0f) go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                anim.applyRootMotion = false;
            }
            else
            {
                Debug.LogWarning("Goblin: аватар не Humanoid, проверьте Rig → Configure.");
            }
            return go;
        }

        // ------------------------------------------------------------------ materials

        static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");

        static Texture2D Tex(string name) =>
            name == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

        static Material GetOrCreate(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(LitShader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = LitShader;
            return m;
        }

        static Material Lit(string name, string texPrefix, string normal, string ao, bool skinDetail)
        {
            var m = GetOrCreate(name);
            m.SetTexture("_BaseMap", Tex(texPrefix + "_Albedo"));
            m.SetColor("_BaseColor", Color.white);

            var n = Tex(normal);
            m.SetTexture("_BumpMap", n);
            m.SetFloat("_BumpScale", 1f);
            Toggle(m, "_NORMALMAP", n != null);

            var ms = Tex(texPrefix + "_MetallicSmoothness");
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 1f);            // множитель альфы карты
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            Toggle(m, "_METALLICSPECGLOSSMAP", ms != null);

            var occ = Tex(ao);
            m.SetTexture("_OcclusionMap", occ);
            m.SetFloat("_OcclusionStrength", 1f);
            Toggle(m, "_OCCLUSIONMAP", occ != null);

            // Поры и микрорельеф кожи: тайловая detail-нормаль (как в AAA-пайплайне)
            var detail = skinDetail ? Tex("Goblin_Skin_Detail_Normal") : null;
            m.SetTexture("_DetailNormalMap", detail);
            m.SetFloat("_DetailNormalMapScale", 0.6f);
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(28f, 28f));
            Toggle(m, "_DETAIL_MULX2", detail != null);

            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Eye(string name, bool glow)
        {
            var m = GetOrCreate(name);
            m.SetTexture("_BaseMap", Tex(name + "_Albedo"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.92f);
            var emi = glow ? Tex(name + "_Emission") : null;
            m.SetTexture("_EmissionMap", emi);
            m.SetColor("_EmissionColor", glow ? new Color(1f, 0.25f, 0.08f) * 6f : Color.black);
            Toggle(m, "_EMISSION", glow);
            m.globalIlluminationFlags = glow ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                                             : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Plain(string name, Color c, float smooth)
        {
            var m = GetOrCreate(name);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Toggle(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
