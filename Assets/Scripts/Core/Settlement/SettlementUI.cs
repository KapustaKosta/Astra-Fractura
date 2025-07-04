using UnityEngine;
using Unity.Entities;
using TMPro;
using UnityEngine.UI;
using Unity.Collections;

/// <summary>
/// Управляет пользовательским интерфейсом Поселения, отображая его статистику
/// и список нанятых NPC с их текущим статусом.
/// </summary>
public class SettlementUI : MonoBehaviour
{
    public static SettlementUI Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private TextMeshProUGUI settlementNameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button closeButton;

    [Header("NPC List Elements")]
    [Tooltip("Контейнер (объект Content из ScrollView), куда будут добавляться элементы списка NPC.")]
    [SerializeField] private GameObject npcListContainer;
    
    [Tooltip("Префаб элемента списка для одного NPC (должен содержать Button и TextMeshPro).")]
    [SerializeField] private GameObject npcListItemPrefab;
    
    private bool isInitialized = false;
    private EntityManager entityManager;

    /// <summary>
    /// Инициализирует Singleton-экземпляр и проверяет наличие UI-элементов.
    /// </summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (uiPanel == null || settlementNameText == null || statsText == null || closeButton == null ||
            npcListContainer == null || npcListItemPrefab == null)
        {
            Debug.LogError("[SettlementUI] Не все UI элементы назначены в инспекторе! Компонент будет отключен.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Инициализирует EntityManager и подписывается на события.
    /// </summary>
    private void Start()
    {
        if (!enabled) return;

        TryInitialize();
        if (isInitialized)
        {
            GameStateEvents.OnUIStateChanged += HandleUIStateChange;
            closeButton.onClick.AddListener(OnCloseButtonPressed);
            uiPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Отписывается от событий при уничтожении объекта.
    /// </summary>
    private void OnDestroy()
    {
        if (isInitialized && Instance == this)
        {
            GameStateEvents.OnUIStateChanged -= HandleUIStateChange;
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
    /// Обрабатывает события изменения состояния UI, открывая или закрывая окно поселения.
    /// </summary>
    private void HandleUIStateChange(UIStateEvent uiEvent, bool shouldBeOpen, Entity target)
    {
        if (!isInitialized) return;

        if (uiEvent == UIStateEvent.SettlementOpened && shouldBeOpen)
        {
            if (entityManager.Exists(target) && entityManager.HasComponent<SettlementComponent>(target))
            {
                var settlementData = entityManager.GetComponentData<SettlementComponent>(target);
                Show(settlementData);
            }
        }
        else if (uiEvent == UIStateEvent.AllUIClosed)
        {
            Hide();
        }
    }

    /// <summary>
    /// Отображает окно UI поселения с актуальными данными.
    /// </summary>
    private void Show(SettlementComponent settlement)
    {
        if (!enabled) return;
        settlementNameText.text = settlement.Name.ToString();
        statsText.text = $"Уровень: {settlement.Level}\nНаселение: {settlement.Population}";
        
        UpdateNPCList(in settlement.NPCs);
        uiPanel.SetActive(true);
    }

    /// <summary>
    /// Скрывает окно UI поселения.
    /// </summary>
    private void Hide()
    {
        if (!enabled || uiPanel == null) return;
        uiPanel.SetActive(false);
    }

    /// <summary>
    /// Обрабатывает нажатие кнопки "Закрыть", отправляя запрос в ECS.
    /// </summary>
    private void OnCloseButtonPressed()
    {
        GameBridge.Instance?.HandleUICloseAction();
    }

    /// <summary>
    /// Очищает и заново заполняет UI-список нанятыми NPC с указанием их текущего статуса.
    /// </summary>
    /// <param name="npcList">Список сущностей NPC из SettlementComponent.</param>
    private void UpdateNPCList(in FixedList64Bytes<Entity> npcList)
    {
        ClearNPCList();

        bool hasNpcs = npcList.Length > 0;
        npcListContainer.SetActive(hasNpcs);

        if (!hasNpcs) return;

        foreach (var npcEntity in npcList)
        {
            if (!entityManager.Exists(npcEntity) || !entityManager.HasComponent<NPCComponent>(npcEntity)) continue;

            GameObject itemGO = Instantiate(npcListItemPrefab, npcListContainer.transform);
            
            TextMeshProUGUI nameText = itemGO.GetComponentInChildren<TextMeshProUGUI>();
            Button npcButton = itemGO.GetComponent<Button>();

            var npcData = entityManager.GetComponentData<NPCComponent>(npcEntity);
            
            if (nameText != null)
            {
                
                string npcName = npcData.Name.ToString();
                string statusText = " - Простаивает"; // Статус по умолчанию

                // Если у NPC есть цель, определяем его статус
                if (npcData.Target != Entity.Null)
                {
                    // Проверяем, является ли цель ресурсным узлом
                    if (entityManager.HasComponent<ResourceNode>(npcData.Target))
                    {
                        var resourceNode = entityManager.GetComponentData<ResourceNode>(npcData.Target);
                        statusText = $" - Добывает ({resourceNode.resourceType})";
                    }
                    else
                    {
                        // Общий статус для других типов задач
                        statusText = " - Занят";
                    }
                }
                
                // Формируем итоговую строку для отображения
                nameText.text = $"{npcName}{statusText}";
                
            }

            if (npcButton != null)
            {
                Entity capturedNpcEntity = npcEntity;
                npcButton.onClick.AddListener(() => OpenNPCUIFor(capturedNpcEntity));
            }
        }
    }

    /// <summary>
    /// Удаляет все дочерние объекты из контейнера списка NPC.
    /// </summary>
    private void ClearNPCList()
    {
        foreach (Transform child in npcListContainer.transform)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Создает ECS-запрос на открытие UI для конкретного NPC.
    /// </summary>
    private void OpenNPCUIFor(Entity npcEntity)
    {
        if (!isInitialized || npcEntity == Entity.Null) return;
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new OpenNPCUIRequest { Target = npcEntity });
    }
}