using UnityEngine;

namespace FalseParadise
{
    /// <summary>
    /// Житель, с которым можно говорить только через Око (зона 2, огр-кузнец).
    /// Временная версия до окна свободного диалога (A6/N19): E → запасная реплика по очереди,
    /// после первого разговора — NpcTalked и реплика Гласа.
    /// </summary>
    public class TalkNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] string npcId = "ogre_smith";
        [SerializeField] string speaker = "Огр-кузнец";
        [SerializeField] bool requiresEye = true;
        [TextArea] [SerializeField] string[] lines =
        {
            "Я кую подковы и ограды. Не мечи. Нам не с кем было воевать… до вас.",
            "Пришельцы с сиянием третий месяц жгут наши деревни.",
            "Вождь живёт в крепости наверху. Иди по дороге в гору — он мудрый и не хочет войны.",
        };
        [SerializeField] string narrationKeyOnFirstTalk = "z2.smith_talk_done";

        int next;
        bool talked;

        public bool CanInteract => !requiresEye || EyeOfTruth.Active;
        public string Prompt => "[E] Говорить";

        public void Interact()
        {
            if (!CanInteract || lines == null || lines.Length == 0) return;
            GameHUD.ShowSubtitle(speaker, lines[next], 6f);
            next = (next + 1) % lines.Length;
            if (talked) return;
            talked = true;
            GameEvents.RaiseNpcTalked(npcId);
            if (!string.IsNullOrEmpty(narrationKeyOnFirstTalk)) GameEvents.RaiseNarrationRequest(narrationKeyOnFirstTalk);
        }
    }
}
