using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Управляет пользовательским интерфейсом инвентаря игрока.
/// Отвечает за отображение и скрытие панели инвентаря, а также за создание и
/// своевременное обновление содержимого слотов на основе данных из мира ECS.
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
    /// Вызывается при запуске сцены. Пытается инициализировать необходимые ECS-ссылки
    /// и гарантирует, что панель инвентаря скрыта по умолчанию.
    /// </summary>
    void Start()
    {
        TryInitialize();
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Вызывается каждый кадр. Проверяет глобальное состояние игры (GameState) в ECS,
    /// чтобы определить, должен ли UI инвентаря быть открыт.
    /// Синхронизирует состояние видимости панели с состоянием из ECS.
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

        // Определяем, должно ли окно быть открыто, сравнивая текущий тип UI в GameState.
        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Inventory;

        // Если состояние видимости панели не совпадает с требуемым, меняем его.
        if (inventoryPanel.activeSelf != shouldBeOpen)
        {
            inventoryPanel.SetActive(shouldBeOpen);
            // При первом открытии окна необходимо полностью перестроить его структуру.
            if (shouldBeOpen)
            {
                RebuildSlots();
            }
        }
    }

    /// <summary>
    /// Вызывается каждый кадр после всех обновлений, включая анимации.
    /// Если панель инвентаря активна, обновляет данные в уже созданных слотах.
    /// Это обеспечивает отображение актуальной информации после всех изменений в инвентаре за кадр.
    /// </summary>
    void LateUpdate()
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
        {
            RefreshSlotData();
        }
    }

    /// <summary>
    /// Пытается инициализировать EntityManager и найти сущность игрока по тегу <c>PlayerTag</c>.
    /// Выполняется до тех пор, пока инициализация не будет успешной.
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
    /// Перестраивает UI-слоты инвентаря. Этот метод вызывается однократно при открытии окна.
    /// Он делегирует всю сложную логику универсальному помощнику <c>InventoryPanelHelper</c>.
    /// </summary>
    private void RebuildSlots()
    {
        if (!isInitialized) return;
        // Передаем все необходимые данные для построения: ссылки на ECS, сущность игрока,
        // UI-элементы и колбэк-метод, который будет вызываться при клике на слот.
        InventoryPanelHelper.RebuildSlots(entityManager, playerEntity, slotsParent, slotPrefab, slots, OnInventorySlotClicked);
    }

    /// <summary>
    /// Обновляет визуальное представление уже существующих слотов.
    /// Делегирует логику помощнику <c>InventoryPanelHelper</c> для консистентности.
    /// </summary>
    private void RefreshSlotData()
    {
        if (!isInitialized) return;
        InventoryPanelHelper.RefreshSlotsData(entityManager, playerEntity, slots);
    }

    /// <summary>
    /// Обработчик события, который вызывается при клике на любой слот в инвентаре.
    /// Определяет действие в зависимости от типа предмета.
    /// </summary>
    /// <param name="clickedSlot">Слот инвентаря, по которому кликнули.</param>
    private void OnInventorySlotClicked(InventorySlot clickedSlot)
    {
        if (!isInitialized || clickedSlot == null || clickedSlot.CurrentItem == null) return;

        var item = clickedSlot.CurrentItem;
        
        if (item.itemType == ItemType.Building && clickedSlot.slotIndex < 8)
        {
            entityManager.SetComponentData(playerEntity, new ActiveQuickbarSlot { Index = clickedSlot.slotIndex });
            
            // Запрашиваем закрытие всех UI, чтобы вернуться в игру и увидеть режим строительства.
            GameBridge.Instance?.HandleUICloseAction();
        }
    }

    /// <summary>
    /// Вызывается при уничтожении объекта. Отписывается от всех событий слотов,
    /// чтобы избежать утечек памяти и вызовов методов на уничтоженных объектах.
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