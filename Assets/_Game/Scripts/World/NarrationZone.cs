using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Невидимая зона: когда игрок входит, Глас получает запрос на реплику (GameEvents.NarrationRequest).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class NarrationZone : MonoBehaviour
    {
        [SerializeField] string narrationKey = "floor1.intro";
        [SerializeField] bool once = true;

        bool fired;

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (fired && once) return;
            if (!other.CompareTag("Player")) return;
            fired = true;
            GameEvents.RaiseNarrationRequest(narrationKey);
        }
    }
}
