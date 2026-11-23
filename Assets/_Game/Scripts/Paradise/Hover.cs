using UnityEngine;

namespace FalseParadise
{
    /// <summary>Плавное парение вверх-вниз и медленный поворот (ангелы, богиня, демоны).</summary>
    public class Hover : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.4f;
        [SerializeField] float speed = 1f;
        [SerializeField] float spin = 0f;

        Vector3 start;
        float phase;

        void Awake()
        {
            start = transform.localPosition;
            phase = Random.value * 10f;
        }

        void Update()
        {
            transform.localPosition = start + Vector3.up * (Mathf.Sin((Time.time + phase) * speed) * amplitude);
            if (spin != 0f) transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
        }
    }
}
