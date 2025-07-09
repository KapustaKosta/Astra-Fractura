using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Управляет пользовательским интерфейсом инвентаря игрока.
/// Отвечает за отображение и скрытие панели инвентаря, а также за обновление содержимого слотов.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Родительский объект для всех элементов UI инвентаря.")]
    [SerializeField] private GameObject inventoryPanel;
    [Tooltip("Контейнер, в который будут добавляться префабы слотов.")]
    [SerializeField] private RectTransform slotsParent;
    [Tooltip("Префаб для одного слота инвентаря.")]
    [SerializeField] private GameObject slotPrefab;

    private List<InventorySlot> slots = new List<InventorySlot>();
    private EntityManager entityManager;
    private Entity playerEntity;
    private bool isInitialized = false;

    /// <summary>
    /// Инициализация EntityManager и поиск сущности игрока. Скрываем панель при старте.
    /// </summary>
    void Start()
    {
        TryInitialize();
        if (isInitialized && inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Каждый кадр проверяет, должен ли UI инвентаря быть открыт.
    /// При открытии выполняется одноразовое построение слотов.
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

        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Inventory;

        if (inventoryPanel.activeSelf != shouldBeOpen)
        {
            inventoryPanel.SetActive(shouldBeOpen);
            if (shouldBeOpen)
            {
                RebuildSlots();    
            }
        }
    }

    /// <summary>
    /// Каждый кадр после обновлений UI обновляем данные в существующих слотах.
    /// </summary>
    void LateUpdate()
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
        {
            RefreshSlotData();    
        }
    }

    /// <summary>
    /// Пытается инициализировать EntityManager и найти сущность игрока по тегу.
    /// </summary>
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
                isInitialized = true;
            }
        }
    }

    /// <summary>
    /// Строит слоты инвентаря один раз при открытии UI.
    /// </summary>
    private void RebuildSlots()
    {
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }
        slots.Clear();

        if (!entityManager.HasBuffer<InventoryItemElement>(playerEntity)) return;
        var properties = entityManager.GetComponentData<InventoryProperties>(playerEntity);
        int capacity = properties.Capacity;

        for (int i = 0; i < capacity; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsParent);
            InventorySlot slot = slotGO.GetComponent<InventorySlot>();

            if (slot == null) continue;
            slot.OnSlotClicked += OnInventorySlotClicked;
            slots.Add(slot);
        }
    }

    /// <summary>
    /// Обновляет содержимое уже существующих слотов по данным буфера.
    /// </summary>
    private void RefreshSlotData()
    {
        if (!entityManager.HasBuffer<InventoryItemElement>(playerEntity)) return;

        var inventoryBuffer = entityManager.GetBuffer<InventoryItemElement>(playerEntity);
        var itemRegistry = ItemRegistry.Instance;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= inventoryBuffer.Length)
            {
                slots[i].InitializeSlot(null, 0, playerEntity, i);
                continue;
            }

            var itemElement = inventoryBuffer[i];
        
            if (itemElement.ItemID != 0)
            {
                var itemData = itemRegistry != null ? itemRegistry.GetItemData(itemElement.ItemID) : null;
                slots[i].InitializeSlot(itemData, itemElement.Amount, playerEntity, i);
            }
            else
            {
                slots[i].InitializeSlot(null, 0, playerEntity, i);
            }
        }
    }

    /// <summary>
    /// Обработчик события, который вызывается при клике на любой слот в инвентаре.
    /// Определяет действие в зависимости от типа предмета.
    /// </summary>
    /// <param name="item">Предмет, который находится в кликнутом слоте.</param>
    private void OnInventorySlotClicked(Item item)
    {
        if (item == null) return;

        switch (item.itemType)
        {
            case ItemType.Building:
                var buildRequest = entityManager.CreateEntity();
                entityManager.AddComponentData(buildRequest, new EnterBuildingModeRequest
                {
                    ItemID = item.itemID
                });
                break;

            case ItemType.Tool:
            case ItemType.Consumable: 
            case ItemType.Resource:
            case ItemType.Weapon:
            case ItemType.Miscellaneous:
                break;
                
            default:
                Debug.LogWarning($"[InventoryUI] Неизвестный тип предмета: {item.itemType}");
                break;
        }
    }

    /// <summary>
    /// Отписываемся от всех событий при уничтожении объекта, чтобы избежать ошибок.
    /// </summary>
    private void OnDestroy()
    {
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.OnSlotClicked -= OnInventorySlotClicked;
            }
        }
    }
}