using UnityEngine;
using Unity.Entities;

/// <summary>
/// Упрощенный класс-мост. Его основная задача - предоставлять удобный статический доступ
/// к глобальным действиям, которые можно вызвать из UI, например, запрос на закрытие всех окон.
/// Логика управления состоянием (например, курсором) вынесена в специализированные классы.
/// </summary>
public class GameBridge : MonoBehaviour
{
    /// <summary>
    /// Singleton-экземпляр GameBridge.
    /// </summary>
    public static GameBridge Instance { get; private set; }

    private EntityManager entityManager;

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

    private void Start()
    {
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }
        else
        {
            #if UNITY_EDITOR
            Debug.LogError("[GameBridge] ECS World не найден при вызове Start().");
            #endif
        }
    }

    /// <summary>
    /// Обрабатывает запрос на закрытие всего UI, создавая ECS-запрос.
    /// Этот метод могут вызывать кнопки "Закрыть" из разных UI-панелей.
    /// </summary>
    public void HandleUICloseAction()
    {
        if (entityManager.World == null || !entityManager.World.IsCreated)
        {
            #if UNITY_EDITOR
            Debug.LogError("[GameBridge] Невозможно отправить CloseAllUIRequest: EntityManager не валиден.");
            #endif
            return;
        }

        var entity = entityManager.CreateEntity();
        entityManager.AddComponentData(entity, new CloseAllUIRequest());
    }
}