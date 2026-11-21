using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>Короткая заморозка времени при попадании.</summary>
    public class HitStop : MonoBehaviour
    {
        static HitStop runner;
        bool running;

        public static void Do(float seconds)
        {
            if (runner == null)
            {
                var go = new GameObject("[HitStop]");
                runner = go.AddComponent<HitStop>();
            }
            if (!runner.running) runner.StartCoroutine(runner.Run(seconds));
        }

        IEnumerator Run(float seconds)
        {
            running = true;
            float prev = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = prev;
            running = false;
        }

        void OnDestroy()
        {
            if (runner == this)
            {
                if (running) Time.timeScale = 1f;
                runner = null;
            }
        }
    }
}
