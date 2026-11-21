using System.Collections;
using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Два облика объекта: иллюзия (что показывает Глас) и правда (что видит Око).
    /// Подходит для врагов, предметов, стен с рисунками, целых частей уровня.
    /// Коллайдеры держите на корневом объекте, а не на моделях.
    /// </summary>
    public class IllusionVisual : MonoBehaviour
    {
        [SerializeField] GameObject illusionModel;
        [SerializeField] GameObject truthModel;

        bool flashing;

        void OnEnable()
        {
            GameEvents.EyeToggled += Apply;
            Apply(EyeOfTruth.Active);
        }

        void OnDisable()
        {
            GameEvents.EyeToggled -= Apply;
            flashing = false;
        }

        void Apply(bool showTruth)
        {
            if (flashing) return;
            Set(showTruth);
        }

        void Set(bool showTruth)
        {
            if (illusionModel != null) illusionModel.SetActive(!showTruth);
            if (truthModel != null) truthModel.SetActive(showTruth);
        }

        /// <summary>Глитч: настоящий облик на долю секунды (этаж 1, до получения Ока).</summary>
        public void FlashTruth(float seconds)
        {
            if (!isActiveAndEnabled || EyeOfTruth.Active || flashing) return;
            StartCoroutine(Flash(seconds));
        }

        IEnumerator Flash(float seconds)
        {
            flashing = true;
            Set(true);
            yield return new WaitForSecondsRealtime(seconds);
            flashing = false;
            Set(EyeOfTruth.Active);
        }
    }
}
