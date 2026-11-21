using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalseParadise
{
    /// <summary>Смерть игрока: блокировка управления и перезапуск сцены.</summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] float reloadDelay = 2f;

        void Awake() => GetComponent<Health>().Died += OnDied;

        void OnDied()
        {
            var c = GetComponent<FirstPersonController>();
            if (c != null) c.InputLocked = true;
            StartCoroutine(Reload());
        }

        IEnumerator Reload()
        {
            yield return new WaitForSeconds(reloadDelay);
            // TODO (N11): чекпоинты этажей вместо полного сброса.
            MercyCounter.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
