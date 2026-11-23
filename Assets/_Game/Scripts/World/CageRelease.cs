using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Клетка с Сияющим (этаж 1). По E дверь открывается, Сияющий поднимается в свет и исчезает,
    /// «улетая к богине». Глас получает реплику floor1.cage_opened.
    /// </summary>
    public class CageRelease : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform door;
        [SerializeField] Transform shining;
        [SerializeField] Light shiningLight;
        [SerializeField] float doorAngle = -105f;
        [SerializeField] float riseHeight = 3.5f;
        [SerializeField] float riseTime = 3f;
        [SerializeField] string narrationKey = "floor1.cage_opened";

        bool opened;

        public bool CanInteract => !opened;
        public string Prompt => "[E] Открыть клетку";

        public void Interact()
        {
            if (opened) return;
            opened = true;
            GameEvents.RaiseNarrationRequest(narrationKey);
            StartCoroutine(Release());
        }

        IEnumerator Release()
        {
            if (door != null)
            {
                var from = door.localRotation;
                var to = from * Quaternion.Euler(0f, doorAngle, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
                {
                    door.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                door.localRotation = to;
            }
            yield return new WaitForSeconds(0.6f);
            if (shining == null) yield break;

            var start = shining.position;
            float baseIntensity = shiningLight != null ? shiningLight.intensity : 0f;
            var renderers = shining.GetComponentsInChildren<Renderer>();
            for (float t = 0f; t < 1f; t += Time.deltaTime / riseTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                shining.position = start + Vector3.up * (riseHeight * k);
                if (shiningLight != null)
                {
                    shiningLight.intensity = baseIntensity * (1f + 4f * Mathf.Sin(k * Mathf.PI));
                    shiningLight.transform.position = shining.position + Vector3.up;
                }
                // в конце фигура растворяется во вспышке
                if (k > 0.8f) shining.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, (k - 0.8f) / 0.2f);
                yield return null;
            }
            foreach (var r in renderers) r.enabled = false;
            if (shiningLight != null) shiningLight.enabled = false;
        }
    }
}
