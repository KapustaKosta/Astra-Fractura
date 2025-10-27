using UnityEngine;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.UI;
using System.Text; 

/// <summary>
/// Вспомогательный класс-расширение для удобной работы с ResourceItemMapping.
/// </summary>
public static class ResourceMappingHelper
{
    public static bool TryGetItemID(this ResourceItemMapping mapping, ResourceCollectionType type, out int itemID)
    {
        itemID = 0;
        if (mapping == null) return false;
        foreach (var resourceItem in mapping.resourceItems)
        {
            if (resourceItem.resourceType == type && resourceItem.item != null)
            {
                itemID = resourceItem.item.itemID;
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Управляет UI-окном для взаимодействия с NPC, а также панелью уведомлений.
/// </summary>
public class NPCUI : MonoBehaviour
{
    public static NPCUI Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject npcMenu;
    [SerializeField] private TextMeshProUGUI npcText;
    [SerializeField] private TextMeshProUGUI taskStatusText;
    [SerializeField] private Button closeButton;

    [Header("Interaction Buttons")]
    [SerializeField] private Button hireButton;
    [Tooltip("Кнопка для открытия окна торговли с этим NPC.")]
    [SerializeField] private Button tradeButton;

    [Header("Task Elements")]
    [SerializeField] private Transform resourceNodeListContainer;
    [SerializeField] private GameObject resourceNodeButtonPrefab;

    [Header("Notifications")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float notificationDisplayTime = 4f;
    private float notificationTimer;

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
        if (isInitialized)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
            hireButton.onClick.AddListener(OnHireButtonPressed);
            tradeButton.onClick.AddListener(OnTradeButtonPressed);
            npcMenu.SetActive(false);
            if (notificationPanel != null) notificationPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Показывает меню для указанной сущности NPC.
    /// </summary>
    public void Show(Entity npcEntity)
    {
        currentNPCEntity = npcEntity;
        npcMenu.SetActive(true);
    }

    /// <summary>
    /// Скрывает меню NPC.
    /// </summary>
    public void Hide()
    {
        npcMenu.SetActive(false);
        currentNPCEntity = Entity.Null;
    }

    private void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            isInitialized = true;
        }
    }

    private void LateUpdate()
    {
        if (!isInitialized) { TryInitialize(); return; }

        var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();
        if (!entityManager.HasComponent<UIState>(gameStateEntity))
        {
            // Если компонента нет, значит UI не должен быть активен.
            if (npcMenu.activeSelf) Hide();
            return;
        }

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

            if (!npcMenu.activeSelf || currentNPCEntity != targetEntity)
            {
                Show(targetEntity);
            }
            RefreshUI(targetEntity);
        }
        else
        {
            if (npcMenu.activeSelf) Hide();
        }

        HandleNotifications();
    }

    private void RefreshUI(Entity npcEntity)
    {
        if (!entityManager.Exists(npcEntity)) return;

        NPCComponent npcData = entityManager.GetComponentData<NPCComponent>(npcEntity);

        
        var sb = new StringBuilder();
        sb.AppendLine($"Имя: {npcData.Name}");
        sb.AppendLine($"Возраст: {npcData.Age}");

        // Добавляем отображение голода и усталости
        if (entityManager.HasComponent<NpcVitalsComponent>(npcEntity) && 
            entityManager.HasComponent<NpcVitalsConfig>(npcEntity))
        {
            var vitals = entityManager.GetComponentData<NpcVitalsComponent>(npcEntity);
            var config = entityManager.GetComponentData<NpcVitalsConfig>(npcEntity);
            sb.AppendLine($"Голод: {vitals.CurrentHunger:F0} / {config.MaxHunger:F0}");
            sb.AppendLine($"Усталость: {vitals.CurrentFatigue:F0} / {config.MaxFatigue:F0}");
        }

        if (entityManager.HasComponent<NPCWorkForce>(npcEntity))
        {
            var workForce = entityManager.GetComponentData<NPCWorkForce>(npcEntity);
            sb.AppendLine($"Эффективность: {workForce.CurrentHammerPool:F0} / {workForce.MaxHammerPool:F0}");
        }

        sb.AppendLine($"Навыки: {npcData.Skills}");
        sb.AppendLine($"Организованность: {npcData.Organizedness}");
        sb.AppendLine($"Лояльность: {npcData.Loyalty}");
        sb.AppendLine($"Трудолюбие: {npcData.Diligence}");

        npcText.text = sb.ToString();

        bool isHired = entityManager.HasComponent<NPCHiredTag>(npcEntity);
        hireButton.gameObject.SetActive(!isHired);
        tradeButton.gameObject.SetActive(isHired && entityManager.HasComponent<HasInventoryTag>(npcEntity));

        if (!isHired)
        {
            taskStatusText.text = "Статус: Не нанят";
            ClearResourceNodeOptions();
        }
        else
        {
            ShowResourceNodeOptions();

            if (entityManager.HasComponent<ActiveGoal>(npcEntity))
            {
                var goal = entityManager.GetComponentData<ActiveGoal>(npcEntity);
                string targetName = GetEntityName(goal.Target);

                if (entityManager.HasComponent<WantsToHarvestTag>(npcEntity))
                {
                    taskStatusText.text = $"Статус: Добывает ({targetName})";
                }
                else if (entityManager.HasComponent<MoveToRequest>(npcEntity))
                {
                    taskStatusText.text = $"Статус: Идет к цели ({goal.Type}: {targetName})";
                }
                else
                {
                    taskStatusText.text = $"Статус: Выполняет задачу ({goal.Type})";
                }
            }
            else
            {
                taskStatusText.text = "Статус: Простаивает";
            }
        }
    }

    private void HandleNotifications()
    {
        if (!isInitialized || notificationPanel == null) return;

        if (notificationPanel.activeSelf)
        {
            notificationTimer -= Time.deltaTime;
            if (notificationTimer <= 0)
            {
                notificationPanel.SetActive(false);
            }
        }

        using var query = entityManager.CreateEntityQuery(typeof(UINotificationRequest));
        if (query.IsEmpty) return;

        using var requests = query.ToEntityArray(Allocator.Temp);

        var requestData = entityManager.GetComponentData<UINotificationRequest>(requests[requests.Length - 1]);

        notificationText.text = requestData.Message.ToString();
        notificationPanel.SetActive(true);
        notificationTimer = notificationDisplayTime;

        entityManager.DestroyEntity(requests);
    }

    private void OnCloseButtonPressed()
    {
        if (!isInitialized) return;
        GameBridge.Instance?.HandleUICloseAction();
    }

    private void OnHireButtonPressed()
    {
        if (currentNPCEntity == Entity.Null) return;
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new HireNPCRequest { NPCToHire = currentNPCEntity });
    }

    private void OnTradeButtonPressed()
    {
        if (currentNPCEntity == Entity.Null) return;
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new OpenTradeUIRequest { Target = currentNPCEntity });
    }

