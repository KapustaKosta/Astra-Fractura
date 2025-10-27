using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using System.Collections.Generic;
using Conveyor;
using Game.Production;
using Unity.Collections;

/// <summary>
/// Основной MonoBehaviour-контроллер для окна интерфейса, отображающего список всех конвейерных маршрутов в игре.
/// Реализован как синглтон и служит мостом между миром ECS и UI.
/// Он отвечает за динамическое создание списка маршрутов и обработку пользовательских действий по их настройке.
/// </summary>
public class ConveyorRoutesUI : MonoBehaviour
{
    /// <summary>
    /// Статический экземпляр класса для глобального доступа (синглтон).
    /// </summary>
    public static ConveyorRoutesUI Instance { get; private set; }

    [Header("Main Panel")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform routesListContent;
    [SerializeField] private GameObject routeListItemPrefab;

    private EntityManager entityManager;
    private Entity currentRouteToConfigure;
    private List<GameObject> activeRouteItems = new List<GameObject>();
    private bool isInitialized = false;

    /// <summary>
    /// Awake, реализующий паттерн синглтон.
    /// </summary>
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Start, вызывается при старте. Пытается инициализировать EntityManager
    /// и назначает обработчик на кнопку закрытия.
    /// </summary>
    void Start()
    {
        TryInitialize();
        closeButton.onClick.AddListener(OnCloseButtonPressed);
        mainPanel.SetActive(false);
    }

    /// <summary>
    /// Безопасно инициализирует EntityManager, если он еще не был получен.
    /// </summary>
    private void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            isInitialized = true;
        }
    }

    /// <summary>
    /// Показывает панель UI и обновляет список маршрутов.
    /// </summary>
    public void Show()
    {
        TryInitialize();
        if (!isInitialized) return;
        mainPanel.SetActive(true);
        RefreshRoutesList();
    }

    /// <summary>
    /// Скрывает панель UI.
    /// </summary>
    public void Hide()
    {
        mainPanel.SetActive(false);
    }

    /// <summary>
    /// Обработчик нажатия на кнопку закрытия панели.
    /// </summary>
    private void OnCloseButtonPressed()
    {
        Hide();
        GameBridge.Instance?.HandleUICloseAction();
    }

    /// <summary>
    /// Ключевой метод, который обновляет UI. Он запрашивает у ECS все сущности-маршруты
    /// и для каждого из них создает и настраивает соответствующий элемент в списке UI.
    /// </summary>
    private void RefreshRoutesList()
    {
        if (!isInitialized) return;

        foreach (var item in activeRouteItems) Destroy(item);
        activeRouteItems.Clear();

        var query = entityManager.CreateEntityQuery(typeof(RouteDefinition));
        using var routes = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var routeEntity in routes)
        {
            var routeDef = entityManager.GetComponentData<RouteDefinition>(routeEntity);
            string startName = GetOwnerBuildingName(routeDef.StartConnector);
            string endName = GetOwnerBuildingName(routeDef.EndConnector);
            string routeName = $"{startName}  ->  {endName}";

            var itemData = ItemRegistry.Instance.GetItemData(routeDef.ItemID);
            
            GameObject itemGO = Instantiate(routeListItemPrefab, routesListContent);
            itemGO.GetComponent<RouteListItemUI>().Initialize(entityManager, routeEntity, routeName, itemData);
            activeRouteItems.Add(itemGO);
        }
    }

    /// <summary>
    /// Вспомогательный метод для получения читаемого имени здания-владельца коннектора.
    /// </summary>
    /// <param name="connectorEntity">Сущность коннектора.</param>
    /// <returns>Имя здания или стандартная строка, если имя не найдено.</returns>
    private string GetOwnerBuildingName(Entity connectorEntity)
    {
        if (!isInitialized || !entityManager.Exists(connectorEntity) || !entityManager.HasComponent<Conveyor.ConveyorConnector>(connectorEntity))
        {
            return "Неизвестно";
        }
        Entity owner = entityManager.GetComponentData<Conveyor.ConveyorConnector>(connectorEntity).Owner;
        if (!entityManager.Exists(owner)) return "Неизвестно";

        if (entityManager.HasComponent<BuildingName>(owner))
        {
            return entityManager.GetComponentData<BuildingName>(owner).Value.ToString();
        }
        else
        {
            return $"Здание {owner.Index}";
        }
    }

    /// <summary>
    /// Вызывается из элемента списка UI, когда пользователь хочет выбрать предмет для маршрута.
    /// Открывает UI инвентаря для выбора ресурса из здания-источника.
    /// </summary>
    /// <param name="routeEntity">Целевой маршрут для настройки.</param>
    public void RequestSourceInventoryForRoute(Entity routeEntity)
    {
        if (!isInitialized || !entityManager.Exists(routeEntity)) return;

        var routeDef = entityManager.GetComponentData<RouteDefinition>(routeEntity);
        var ownerBuilding = entityManager.GetComponentData<ConveyorConnector>(routeDef.StartConnector).Owner;

        if (entityManager.Exists(ownerBuilding))
        {
            currentRouteToConfigure = routeEntity;
            Hide();

            // Открываем UI выбора предметов, передавая ему колбэк на случай успешного выбора.
            if (entityManager.HasComponent<OutputInventoryCapacity>(ownerBuilding))
            {
                TradeUI.Instance.ShowForItemSelection(ownerBuilding, InventoryType.Output, OnResourceSelectedForRoute);
            }
            else if (entityManager.HasComponent<InventoryProperties>(ownerBuilding))
            {
                TradeUI.Instance.ShowForItemSelection(ownerBuilding, InventoryType.General, OnResourceSelectedForRoute);
            }
            else
            {
                Show();
            }
        }
    }

    /// <summary>
    /// Коллбэк, который вызывается после того, как пользователь выбрал предмет в `TradeUI`.
    /// Создает ECS-сущность-запрос для изменения предмета на маршруте.
    /// </summary>
    /// <param name="selectedItem">Выбранный предмет.</param>
    private void OnResourceSelectedForRoute(Item selectedItem)
    {
        if (selectedItem != null)
        {
            // Создаем запрос, который будет обработан ConveyorRoutesUISystem.
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new SetRouteItemRequest
            {
                RouteEntity = currentRouteToConfigure,
                NewItemID = selectedItem.itemID
            });
        }
        Show(); // Возвращаемся к списку маршрутов.
    }
}