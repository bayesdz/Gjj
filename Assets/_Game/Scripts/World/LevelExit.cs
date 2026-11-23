using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalseParadise
{
    /// <summary>
    /// Выход с уровня: реплика Гласа и (если задано) загрузка следующей сцены.
    /// Полноценный переход с катсценой делает SceneFlow (N11).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LevelExit : MonoBehaviour
    {
        [SerializeField] string narrationKey = "floor1.exit";
        [SerializeField] string nextScene = "";
        [SerializeField] float delay = 2f;

        bool used;

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (used || !other.CompareTag("Player")) return;
            used = true;
            GameEvents.RaiseNarrationRequest(narrationKey);
            if (!string.IsNullOrEmpty(nextScene)) StartCoroutine(Load());
        }

        IEnumerator Load()
        {
            yield return new WaitForSeconds(delay);
            if (Application.CanStreamedLevelBeLoaded(nextScene)) SceneManager.LoadScene(nextScene);
            else Debug.LogWarning($"LevelExit: сцена {nextScene} не добавлена в Build Settings");
        }
    }
}
