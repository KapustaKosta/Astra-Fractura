using UnityEngine;
using Unity.Entities;
using Unity.Physics;

public class SettlementClickHandler : MonoBehaviour
{
    public static SettlementClickHandler Instance { get; private set; } // Реализация Singleton

    public float interactionRange = 5f; // Дистанция взаимодействия с поселением

    private EntityManager entityManager;
    private CollisionWorld collisionWorld;

    private void Awake()
    {
        // Убедимся, что существует только один экземпляр
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    private void Start()
    {

    }

    public void HandleRightClick()
    {
        TryInteractWithSettlement();
    }

    private void TryInteractWithSettlement()
    {
        // Создаем луч из позиции камеры
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Получаем индекс слоя "Building"
        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer == -1)
        {
            Debug.LogError("Слой 'Building' не найден. Убедитесь, что он существует в настройках проекта.");
            return;
        }

        // Создаем RaycastInput для ECS
        RaycastInput raycastInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * interactionRange,
            Filter = new CollisionFilter
            {
                BelongsTo = (uint)(1 << buildingLayer), // Проверяем только слой "Building"
                CollidesWith = (uint)(1 << buildingLayer), // Столкновения только с "Building"
                GroupIndex = 0
            }
        };

        // Получаем CollisionWorld из PhysicsWorldSingleton
        EntityQuery query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
        collisionWorld = physicsWorld.CollisionWorld;

        // Выполняем Raycast
        if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit hit))
        {
            // Получаем Entity из RaycastHit
            Entity entity = hit.Entity;

            // Проверяем, есть ли у Entity компонент SettlementComponent
            if (entityManager.HasComponent<SettlementComponent>(entity))
            {
                // Получаем данные компонента SettlementComponent
                var settlement = entityManager.GetComponentData<SettlementComponent>(entity);

                // Логика взаимодействия с поселением
                Debug.Log($"Взаимодействие с поселением. Уровень: {settlement.Level}, Население: {settlement.Population}");

                // Используем SettlementUI для отображения информации
                SettlementUI.Instance.Show(settlement);

                LockPlayerControls(true);

                return;
            }
        }
    }

    private void LockPlayerControls(bool isLocked)
    {
        // Включаем или отключаем курсор
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isLocked;
    }
}
