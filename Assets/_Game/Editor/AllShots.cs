using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FalseParadise.EditorTools
{
    /// <summary>Снимает скриншоты всех авторских карт (Zone 1, Elven Gate, Zone 2, Paradise) за один проход Unity.</summary>
    public static class AllShots
    {
        public static void RunAll()
        {
            SandboxBuilder.Build();
            Zone1Shots.Run();
            GateShots.Run();
            Zone2Shots.Run();
            RunParadise();
        }

        public static void RunParadise()
        {
            bool async = EditorSettings.asyncShaderCompilation;
            try
            {
                int pc = System.Array.IndexOf(QualitySettings.names, "PC");
                if (pc >= 0) QualitySettings.SetQualityLevel(pc, true);
                EditorSettings.asyncShaderCompilation = false;
                ParadiseBuilder.Build();
                CaptureParadise();
            }
            finally
            {
                EditorSettings.asyncShaderCompilation = async;
            }
        }

        static void CaptureParadise()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            var cam = Camera.main;
            if (cam == null) return;

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;

            var fall = Object.FindFirstObjectByType<ParadiseFall>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var paradise = fall != null ? typeof(ParadiseFall).GetField("paradiseRoot", flags)?.GetValue(fall) as GameObject : GameObject.Find("Paradise");
            var hell = fall != null ? typeof(ParadiseFall).GetField("hellRoot", flags)?.GetValue(fall) as GameObject : null;

            void Snap(string name, Vector3 pos, Vector3 target, bool isHell)
            {
                if (paradise) paradise.SetActive(!isHell);
                if (hell) hell.SetActive(isHell);
                if (fall != null)
                {
                    typeof(ParadiseFall).GetMethod("ApplyEnvironment", flags)?.Invoke(fall, new object[] { isHell ? 1f : 0f });
                    typeof(ParadiseFall).GetMethod("SetWeights", flags)?.Invoke(fall, new object[] { isHell ? 0f : 1f, isHell ? 1f : 0f, 0f });
                }
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    ps.Simulate(4f, false, true, false);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
                cam.Render();
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, "par_" + name + ".png"), tex.EncodeToPNG());
            }

            Snap("01_paradise_avenue", new Vector3(0f, 2.2f, -12f), new Vector3(0f, 4f, 30f), false);
            Snap("02_paradise_pond", new Vector3(-6f, 2.5f, 4f), new Vector3(-16f, 1.5f, 16f), false);
            Snap("03_hell_avenue", new Vector3(0f, 2.2f, -12f), new Vector3(0f, 4f, 30f), true);
            Snap("04_hell_lava", new Vector3(-6f, 2.5f, 4f), new Vector3(-16f, 1.5f, 16f), true);

            if (paradise) paradise.SetActive(true);
            if (hell) hell.SetActive(false);
            cam.targetTexture = prevTarget;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
