using UnityEngine;
using Unity.Entities;

/// <summary>
/// Специализированный MonoBehaviour, отвечающий только за одну задачу:
/// управление состоянием блокировки и видимости курсора мыши.
/// Он напрямую читает GameState из мира ECS и принудительно синхронизирует
/// состояние курсора каждый кадр для максимальной надежности.
/// </summary>
public class CursorLockManager : MonoBehaviour
{
    private EntityManager entityManager;
    private bool isInitialized = false;

    void Start()
    {
        // Попытка инициализации при старте
        TryInitialize();
    }

    void Update()
    {
        // Если инициализация не удалась, пытаемся снова каждый кадр
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }

        var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;

        // Получаем единственный источник правды о состоянии игры
        var gameState = gameStateQuery.GetSingleton<GameState>();

        // Определяем, должен ли быть активен режим UI
        bool isUiModeActive = gameState.CurrentMode == GameMode.UI;

        // Применяем нужное состояние к курсору
        ForceSetCursorState(isUiModeActive);
    }

    private void TryInitialize()
    {
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            isInitialized = true;
        }
    }

    /// <summary>
    /// Принудительно устанавливает состояние блокировки и видимости курсора.
    /// Этот метод не проверяет текущее состояние, а напрямую задает его,
    /// что делает его более устойчивым к внешним изменениям.
    /// </summary>
    private void ForceSetCursorState(bool isUiMode)
    {
        if (isUiMode)
        {
            // Если мы в режиме UI: курсор разблокирован и видим
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Если мы в игровом режиме: курсор заблокирован и скрыт
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}