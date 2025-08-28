using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Управляет UI квикбара, который отображает первые 8 слотов инвентаря игрока
/// и подсвечивает текущий выбранный слот путем изменения его цвета.
/// </summary>
public class QuickbarUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Контейнер, в который будут добавляться префабы слотов.")]
    [SerializeField] private RectTransform slotsParent;

    [Tooltip("Префаб для одного слота инвентаря (должен иметь компонент InventorySlot).")]
    [SerializeField] private GameObject slotPrefab;

    [Header("Settings")]
    [Tooltip("Ассет с настройками цветов для слотов квикбара.")]
    [SerializeField] private QuickbarSettings settings;

    private const int QuickbarSize = 8;
    private List<InventorySlot> slots = new List<InventorySlot>();
    private EntityManager entityManager;
    private Entity playerEntity;
    private bool isInitialized = false;

    /// <summary>
    /// Вызывается при запуске сцены. Пытается инициализировать необходимые ECS-ссылки.
    /// </summary>
    void Start()
    {
        TryInitialize();
    }

    /// <summary>
    /// Вызывается каждый кадр после всех обновлений.
    /// Отвечает за обновление UI квикбара.
    /// </summary>
    void LateUpdate()
    {
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }

        var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();

        bool shouldBeVisible = !entityManager.HasComponent<InUIMode>(gameStateEntity);

        if (slotsParent.gameObject.activeSelf != shouldBeVisible)
        {
            slotsParent.gameObject.SetActive(shouldBeVisible);
        }

        if (shouldBeVisible)
        {
            RefreshSlots();
            UpdateSlotHighlights();
        }
    }

    /// <summary>
    /// Пытается инициализировать EntityManager и найти сущность игрока.
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
                RebuildSlots();
                isInitialized = true;
            }
        }
    }

    /// <summary>
    /// Создает UI слоты для квикбара. Вызывается один раз при инициализации.
    /// </summary>
    private void RebuildSlots()
    {
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }
        slots.Clear();

        for (int i = 0; i < QuickbarSize; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsParent);
            slotGO.name = $"QuickbarSlot_{i}";
            InventorySlot slot = slotGO.GetComponent<InventorySlot>();
            if (slot != null)
            {
                slots.Add(slot);
            }
        }
    }

    /// <summary>
    /// Обновляет данные (иконки, количество) в слотах квикбара.
    /// </summary>
    private void RefreshSlots()
    {
        if (!isInitialized || !entityManager.HasBuffer<InventoryItemElement>(playerEntity)) return;

        var inventoryBuffer = entityManager.GetBuffer<InventoryItemElement>(playerEntity);
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        for (int i = 0; i < QuickbarSize; i++)
        {
            if (i < inventoryBuffer.Length)
            {
                var itemElement = inventoryBuffer[i];
                var itemData = itemRegistry.GetItemData(itemElement.ItemID);
                slots[i].InitializeSlot(itemData, itemElement.Amount, playerEntity, i, InventoryType.General);
            }
            else
            {
                slots[i].ClearSlot();
            }
        }
    }

    /// <summary>
    /// Проходит по всем слотам и устанавливает их статус (активный/неактивный).
    /// </summary>
    private void UpdateSlotHighlights()
    {
        if (settings == null) return; // Не делаем ничего, если настройки не заданы.

        if (!isInitialized || !entityManager.HasComponent<ActiveQuickbarSlot>(playerEntity))
        {
            foreach (var slot in slots)
            {
                slot.SetHighlightStatus(false, settings);
            }
            return;
        }

        var activeSlotData = entityManager.GetComponentData<ActiveQuickbarSlot>(playerEntity);
        int activeIndex = activeSlotData.Index;

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].SetHighlightStatus(i == activeIndex, settings);
        }
    }
}