using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using System.Collections.Generic;
using Conveyor;
using Game.Production;
using Unity.Collections;

public class ConveyorRoutesUI : MonoBehaviour
{
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

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        TryInitialize();
        closeButton.onClick.AddListener(OnCloseButtonPressed);
        mainPanel.SetActive(false);
    }

    private void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            isInitialized = true;
        }
    }

    public void Show()
    {
        TryInitialize();
        if (!isInitialized) return;
        mainPanel.SetActive(true);
        RefreshRoutesList();
    }

    public void Hide()
    {
        mainPanel.SetActive(false);
    }

    private void OnCloseButtonPressed()
    {
        Hide();
        GameBridge.Instance?.HandleUICloseAction();
    }

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
            bool isActive = entityManager.HasComponent<ActiveRouteTag>(routeEntity);

            GameObject itemGO = Instantiate(routeListItemPrefab, routesListContent);
            itemGO.GetComponent<RouteListItemUI>().Initialize(entityManager, routeEntity, routeName, itemData, isActive);
            activeRouteItems.Add(itemGO);
        }
    }

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

    public void RequestSourceInventoryForRoute(Entity routeEntity)
    {
        if (!isInitialized || !entityManager.Exists(routeEntity)) return;

        var routeDef = entityManager.GetComponentData<RouteDefinition>(routeEntity);
        var ownerBuilding = entityManager.GetComponentData<ConveyorConnector>(routeDef.StartConnector).Owner;

        if (entityManager.Exists(ownerBuilding))
        {
            currentRouteToConfigure = routeEntity;
            Hide();

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

    private void OnResourceSelectedForRoute(Item selectedItem)
    {
        if (selectedItem != null)
        {
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new SetRouteItemRequest
            {
                RouteEntity = currentRouteToConfigure,
                NewItemID = selectedItem.itemID
            });
        }
        Show();
    }
}
