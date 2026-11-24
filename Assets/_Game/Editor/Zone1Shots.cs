using System.IO;
using UnityEditor;
using UnityEngine;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Пакетные скриншоты зоны 1: собирает сцену (Zone1Builder.Build) и рендерит 6 ракурсов
    /// на высоте глаз 1.7 м в PNG 1920×1080 → &lt;проект&gt;/Screenshots/.
    /// Запуск: Unity.exe -batchmode -projectPath C:\Gjj -executeMethod FalseParadise.EditorTools.Zone1Shots.Run -logFile ... -quit
    /// Также доступно из меню False Paradise → Zone 1 Screenshots.
    /// </summary>
    public static class Zone1Shots
    {
        const int Width = 1920, Height = 1080;
        const float Eye = 1.7f;

        struct Shot
        {
            public string name; public Vector2 from, to; public float lookY;
            public Shot(string n, float fx, float fz, float tx, float tz, float lookY = 0f)
            { name = n; from = new Vector2(fx, fz); to = new Vector2(tx, tz); this.lookY = lookY; }
        }

        static readonly Shot[] Shots =
        {
            new Shot("01_start", 0f, -4f, -5f, 22f),
            new Shot("02_first_fight", -3f, 30f, 10f, 56f),
            new Shot("03_camp", -12f, 98f, -27f, 94f, -0.5f),
            new Shot("04_bridge", -5.5f, 113.5f, 12f, 135f),
            new Shot("05_cage", 18f, 143f, 40f, 156f),
            new Shot("06_exit", 42f, 172f, 58f, 200f),
        };

        [MenuItem("False Paradise/Zone 1 Screenshots")]
        public static void Run()
        {
            bool async = EditorSettings.asyncShaderCompilation;
            try
            {
                int pc = System.Array.IndexOf(QualitySettings.names, "PC");
                if (pc >= 0) QualitySettings.SetQualityLevel(pc, true);
                EditorSettings.asyncShaderCompilation = false; // иначе вместо шейдеров голубые заглушки
                Zone1Builder.Build();
                Capture();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            finally
            {
                EditorSettings.asyncShaderCompilation = async;
            }
        }

        static void Capture()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);

            var cam = Camera.main;
            if (cam == null) throw new System.Exception("Main Camera не найдена в собранной сцене");

            // Частицы в редакторе сами не симулируются: прокручиваем, чтобы были дым и искры
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                ps.Simulate(4f, false, true, false);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var budget = Object.FindFirstObjectByType<ShadowBudget>();
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;

            foreach (var s in Shots)
            {
                var pos = new Vector3(s.from.x, Zone1Builder.Height(s.from.x, s.from.y) + Eye, s.from.y);
                var target = new Vector3(s.to.x, Zone1Builder.Height(s.to.x, s.to.y) + Eye + s.lookY, s.to.y);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
                if (budget != null) budget.ApplyFor(pos); // тени как в игре

                // Два прохода: первый прогревает шейдеры и временные буферы
                cam.Render();
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                string file = Path.Combine(dir, s.name + ".png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Debug.Log($"[Zone1Shots] {file}  pos={pos}");
            }

            cam.targetTexture = prevTarget;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log($"[Zone1Shots] Готово: {Shots.Length} кадров в {dir}");
        }
    }
}
