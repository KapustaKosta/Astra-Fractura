using UnityEngine;
using Unity.Entities;

/// <summary>
/// Специализированный MonoBehaviour, отвечающий только за одну задачу:
/// управление состоянием блокировки и видимости курсора мыши.
/// Он напрямую читает состояние из мира ECS и принудительно синхронизирует 
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
        
        var gameStateEntity = gameStateQuery.GetSingletonEntity();

        // Определяем, должен ли быть активен режим UI, по наличию тега
        bool isUiModeActive = entityManager.HasComponent<InUIMode>(gameStateEntity);

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