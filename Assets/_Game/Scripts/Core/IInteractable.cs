namespace FalseParadise
{
    /// <summary>Всё, с чем игрок взаимодействует клавишей E.</summary>
    public interface IInteractable
    {
        bool CanInteract { get; }
        /// <summary>Подсказка на экране, например "[E] Осмотреть".</summary>
        string Prompt { get; }
        void Interact();
    }
}
