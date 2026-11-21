using System;
using UnityEngine;

namespace FalseParadise
{
    public class Health : MonoBehaviour
    {
        [SerializeField] float max = 100f;

        public float Max => max;
        public float Current { get; private set; }
        public float Normalized => max > 0f ? Current / max : 0f;
        public bool IsDead => Current <= 0f;

        /// <summary>Не получает урон (например, враг стоит на коленях или пощажён).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Здоровье не опускается ниже этого значения от урона (для пощады). Kill() игнорирует его.</summary>
        public float Floor { get; set; }

        public event Action<float> Damaged; // величина урона
        public event Action Died;

        void Awake() => Current = max;

        public void TakeDamage(float amount)
        {
            if (IsDead || Invulnerable || amount <= 0f) return;
            Current = Mathf.Max(Floor, Current - amount);
            Damaged?.Invoke(amount);
            if (Current <= 0f) Died?.Invoke();
        }

        public void Kill()
        {
            if (IsDead) return;
            Current = 0f;
            Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(max, Current + amount);
        }
    }
}
