using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Плавно сдвигает UV-координаты материала (вода, лава, магические руны).
    /// Использует MaterialPropertyBlock или экземпляр материала, чтобы не менять ассет на диске.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class SurfaceAnimator : MonoBehaviour
    {
        [SerializeField] Vector2 scrollSpeed = new Vector2(0.04f, 0.025f);
        [SerializeField] float pulseSpeed = 0f;
        [SerializeField] float pulseAmount = 0.2f;

        Renderer rend;
        Material matInstance;
        Vector2 offset;
        Color baseEmission;
        bool hasEmission;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            rend = GetComponent<Renderer>();
            if (rend == null || rend.sharedMaterial == null) return;
            matInstance = rend.material; // создаёт runtime-копию, не трогая ассет
            if (matInstance.HasProperty(EmissionColorId))
            {
                baseEmission = matInstance.GetColor(EmissionColorId);
                hasEmission = true;
            }
        }

        void Update()
        {
            if (matInstance == null) return;
            offset += scrollSpeed * Time.deltaTime;
            offset.x %= 1f;
            offset.y %= 1f;

            if (matInstance.HasProperty(BaseMapId))
                matInstance.SetTextureOffset(BaseMapId, offset);
            if (matInstance.HasProperty(BumpMapId))
                matInstance.SetTextureOffset(BumpMapId, offset * 1.15f);

            if (hasEmission && pulseSpeed > 0f)
            {
                float k = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                matInstance.SetColor(EmissionColorId, baseEmission * k);
            }
        }

        void OnDestroy()
        {
            if (matInstance != null) Destroy(matInstance);
        }
    }
}
