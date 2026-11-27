using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Главный выбор зоны 2. Игрок входит в тронный зал → вождь опускает оружие (Surrender),
    /// говорит речь; дальше [E] Пощадить или [ЛКМ] Добить. После выбора — реплика Гласа
    /// и появляется лестница к свету.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ChiefChoice : MonoBehaviour
    {
        [SerializeField] EnemyController chief;
        [SerializeField] GameObject stairs;
        [SerializeField] string speaker = "Огр-вождь";
        [TextArea] [SerializeField] string speech = "Остановись. Ты называешь нас чудовищами, но посмотри, кого ты освобождал. Те Сияющие — её слуги. Мы ловили их, потому что они приходят убивать нас.";
        [TextArea] [SerializeField] string spareLine = "Если понадобится помощь, наш народ придёт.";
        [SerializeField] string speechKey = "z2.chief_speech";
        [SerializeField] string sparedKey = "z2.chief_spared";
        [SerializeField] string killedKey = "z2.chief_killed";
        [SerializeField] float stairsDelay = 4f;

        bool spoke, decided;

        void OnEnable()
        {
            GameEvents.EnemySpared += OnSpared;
            GameEvents.EnemyKilled += OnKilled;
        }

        void OnDisable()
        {
            GameEvents.EnemySpared -= OnSpared;
            GameEvents.EnemyKilled -= OnKilled;
        }

        void OnTriggerEnter(Collider other)
        {
            if (spoke || !other.CompareTag("Player")) return;
            spoke = true;
            if (chief != null) chief.Surrender();
            GameHUD.ShowSubtitle(speaker, speech, 9f);
            if (!string.IsNullOrEmpty(speechKey)) GameEvents.RaiseNarrationRequest(speechKey);
        }

        void OnSpared(string id)
        {
            if (decided || chief == null || id != chief.EnemyId) return;
            decided = true;
            GameHUD.ShowSubtitle(speaker, spareLine, 6f);
            if (!string.IsNullOrEmpty(sparedKey)) GameEvents.RaiseNarrationRequest(sparedKey);
            StartCoroutine(ShowStairs());
        }

        void OnKilled(string id)
        {
            if (decided || chief == null || id != chief.EnemyId) return;
            decided = true;
            if (!string.IsNullOrEmpty(killedKey)) GameEvents.RaiseNarrationRequest(killedKey);
            StartCoroutine(ShowStairs());
        }

        IEnumerator ShowStairs()
        {
            yield return new WaitForSeconds(stairsDelay);
            if (stairs != null) stairs.SetActive(true);
            CameraShake.Shake(0.15f, 1.5f);
        }
    }
}
