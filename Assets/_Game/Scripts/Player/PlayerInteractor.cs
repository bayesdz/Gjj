using UnityEngine;
using UnityEngine.InputSystem;

namespace FalseParadise
{
    /// <summary>Ищет IInteractable перед камерой; E — взаимодействие.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float range = 2.5f;
        [SerializeField] LayerMask mask = ~0;

        public IInteractable Current { get; private set; }

        FirstPersonController controller;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
        }

        void Update()
        {
            Current = null;
            if (cam == null || GameHUD.InspectBusy) return;
            if (controller != null && controller.InputLocked) return;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            int n = Physics.RaycastNonAlloc(ray, hits, range, mask, QueryTriggerInteraction.Collide);
            // Берём ближайшее попадание: так нельзя взаимодействовать сквозь стены.
            float best = float.MaxValue;
            Collider nearest = null;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                nearest = h.collider;
            }
            if (nearest != null)
            {
                var it = nearest.GetComponentInParent<IInteractable>();
                if (it != null && it.CanInteract) Current = it;
            }

            var kb = Keyboard.current;
            if (Current != null && kb != null && kb.eKey.wasPressedThisFrame)
                Current.Interact();
        }
    }
}
