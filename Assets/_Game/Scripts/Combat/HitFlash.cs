using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>Белая вспышка моделей при получении урона (URP Lit: свойство _BaseColor).</summary>
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float duration = 0.08f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color_ = Shader.PropertyToID("_Color");

        Renderer[] renderers;
        MaterialPropertyBlock flashBlock;
        MaterialPropertyBlock emptyBlock;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            flashBlock = new MaterialPropertyBlock();
            flashBlock.SetColor(BaseColor, flashColor);
            flashBlock.SetColor(Color_, flashColor);
            emptyBlock = new MaterialPropertyBlock();
            GetComponent<Health>().Damaged += _ => { if (isActiveAndEnabled) StartCoroutine(Flash()); };
        }

        IEnumerator Flash()
        {
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(flashBlock);
            yield return new WaitForSecondsRealtime(duration);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(emptyBlock);
        }
    }
}
