using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using TMPro;

public class PlayerToolController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI harvestText;
    [SerializeField] private ResourceItemMapping resourceItemMapping;

    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    public float resourceHarvestInterval = 1f; // Интервал между добычей ресурсов

    private float lastAttackTime;
    private float lastHarvestTime;
    private bool isHarvesting;
    private bool isInHarvestRange; // Флаг для проверки, находится ли игрок в зоне ресурса

    [SerializeField] private Inventory inventory;

    void Start()
    {
        harvestText.text = "";
        harvestText.gameObject.SetActive(false);
    }

    void Update()
    {
        // Проверяем, зажата ли левая кнопка мыши
        if (Input.GetMouseButton(0))
        {
            TryHarvest();
        }
        else
        {
            ResetHarvestState(); // Сбрасываем состояние добычи, если кнопка отпущена
        }

        // Проверяем, можно ли атаковать
        if (Input.GetMouseButtonDown(0) && Time.time > lastAttackTime + attackCooldown)
        {
            PerformAttack();
            lastAttackTime = Time.time;
        }
    }

    private void TryHarvest()
    {
        // Проверяем, можно ли добывать ресурс
        if (Time.time < lastHarvestTime + resourceHarvestInterval)
        {
            return;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        var resourceLayer = 1 << LayerMask.NameToLayer("Resources");

        var rayInput = new Unity.Physics.RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * attackRange,

            Filter = new CollisionFilter
            {
                BelongsTo = (uint)resourceLayer,
                CollidesWith = (uint)resourceLayer,
                GroupIndex = 0
            }
        };

        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();

        if (physicsWorld.CollisionWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
        {
            // Получаем сущность, с которой произошло столкновение  
            var entity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;

            // Проверяем, есть ли у сущности компонент ResourceNode
            if (entityManager.HasComponent<ResourceNode>(entity))
            {
                // Получаем компонент ResourceNode  
                var resourceNode = entityManager.GetComponentData<ResourceNode>(entity);

                // Логика добычи ресурсов  
                Debug.Log($"Добыча ресурса: {resourceNode.resourceType}");

                Harvest(resourceNode);
                lastHarvestTime = Time.time; // Обновляем время последней добычи
                isHarvesting = true; // Устанавливаем флаг состояния добычи
                isInHarvestRange = true; // Устанавливаем флаг, что игрок в зоне ресурса

                // Обновляем текст на экране
                UpdateHarvestText(resourceNode);
                return;
            }
        }

        // Если луч не попал в ресурс, сбрасываем состояние
        isInHarvestRange = false;
        ResetHarvestState();
    }

    private void PerformAttack()
    {
        // Логика атаки
        Debug.Log("Атака!");
    }

    private void Harvest(ResourceNode resourceNode)
    {
        Item item = resourceItemMapping.GetItemByResourceType(resourceNode.resourceType);

        inventory.Add(item, resourceNode.speedOfCollection);
    }

    private void UpdateHarvestText(ResourceNode resourceNode)
    {
        // Обновляем текст с информацией о добываемом ресурсе
        harvestText.text = $"Добывается: {resourceNode.resourceType}";
        harvestText.gameObject.SetActive(true); // Показываем текст
    }

    private void ResetHarvestState()
    {
        if (!isInHarvestRange || !isHarvesting)
        {
            // Сбрасываем текст и скрываем его
            harvestText.text = "";
            harvestText.gameObject.SetActive(false);
            isHarvesting = false;
        }
    }
}
