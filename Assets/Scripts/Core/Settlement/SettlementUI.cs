using Conveyor;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class SettlementUI : MonoBehaviour
{
    public static SettlementUI Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private TextMeshProUGUI settlementNameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button tradeButton;
    [Tooltip("НОВАЯ КНОПКА: Открывает UI управления маршрутами конвейеров.")]
    [SerializeField] private Button conveyorRoutesButton; 

    [Header("NPC List Elements")]
    [SerializeField] private GameObject npcListContainer;
    [SerializeField] private GameObject npcListItemPrefab;

    private bool isInitialized = false;
    private EntityManager entityManager;
    private Entity currentSettlementEntity;

    private int lastKnownNpcCount = -1;
    private bool lastTradeButtonActiveState = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Добавлена проверка новой кнопки
        if (uiPanel == null || settlementNameText == null || statsText == null || closeButton == null ||
            tradeButton == null || conveyorRoutesButton == null || 
            npcListContainer == null || npcListItemPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[SettlementUI] Не все UI элементы назначены в инспекторе!", this);
#endif
            enabled = false;
        }
    }

    private void Start()
    {
        TryInitialize();
        if (isInitialized)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
            tradeButton.onClick.AddListener(OnTradeButtonPressed);
            conveyorRoutesButton.onClick.AddListener(OnConveyorRoutesButtonPressed); 
            uiPanel.SetActive(false);
        }
    }


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
        
    
        // 1. Сначала проверяем, есть ли вообще компонент UIState. Если его нет, UI не может быть открыт.
        if (!entityManager.HasComponent<UIState>(gameStateEntity))
        {
            // Если компонента нет, но панель почему-то активна, скрываем ее.
            if (uiPanel.activeSelf)
            {
                Hide();
            }
            return; // Выходим из метода, чтобы избежать ошибки.
        }
    

        var uiState = entityManager.GetComponentData<UIState>(gameStateEntity);
        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            uiState.ActiveUIType == UIType.Settlement;
        

        if (uiPanel.activeSelf != shouldBeOpen)
        {
            if (shouldBeOpen)
            {
                // Здесь uiState уже получена, повторно получать не нужно.
                if (entityManager.Exists(uiState.ActiveUITarget) && entityManager.HasComponent<SettlementComponent>(uiState.ActiveUITarget))
                {
                    currentSettlementEntity = uiState.ActiveUITarget;
                    Show();
                }
            }
            else
            {
                Hide();
            }
        }
    }

    void LateUpdate()
    {
        if (uiPanel != null && uiPanel.activeSelf && currentSettlementEntity != Entity.Null && entityManager.Exists(currentSettlementEntity))
        {
            var settlementData = entityManager.GetComponentData<SettlementComponent>(currentSettlementEntity);
            RefreshUI(in settlementData);
        }
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

    private void Show()
    {
        if (!enabled) return;
        uiPanel.SetActive(true);
        lastKnownNpcCount = -1;
        lastTradeButtonActiveState = !tradeButton.gameObject.activeSelf;

        if (entityManager.Exists(currentSettlementEntity))
        {
            var settlementData = entityManager.GetComponentData<SettlementComponent>(currentSettlementEntity);
            RefreshUI(in settlementData, true);
        }
    }

    private void RefreshUI(in SettlementComponent settlement, bool forceUpdateNPCList = false)
    {
        settlementNameText.text = settlement.Name.ToString();
        statsText.text = $"Уровень: {settlement.Level}\nНаселение: {settlement.Population}";

        if (forceUpdateNPCList || settlement.NPCs.Length != lastKnownNpcCount)
        {
            UpdateNPCList(in settlement.NPCs);
            lastKnownNpcCount = settlement.NPCs.Length;
        }

        bool hasInventory = entityManager.HasComponent<HasInventoryTag>(currentSettlementEntity);
        if (tradeButton.gameObject.activeSelf != hasInventory || forceUpdateNPCList)
        {
            tradeButton.gameObject.SetActive(hasInventory);
            lastTradeButtonActiveState = hasInventory;
        }
    }

    private void Hide()
    {
        if (!enabled || uiPanel == null) return;
        uiPanel.SetActive(false);
        currentSettlementEntity = Entity.Null;
        ClearNPCList();
        lastKnownNpcCount = -1;
    }

    private void OnCloseButtonPressed()
    {
        GameBridge.Instance?.HandleUICloseAction();
    }

    private void OnTradeButtonPressed()
    {
        if (!isInitialized || currentSettlementEntity == Entity.Null) return;

        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new OpenTradeUIRequest { Target = currentSettlementEntity });
    }

    /// <summary>
    /// НОВЫЙ МЕТОД: Обрабатывает нажатие кнопки "Маршруты", создавая ECS-запрос.
    /// </summary>
    private void OnConveyorRoutesButtonPressed()
    {
        if (!isInitialized) return;
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new OpenConveyorRoutesUIRequest());

    }

 

    private void UpdateNPCList(in FixedList64Bytes<Entity> npcList)
    {
        ClearNPCList();
        bool hasNpcs = npcList.Length > 0;
        npcListContainer.gameObject.SetActive(hasNpcs);
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
                string statusText = " - Простаивает";
                if (npcData.Target != Entity.Null && entityManager.Exists(npcData.Target))
                {
                    if (entityManager.HasComponent<ResourceNode>(npcData.Target))
                    {
                        var resourceNode = entityManager.GetComponentData<ResourceNode>(npcData.Target);
                        statusText = $" - Добывает ({resourceNode.resourceType})";
                    }
                    else
                    {
                        statusText = " - Занят";
                    }
                }
                nameText.text = $"{npcName}{statusText}";
            }

            if (npcButton != null)
            {
                Entity capturedNpcEntity = npcEntity;
                npcButton.onClick.AddListener(() => OpenNPCUIFor(capturedNpcEntity));
            }
        }
    }

    private void ClearNPCList()
    {
        foreach (Transform child in npcListContainer.transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void OpenNPCUIFor(Entity npcEntity)
    {
        if (!isInitialized || npcEntity == Entity.Null) return;
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new OpenNPCUIRequest { Target = npcEntity });
    }
}