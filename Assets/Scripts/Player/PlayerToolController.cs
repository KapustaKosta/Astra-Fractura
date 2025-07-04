using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using TMPro;

/// <summary>
/// Управляет инструментами игрока, в частности, механикой сбора ресурсов.
/// Отвечает за обнаружение ресурсных узлов, отображение информации о сборе
/// и добавление ресурсов в инвентарь.
/// </summary>
public class PlayerToolController : MonoBehaviour
{
    /// <summary>
    /// Текстовое поле для отображения информации о сборе ресурсов.
    /// </summary>
    [Header("UI and Asset References")]
    [SerializeField] private TextMeshProUGUI harvestText;

    /// <summary>
    /// Ссылка на ScriptableObject, который сопоставляет типы ресурсов с предметами инвентаря.
    /// </summary>
    [SerializeField] private ResourceItemMapping resourceItemMapping;

    /// <summary>
    /// Ссылка на экземпляр Inventory.
    /// </summary>
    [SerializeField] private Inventory inventory;

    /// <summary>
    /// Дальность, на которой игрок может собирать ресурсы.
    /// </summary>
    [Header("Tool Settings")]
    public float attackRange = 2f;

    /// <summary>
    /// Интервал времени между попытками сбора ресурсов.
    /// </summary>
    public float resourceHarvestInterval = 0.5f;

    private float lastHarvestTime;
    private bool isHarvesting;
    private EntityManager entityManager;
    private bool isInitialized = false;

    /// <summary>
    /// Вызывается в первом кадре. Пытается инициализировать необходимые компоненты и ссылки.
    /// </summary>
    void Start() => TryInitialize();

    /// <summary>
    /// Вызывается, когда объект становится неактивным или выключается.
    /// Сбрасывает состояние сбора ресурсов, если оно активно.
    /// </summary>
    void OnDisable()
    {
        if (isHarvesting) ResetHarvestState();
    }
    
    /// <summary>
    /// Пытается инициализировать EntityManager и другие необходимые ссылки.
    /// </summary>
    void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }
        else
        {
            return;
        }

        if (harvestText != null)
        {
            harvestText.text = "";
            harvestText.gameObject.SetActive(false);
        }

        if (inventory == null) inventory = Inventory.Instance;

        isInitialized = entityManager.World.IsCreated && inventory != null;
    }

    /// <summary>
    /// Вызывается один раз за кадр. Обрабатывает логику сбора ресурсов,
    /// реагируя на ввод игрока и текущее состояние игры.
    /// </summary>
    void Update()
    {
        if (!isInitialized)
        {
            TryInitialize();
            if (!isInitialized) return;
        }

        var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameState = gameStateQuery.GetSingleton<GameState>();

        bool canPerformActions = gameState.CurrentMode == GameMode.Default;

        if (!canPerformActions)
        {
            if (isHarvesting) ResetHarvestState();
            return;
        }

        if (Input.GetMouseButton(0))
            TryHarvest();
        else if (isHarvesting)
            ResetHarvestState();
    }

    /// <summary>
    /// Пытается выполнить сбор ресурсов. Выполняет трассировку луча для обнаружения ресурсных узлов
    /// и инициирует процесс сбора, если найден подходящий узел.
    /// </summary>
    private void TryHarvest()
    {
        if (Time.time < lastHarvestTime + resourceHarvestInterval) return;
        if (Camera.main == null) { ResetHarvestState(); return; }
        
        var physicsWorldQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        if (physicsWorldQuery.IsEmpty)
        {
            ResetHarvestState();
            return;
        }
        var physicsWorldSingleton = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>();

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        var resourceLayerIndex = LayerMask.NameToLayer("Resources");
        if (resourceLayerIndex == -1) { ResetHarvestState(); return; }

        uint resourceLayerMask = (uint)(1 << resourceLayerIndex);
        var rayInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * attackRange,
            Filter = new CollisionFilter
                { BelongsTo = ~0u, CollidesWith = resourceLayerMask, GroupIndex = 0 }
        };

        bool hitResourceThisFrame = false;
        if (physicsWorldSingleton.CollisionWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
        {
            Entity entity = hit.Entity;
            if (entityManager.Exists(entity) && entityManager.HasComponent<ResourceNode>(entity))
            {
                var resourceNode = entityManager.GetComponentData<ResourceNode>(entity);
                Harvest(resourceNode);
                lastHarvestTime = Time.time;
                if (!isHarvesting)
                {
                    isHarvesting = true;
                    UpdateHarvestText(resourceNode);
                }
                hitResourceThisFrame = true;
            }
        }

        if (!hitResourceThisFrame && isHarvesting)
        {
            ResetHarvestState();
        }
    }

    /// <summary>
    /// Выполняет сбор ресурсов из указанного ресурсного узла и добавляет их в инвентарь.
    /// </summary>
    /// <param name="resourceNode">Данные ресурсного узла, из которого происходит сбор.</param>
    private void Harvest(ResourceNode resourceNode)
    {
        if (resourceItemMapping == null || inventory == null) return;
        Item itemToGive = resourceItemMapping.GetItemByResourceType(resourceNode.resourceType);
        if (itemToGive != null)
        {
            inventory.Add(itemToGive, resourceNode.speedOfCollection);
        }
    }

    /// <summary>
    /// Обновляет текстовую информацию о текущем собираемом ресурсе в UI.
    /// </summary>
    /// <param name="resourceNode">Данные ресурсного узла, который в данный момент собирается.</param>
    private void UpdateHarvestText(ResourceNode resourceNode)
    {
        if (harvestText != null)
        {
            Item item = resourceItemMapping.GetItemByResourceType(resourceNode.resourceType);
            string textToDisplay =
                $"Добывается: {(item != null ? item.itemName : resourceNode.resourceType.ToString())}";
            harvestText.text = textToDisplay;
            harvestText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Сбрасывает состояние сбора ресурсов, скрывая соответствующий текст в UI.
    /// </summary>
    private void ResetHarvestState()
    {
        if (isHarvesting)
        {
            if (harvestText != null)
            {
                harvestText.text = "";
                harvestText.gameObject.SetActive(false);
            }
            isHarvesting = false;
        }
    }
}