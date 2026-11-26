using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Врата эльфов (N13): закрыты, пока жив Страж. Когда Страж убит (GameEvents.EnemyKilled с guardianId),
    /// Глас отмечает непослушание (GlasDisobeyed), створки медленно распахиваются, руны разгораются.
    /// </summary>
    public class ElvenGateDoor : MonoBehaviour
    {
        [SerializeField] Transform leftDoor;
        [SerializeField] Transform rightDoor;
        [SerializeField] float openAngle = 100f;
        [SerializeField] float openTime = 3.5f;
        [SerializeField] float openDelay = 2.5f;
        [SerializeField] string guardianId = "gate_guardian";
        [Tooltip("Ключ реплики после победы над Стражем (можно пусто)")]
        [SerializeField] string narrationKeyOnDefeat = "gate.guardian_defeated";
        [SerializeField] Light runeLight;
        [SerializeField] float runeLightOpen = 14f;

        public bool Opened { get; private set; }
        public bool GuardianDefeated { get; private set; }

        void OnEnable() => GameEvents.EnemyKilled += OnEnemyKilled;
        void OnDisable() => GameEvents.EnemyKilled -= OnEnemyKilled;

        void OnEnemyKilled(string enemyId)
        {
            if (enemyId != guardianId || GuardianDefeated) return;
            GuardianDefeated = true;
            GameEvents.RaiseGlasDisobeyed(guardianId);
            if (!string.IsNullOrEmpty(narrationKeyOnDefeat)) GameEvents.RaiseNarrationRequest(narrationKeyOnDefeat);
            StartCoroutine(OpenRoutine(openDelay));
        }

        /// <summary>Открыть сразу (отладка, повторный вход в зону через SceneFlow).</summary>
        public void Open()
        {
            GuardianDefeated = true;
            StartCoroutine(OpenRoutine(0f));
        }

        IEnumerator OpenRoutine(float delay)
        {
            if (Opened) yield break;
            Opened = true;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            var l0 = leftDoor != null ? leftDoor.localRotation : Quaternion.identity;
            var r0 = rightDoor != null ? rightDoor.localRotation : Quaternion.identity;
            float light0 = runeLight != null ? runeLight.intensity : 0f;
            CameraShake.Shake(0.2f, openTime * 0.6f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / openTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                // Левая петля (локальный -x) поворачивается на +угол, правая на −угол: створки уходят от игрока
                if (leftDoor != null) leftDoor.localRotation = l0 * Quaternion.Euler(0f, openAngle * k, 0f);
                if (rightDoor != null) rightDoor.localRotation = r0 * Quaternion.Euler(0f, -openAngle * k, 0f);
                if (runeLight != null) runeLight.intensity = Mathf.Lerp(light0, runeLightOpen, k);
                yield return null;
            }
            if (leftDoor != null) leftDoor.localRotation = l0 * Quaternion.Euler(0f, openAngle, 0f);
            if (rightDoor != null) rightDoor.localRotation = r0 * Quaternion.Euler(0f, -openAngle, 0f);
        }
    }
}
