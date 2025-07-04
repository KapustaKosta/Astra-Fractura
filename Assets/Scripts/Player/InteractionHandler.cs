using UnityEngine;
using Unity.Entities;
using Unity.Physics;

/// <summary>
/// Обработчик взаимодействия. Использует трассировку лучей для определения интерактивных сущностей
/// в мире ECS, на которые смотрит игрок.
/// </summary>
public class InteractionHandler : MonoBehaviour
{
    /// <summary>
    /// Дальность взаимодействия для трассировки лучей.
    /// </summary>
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 5f;

    /// <summary>
    /// Маска слоев, с которыми возможно взаимодействие.
    /// </summary>
    [SerializeField] private LayerMask interactiveLayers;

    private EntityManager entityManager;
    private bool isInitialized = false;

    /// <summary>
    /// Вызывается в первом кадре. Инициализирует EntityManager.
    /// </summary>
    private void Start()
    {
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            isInitialized = true;
        }
        else
        {
            // Debug.LogError("InteractionHandler не смог инициализироваться.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Выполняет трассировку луча от позиции мыши и возвращает сущность, с которой произошло взаимодействие.
    /// </summary>
    /// <returns>Сущность, с которой произошло взаимодействие, или Entity.Null, если взаимодействие не найдено.</returns>
    public Entity GetInteractedEntity()
    {
        if (!isInitialized || Camera.main == null) return Entity.Null;

        var physicsWorldQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        if (physicsWorldQuery.IsEmpty) return Entity.Null;
        var physicsWorld = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>();

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        var ecsRayInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * interactionRange,
            Filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = (uint)interactiveLayers.value,
                GroupIndex = 0
            }
        };

        if (physicsWorld.CollisionWorld.CastRay(ecsRayInput, out Unity.Physics.RaycastHit ecsHit))
        {
            return ecsHit.Entity;
        }
        
        return Entity.Null;
    }
}