using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Управляет двухоконным UI для торговли/обмена предметами между игроком и другой сущностью.
/// Использует архитектуру с однократным созданием слотов и последующим обновлением данных.
/// </summary>
public class TradeUI : MonoBehaviour
{
    [Header("Main Panel")]
    [SerializeField] private GameObject tradePanel;
    [SerializeField] private Button closeButton;

    [Header("Player Inventory UI")]
    [SerializeField] private RectTransform playerSlotsParent;
    [SerializeField] private GameObject playerSlotPrefab;

    [Header("Target Inventory UI")]
    [SerializeField] private RectTransform targetSlotsParent;
    [SerializeField] private GameObject targetSlotPrefab;
    
    private EntityManager entityManager;
    private Entity playerEntity;
    private Entity targetEntity;
    private bool isInitialized = false;

    private List<InventorySlot> playerSlots = new List<InventorySlot>();
    private List<InventorySlot> targetSlots = new List<InventorySlot>();

    void Start()
    {
        TryInitialize();
        if (isInitialized && closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
        }
        tradePanel.SetActive(false);
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

        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Trade;

        if (tradePanel.activeSelf != shouldBeOpen)
        {
            if (shouldBeOpen)
            {
                var uiState = entityManager.GetComponentData<UIState>(gameStateEntity);
                Show(uiState.ActiveUITarget);
            }
            else
            {
                Hide();
            }
        }
    }

    private void LateUpdate()
    {
        if (tradePanel != null && tradePanel.activeSelf)
        {
            // Обновляем данные в существующих слотах
            RefreshPanels();
        }
    }

    private void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag)); 
            if(!playerQuery.IsEmpty)
            {
                 playerEntity = playerQuery.GetSingletonEntity();
            }
            isInitialized = true;
        }
    }
    
    private void Show(Entity currentTarget)
    {
        if (!isInitialized || playerEntity == Entity.Null)
        {
            Debug.LogError("[TradeUI] Не удалось показать окно: UI не инициализирован или не найдена сущность игрока.");
            return;
        }
        
        targetEntity = currentTarget;
        tradePanel.SetActive(true);
        // Перестраиваем слоты один раз при открытии
        RebuildPanels();
    }

    private void Hide()
    {
        tradePanel.SetActive(false);
    }
    
    private void OnCloseButtonPressed()
    {
        GameBridge.Instance?.HandleUICloseAction();
    }
    
    /// <summary>
    /// Перестраивает слоты для обоих инвентарей. Вызывается один раз при открытии.
    /// </summary>
    private void RebuildPanels()
    {
        RebuildInventoryPanel(playerEntity, playerSlotsParent, playerSlotPrefab, playerSlots);
        RebuildInventoryPanel(targetEntity, targetSlotsParent, targetSlotPrefab, targetSlots);
    }

    /// <summary>
    /// Обновляет данные в уже существующих слотах. Вызывается каждый кадр в LateUpdate.
    /// </summary>
    private void RefreshPanels()
    {
        RefreshInventoryData(playerEntity, playerSlots);
        RefreshInventoryData(targetEntity, targetSlots);
    }

    /// <summary>
    /// Универсальный метод для создания слотов для одного инвентаря.
    /// </summary>
    private void RebuildInventoryPanel(Entity owner, RectTransform slotsParent, GameObject slotPrefab, List<InventorySlot> slotList)
    {
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }
        slotList.Clear();

        if (!entityManager.Exists(owner) || !entityManager.HasComponent<InventoryProperties>(owner)) return;

        var properties = entityManager.GetComponentData<InventoryProperties>(owner);

        for (int i = 0; i < properties.Capacity; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsParent);
            InventorySlot slot = slotGO.GetComponent<InventorySlot>();
            if (slot != null)
            {
                slotList.Add(slot);
            }
        }
    }

    /// <summary>
    /// Универсальный метод для обновления данных в слотах одного инвентаря.
    /// **Этот метод должен быть идентичен по логике методу RefreshSlotData в InventoryUI.cs**
    /// </summary>
    private void RefreshInventoryData(Entity owner, List<InventorySlot> slotList)
    {
        if (!entityManager.Exists(owner) || !entityManager.HasBuffer<InventoryItemElement>(owner))
        {
            // Если инвентаря нет, очищаем все слоты
            for (int i = 0; i < slotList.Count; i++)
            {
                // Все равно передаем контекст, чтобы Drop работал на пустых слотах
                slotList[i].InitializeSlot(null, 0, owner, i);
            }
            return;
        }

        var inventoryBuffer = entityManager.GetBuffer<InventoryItemElement>(owner);
        var itemRegistry = ItemRegistry.Instance;

        // Итерируем по всем слотам UI
        for (int i = 0; i < slotList.Count; i++)
        {
            // Проверяем, что в буфере есть элемент с таким индексом (защита от несоответствия размеров)
            if (i >= inventoryBuffer.Length)
            {
                slotList[i].InitializeSlot(null, 0, owner, i);
                continue;
            }

            var itemElement = inventoryBuffer[i];
            
            // Проверяем, является ли слот "пустым" (по ItemID == 0)
            if (itemElement.ItemID != 0)
            {
                var itemData = itemRegistry != null ? itemRegistry.GetItemData(itemElement.ItemID) : null;
                slotList[i].InitializeSlot(itemData, itemElement.Amount, owner, i);
            }
            else
            {
                // Это пустой слот
                slotList[i].InitializeSlot(null, 0, owner, i);
            }
        }
    }
}