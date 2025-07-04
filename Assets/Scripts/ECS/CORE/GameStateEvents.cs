using System;
using Unity.Entities;

/// <summary>
/// Перечисление событий, которые могут произойти в UI,
/// используемые для коммуникации между ECS и MonoBehaviour.
/// </summary>
public enum UIStateEvent
{
    /// <summary>
    /// Событие, указывающее на переключение состояния инвентаря (открытие/закрытие).
    /// </summary>
    InventoryToggled,

    /// <summary>
    /// Событие, указывающее на открытие UI поселения.
    /// </summary>
    SettlementOpened,

    /// <summary>
    /// Событие, указывающее на открытие UI NPC.
    /// </summary>
    NPCOpened,

    /// <summary>
    /// Событие, указывающее на закрытие всех UI.
    /// </summary>
    AllUIClosed
}

/// <summary>
/// Статический класс для трансляции событий из мира ECS в мир MonoBehaviour.
/// Предоставляет централизованный механизм для подписки и вызова глобальных событий состояния UI.
/// </summary>
public static class GameStateEvents
{
    /// <summary>
    /// Событие, которое передает тип UI, команду (открыть/закрыть) и целевую сущность (если есть).
    /// </summary>
    public static event Action<UIStateEvent, bool, Entity> OnUIStateChanged;

    /// <summary>
    /// Вызывает событие OnUIStateChanged.
    /// </summary>
    /// <param name="state">Тип события UI.</param>
    /// <param name="isOpen">Флаг, указывающий, открывается ли UI (true) или закрывается (false).</param>
    /// <param name="target">Целевая сущность, связанная с событием UI (по умолчанию Entity.Null).</param>
    public static void RaiseUIStateChanged(UIStateEvent state, bool isOpen, Entity target = default)
    {
        OnUIStateChanged?.Invoke(state, isOpen, target);
    }
}