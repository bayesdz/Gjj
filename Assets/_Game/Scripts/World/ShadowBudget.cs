using System.Collections.Generic;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Бюджет теней: point-тень стоит 6 карт в атласе и 6 проходов отрисовки, поэтому
    /// в игре тени включены только у нескольких ближайших к камере источников из списка.
    /// Остальные светят без теней. Проверка несколько раз в секунду, без аллокаций.
    /// </summary>
    public class ShadowBudget : MonoBehaviour
    {
        [SerializeField] List<Light> candidates = new List<Light>();
        [SerializeField] int maxShadowed = 3;
        [SerializeField] float maxDistance = 30f;
        [SerializeField] float interval = 0.25f;

        readonly List<(float d, Light l)> sorted = new List<(float, Light)>();
        Transform cam;
        float next;

        void Start()
        {
            if (Camera.main != null) cam = Camera.main.transform;
            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            Refresh();
        }

        void Refresh()
        {
            if (cam == null)
            {
                if (Camera.main == null) return;
                cam = Camera.main.transform;
            }
            ApplyFor(cam.position);
        }

        /// <summary>Раздать тени для точки обзора (используется и редакторскими скриншотами).</summary>
        public void ApplyFor(Vector3 p)
        {
            sorted.Clear();
            foreach (var l in candidates)
                if (l != null && l.isActiveAndEnabled)
                    sorted.Add(((l.transform.position - p).sqrMagnitude, l));
            sorted.Sort((a, b) => a.d.CompareTo(b.d));
            float max2 = maxDistance * maxDistance;
            for (int i = 0; i < sorted.Count; i++)
            {
                bool on = i < maxShadowed && sorted[i].d < max2;
                var want = on ? LightShadows.Soft : LightShadows.None;
                if (sorted[i].l.shadows != want) sorted[i].l.shadows = want;
            }
        }
    }
}
