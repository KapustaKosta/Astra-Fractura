// --- Файл: GameBridge.cs ---

using UnityEngine;
using Unity.Entities;
using System;

/// <summary>
/// Класс-мост между MonoBehaviour-миром и ECS-миром.
/// Отвечает за глобальные настройки игры, такие как блокировка курсора,
/// и трансляцию общих UI-событий в ECS-запросы. Является Singleton-классом.
/// </summary>
public class GameBridge : MonoBehaviour
{
    /// <summary>
    /// Singleton-экземпляр GameBridge.
    /// </summary>
    public static GameBridge Instance { get; private set; }

    private EntityManager entityManager;

    /// <summary>
    /// Вызывается при загрузке скрипта. Инициализирует Singleton-экземпляр.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Вызывается в первом кадре. Инициализирует EntityManager, подписывается на события
    /// и немедленно синхронизирует состояние курсора с текущим состоянием игры.
    /// </summary>
    private void Start()
    {
        Debug.Log("<color=green>[GameBridge]</color> Start() вызван."); // ЛОГ 3
        
        GameStateEvents.OnUIStateChanged += HandleUIStateChange;

        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var gameState = gameStateQuery.GetSingleton<GameState>();
                Debug.Log($"<color=green>[GameBridge]</color> Немедленная синхронизация. Текущий режим в ECS: {gameState.CurrentMode}");
                
                bool isUiOpen = gameState.CurrentMode == GameMode.UI;
                HandleUIStateChange(UIStateEvent.AllUIClosed, isUiOpen, gameState.ActiveUITarget); 
            }
            else
            {
                Debug.LogWarning("[GameBridge] GameState еще не существует при вызове Start(). Ожидаем события...");
            }
        }
        else
        {
            Debug.LogError("[GameBridge] ECS World не найден при вызове Start().");
        }
    }

    /// <summary>
    /// Вызывается при уничтожении объекта. Отписывается от событий для предотвращения утечек памяти.
    /// </summary>
    private void OnDestroy()
    {
        if(Instance == this)
        {
            GameStateEvents.OnUIStateChanged -= HandleUIStateChange;
        }
    }

    /// <summary>
    /// Обрабатывает изменение состояния UI, полученное от GameStateEvents.
    /// Блокирует или разблокирует управление игроком в зависимости от состояния UI.
    /// </summary>
    private void HandleUIStateChange(UIStateEvent uiEvent, bool isOpen, Entity target)
    {
        Debug.Log($"<color=green>[GameBridge]</color> ПОЛУЧИЛ СОБЫТИЕ OnUIStateChanged! Event: {uiEvent}, isOpen: {isOpen}"); // ЛОГ 4
        LockPlayerControls(isOpen);
    }
    
    /// <summary>
    /// Устанавливает состояние блокировки управления игроком и видимости курсора.
    /// </summary>
    public void LockPlayerControls(bool isUiMode)
    {
        CursorLockMode targetLockMode = isUiMode ? CursorLockMode.None : CursorLockMode.Locked;
        Debug.Log($"<color=yellow>[GameBridge.LockPlayerControls]</color> Попытка установить LockState: {targetLockMode}. Текущий LockState: {Cursor.lockState}"); // ЛОГ 5

        if (Cursor.lockState == targetLockMode && Cursor.visible == isUiMode)
        {
            Debug.Log("<color=yellow>[GameBridge.LockPlayerControls]</color> Состояние курсора уже правильное. Выход.");
            return;
        }

        Cursor.lockState = targetLockMode;
        Cursor.visible = isUiMode;
        Debug.Log($"<color=yellow>[GameBridge.LockPlayerControls]</color> СОСТОЯНИЕ УСТАНОВЛЕНО. LockState: {Cursor.lockState}, Visible: {Cursor.visible}"); // ЛОГ 6
    }

    /// <summary>
    /// Обрабатывает запрос на закрытие всего UI, создавая ECS-запрос.
    /// </summary>
    public void HandleUICloseAction()
    {
        if (entityManager.World == null || !entityManager.World.IsCreated)
        {
            Debug.LogError("[GameBridge] Невозможно отправить CloseAllUIRequest: EntityManager не валиден.");
            return;
        }
        
        Debug.Log("[GameBridge] Отправляю запрос CloseAllUIRequest в ECS.");
        var entity = entityManager.CreateEntity();
        entityManager.AddComponentData(entity, new CloseAllUIRequest());
    }
}