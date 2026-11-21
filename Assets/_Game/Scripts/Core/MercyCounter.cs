using System;
using System.Collections.Generic;
using UnityEngine;

namespace FalseParadise
{
    public enum Ending
    {
        A_AllRescued, // Огр-вождь пощажён и набран порог: никто не попадает под гипноз
        B_TwoLost     // двое друзей остаются у богини
    }

    /// <summary>
    /// Скрытый счётчик милосердия (docs/STORY.md, раздел 8).
    /// Каждое действие засчитывается один раз (по id).
    /// </summary>
    public static class MercyCounter
    {
        public const string OgreChiefId = "ogre_chief";

        public const int PointsSpare = 1;
        public const int PointsItem = 1;
        public const int PointsNpcTalk = 1;
        public const int PointsOgreChief = 3;

        /// <summary>Порог для концовки А. Подстраиваем на плейтесте.</summary>
        public static int Threshold = 7;

        public static int Score { get; private set; }
        public static bool OgreChiefSpared { get; private set; }
        public static int SparedCount { get; private set; }
        public static int KilledCount { get; private set; }

        public static event Action<int> ScoreChanged;

        static readonly HashSet<string> counted = new HashSet<string>();

        public static Ending GetEnding()
        {
            return OgreChiefSpared && Score >= Threshold ? Ending.A_AllRescued : Ending.B_TwoLost;
        }

        public static void Reset()
        {
            counted.Clear();
            Score = 0;
            OgreChiefSpared = false;
            SparedCount = 0;
            KilledCount = 0;
            ScoreChanged?.Invoke(Score);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ScoreChanged = null;
        }

        // Выполняется после GameEvents.ResetStatics, поэтому подписка не дублируется.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Reset();
            GameEvents.EnemySpared += OnEnemySpared;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.ItemInspected += OnItemInspected;
            GameEvents.NpcTalked += OnNpcTalked;
        }

        static void OnEnemySpared(string enemyId)
        {
            if (!counted.Contains("spare:" + enemyId)) SparedCount++;
            if (enemyId == OgreChiefId)
            {
                OgreChiefSpared = true;
                Add("spare:" + enemyId, PointsOgreChief);
            }
            else
            {
                Add("spare:" + enemyId, PointsSpare);
            }
        }

        static void OnEnemyKilled(string enemyId)
        {
            KilledCount++;
        }

        static void OnItemInspected(string itemId) => Add("item:" + itemId, PointsItem);
        static void OnNpcTalked(string npcId) => Add("npc:" + npcId, PointsNpcTalk);

        static void Add(string key, int points)
        {
            if (!counted.Add(key)) return;
            Score += points;
            ScoreChanged?.Invoke(Score);
        }
    }
}
