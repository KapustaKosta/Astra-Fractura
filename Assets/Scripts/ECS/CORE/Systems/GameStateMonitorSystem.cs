// --- Файл: GameStateMonitorSystem.cs ---

using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система-наблюдатель. Отслеживает изменения в синглтоне GameState
/// и транслирует их в статические C# события для мира MonoBehaviour.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GameStateMonitorSystem : SystemBase
{
    // Инициализируем lastTrackedMode значением, которого никогда не будет в GameMode,
    // чтобы гарантированно сработать в первый раз.
    private GameMode lastTrackedMode = (GameMode)(-1);
    private UIType lastTrackedUIType = (UIType)(-1);
    private bool isFirstUpdate = true; // Добавляем флаг первого обновления

    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var gameState = SystemAPI.GetSingleton<GameState>();

        // Если состояние не изменилось с прошлого кадра, и это не первый кадр, выходим.
        if (!isFirstUpdate && gameState.CurrentMode == lastTrackedMode && gameState.ActiveUIType == lastTrackedUIType)
        {
            return;
        }

        GameMode previousMode = lastTrackedMode;
        
        // Обновляем отслеживаемые значения
        lastTrackedMode = gameState.CurrentMode;
        lastTrackedUIType = gameState.ActiveUIType;
        
        // Логика для первого запуска
        if (isFirstUpdate)
        {
            Debug.Log("<color=cyan>[GameStateMonitor]</color> Первый запуск! Текущий режим: " + gameState.CurrentMode); // ЛОГ 1
            isFirstUpdate = false;
            
            // Определяем, должно ли UI быть открыто при старте
            bool isUiOpen = gameState.CurrentMode == GameMode.UI;

            if (isUiOpen)
            {
                // Если игра каким-то чудом начнется в UI режиме, обработаем и это.
                Debug.Log("<color=cyan>[GameStateMonitor]</color> Отправляю событие " + GetEventFromUIType(gameState.ActiveUIType) + ", isOpen=true");
                GameStateEvents.RaiseUIStateChanged(GetEventFromUIType(gameState.ActiveUIType), true, gameState.ActiveUITarget);
            }
            else
            {
                // Стандартный запуск в режиме Default или Building
                Debug.Log("<color=cyan>[GameStateMonitor]</color> Отправляю событие AllUIClosed, isOpen=false"); // ЛОГ 2
                GameStateEvents.RaiseUIStateChanged(UIStateEvent.AllUIClosed, false, default);
            }
            
            // Вне зависимости от того, какое событие отправили, логика для первого кадра завершена.
            // Но мы не используем `return` здесь, чтобы позволить следующему блоку `if`
            // обработать возможную смену состояния, если она произойдет в том же кадре.
        }

        // Старая логика для последующих изменений состояния (сравниваем с `previousMode`)
        if (gameState.CurrentMode == GameMode.UI && previousMode != GameMode.UI)
        {
            Debug.Log($"<color=cyan>[GameStateMonitor]</color> Состояние изменилось на UI. Отправляю событие {GetEventFromUIType(gameState.ActiveUIType)}, isOpen=true");
            GameStateEvents.RaiseUIStateChanged(GetEventFromUIType(gameState.ActiveUIType), true, gameState.ActiveUITarget);
        }
        else if (previousMode == GameMode.UI && gameState.CurrentMode != GameMode.UI)
        {
            Debug.Log($"<color=cyan>[GameStateMonitor]</color> Состояние изменилось с UI на {gameState.CurrentMode}. Отправляю событие AllUIClosed, isOpen=false");
            GameStateEvents.RaiseUIStateChanged(UIStateEvent.AllUIClosed, false, default);
        }
    }
    
    // Вспомогательный метод, чтобы не дублировать код
    private UIStateEvent GetEventFromUIType(UIType type)
    {
        switch(type)
        {
            case UIType.Inventory: return UIStateEvent.InventoryToggled;
            case UIType.NPC: return UIStateEvent.NPCOpened;
            case UIType.Settlement: return UIStateEvent.SettlementOpened;
            default: return UIStateEvent.AllUIClosed; // Безопасное значение по умолчанию
        }
    }
}