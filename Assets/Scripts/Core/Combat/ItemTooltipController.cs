using Unity.Entities;
using UnityEngine;
using TMPro;

public class ItemTooltipController : MonoBehaviour
{
    public GameObject tooltipPanel;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemAmountText;

    private EntityManager entityManager;
    private EntityQuery hoveredItemQuery;
    private ItemRegistry itemRegistry;

    void Start()
    {
        // Убедитесь, что панель скрыта при старте
        if(tooltipPanel != null) tooltipPanel.SetActive(false);

        // Получаем доступ к миру ECS
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        
        entityManager = world.EntityManager;
        hoveredItemQuery = entityManager.CreateEntityQuery(typeof(HoveredItem));
        itemRegistry = ItemRegistry.Instance;
    }

    void LateUpdate()
    {
        if (entityManager == null || itemRegistry == null) return;
        
        // Проверяем, существует ли синглтон HoveredItem
        if (!hoveredItemQuery.IsEmpty)
        {
            var hoveredItem = hoveredItemQuery.GetSingleton<HoveredItem>();
            var itemData = itemRegistry.GetItemData(hoveredItem.ItemID);

            if (itemData != null)
            {
                if(!tooltipPanel.activeSelf) tooltipPanel.SetActive(true);
                
                itemNameText.text = itemData.itemName;
                itemAmountText.text = $"Amount: {hoveredItem.Amount}";
            }
        }
        else
        {
            if(tooltipPanel.activeSelf) tooltipPanel.SetActive(false);
        }
    }
}