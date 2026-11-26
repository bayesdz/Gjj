using UnityEditor;
using static FalseParadise.EditorTools.Zone1Shots;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Пакетные скриншоты Врат эльфов: собирает сцену (GateBuilder.Build) и рендерит 6 ракурсов
    /// в &lt;проект&gt;/Screenshots/gate_*.png.
    /// Запуск: Unity.exe -batchmode -projectPath C:\Gjj -executeMethod FalseParadise.EditorTools.GateShots.Run -logFile ... -quit
    /// </summary>
    public static class GateShots
    {
        static readonly Shot[] Shots =
        {
            new Shot("01_arrival", -36f, 0f, 4f, 0f, 1.5f),
            new Shot("02_moon_shaft", -13f, -11f, 4f, 2f, 3f),
            new Shot("03_camp", 0f, 3f, -14f, 15f, 0.3f),
            new Shot("04_guardian", -7f, -4f, 4f, -1f, 2.2f),
            new Shot("05_gate", 6f, 1f, 28f, 0f, 4f),
            new Shot("06_oculus", -1f, -6f, 2f, 11f, 13f),
        };

        [MenuItem("False Paradise/Elven Gate Screenshots")]
        public static void Run() => RunFor(GateBuilder.Build, Shots, GateBuilder.Height, "gate_");
    }
}
