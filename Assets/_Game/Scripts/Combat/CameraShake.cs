using UnityEngine;

namespace FalseParadise
{
    /// <summary>Тряска камеры. Повесить на Main Camera (дочерний объект CameraPivot).</summary>
    public class CameraShake : MonoBehaviour
    {
        static CameraShake instance;

        Vector3 basePos;
        float intensity;
        float timeLeft;
        float duration;

        void Awake()
        {
            instance = this;
            basePos = transform.localPosition;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Shake(float intensity, float duration)
        {
            if (instance == null) return;
            instance.intensity = instance.timeLeft > 0f ? Mathf.Max(instance.intensity, intensity) : intensity;
            instance.duration = duration;
            instance.timeLeft = duration;
        }

        void LateUpdate()
        {
            if (timeLeft > 0f)
            {
                timeLeft -= Time.unscaledDeltaTime;
                float k = duration > 0f ? Mathf.Clamp01(timeLeft / duration) : 0f;
                transform.localPosition = basePos + Random.insideUnitSphere * (intensity * k);
            }
            else
            {
                intensity = 0f;
                transform.localPosition = basePos;
            }
        }
    }
}
