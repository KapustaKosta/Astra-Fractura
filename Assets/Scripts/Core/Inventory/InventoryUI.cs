using UnityEngine;
using Unity.Entities;

/// <summary>
/// Управляет пользовательским интерфейсом инвентаря. Теперь он полностью независим и читает состояние напрямую из ECS.
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
    private EntityManager entityManager;
    private bool isInitialized = false;

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
    /// инициализирует слоты и подписывается на события инвентаря.
    /// </summary>
    void Start()
    {
        TryInitialize();
        if (isInitialized)
        {
            inventory.onItemChanged += UpdateUI;
            InitializeSlots();
            SetInventoryState(false); // Изначально инвентарь закрыт
        }
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
    }

    /// <summary>
    /// Каждый кадр проверяет глобальное состояние игры и синхронизирует с ним видимость своей панели.
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
        
        // Наше единственное условие для отображения: текущий режим UI и тип UI - инвентарь.
        bool shouldBeOpen = gameState.CurrentMode == GameMode.UI && gameState.ActiveUIType == UIType.Inventory;
        
        // Если фактическое состояние панели не соответствует данным из ECS, исправляем это.
        if (inventoryPanel.activeSelf != shouldBeOpen)
        {
            SetInventoryState(shouldBeOpen);
        }
    }

    /// <summary>
    /// Пытается инициализировать все необходимые ссылки.
    /// </summary>
    private void TryInitialize()
    {
        if (isInitialized) return;

        inventory = Inventory.Instance;
        if (inventory == null) { return; }

        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }
        else { return; }

        if (inventoryPanel == null || slotsParent == null || slotPrefab == null) { return; }

        isInitialized = true;
    }

    /// <summary>
    /// Запрашивает переключение состояния инвентаря путем создания ECS-запроса.
    /// </summary>
    public void RequestToggleInventory()
    {
        if (!isInitialized) return;
        
        var toggleEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(toggleEntity, new ToggleInventoryRequest());
    }

    /// <summary>
    /// Устанавливает состояние отображения инвентаря (открыт/закрыт).
    /// </summary>
    /// <param name="state">True для открытия, false для закрытия.</param>
    private void SetInventoryState(bool state)
    {
        inventoryPanel.SetActive(state);
        if (state)
        {
            UpdateUI();
        }
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
        if (clickedItem == null || !isInitialized) return;

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