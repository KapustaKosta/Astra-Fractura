using UnityEngine;
using TMPro;
using Unity.Entities;
using UnityEngine.UI;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Управляет пользовательским интерфейсом NPC, отображая информацию о NPC
/// и предоставляя опции для взаимодействия. Читает состояние напрямую из ECS.
/// </summary>
public class NPCUI : MonoBehaviour
{
    public static NPCUI Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject npcMenu;
    [SerializeField] private TextMeshProUGUI npcText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button hireButton;
    
    [Header("Task Elements")]
    [SerializeField] private Transform resourceNodeListContainer;
    [SerializeField] private GameObject resourceNodeButtonPrefab;
    [SerializeField] private TextMeshProUGUI taskStatusText;

    private Entity currentNPCEntity;
    private EntityManager entityManager;
    private bool isInitialized = false;

    /// <summary>
    /// Инициализирует Singleton-экземпляр и проверяет наличие UI-элементов.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (npcMenu == null || npcText == null || closeButton == null || hireButton == null || 
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
            npcMenu.SetActive(false);
        }
    }
    
    /// <summary>
    /// Каждый кадр проверяет глобальное состояние и решает, должно ли быть открыто окно NPC.
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
        
        var gameState = gameStateQuery.GetSingleton<GameState>();

        bool shouldBeOpen = gameState.CurrentMode == GameMode.UI && gameState.ActiveUIType == UIType.NPC;

        if (npcMenu.activeSelf != shouldBeOpen)
        {
            if (shouldBeOpen)
            {
                Show(gameState.ActiveUITarget);
            }
            else
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
    /// Отображает UI для указанного NPC, адаптируя его под текущее состояние NPC (нанят, занят, свободен).
    /// </summary>
    /// <param name="npcEntity">Сущность NPC для отображения.</param>
    private void Show(Entity npcEntity)
    {
        if (!isInitialized || !entityManager.Exists(npcEntity)) return;
        
        currentNPCEntity = npcEntity;
        NPCComponent npc = entityManager.GetComponentData<NPCComponent>(npcEntity);

        npcText.text = $"Имя: {npc.Name}\nВозраст: {npc.Age}\nНавыки: {npc.Skills}\n" +
                       $"Организованность: {npc.Organizedness}\nЛояльность: {npc.Loyalty}\nТрудолюбие: {npc.Diligence}";
        npcText.gameObject.SetActive(true);
        npcMenu.SetActive(true);

        taskStatusText.gameObject.SetActive(false);
        ClearResourceNodeOptions();

        if (IsHired(npcEntity))
        {
            hireButton.gameObject.SetActive(false);
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
        else
        {
            hireButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Скрывает окно UI NPC.
    /// </summary>
    public void Hide()
    {
        if (!enabled || npcMenu == null) return;
        npcMenu.SetActive(false);
        ClearResourceNodeOptions();
        currentNPCEntity = Entity.Null;
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
    /// Обрабатывает нажатие кнопки "Нанять", проверяя наличие поселения у игрока.
    /// </summary>
    private void OnHireButtonPressed()
    {
        if (!isInitialized || currentNPCEntity == Entity.Null) return;

        var playerSettlementQuery = entityManager.CreateEntityQuery(typeof(PlayerSettlementTag));
        if (playerSettlementQuery.IsEmpty)
        {
            // TODO: Показать игроку уведомление "Сначала постройте главное поселение!"
            return; 
        }
        
        var entity = entityManager.CreateEntity();
        entityManager.AddComponentData(entity, new HireNPCRequest { NPCToHire = currentNPCEntity });

        hireButton.gameObject.SetActive(false);
        ShowResourceNodeOptions();
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
    
    /// <summary>
    /// Проверяет, была ли нанята указанная сущность NPC.
    /// </summary>
    /// <returns>True, если у сущности есть тег NPCHiredTag.</returns>
    private bool IsHired(Entity npcEntity)
    {
        if (!isInitialized || npcEntity == Entity.Null || !entityManager.Exists(npcEntity)) return false;
        return entityManager.HasComponent<NPCHiredTag>(npcEntity);
    }
}