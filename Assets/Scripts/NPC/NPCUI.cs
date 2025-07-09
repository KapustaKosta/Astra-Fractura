using UnityEngine;
using TMPro;
using Unity.Entities;
using UnityEngine.UI;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Управляет пользовательским интерфейсом NPC, отображая информацию о NPC
/// и предоставляя опции для взаимодействия, такие как найм или назначение задач.
/// </summary>
public class NPCUI : MonoBehaviour
{
    public static NPCUI Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject npcMenu;
    [SerializeField] private TextMeshProUGUI npcText;
    [SerializeField] private Button closeButton;

    [Header("Interaction Buttons")]
    [SerializeField] private Button hireButton;
    [Tooltip("Кнопка для открытия окна торговли с этим NPC.")]
    [SerializeField] private Button tradeButton;
    
    [Header("Task Elements")]
    [SerializeField] private Transform resourceNodeListContainer;
    [SerializeField] private GameObject resourceNodeButtonPrefab;
    [SerializeField] private TextMeshProUGUI taskStatusText;

    private Entity currentNPCEntity;
    private EntityManager entityManager;
    private bool isInitialized = false;
    
    /// <summary>
    /// Хранит состояние найма NPC из предыдущего кадра обновления UI.
    /// Используется для определения необходимости перерисовки интерфейса при изменении статуса найма.
    /// </summary>
    private bool wasHiredState = false; 

