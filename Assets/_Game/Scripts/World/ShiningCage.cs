using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Клетка с Сияющим (зона 1, задача N16): E — открыть, дверь распахивается,
    /// Сияющий благодарит и улетает вверх, в темноту.
    /// </summary>
    public class ShiningCage : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform door;
        [SerializeField] float doorOpenAngle = 115f;
        [SerializeField] Transform shining;
        [SerializeField] Light shiningLight;
        [SerializeField] string speaker = "Сияющий";
        [TextArea] [SerializeField] string thanksLine = "Свобода… Спасибо, дитя. Я лечу к ней — она узнает о тебе.";
        [Tooltip("Ключ реплики Гласа после открытия (можно пусто)")]
        [SerializeField] string narrationKeyOnOpen = "";
        [SerializeField] float flyHeight = 45f;
        [SerializeField] float flyTime = 4.5f;

        public bool Opened { get; private set; }

        public bool CanInteract => !Opened;
        public string Prompt => "[E] Открыть клетку";

        public void Interact()
        {
            if (Opened) return;
            Opened = true;
            StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            // Дверь
            if (door != null)
            {
                var from = door.localRotation;
                var to = from * Quaternion.Euler(0f, doorOpenAngle, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
                {
                    door.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                door.localRotation = to;
            }

            if (shining == null) yield break;
            var hover = shining.GetComponent<Hover>();
            if (hover != null) hover.enabled = false;

            GameHUD.ShowSubtitle(speaker, thanksLine, 4f);
            if (!string.IsNullOrEmpty(narrationKeyOnOpen)) GameEvents.RaiseNarrationRequest(narrationKeyOnOpen);
            yield return new WaitForSeconds(0.8f);

            // Полёт вверх по спирали
            Vector3 start = shining.position;
            Vector3 startScale = shining.localScale;
            float startIntensity = shiningLight != null ? shiningLight.intensity : 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / flyTime)
            {
                float e = t * t;
                float angle = t * Mathf.PI * 3f;
                shining.position = start + new Vector3(Mathf.Cos(angle) * 2f * t, e * flyHeight, Mathf.Sin(angle) * 2f * t);
                shining.localScale = startScale * Mathf.Lerp(1f, 0.3f, t);
                if (shiningLight != null) shiningLight.intensity = Mathf.Lerp(startIntensity, startIntensity * 2f, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            shining.gameObject.SetActive(false);
        }
    }
}
