using UnityEngine;
using Unity.Entities;
using System;

/// <summary>
/// Управляет пользовательским интерфейсом инвентаря, отображая слоты и обрабатывая взаимодействие с ними.
/// Является Singleton-классом.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    /// <summary>
    /// Singleton-экземпляр InventoryUI.
    /// </summary>
    public static InventoryUI Instance { get; private set; }

    /// <summary>
    /// Родительский Transform для слотов инвентаря.
    /// </summary>
    [Header("UI References")]
    public Transform slotsParent;

    /// <summary>
    /// Префаб GameObject для одного слота инвентаря.
    /// </summary>
    public GameObject slotPrefab;

    /// <summary>
    /// Панель, содержащая весь UI инвентаря.
    /// </summary>
    public GameObject inventoryPanel;

    private Inventory inventory;
    private InventorySlot[] slots;
    private bool isOpen = false;
    private EntityManager entityManager;

    /// <summary>
    /// Возвращает текущее состояние открытия/закрытия инвентаря.
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// Вызывается при загрузке скрипта. Инициализирует Singleton-экземпляр.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Вызывается в первом кадре. Получает ссылки на Inventory, EntityManager,
    /// инициализирует слоты и подписывается на события.
    /// </summary>
    void Start()
    {
        inventory = Inventory.Instance;
        if (inventory == null) { enabled = false; return; }
        
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        inventory.onItemChanged += UpdateUI;

        if (inventoryPanel == null || slotsParent == null || slotPrefab == null) { enabled = false; return; }

        InitializeSlots();
        SetInventoryState(false);
        
        GameStateEvents.OnUIStateChanged += HandleUIStateChange;
    }

    /// <summary>
    /// Обрабатывает изменение состояния UI, полученное от GameStateEvents.
    /// </summary>
    /// <param name="uiEvent">Тип события UI.</param>
    /// <param name="shouldBeOpen">True, если UI должен быть открыт, false, если закрыт.</param>
    /// <param name="target">Целевая сущность, если применимо (например, для UI NPC/Поселения).</param>
    private void HandleUIStateChange(UIStateEvent uiEvent, bool shouldBeOpen, Entity target)
    {
        if (uiEvent == UIStateEvent.InventoryToggled)
        {
            SetInventoryState(shouldBeOpen);
        }
        else if (uiEvent == UIStateEvent.AllUIClosed)
        {
            SetInventoryState(false);
        }
    }
    
    /// <summary>
    /// Запрашивает переключение состояния инвентаря путем создания ECS-запроса.
    /// </summary>
    public void RequestToggleInventory()
    {
        if (!entityManager.World.IsCreated) return;
        
        var toggleEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(toggleEntity, new ToggleInventoryRequest());
    }

    /// <summary>
    /// Устанавливает состояние отображения инвентаря (открыт/закрыт).
    /// </summary>
    /// <param name="state">True для открытия, false для закрытия.</param>
    private void SetInventoryState(bool state)
    {
        if (isOpen == state) return;
        isOpen = state;
        inventoryPanel.SetActive(state);
        
        if (isOpen)
        {
            UpdateUI();
        }
    }

    /// <summary>
    /// Закрывает инвентарь.
    /// </summary>
    public void CloseInventory()
    {
        SetInventoryState(false);
    }

    /// <summary>
    /// Вызывается при уничтожении объекта. Отписывается от событий для предотвращения утечек памяти.
    /// </summary>
    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.onItemChanged -= UpdateUI;
        }
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null) slot.OnSlotClicked -= HandleSlotClicked;
            }
        }
        GameStateEvents.OnUIStateChanged -= HandleUIStateChange;
    }

    /// <summary>
    /// Инициализирует слоты инвентаря, создавая их на основе префаба и количества доступного места.
    /// </summary>
    private void InitializeSlots()
    {
        slots = new InventorySlot[inventory.space];
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < inventory.space; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsParent);
            InventorySlot slotComponent = slotGO.GetComponent<InventorySlot>();
            if (slotComponent != null)
            {
                slots[i] = slotComponent;
                slotComponent.OnSlotClicked += HandleSlotClicked;
            }
        }
    }

    /// <summary>
    /// Обновляет отображение UI инвентаря, синхронизируя его с текущим состоянием инвентаря.
    /// </summary>
    public void UpdateUI()
    {
        if (inventory == null || slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i < inventory.items.Count)
            {
                slots[i].SetupSlot(inventory.items[i].item, inventory.items[i].amount);
            }
            else
            {
                slots[i].ClearSlot();
            }
        }
    }

    /// <summary>
    /// Обрабатывает клик по слоту инвентаря. Выбирает предмет и, в зависимости от его типа,
    /// инициирует режим строительства или использует расходуемый предмет.
    /// </summary>
    /// <param name="clickedItem">Предмет, по которому был произведен клик.</param>
    private void HandleSlotClicked(Item clickedItem)
    {
        if (clickedItem == null || !entityManager.World.IsCreated) return;

        Inventory.Instance.SelectItem(clickedItem);

        if (clickedItem.itemType == ItemType.Building)
        {
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new EnterBuildingModeRequest { ItemID = clickedItem.itemID });
        }
        else if (clickedItem.itemType == ItemType.Consumable)
        {
            Inventory.Instance.Remove(clickedItem, 1);
            // Debug.Log($"Использован: {clickedItem.itemName}");
        }
    }
}