    /// <summary>
    /// Инициализирует Singleton-экземпляр и проверяет наличие UI-элементов.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (npcMenu == null || npcText == null || closeButton == null || hireButton == null || 
            tradeButton == null || 
            resourceNodeListContainer == null || resourceNodeButtonPrefab == null || taskStatusText == null) 
        { 
            enabled = false; 
        }
    }
    
    /// <summary>
    /// Инициализирует EntityManager и подписывается на события кнопок.
    /// </summary>
    private void Start()
    {
        TryInitialize();
        if(isInitialized)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
            hireButton.onClick.AddListener(OnHireButtonPressed);
            tradeButton.onClick.AddListener(OnTradeButtonPressed);
            npcMenu.SetActive(false);
        }
    }
    
    /// <summary>
    /// Каждый кадр проверяет глобальное состояние, решает, должно ли быть открыто окно NPC,
    /// и обновляет его содержимое, если состояние целевого NPC изменилось.
    /// </summary>
    void Update()
    {
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }

        var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();

        // Проверяем, должен ли наш UI быть открыт, на основе данных из ECS
        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.NPC;

        
        if (shouldBeOpen)
        {
            var targetEntity = entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUITarget;
            
            if (!entityManager.Exists(targetEntity))
            {
                if (npcMenu.activeSelf) Hide();
                return;
            }

            bool isHiredNow = entityManager.HasComponent<NPCHiredTag>(targetEntity);
            
            // Обновляем UI, если он открывается впервые, сменился целевой NPC,
            // или изменился его статус найма.
            if (!npcMenu.activeSelf || currentNPCEntity != targetEntity || wasHiredState != isHiredNow)
            {
                Show(targetEntity);
            }
        }
        else
        {
            if (npcMenu.activeSelf)
            {
                Hide();
            }
        }
    }

    /// <summary>
    /// Пытается инициализировать EntityManager, если он еще не был инициализирован.
    /// </summary>
    private void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            isInitialized = true;
        }
    }

    /// <summary>
    /// Отображает и полностью перестраивает UI для указанного NPC.
    /// Вызывается только при необходимости, а не каждый кадр, для оптимизации.
    /// </summary>
    /// <param name="npcEntity">Сущность NPC для отображения.</param>
    private void Show(Entity npcEntity)
    {
        if (!isInitialized || !entityManager.Exists(npcEntity)) 
        {
            Hide();
            return;
        }
        
        currentNPCEntity = npcEntity;
        npcMenu.SetActive(true);

        NPCComponent npc = entityManager.GetComponentData<NPCComponent>(npcEntity);

        npcText.text = $"Имя: {npc.Name}\nВозраст: {npc.Age}\nНавыки: {npc.Skills}\n" +
                       $"Организованность: {npc.Organizedness}\nЛояльность: {npc.Loyalty}\nТрудолюбие: {npc.Diligence}";
        
        taskStatusText.gameObject.SetActive(false);
        ClearResourceNodeOptions();

        bool hired = entityManager.HasComponent<NPCHiredTag>(npcEntity);
        wasHiredState = hired;
        
        bool hasInventory = entityManager.HasComponent<HasInventoryTag>(npcEntity);

        hireButton.gameObject.SetActive(!hired);
        // Кнопка обмена видна только если NPC нанят и у него есть инвентарь
        tradeButton.gameObject.SetActive(hired && hasInventory);

        if (hired)
        {
            if (npc.Target != Entity.Null)
            {
                taskStatusText.gameObject.SetActive(true);
                if (entityManager.HasComponent<ResourceNode>(npc.Target))
                {
                    var resourceNode = entityManager.GetComponentData<ResourceNode>(npc.Target);
                    taskStatusText.text = $"Задача: Добыча ({resourceNode.resourceType})";
                }
                else
                {
                    taskStatusText.text = "Задача: Выполняется...";
                }
            }
            else
            {
                ShowResourceNodeOptions();
            }
        }
    }

    /// <summary>
    /// Скрывает окно UI NPC.
    /// </summary>
    public void Hide()
    {
        if (!enabled || npcMenu == null || !npcMenu.activeSelf) return;
        npcMenu.SetActive(false);
        ClearResourceNodeOptions();
        currentNPCEntity = Entity.Null;
        wasHiredState = false;
    }

    /// <summary>
    /// Обрабатывает нажатие кнопки "Закрыть", отправляя запрос в ECS.
    /// </summary>
    private void OnCloseButtonPressed()
    {
        if (!isInitialized) return;
        GameBridge.Instance?.HandleUICloseAction();
    }
    
    /// <summary>
    /// Обрабатывает нажатие кнопки "Нанять", создавая ECS-запрос на найм.
    /// </summary>
    private void OnHireButtonPressed()
    {
        if (!isInitialized || currentNPCEntity == Entity.Null) return;

        var playerSettlementQuery = entityManager.CreateEntityQuery(typeof(PlayerSettlementTag));
        if (playerSettlementQuery.IsEmpty)
        {
            return; 
        }
        
        var entity = entityManager.CreateEntity();
        entityManager.AddComponentData(entity, new HireNPCRequest { NPCToHire = currentNPCEntity });
    }

    /// <summary>
    /// Обрабатывает нажатие кнопки "Обмен", создавая запрос на открытие TradeUI.
    /// </summary>
    private void OnTradeButtonPressed()
    {
        if (!isInitialized || currentNPCEntity == Entity.Null) return;

        // Создаем запрос на открытие окна торговли, передавая текущего NPC как цель
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new OpenTradeUIRequest { Target = currentNPCEntity });
    }

    /// <summary>
    /// Отображает список доступных ресурсов для назначения задачи NPC.
    /// </summary>
    private void ShowResourceNodeOptions()
    {
        if (!isInitialized || resourceNodeListContainer == null || resourceNodeButtonPrefab == null) return;
        ClearResourceNodeOptions();
        resourceNodeListContainer.gameObject.SetActive(true);
        EntityQuery query = entityManager.CreateEntityQuery(typeof(ResourceNode), typeof(LocalTransform));
        using var resourceNodeEntities = query.ToEntityArray(Allocator.Temp);

        foreach (var entity in resourceNodeEntities)
        {
            GameObject buttonObject = Instantiate(resourceNodeButtonPrefab, resourceNodeListContainer.transform);
            Button button = buttonObject.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText != null && entityManager.HasComponent<ResourceNode>(entity))
            {
                ResourceNode resourceNodeData = entityManager.GetComponentData<ResourceNode>(entity);
                buttonText.text = $"Добывать: {resourceNodeData.resourceType}";
            }
            if (button != null)
            {
                Entity capturedEntity = entity;
                button.onClick.AddListener(() => AssignNPCToResourceNode(capturedEntity));
            }
        }
    }

    /// <summary>
    /// Удаляет все дочерние объекты из контейнера списка ресурсов.
    /// </summary>
    private void ClearResourceNodeOptions()
    {
        if (resourceNodeListContainer == null) return;
        foreach (Transform child in resourceNodeListContainer.transform) { Destroy(child.gameObject); }
        resourceNodeListContainer.gameObject.SetActive(false);
    }

    /// <summary>
    /// Назначает текущего NPC на указанный ресурсный узел и закрывает UI.
    /// </summary>
    /// <param name="resourceNodeEntity">Сущность целевого ресурса.</param>
    private void AssignNPCToResourceNode(Entity resourceNodeEntity)
    {
        if (isInitialized && currentNPCEntity != Entity.Null && resourceNodeEntity != Entity.Null)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new AssignNPCToTaskRequest
            {
                NPC = currentNPCEntity,
                TargetResourceNode = resourceNodeEntity
            });
            OnCloseButtonPressed();
        }
    }
}