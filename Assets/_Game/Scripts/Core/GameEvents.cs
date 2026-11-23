using System;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Общая шина событий. Геймплей только вызывает Raise*, остальные системы
    /// (ИИ-модуль, счётчик милосердия, UI) только подписываются.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<string> EnemySpared;      // enemyId
        public static event Action<string> EnemyKilled;      // enemyId
        public static event Action<string> ItemInspected;    // itemId
        public static event Action<bool> EyeToggled;         // true = Око включено
        public static event Action<string> NpcTalked;        // npcId
        public static event Action<string> GlasDisobeyed;    // momentId
        public static event Action<string> NarrationRequest; // ключ реплики, например "floor1.goblin_first_seen"
        public static event Action ParadiseFallStarted;      // начался глитч рая
        public static event Action ParadiseFallen;           // рай стал адом

        public static void RaiseEnemySpared(string enemyId) { Log("EnemySpared", enemyId); EnemySpared?.Invoke(enemyId); }
        public static void RaiseEnemyKilled(string enemyId) { Log("EnemyKilled", enemyId); EnemyKilled?.Invoke(enemyId); }
        public static void RaiseItemInspected(string itemId) { Log("ItemInspected", itemId); ItemInspected?.Invoke(itemId); }
        public static void RaiseEyeToggled(bool active) { Log("EyeToggled", active.ToString()); EyeToggled?.Invoke(active); }
        public static void RaiseNpcTalked(string npcId) { Log("NpcTalked", npcId); NpcTalked?.Invoke(npcId); }
        public static void RaiseGlasDisobeyed(string momentId) { Log("GlasDisobeyed", momentId); GlasDisobeyed?.Invoke(momentId); }
        public static void RaiseNarrationRequest(string lineKey) { Log("NarrationRequest", lineKey); NarrationRequest?.Invoke(lineKey); }
        public static void RaiseParadiseFallStarted() { Log("ParadiseFallStarted", ""); ParadiseFallStarted?.Invoke(); }
        public static void RaiseParadiseFallen() { Log("ParadiseFallen", ""); ParadiseFallen?.Invoke(); }

        /// <summary>Последние события для отладочного HUD.</summary>
        public static event Action<string> Logged;

        static void Log(string name, string arg)
        {
            Logged?.Invoke($"{name}({arg})");
        }

        // Если в проекте выключена перезагрузка домена (Enter Play Mode Options),
        // статические подписки переживают выход из Play Mode. Сбрасываем их.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EnemySpared = null;
            EnemyKilled = null;
            ItemInspected = null;
            EyeToggled = null;
            NpcTalked = null;
            GlasDisobeyed = null;
            NarrationRequest = null;
            ParadiseFallStarted = null;
            ParadiseFallen = null;
            Logged = null;
        }
    }
}
