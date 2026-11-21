using UnityEngine;
using UnityEngine.InputSystem;

namespace FalseParadise
{
    /// <summary>
    /// Око истины: Q — на несколько секунд показать настоящий мир, затем перезарядка.
    /// Повесить на игрока. До города эльфов unlocked = false.
    /// </summary>
    public class EyeOfTruth : MonoBehaviour
    {
        [SerializeField] bool unlocked = true;
        [SerializeField] float duration = 5f;
        [SerializeField] float cooldown = 8f;

        public static bool Active { get; private set; }

        public bool Unlocked { get => unlocked; set => unlocked = value; }
        public float Duration => duration;
        public float Cooldown => cooldown;
        public float ActiveRemaining => Active ? Mathf.Max(0f, endsAt - Time.time) : 0f;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public bool Ready => unlocked && !Active && Time.time >= readyAt;

        float endsAt;
        float readyAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = false;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.qKey.wasPressedThisFrame && !GameHUD.InspectBusy) TryActivate();
            if (Active && Time.time >= endsAt) Deactivate();
        }

        public bool TryActivate()
        {
            if (!Ready) return false;
            Active = true;
            endsAt = Time.time + duration;
            GameEvents.RaiseEyeToggled(true);
            return true;
        }

        public void Deactivate()
        {
            if (!Active) return;
            Active = false;
            readyAt = Time.time + cooldown;
            GameEvents.RaiseEyeToggled(false);
        }

        void OnDisable() => Deactivate();
    }
}
