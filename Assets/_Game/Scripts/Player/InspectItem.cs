using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Предмет для осмотра (игрушка, рисунок, письмо). Текст зависит от Ока истины.
    /// Даёт +1 к милосердию (один раз на itemId).
    /// </summary>
    public class InspectItem : MonoBehaviour, IInteractable
    {
        [SerializeField] string itemId = "floor1_toy";
        [SerializeField] string title = "Детская игрушка";
        [TextArea(2, 5)] [SerializeField] string illusionText = "Трофей их кровавых обрядов. Не трогайте.";
        [TextArea(2, 5)] [SerializeField] string truthText = "Деревянная лошадка. На боку вырезано имя ребёнка.";
        [Tooltip("Ключ реплики Гласа при осмотре (можно пусто)")]
        [SerializeField] string narrationKey = "";

        public bool CanInteract => true;
        public string Prompt => "[E] Осмотреть";

        public void Interact()
        {
            GameHUD.ShowInspect(title, EyeOfTruth.Active ? truthText : illusionText);
            GameEvents.RaiseItemInspected(itemId);
            if (!string.IsNullOrEmpty(narrationKey)) GameEvents.RaiseNarrationRequest(narrationKey);
        }
    }
}