    private void ShowResourceNodeOptions()
    {
        if (!isInitialized || resourceNodeListContainer == null || resourceNodeButtonPrefab == null) return;
        if (resourceNodeListContainer.gameObject.activeSelf && resourceNodeListContainer.transform.childCount > 0) return;

        foreach (Transform child in resourceNodeListContainer.transform) { Destroy(child.gameObject); }

        resourceNodeListContainer.gameObject.SetActive(true);

        EntityQuery query = entityManager.CreateEntityQuery(typeof(ResourceNode));
        using var resourceNodeEntities = query.ToEntityArray(Allocator.Temp);

        foreach (var entity in resourceNodeEntities)
        {
            GameObject buttonObject = Instantiate(resourceNodeButtonPrefab, resourceNodeListContainer.transform);
            Button button = buttonObject.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>();

            var resourceNodeData = entityManager.GetComponentData<ResourceNode>(entity);
            buttonText.text = $"Добывать: {resourceNodeData.resourceType}";

            Entity capturedEntity = entity;
            button.onClick.AddListener(() => AssignNPCToResourceNode(capturedEntity));
        }
    }

    private void ClearResourceNodeOptions()
    {
        if (resourceNodeListContainer == null || !resourceNodeListContainer.gameObject.activeSelf) return;

        foreach (Transform child in resourceNodeListContainer.transform)
        {
            Destroy(child.gameObject);
        }
        resourceNodeListContainer.gameObject.SetActive(false);
    }

    private void AssignNPCToResourceNode(Entity resourceNodeEntity)
    {
        if (!isInitialized || currentNPCEntity == Entity.Null) return;

        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new PlayerAssignHarvestRequest
        {
            TargetNPC = currentNPCEntity,
            TargetResourceNode = resourceNodeEntity
        });
    }

    private string GetEntityName(Entity entity)
    {
        if (entityManager.Exists(entity) && entityManager.HasComponent<ResourceNode>(entity))
        {
            return entityManager.GetComponentData<ResourceNode>(entity).resourceType.ToString();
        }
        return "цель";
    }
}