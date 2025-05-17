using UnityEngine;
using Unity.Entities;
using Unity.Physics;

public class NPCClickHandler : MonoBehaviour
{
    public static NPCClickHandler Instance { get; private set; } // Реализация Singleton

    public float interactionRange = 5f; // Дистанция взаимодействия с NPC

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
        // Получаем CollisionWorld из PhysicsWorldSingleton
    }

    public void HandleRightClick()
    {
        TryInteractWithNPC();
    }

    private void TryInteractWithNPC()
    {
        // Создаем луч из позиции камеры
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Получаем индекс слоя "NPC"
        int npcLayer = LayerMask.NameToLayer("NPC");
        if (npcLayer == -1)
        {
            Debug.LogError("Слой 'NPC' не найден. Убедитесь, что он существует в настройках проекта.");
            return;
        }

        // Создаем RaycastInput для ECS
        RaycastInput raycastInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * interactionRange,
            Filter = new CollisionFilter
            {
                BelongsTo = (uint)(1 << npcLayer), // Проверяем только слой "NPC"
                CollidesWith = (uint)(1 << npcLayer), // Столкновения только с "NPC"
                GroupIndex = 0
            }
        };

        EntityQuery query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
        collisionWorld = physicsWorld.CollisionWorld;

        // Выполняем Raycast
        if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit hit))
        {
            // Получаем Entity из RaycastHit
            Entity entity = hit.Entity;

            // Проверяем, есть ли у Entity компонент NPCComponent
            if (entityManager.HasComponent<NPCComponent>(entity))
            {
                // Получаем данные компонента NPCComponent
                var npc = entityManager.GetComponentData<NPCComponent>(entity);

                // Логика взаимодействия с NPC
                Debug.Log($"Взаимодействие с NPC. Имя: {npc.Name}, Навыки: {npc.Skills}");

                // Используем NPCUI для отображения информации
                NPCUI.Instance.Show(npc, entity);

                LockPlayerControls(true);
                return;
            }
        }

        // Если луч не попал в NPC, скрываем UI
        NPCUI.Instance.Hide();

        LockPlayerControls(false);
    }

    public void LockPlayerControls(bool isLocked)
    {
        // Включаем или отключаем курсор
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isLocked;
    }
}
