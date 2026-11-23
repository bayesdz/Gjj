using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FalseParadise
{
    /// <summary>
    /// Падение рая (STORY.md, раздел 6): рай → глитч → чернота → ад.
    /// Две версии мира лежат в paradiseRoot и hellRoot на одних и тех же местах.
    /// Запуск: Trigger() из катсцены/триггера, или F9 в отладке.
    /// </summary>
    public class ParadiseFall : MonoBehaviour
    {
        [Header("Две версии мира")]
        [SerializeField] GameObject paradiseRoot;
        [SerializeField] GameObject hellRoot;

        [Header("Постобработка (вес 0..1)")]
        [SerializeField] Volume paradiseVolume;
        [SerializeField] Volume hellVolume;
        [SerializeField] Volume glitchVolume;

        [Header("Солнце")]
        [SerializeField] Light sun;
        [SerializeField] Color paradiseSun = new Color(1f, 0.9f, 0.65f);
        [SerializeField] float paradiseSunIntensity = 1.6f;
        [SerializeField] Color hellSun = new Color(1f, 0.15f, 0.05f);
        [SerializeField] float hellSunIntensity = 0.7f;

        [Header("Небо и туман")]
        [SerializeField] Color paradiseSky = new Color(0.95f, 0.82f, 0.55f);
        [SerializeField] Color paradiseAmbient = new Color(0.75f, 0.72f, 0.6f);
        [SerializeField] float paradiseFogDensity = 0.008f;
        [SerializeField] Color hellSky = new Color(0.35f, 0.02f, 0.02f);
        [SerializeField] Color hellAmbient = new Color(0.3f, 0.05f, 0.04f);
        [SerializeField] float hellFogDensity = 0.03f;

        [Header("Тайминг")]
        [SerializeField] float glitchDuration = 2.2f;
        [SerializeField] float blackoutDuration = 0.8f;
        [SerializeField] float revealDuration = 2.5f;

        [Header("Звук (необязательно)")]
        [SerializeField] AudioSource paradiseMusic;
        [SerializeField] AudioSource hellMusic;
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioClip glitchClip;
        [SerializeField] AudioClip revealClip;

        [Header("Запуск: игрок подходит к богине")]
        [SerializeField] Transform triggerPoint;
        [SerializeField] float triggerRadius = 7f;
        [SerializeField] float speechDuration = 6f;
        [TextArea] [SerializeField] string goddessLine =
            "Мои дорогие дети, вы справились. Благодаря вам этот мир наконец очищен. Теперь вы можете отдохнуть. Вы заслужили свой рай.";
        [TextArea] [SerializeField] string heroLine = "Что… это такое?!";

        [SerializeField] bool debugKey = true;

        public bool Fallen { get; private set; }
        bool running;
        bool triggered;
        Transform player;

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p) player = p.transform;
            SetParadise();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (debugKey && kb != null && kb.f9Key.wasPressedThisFrame)
            {
                if (Fallen || running) { StopAllCoroutines(); running = false; triggered = false; SetParadise(); }
                else Trigger();
            }

            if (!triggered && !running && !Fallen && triggerPoint && player &&
                Vector3.Distance(player.position, triggerPoint.position) <= triggerRadius)
            {
                triggered = true;
                StartCoroutine(SpeechThenFall());
            }
        }

        IEnumerator SpeechThenFall()
        {
            GameEvents.RaiseNarrationRequest("paradise.goddess_thanks");
            GameHUD.ShowSubtitle("Богиня", goddessLine, speechDuration);
            yield return new WaitForSecondsRealtime(speechDuration);
            Trigger();
        }

        public void Trigger()
        {
            if (running || Fallen) return;
            triggered = true;
            StartCoroutine(Sequence());
        }

        void SetParadise()
        {
            Fallen = false;
            if (paradiseRoot) paradiseRoot.SetActive(true);
            if (hellRoot) hellRoot.SetActive(false);
            SetWeights(1f, 0f, 0f);
            ApplyEnvironment(0f);
            if (GlitchOverlay.Instance) { GlitchOverlay.Instance.Strength = 0f; GlitchOverlay.Instance.Blackout = 0f; }
            if (paradiseMusic) { paradiseMusic.volume = 1f; paradiseMusic.pitch = 1f; if (!paradiseMusic.isPlaying) paradiseMusic.Play(); }
            if (hellMusic) hellMusic.Stop();
        }

        IEnumerator Sequence()
        {
            running = true;
            GameEvents.RaiseParadiseFallStarted();
            var glitch = GlitchOverlay.Instance;
            if (sfx && glitchClip) sfx.PlayOneShot(glitchClip);

            // 1. Глитч: реальность мигает, ад проступает кадрами
            float t = 0f;
            while (t < glitchDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / glitchDuration;                   // 0 → 1, нарастание
                if (glitch) glitch.Strength = k;
                if (glitchVolume) glitchVolume.weight = Mathf.Clamp01(k + Random.Range(-0.3f, 0.3f));

                bool flashHell = Random.value < k * 0.35f;      // всё чаще мелькает ад
                if (paradiseRoot) paradiseRoot.SetActive(!flashHell);
                if (hellRoot) hellRoot.SetActive(flashHell);
                ApplyEnvironment(flashHell ? 1f : 0f);
                SetWeights(flashHell ? 0f : 1f, flashHell ? 1f : 0f, glitchVolume ? glitchVolume.weight : 0f);

                if (sun) sun.intensity *= Random.Range(0.6f, 1.1f);
                if (paradiseMusic) paradiseMusic.pitch = 1f - k * 0.5f + Random.Range(-0.05f, 0.05f);
                CameraShake.Shake(0.02f + 0.06f * k, 0.1f);
                yield return null;
            }

            // 2. Чернота
            if (glitch) { glitch.Strength = 0f; glitch.Blackout = 1f; }
            if (paradiseMusic) paradiseMusic.Stop();
            if (paradiseRoot) paradiseRoot.SetActive(false);
            if (hellRoot) hellRoot.SetActive(true);
            ApplyEnvironment(1f);
            SetWeights(0f, 1f, 0f);
            yield return new WaitForSecondsRealtime(blackoutDuration);

            // 3. Ад медленно проявляется из темноты
            if (sfx && revealClip) sfx.PlayOneShot(revealClip);
            if (hellMusic) hellMusic.Play();
            Fallen = true;
            GameEvents.RaiseParadiseFallen();
            CameraShake.Shake(0.15f, 0.6f);

            t = 0f;
            while (t < revealDuration)
            {
                t += Time.unscaledDeltaTime;
                if (glitch) glitch.Blackout = 1f - Mathf.SmoothStep(0f, 1f, t / revealDuration);
                yield return null;
            }
            if (glitch) glitch.Blackout = 0f;
            GameHUD.ShowSubtitle("Ты", heroLine, 4f);
            running = false;
        }

        void SetWeights(float paradise, float hell, float glitchW)
        {
            if (paradiseVolume) paradiseVolume.weight = paradise;
            if (hellVolume) hellVolume.weight = hell;
            if (glitchVolume) glitchVolume.weight = glitchW;
        }

        void ApplyEnvironment(float hell)
        {
            RenderSettings.ambientLight = Color.Lerp(paradiseAmbient, hellAmbient, hell);
            RenderSettings.fogColor = Color.Lerp(paradiseSky, hellSky, hell);
            RenderSettings.fogDensity = Mathf.Lerp(paradiseFogDensity, hellFogDensity, hell);
            var cam = Camera.main;
            if (cam) cam.backgroundColor = RenderSettings.fogColor;
            if (sun)
            {
                sun.color = Color.Lerp(paradiseSun, hellSun, hell);
                sun.intensity = Mathf.Lerp(paradiseSunIntensity, hellSunIntensity, hell);
            }
        }
    }
}
