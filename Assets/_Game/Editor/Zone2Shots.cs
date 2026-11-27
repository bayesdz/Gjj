using UnityEditor;
using static FalseParadise.EditorTools.Zone1Shots;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Пакетные скриншоты зоны 2 (деревня и крепость огров) в иллюзии и «через Око» (truth = true)
    /// → &lt;проект&gt;/Screenshots/z2_*.png.
    /// Запуск: Unity.exe -batchmode -projectPath C:\Gjj -executeMethod FalseParadise.EditorTools.Zone2Shots.Run -logFile ... -quit
    /// </summary>
    public static class Zone2Shots
    {
        static readonly Shot[] Shots =
        {
            new Shot("01_entry", -26f, 3f, 20f, 4f, 2f),
            new Shot("02_village", 2f, -2f, 32f, 10f, 1f),
            new Shot("03_village_truth", 2f, -2f, 32f, 10f, 1f, true),
            new Shot("04_forge", 24f, -1f, 13f, -7f, 0.5f),
            new Shot("05_forge_truth", 24f, -1f, 13f, -7f, 0.5f, true),
            new Shot("06_gardens", 18f, -19f, 28f, -9f, 0f),
            new Shot("07_gardens_truth", 18f, -19f, 28f, -9f, 0f, true),
            new Shot("08_fortress_view", 38f, 18f, 44f, 70f, 6f),
            new Shot("09_fortress_view_truth", 38f, 18f, 44f, 70f, 6f, true),
            new Shot("10_courtyard", 44f, 60f, 44f, 80f, 3f),
            new Shot("11_throne", 44f, 76f, 44f, 90f, 1.5f),
            new Shot("12_throne_truth", 44f, 76f, 44f, 90f, 1.5f, true),
        };

        [MenuItem("False Paradise/Zone 2 Screenshots")]
        public static void Run() => RunFor(Zone2Builder.Build, Shots, Zone2Builder.Height, "z2_");
    }
}
