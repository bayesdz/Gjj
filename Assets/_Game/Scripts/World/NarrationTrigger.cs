using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Зона-триггер: когда игрок входит, просит Глас сказать реплику по ключу (один раз).
    /// raiseOnStart — сказать сразу после загрузки сцены (например, z1.enter).
    /// </summary>
    public class NarrationTrigger : MonoBehaviour
    {
        [SerializeField] string lineKey = "z1.enter";
        [SerializeField] bool once = true;
        [SerializeField] bool raiseOnStart;
        [SerializeField] float startDelay = 1.5f;

        bool fired;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c == null) c = gameObject.AddComponent<SphereCollider>();
            c.isTrigger = true;
        }

        IEnumerator Start()
        {
            if (!raiseOnStart) yield break;
            yield return new WaitForSeconds(startDelay);
            Fire();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            Fire();
        }

        void Fire()
        {
            if (once && fired) return;
            fired = true;
            if (!string.IsNullOrEmpty(lineKey)) GameEvents.RaiseNarrationRequest(lineKey);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.25f, 0.5f, 1f, 0.35f);
            var box = GetComponent<BoxCollider>();
            var sphere = GetComponent<SphereCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            if (box != null) Gizmos.DrawCube(box.center, box.size);
            else if (sphere != null) Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
    }
}
