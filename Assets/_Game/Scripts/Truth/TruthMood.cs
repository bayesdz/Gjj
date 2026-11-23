using System.Collections.Generic;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Настроение мира в иллюзии и в правде. Когда включается Око, плавно меняет
    /// фоновый свет, туман и цвет факелов: иллюзия — тревожная, красная; правда — тёплая, живая.
    /// </summary>
    public class TruthMood : MonoBehaviour
    {
        [Header("Иллюзия (что показывает Глас)")]
        [SerializeField] Color illusionAmbient = new Color(0.07f, 0.05f, 0.07f);
        [SerializeField] Color illusionFog = new Color(0.08f, 0.03f, 0.04f);
        [SerializeField] Color illusionTorch = new Color(1f, 0.35f, 0.15f);

        [Header("Правда (Око истины)")]
        [SerializeField] Color truthAmbient = new Color(0.16f, 0.15f, 0.12f);
        [SerializeField] Color truthFog = new Color(0.12f, 0.11f, 0.09f);
        [SerializeField] Color truthTorch = new Color(1f, 0.78f, 0.45f);

        [SerializeField] float fadeTime = 0.35f;
        [SerializeField] List<Light> torches = new List<Light>();

        float t;      // 0 = иллюзия, 1 = правда
        float target;

        void OnEnable()
        {
            GameEvents.EyeToggled += OnEye;
            target = t = EyeOfTruth.Active ? 1f : 0f;
            Apply();
        }

        void OnDisable() => GameEvents.EyeToggled -= OnEye;

        void OnEye(bool active) => target = active ? 1f : 0f;

        void Update()
        {
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
            Apply();
        }

        void Apply()
        {
            RenderSettings.ambientLight = Color.Lerp(illusionAmbient, truthAmbient, t);
            RenderSettings.fogColor = Color.Lerp(illusionFog, truthFog, t);
            var torch = Color.Lerp(illusionTorch, truthTorch, t);
            foreach (var l in torches) if (l != null) l.color = torch;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
        }
    }
}
