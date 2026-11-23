using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FalseParadise
{
    /// <summary>
    /// Временный HUD на IMGUI для проверки механик (без настройки Canvas).
    /// Прицел, подсказка взаимодействия, здоровье, Око, окно осмотра, урон, отладка (F1).
    /// Баха позже заменит вид на префабы UI (задача B5), логика останется.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] EyeOfTruth eye;
        [SerializeField] Health playerHealth;
        [SerializeField] FirstPersonController controller;
        [SerializeField] bool showDebug = true;

        public static bool InspectOpen => instance != null && instance.inspectOpen;

        /// <summary>Окно осмотра открыто или закрыто в этом кадре (чтобы E не открыло его снова).</summary>
        public static bool InspectBusy => instance != null && (instance.inspectOpen || instance.closedFrame == Time.frameCount);

        static GameHUD instance;

        bool inspectOpen;
        int inspectOpenedFrame;
        int closedFrame = -1;
        string inspectTitle;
        string inspectText;
        float hurtFlash;
        string subtitleSpeaker;
        string subtitleText;
        float subtitleUntil;
        readonly Queue<string> log = new Queue<string>();

        GUIStyle center, box, title, small;

        void Awake()
        {
            instance = this;
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                if (interactor == null) interactor = p.GetComponent<PlayerInteractor>();
                if (eye == null) eye = p.GetComponent<EyeOfTruth>();
                if (playerHealth == null) playerHealth = p.GetComponent<Health>();
                if (controller == null) controller = p.GetComponent<FirstPersonController>();
            }
            if (playerHealth != null) playerHealth.Damaged += _ => hurtFlash = 0.35f;
        }

        void OnEnable() => GameEvents.Logged += OnLogged;
        void OnDisable() => GameEvents.Logged -= OnLogged;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void OnLogged(string line)
        {
            log.Enqueue($"{Time.time:0.0}  {line}");
            while (log.Count > 8) log.Dequeue();
        }

        public static void ShowInspect(string itemTitle, string text)
        {
            if (instance == null) return;
            instance.inspectOpen = true;
            instance.inspectOpenedFrame = Time.frameCount;
            instance.inspectTitle = itemTitle;
            instance.inspectText = text;
            if (instance.controller != null) instance.controller.InputLocked = true;
        }

        /// <summary>Субтитр внизу экрана (реплики Гласа, богини, NPC).</summary>
        public static void ShowSubtitle(string speaker, string text, float seconds)
        {
            if (instance == null) return;
            instance.subtitleSpeaker = speaker;
            instance.subtitleText = text;
            instance.subtitleUntil = Time.unscaledTime + seconds;
        }

        static void CloseInspect()
        {
            if (instance == null) return;
            instance.inspectOpen = false;
            instance.closedFrame = Time.frameCount;
            if (instance.controller != null) instance.controller.InputLocked = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (inspectOpen && Time.frameCount != inspectOpenedFrame &&
                (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                CloseInspect();

            if (kb.f1Key.wasPressedThisFrame) showDebug = !showDebug;
            if (hurtFlash > 0f) hurtFlash -= Time.unscaledDeltaTime;
        }

        void InitStyles()
        {
            if (center != null) return;
            center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, richText = true };
            center.normal.textColor = Color.white;
            box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 18, wordWrap = true, padding = new RectOffset(16, 16, 12, 12) };
            box.normal.textColor = Color.white;
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(1f, 0.85f, 0.5f);
            small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            small.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            InitStyles();
            float w = Screen.width, h = Screen.height;

            // Красная вспышка при получении урона
            if (hurtFlash > 0f)
            {
                var prev = GUI.color;
                GUI.color = new Color(0.8f, 0f, 0f, Mathf.Clamp01(hurtFlash) * 0.6f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // Синий оттенок, пока активно Око
            if (EyeOfTruth.Active)
            {
                var prev = GUI.color;
                GUI.color = new Color(0.3f, 0.6f, 1f, 0.12f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // Прицел
            GUI.Label(new Rect(w / 2 - 10, h / 2 - 12, 20, 24), "·", center);

            // Подсказка взаимодействия
            if (!inspectOpen && interactor != null && interactor.Current != null)
                GUI.Label(new Rect(0, h / 2 + 30, w, 30), interactor.Current.Prompt, center);

            // Здоровье
            if (playerHealth != null)
                GUI.Label(new Rect(20, h - 60, 300, 24), $"Здоровье: {Mathf.CeilToInt(playerHealth.Current)}/{playerHealth.Max}", small);

            // Око истины
            if (eye != null)
            {
                string s = !eye.Unlocked ? "Око: нет"
                    : EyeOfTruth.Active ? $"<color=#6cf>Око активно: {eye.ActiveRemaining:0.0} с</color>"
                    : eye.Ready ? "Око: готово [Q]"
                    : $"Око: перезарядка {eye.CooldownRemaining:0.0} с";
                GUI.Label(new Rect(20, h - 36, 400, 24), s, new GUIStyle(small) { richText = true });
            }

            // Субтитры
            if (!string.IsNullOrEmpty(subtitleText) && Time.unscaledTime < subtitleUntil)
            {
                var r = new Rect(w / 2 - 420, h - 170, 840, 90);
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = prev;
                string line = string.IsNullOrEmpty(subtitleSpeaker)
                    ? subtitleText
                    : $"<color=#f5d38a><b>{subtitleSpeaker}:</b></color> {subtitleText}";
                var st = new GUIStyle(center) { wordWrap = true, fontSize = 20 };
                GUI.Label(new Rect(r.x + 16, r.y, r.width - 32, r.height), line, st);
            }

            // Окно осмотра
            if (inspectOpen)
            {
                var r = new Rect(w / 2 - 300, h / 2 - 140, 600, 280);
                GUI.Box(r, GUIContent.none, box);
                GUI.Label(new Rect(r.x + 20, r.y + 14, r.width - 40, 30), inspectTitle, title);
                GUI.Label(new Rect(r.x + 20, r.y + 54, r.width - 40, r.height - 100), inspectText, new GUIStyle(box) { normal = { background = null, textColor = Color.white } });
                GUI.Label(new Rect(r.x, r.yMax - 36, r.width, 24), "[E] Закрыть", center);
            }

            // Отладка
            if (showDebug)
            {
                var r = new Rect(w - 380, 10, 370, 230);
                GUI.Box(r, GUIContent.none);
                float y = r.y + 6;
                GUI.Label(new Rect(r.x + 8, y, r.width, 20), $"Милосердие: {MercyCounter.Score} / порог {MercyCounter.Threshold}", small); y += 20;
                GUI.Label(new Rect(r.x + 8, y, r.width, 20), $"Пощажено: {MercyCounter.SparedCount}   Убито: {MercyCounter.KilledCount}", small); y += 20;
                GUI.Label(new Rect(r.x + 8, y, r.width, 20), $"Огр-вождь пощажён: {MercyCounter.OgreChiefSpared}   Концовка: {MercyCounter.GetEnding()}", small); y += 26;
                foreach (var line in log)
                {
                    GUI.Label(new Rect(r.x + 8, y, r.width, 20), line, small);
                    y += 18;
                }
                GUI.Label(new Rect(r.x + 8, r.yMax - 22, r.width, 20), "F1 — скрыть", small);
            }
        }
    }
}
