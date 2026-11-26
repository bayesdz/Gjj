using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Старейшина эльфов — временная версия для серого уровня (N13): после победы над Стражем
    /// E → реплика старейшины и выдача Ока истины (Q начинает работать).
    /// В N14 заменяется диалогом на трёх кнопках.
    /// </summary>
    public class ElderNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] string npcId = "elf_elder";
        [SerializeField] string speaker = "Старейшина эльфов";
        [TextArea] [SerializeField] string line = "Твои глаза лгут тебе, путник. Это не твоя вина. Но теперь ты можешь смотреть иначе.";
        [SerializeField] string narrationKeyOnGive = "gate.eye_received";
        [Tooltip("Если задано — говорить можно только после победы над Стражем")]
        [SerializeField] ElvenGateDoor gate;

        public bool EyeGiven { get; private set; }

        public bool CanInteract => !EyeGiven && (gate == null || gate.GuardianDefeated);
        public string Prompt => "[E] Говорить";

        public void Interact()
        {
            if (!CanInteract) return;
            EyeGiven = true;
            GameHUD.ShowSubtitle(speaker, line, 6f);

            var player = GameObject.FindWithTag("Player");
            var eye = player != null ? player.GetComponent<EyeOfTruth>() : null;
            if (eye != null) eye.Unlocked = true;

            GameEvents.RaiseNpcTalked(npcId);
            if (!string.IsNullOrEmpty(narrationKeyOnGive)) GameEvents.RaiseNarrationRequest(narrationKeyOnGive);
        }
    }
}
