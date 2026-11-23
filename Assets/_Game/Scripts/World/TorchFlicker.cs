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

        Light lightSource;
        float baseRange;
        float seed;

        void Awake()
        {
            lightSource = GetComponent<Light>();
            baseRange = lightSource.range;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
            lightSource.intensity = baseIntensity + n * 2f * flickerAmount;
            lightSource.range = baseRange * (1f + n * 0.08f);
        }
    }
}
