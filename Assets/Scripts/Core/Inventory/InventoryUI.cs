using UnityEngine;
using Unity.Entities;

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

    [Header("UI References")]
    [Tooltip("Родительский Transform для слотов инвентаря.")]
    public Transform slotsParent;
    [Tooltip("Префаб одного слота инвентаря.")]
    public GameObject slotPrefab;
    [Tooltip("Панель UI инвентаря, которая будет активироваться/деактивироваться.")]
    public GameObject inventoryPanel;

    private Inventory inventory;
    private InventorySlot[] slots;
    private EntityManager entityManager;
    private bool isInitialized = false;

    /// <summary>
    /// Вызывается при загрузке скрипта. Устанавливает синглтон-экземпляр.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Вызывается при первом кадре. Пытается инициализировать менеджер сущностей и инвентарь.
    /// </summary>
    void Start()
    {
        TryInitialize();
    }

    /// <summary>
    /// Вызывается при уничтожении объекта. Отписывается от событий инвентаря и слотов.
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
    /// Вызывается каждый кадр. Проверяет состояние игры и соответственно
    /// активирует или деактивирует панель инвентаря.
    /// </summary>
    void Update()
    {
        // Если инициализация не удалась в Start(), продолжаем пытаться в Update().
        if (!isInitialized)
        {
            TryInitialize();
            if (!isInitialized) return;
        }

        var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();

        // Определяем, должен ли инвентарь быть открыт, на основе состояния игры (InUIMode и ActiveUIType).
        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Inventory;

        // Если текущее состояние панели не совпадает с желаемым, обновляем его.
        if (inventoryPanel.activeSelf != shouldBeOpen)
        {
            SetInventoryState(shouldBeOpen);
        }
    }

    /// <summary>
    /// Пытается инициализировать необходимые компоненты, ссылки и выполнить одноразовую настройку UI инвентаря.
    /// </summary>
    private void TryInitialize()
    {
        if (isInitialized) return;

        // Проверяем все зависимости перед выполнением настройки.
        inventory = Inventory.Instance;
        if (inventory == null) return;

        if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated) return;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (inventoryPanel == null || slotsParent == null || slotPrefab == null) return;

        // Все зависимости на месте, выполняем одноразовую настройку.
        Debug.Log("[InventoryUI] Инициализация прошла успешно. Настраиваю слоты и события.");

        isInitialized = true;
        
        inventory.onItemChanged += UpdateUI; // Подписываемся на событие изменения инвентаря.
        InitializeSlots();                   // Создаем и настраиваем слоты UI.
        SetInventoryState(false);            // Убедимся, что инвентарь закрыт при старте.
    }

    /// <summary>
    /// Отправляет ECS-запрос на переключение состояния инвентаря (открыть/закрыть).
    /// Этот метод может быть вызван кнопкой UI.
    /// </summary>
    public void RequestToggleInventory()
    {
        if (!isInitialized) return;
        var toggleEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(toggleEntity, new ToggleInventoryRequest());
    }

    /// <summary>
    /// Устанавливает активность панели инвентаря. При открытии обновляет содержимое слотов.
    /// </summary>
    /// <param name="state">True для открытия, False для закрытия.</param>
    private void SetInventoryState(bool state)
    {
        inventoryPanel.SetActive(state);
        if (state)
        {
            UpdateUI(); 
        }
    }

    /// <summary>
    /// Инициализирует слоты инвентаря: удаляет старые и создает новые на основе размера инвентаря.
    /// Каждый новый слот подписывается на событие клика.
    /// </summary>
    private void InitializeSlots()
    {
        slots = new InventorySlot[inventory.space];
        // Удаляем все существующие дочерние объекты из родителя слотов.
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }

        // Создаем новые слоты.
        for (int i = 0; i < inventory.space; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsParent);
            InventorySlot slotComponent = slotGO.GetComponent<InventorySlot>();
            if (slotComponent != null)
            {
                slots[i] = slotComponent;
                // Подписываемся на событие клика по слоту.
                slotComponent.OnSlotClicked += HandleSlotClicked;
            }
        }
    }

    /// <summary>
    /// Обновляет визуальное представление слотов инвентаря, отображая текущие предметы и их количество.
    /// </summary>
    public void UpdateUI()
    {
        if (!isInitialized) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (i < inventory.items.Count)
            {
                // Если в инвентаре есть предмет для этого слота, настраиваем его.
                slots[i].SetupSlot(inventory.items[i].item, inventory.items[i].amount);
            }
            else
            {
                
                slots[i].ClearSlot();
            }
        }
    }

    /// <summary>
    /// Обрабатывает клик по слоту инвентаря. В зависимости от типа предмета,
    /// либо создает запрос на вход в режим строительства, либо расходует предмет.
    /// </summary>
    /// <param name="clickedItem">Предмет, по которому был произведен клик.</param>
    private void HandleSlotClicked(Item clickedItem)
    {
        if (clickedItem == null || !isInitialized) return;

        Debug.Log($"[InventoryUI] Клик по слоту. Предмет: '{clickedItem.name}', Тип: '{clickedItem.itemType}'.");


        Inventory.Instance.SelectItem(clickedItem);

        // Если тип предмета - "Building", создаем запрос на вход в режим строительства.
        if (clickedItem.itemType == ItemType.Building)
        {
            Debug.Log($"[InventoryUI] Предмет является зданием. Создание EnterBuildingModeRequest для ItemID: {clickedItem.itemID}");
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new EnterBuildingModeRequest { ItemID = clickedItem.itemID });
        }
        // Если тип предмета - "Consumable", удаляем его из инвентаря (используем).(на будущее)
        else if (clickedItem.itemType == ItemType.Consumable)
        {
            Inventory.Instance.Remove(clickedItem, 1);
        }
    }
}