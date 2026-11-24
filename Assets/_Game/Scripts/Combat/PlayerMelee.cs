using UnityEngine;
using UnityEngine.InputSystem;

namespace FalseParadise
{
    /// <summary>Ближний бой: ЛКМ — удар. По врагу на коленях удар = «Добить».</summary>
    public class PlayerMelee : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float damage = 25f;
        [SerializeField] float range = 2.2f;
        [SerializeField] float radius = 0.35f;
        [SerializeField] float cooldown = 0.55f;
        [SerializeField] LayerMask hitMask = ~0;

        [Header("Отклик (необязательно)")]
        [Tooltip("Аниматор оружия в руках, триггер \"Swing\"")]
        [SerializeField] Animator weaponAnimator;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip swingClip;
        [SerializeField] AudioClip hitClip;
        [SerializeField] float hitShake = 0.06f;
        [Tooltip("Короткая пауза при попадании, усиливает ощущение удара")]
        [SerializeField] float hitStop = 0.05f;

        static readonly int SwingHash = Animator.StringToHash("Swing");

        /// <summary>Взмах оружием (для анимации руки от первого лица).</summary>
        public event System.Action Swung;

        FirstPersonController controller;
        readonly RaycastHit[] hits = new RaycastHit[16];
        float nextAttackTime;

        void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || cam == null || GameHUD.InspectBusy) return;
            if (controller != null && controller.InputLocked) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + cooldown;
                Swing();
            }
        }

        void Swing()
        {
            AnimatorUtil.SetTrigger(weaponAnimator, SwingHash);
            Swung?.Invoke();
            Play(swingClip);

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            int n = Physics.SphereCastNonAlloc(ray, radius, hits, range, hitMask, QueryTriggerInteraction.Ignore);

            // Ближайшее попадание, кроме самого игрока.
            float best = float.MaxValue;
            Collider target = null;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < best) { best = h.distance; target = h.collider; }
            }
            if (target == null) return;

            var enemy = target.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                if (enemy.ReceivePlayerHit(damage)) OnHitFeedback();
                return;
            }

            var health = target.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                OnHitFeedback();
            }
        }

        void OnHitFeedback()
        {
            Play(hitClip);
            CameraShake.Shake(hitShake, 0.12f);
            if (hitStop > 0f) HitStop.Do(hitStop);
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
