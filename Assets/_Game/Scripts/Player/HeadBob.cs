using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Покачивание головы при ходьбе. Висит на CameraPivot (позицию камеры внутри него
    /// двигает CameraShake, поворот пивота — FirstPersonController, конфликтов нет).
    /// </summary>
    public class HeadBob : MonoBehaviour
    {
        [SerializeField] FirstPersonController controller;
        [SerializeField] float amplitude = 0.04f;
        [SerializeField] float sideAmplitude = 0.022f;
        [Tooltip("Шагов в секунду при обычной ходьбе")]
        [SerializeField] float stepsPerSecond = 1.9f;
        [SerializeField] float walkSpeed = 4f;

        CharacterController cc;
        Vector3 basePos;
        float phase;
        float weight;

        void Awake()
        {
            basePos = transform.localPosition;
            if (controller == null) controller = GetComponentInParent<FirstPersonController>();
            if (controller != null) cc = controller.GetComponent<CharacterController>();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool grounded = cc == null || cc.isGrounded;
            float target = controller != null && grounded ? controller.MoveAmount : 0f;
            weight = Mathf.MoveTowards(weight, target, dt * 4f);

            float speed = 0f;
            if (cc != null)
            {
                var v = cc.velocity;
                speed = new Vector2(v.x, v.z).magnitude;
            }
            float rate = stepsPerSecond * Mathf.Clamp(speed / Mathf.Max(0.1f, walkSpeed), 0.6f, 1.6f);
            // Один полный цикл фазы = два шага: вверх-вниз дважды, влево-вправо один раз
            phase += dt * rate * Mathf.PI;
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

            var offset = new Vector3(Mathf.Sin(phase) * sideAmplitude, -Mathf.Abs(Mathf.Sin(phase)) * amplitude, 0f);
            transform.localPosition = basePos + offset * weight;
        }
    }
}
