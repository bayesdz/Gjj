using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Висит на игроке. Если игрок упал ниже killY (в пропасть), он возвращается
    /// на ближайшую безопасную точку. Без смерти и перезагрузки сцены.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FallRespawn : MonoBehaviour
    {
        [SerializeField] float killY = -14f;
        [SerializeField] Transform[] safePoints;

        CharacterController cc;
        Vector3 lastPosition;

        void Awake() => cc = GetComponent<CharacterController>();

        void LateUpdate()
        {
            if (transform.position.y >= killY)
            {
                lastPosition = transform.position;
                return;
            }

            Transform best = null;
            float bestDist = float.MaxValue;
            if (safePoints != null)
            {
                foreach (var p in safePoints)
                {
                    if (p == null) continue;
                    float d = Vector3.SqrMagnitude(new Vector3(p.position.x - lastPosition.x, 0f, p.position.z - lastPosition.z));
                    if (d < bestDist) { bestDist = d; best = p; }
                }
            }
            var target = best != null ? best.position : lastPosition + Vector3.up;

            cc.enabled = false;
            transform.position = target;
            if (best != null) transform.rotation = Quaternion.Euler(0f, best.eulerAngles.y, 0f);
            cc.enabled = true;
            CameraShake.Shake(0.12f, 0.3f);
        }
    }
}
