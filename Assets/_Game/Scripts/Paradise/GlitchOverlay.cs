using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Экранный глитч поверх всего: рваные цветные полосы, сдвиги, мигание чёрным.
    /// Сила 0..1 задаётся снаружи (ParadiseFall). Дёшево и без настройки рендера.
    /// </summary>
    public class GlitchOverlay : MonoBehaviour
    {
        public static GlitchOverlay Instance { get; private set; }

        /// <summary>0 — нет глитча, 1 — максимум.</summary>
        public float Strength { get; set; }

        /// <summary>0..1 — затемнение экрана (блэкаут).</summary>
        public float Blackout { get; set; }

        Texture2D white;
        readonly Color[] palette =
        {
            new Color(1f, 0f, 0.3f), new Color(0f, 1f, 0.9f), new Color(1f, 1f, 1f), new Color(0.6f, 0f, 0f)
        };

        void Awake()
        {
            Instance = this;
            white = Texture2D.whiteTexture;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.depth = -1000;
            float w = Screen.width, h = Screen.height;
            var prev = GUI.color;

            if (Strength > 0.01f)
            {
                // Полосы: количество и непрозрачность растут с силой
                int bars = Mathf.RoundToInt(Mathf.Lerp(2, 24, Strength));
                for (int i = 0; i < bars; i++)
                {
                    if (Random.value > 0.35f + Strength * 0.5f) continue;
                    float y = Random.value * h;
                    float bh = Random.Range(2f, 6f + 40f * Strength);
                    float x = (Random.value - 0.5f) * w * 0.3f * Strength;
                    var c = palette[Random.Range(0, palette.Length)];
                    c.a = Random.Range(0.15f, 0.6f) * Strength;
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(x, y, w, bh), white);
                }

                // Иногда весь экран вспыхивает
                if (Random.value < 0.08f * Strength)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.25f * Strength);
                    GUI.DrawTexture(new Rect(0, 0, w, h), white);
                }
            }

            if (Blackout > 0.01f)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(Blackout));
                GUI.DrawTexture(new Rect(0, 0, w, h), white);
            }

            GUI.color = prev;
        }
    }
}
