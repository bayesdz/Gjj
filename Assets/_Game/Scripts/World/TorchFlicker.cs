using UnityEngine;

namespace FalseParadise
{
    /// <summary>Мерцание факела: шум Перлина по яркости и чуть-чуть по радиусу.</summary>
    [RequireComponent(typeof(Light))]
    public class TorchFlicker : MonoBehaviour
    {
        [SerializeField] float baseIntensity = 3f;
        [SerializeField] float flickerAmount = 0.6f;
        [SerializeField] float speed = 6f;
        [Tooltip("Дрожание позиции света, м: тени от факела «живут»")]
        [SerializeField] float jitter = 0.035f;

        Light lightSource;
        float baseRange;
        float seed;
        Vector3 basePos;

        void Awake()
        {
            lightSource = GetComponent<Light>();
            baseRange = lightSource.range;
            seed = Random.value * 100f;
            basePos = transform.localPosition;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
            lightSource.intensity = baseIntensity + n * 2f * flickerAmount;
            lightSource.range = baseRange * (1f + n * 0.08f);
            if (jitter > 0f)
            {
                float t = Time.time * speed * 1.3f;
                transform.localPosition = basePos + new Vector3(
                    Mathf.PerlinNoise(seed + 11f, t) - 0.5f,
                    (Mathf.PerlinNoise(seed + 23f, t) - 0.5f) * 0.6f,
                    Mathf.PerlinNoise(seed + 37f, t) - 0.5f) * (2f * jitter);
            }
        }
    }
}
