using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using System.Collections.Generic;
using System;
using TMPro;

public class TradeUI : MonoBehaviour
{
    public static TradeUI Instance { get; private set; }

    [Header("Main Panel")]
    [SerializeField] private GameObject tradePanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Player Inventory UI")]
    [SerializeField] private RectTransform playerSlotsParent;
    [SerializeField] private GameObject playerSlotPrefab;

    [Header("Target Inventory UI")]
    [SerializeField] private RectTransform targetSlotsParent;
    [SerializeField] private GameObject targetSlotPrefab;

    private EntityManager entityManager;
    private Entity playerEntity;
    private Entity targetEntity;
    private InventoryType targetInventoryType;
    private bool isInitialized = false;

    private List<InventorySlot> playerSlots = new List<InventorySlot>();
    private List<InventorySlot> targetSlots = new List<InventorySlot>();

    private Action<Item> _onItemSelectedCallback;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        TryInitialize();
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
        }
        tradePanel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (tradePanel != null && tradePanel.activeSelf)
        {
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
            if (!playerQuery.IsEmpty)
            {
                playerEntity = playerQuery.GetSingletonEntity();
            }
            isInitialized = true;
        }
    }


    private void InternalShow(Entity currentTarget, InventoryType type, Action<Item> callback)
    {
        TryInitialize();
        if (!isInitialized)
        {
            Debug.LogError("[TradeUI] Не удалось показать окно: UI не инициализирован.");
            return;
        }

        _onItemSelectedCallback = callback;
        targetEntity = currentTarget;
        targetInventoryType = type;

        bool isSelectionMode = _onItemSelectedCallback != null;

        string windowTitle;
        if (isSelectionMode)
        {
            windowTitle = "Выберите ресурс для маршрута";
        }
        else
        {
            switch (type)
            {
                case InventoryType.Input:
                    windowTitle = "Входной инвентарь";
                    break;
                case InventoryType.Output:
                    windowTitle = "Выходной инвентарь";
                    break;
                case InventoryType.WIP:
                    windowTitle = "Буферный инвентарь";
                    break;
                case InventoryType.General:
                default:
                    windowTitle = "Торговля";
                    break;
            }
        }

        if (titleText != null) titleText.text = windowTitle;

        // Показываем/скрываем инвентарь игрока только в зависимости от режима (выбор/взаимодействие)
        if (playerSlotsParent != null)
        {
            playerSlotsParent.parent.gameObject.SetActive(!isSelectionMode);
        }

        tradePanel.SetActive(true);
        RebuildPanels();
    }


    public void Show(Entity currentTarget, InventoryType type)
    {
        // Стандартный вызов для взаимодействия с инвентарями
        InternalShow(currentTarget, type, null);
    }

    public void ShowForItemSelection(Entity inventoryOwner, InventoryType type, Action<Item> selectionCallback)
    {
        // Вызов для режима выбора предмета
        InternalShow(inventoryOwner, type, selectionCallback);
    }

    public void Hide()
    {
        tradePanel.SetActive(false);
        _onItemSelectedCallback = null;
    }

    private void OnCloseButtonPressed()
    {
        Hide();
        GameBridge.Instance?.HandleUICloseAction();
    }

    private void RebuildPanels()
    {
        bool isSelectionMode = _onItemSelectedCallback != null;

        // Перестраиваем инвентарь игрока, если мы не в режиме выбора
        if (!isSelectionMode)
        {
            InventoryPanelHelper.RebuildSlots(entityManager, playerEntity, playerSlotsParent, playerSlotPrefab, playerSlots, InventoryType.General);
        }

        // Всегда перестраиваем инвентарь цели
        Action<InventorySlot> slotClickAction = isSelectionMode ? HandleSlotClickForSelection : null;
        InventoryPanelHelper.RebuildSlots(entityManager, targetEntity, targetSlotsParent, targetSlotPrefab, targetSlots, targetInventoryType, slotClickAction);
    }

    private void HandleSlotClickForSelection(InventorySlot slot)
    {
        if (slot.CurrentItem != null)
        {
            _onItemSelectedCallback?.Invoke(slot.CurrentItem);
            Hide();
        }
    }

    private void RefreshPanels()
    {
        // Обновляем инвентарь игрока, если он отображается
        if (playerSlotsParent != null && playerSlotsParent.parent.gameObject.activeSelf)
        {
            InventoryPanelHelper.RefreshSlotsData(entityManager, playerEntity, playerSlots, InventoryType.General);
        }

        // Всегда обновляем инвентарь цели
        InventoryPanelHelper.RefreshSlotsData(entityManager, targetEntity, targetSlots, targetInventoryType);
    }
}