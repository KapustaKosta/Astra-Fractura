using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Управляет двухоконным UI для торговли или обмена предметами между игроком и другой сущностью.
/// Использует архитектуру с однократным созданием слотов при открытии и последующим
/// покадровым обновлением данных для отображения актуального состояния инвентарей.
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

    /// <summary>
    /// Вызывается при старте. Инициализирует ECS-ссылки, подписывается на событие кнопки
    /// и скрывает панель торговли по умолчанию.
    /// </summary>
    void Start()
    {
        TryInitialize();
        if (isInitialized && closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
        }
        tradePanel.SetActive(false);
    }

    /// <summary>
    /// Вызывается каждый кадр. Проверяет глобальное состояние игры, чтобы определить,
    /// должен ли UI торговли быть открыт, и синхронизирует состояние панели.
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

        // Определяем, должно ли окно быть открыто, по данным из UIState.
        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Trade;

        if (tradePanel.activeSelf != shouldBeOpen)
        {
            if (shouldBeOpen)
            {
                // При открытии получаем целевую сущность из GameState и вызываем Show.
                var uiState = entityManager.GetComponentData<UIState>(gameStateEntity);
                Show(uiState.ActiveUITarget);
            }
            else
            {
                Hide();
            }
        }
    }

    /// <summary>
    /// Вызывается каждый кадр после всех обновлений. Если панель торговли активна,
    /// обновляет данные в слотах обоих инвентарей (игрока и цели).
    /// </summary>
    private void LateUpdate()
    {
        if (tradePanel != null && tradePanel.activeSelf)
        {
            // Обновляем данные в существующих слотах
            RefreshPanels();
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
            if(!playerQuery.IsEmpty)
            {
                 playerEntity = playerQuery.GetSingletonEntity();
            }
            isInitialized = true;
        }
    }
    
    /// <summary>
    /// Отображает окно торговли для указанной целевой сущности.
    /// </summary>
    /// <param name="currentTarget">Сущность, с которой игрок будет торговать.</param>
    private void Show(Entity currentTarget)
    {
        if (!isInitialized || playerEntity == Entity.Null)
        {
            Debug.LogError("[TradeUI] Не удалось показать окно: UI не инициализирован или не найдена сущность игрока.");
            return;
        }
        
        targetEntity = currentTarget;
        tradePanel.SetActive(true);
        // При первом открытии полностью перестраиваем слоты для обоих инвентарей.
        RebuildPanels();
    }

    /// <summary>
    /// Скрывает панель торговли.
    /// </summary>
    private void Hide()
    {
        tradePanel.SetActive(false);
    }
    
    /// <summary>
    /// Обрабатывает нажатие кнопки "Закрыть", создавая ECS-запрос на закрытие UI.
    /// </summary>
    private void OnCloseButtonPressed()
    {
        GameBridge.Instance?.HandleUICloseAction();
    }
    
    /// <summary>
    /// Перестраивает слоты для инвентарей игрока и цели, используя универсальный помощник.
    /// Вызывается один раз при открытии окна.
    /// </summary>
    private void RebuildPanels()
    {
        InventoryPanelHelper.RebuildSlots(entityManager, playerEntity, playerSlotsParent, playerSlotPrefab, playerSlots);
        InventoryPanelHelper.RebuildSlots(entityManager, targetEntity, targetSlotsParent, targetSlotPrefab, targetSlots);
    }

    /// <summary>
    /// Обновляет данные в уже существующих слотах для обоих инвентарей, используя универсальный помощник.
    /// Вызывается каждый кадр, пока окно открыто.
    /// </summary>
    private void RefreshPanels()
    {
        InventoryPanelHelper.RefreshSlotsData(entityManager, playerEntity, playerSlots);
        InventoryPanelHelper.RefreshSlotsData(entityManager, targetEntity, targetSlots);
    }
}