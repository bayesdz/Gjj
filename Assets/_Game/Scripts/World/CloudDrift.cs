using UnityEngine;

namespace FalseParadise
{
    /// <summary>Медленное движение облаков по небу с бесшовным возвратом.</summary>
    public class CloudDrift : MonoBehaviour
    {
        [SerializeField] Vector3 velocity = new Vector3(1.2f, 0f, 0.3f);
        [SerializeField] float wrapRadius = 120f;

        Vector3 origin;

        void Awake() => origin = transform.localPosition;

        void Update()
        {
            transform.localPosition += velocity * Time.deltaTime;
            Vector3 d = transform.localPosition - origin;
            if (d.x > wrapRadius) transform.localPosition -= new Vector3(wrapRadius * 2f, 0f, 0f);
            if (d.x < -wrapRadius) transform.localPosition += new Vector3(wrapRadius * 2f, 0f, 0f);
        }
    }
}
