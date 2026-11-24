using UnityEngine;
using UnityEngine.InputSystem;

namespace FalseParadise
{
    /// <summary>
    /// Рука с кинжалом внизу кадра: инерция за мышью, покачивание при шаге,
    /// процедурный взмах при атаке (событие PlayerMelee.Swung). Висит на дочернем объекте камеры.
    /// </summary>
    public class ViewModel : MonoBehaviour
    {
        [SerializeField] FirstPersonController controller;
        [SerializeField] PlayerMelee melee;
        [SerializeField] float swayAmount = 0.0012f;
        [SerializeField] float swayMax = 0.045f;
        [SerializeField] float swaySmooth = 9f;
        [SerializeField] float bobAmount = 0.014f;
        [SerializeField] float swingTime = 0.3f;

        Vector3 basePos;
        Quaternion baseRot;
        Vector2 sway;
        float bobPhase;
        float swingT = 1f;

        void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            if (controller == null) controller = GetComponentInParent<FirstPersonController>();
            if (melee == null) melee = GetComponentInParent<PlayerMelee>();
        }

        void OnEnable() { if (melee != null) melee.Swung += OnSwing; }
        void OnDisable() { if (melee != null) melee.Swung -= OnSwing; }

        void OnSwing() => swingT = 0f;

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            Vector2 md = Vector2.zero;
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
                md = Mouse.current.delta.ReadValue();
            var target = Vector2.ClampMagnitude(md * swayAmount, swayMax);
            sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-swaySmooth * dt));

            float move = controller != null ? controller.MoveAmount : 0f;
            bobPhase += dt * 8f * move;
            var bob = new Vector3(Mathf.Cos(bobPhase) * bobAmount, -Mathf.Abs(Mathf.Sin(bobPhase)) * bobAmount, 0f) * move;

            swingT = Mathf.Min(1f, swingT + dt / Mathf.Max(0.05f, swingTime));
            float s = Mathf.Sin(swingT * Mathf.PI);               // 0 → 1 → 0
            float lunge = Mathf.Sin(Mathf.Min(1f, swingT * 1.6f) * Mathf.PI);
            var swingPos = new Vector3(-0.2f * s, 0.06f * s, 0.16f * lunge);
            var swingRot = Quaternion.Euler(-35f * s, -40f * s, -25f * s);

            transform.localPosition = basePos + new Vector3(-sway.x, -sway.y, 0f) + bob + swingPos;
            transform.localRotation = baseRot * Quaternion.Euler(sway.y * 60f, -sway.x * 60f, sway.x * 30f) * swingRot;
        }
    }
}
