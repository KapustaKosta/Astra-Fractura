using UnityEngine;
using Unity.Entities;
using System.Text;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform slotsParent;
    public GameObject slotPrefab;
    public GameObject inventoryPanel;

    private InventorySlot[] slots;
    private EntityManager entityManager;
    private EntityQuery playerQuery;
    private Entity currentTargetEntity;
    private bool isInitialized = false;

    void Start()
    {
        TryInitialize();
    }
    
    private void OnDestroy()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot != null) slot.OnSlotClicked -= HandleSlotClicked;
        }
    }

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
            if (shouldBeOpen)
            {
                if (!playerQuery.IsEmpty)
                {
                    Show(playerQuery.GetSingletonEntity());
                }
            }
            else
            {
                Hide();
            }
        }
        else if (inventoryPanel.activeSelf)
        {
            UpdateUI();
        }
    }

    private void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated) return;

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        playerQuery = entityManager.CreateEntityQuery(typeof(PlayerControllerData));
        
        if (inventoryPanel == null || slotsParent == null || slotPrefab == null)
        {
            return;
        }

        isInitialized = true;
        Hide();
    }
    
    public void RequestToggleInventory()
    {
        if (!isInitialized) return;
        var toggleEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(toggleEntity, new ToggleInventoryRequest());
    }

    public void Show(Entity targetEntity)
    {
        if (!isInitialized || targetEntity == Entity.Null || !entityManager.Exists(targetEntity) || !entityManager.HasComponent<HasInventoryTag>(targetEntity))
        {
            Hide();
            return;
        }
        
        inventoryPanel.SetActive(true);
        currentTargetEntity = targetEntity;
        
        CreateOrVerifySlots(targetEntity);
        UpdateUI();
    }

    public void Hide()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
        currentTargetEntity = Entity.Null;
    }
    
    private void CreateOrVerifySlots(Entity targetEntity)
    {
        if (!entityManager.HasComponent<InventoryProperties>(targetEntity))
        {
             return;
        }
        
        int requiredCapacity = entityManager.GetComponentData<InventoryProperties>(targetEntity).Capacity;

        if (slots != null && slots.Length == requiredCapacity)
        {
            return;
        }
        
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }
        if (slots != null)
        {
             foreach (var slot in slots)
             {
                 if (slot != null) slot.OnSlotClicked -= HandleSlotClicked;
             }
        }
        
        slots = new InventorySlot[requiredCapacity];
        for (int i = 0; i < requiredCapacity; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsParent);
            if (slotGO == null) { return; }
            
            InventorySlot slotComponent = slotGO.GetComponent<InventorySlot>();
            if (slotComponent != null)
            {
                slots[i] = slotComponent;
                slotComponent.OnSlotClicked += HandleSlotClicked;
            }
        }
    }

    public void UpdateUI()
    {
        if (!isInitialized || currentTargetEntity == Entity.Null || slots == null)
        {
            return;
        }
        
        var itemBuffer = entityManager.GetBuffer<InventoryItemElement>(currentTargetEntity);

        for (int i = 0; i < slots.Length; i++)
        {
            // Эта проверка больше не нужна, но она не мешает.
            if (slots[i] == null) continue;
            
            if (i < itemBuffer.Length)
            {
                var itemElement = itemBuffer[i];
                // Этот вызов теперь полностью безопасен.
                var itemData = ItemRegistry.Instance.GetItemData(itemElement.ItemID);
                
                slots[i].SetupSlot(itemData, itemElement.Amount);
            }
            else
            {
                slots[i].ClearSlot();
            }
        }
    }
    
    private void HandleSlotClicked(Item clickedItem)
    {
        if (clickedItem == null || !isInitialized) return;

        if (clickedItem.itemType == ItemType.Building)
        {
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new EnterBuildingModeRequest { ItemID = clickedItem.itemID });
        }
        else if (clickedItem.itemType == ItemType.Consumable)
        {
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new RemoveItemRequest
            {
                TargetInventoryOwner = currentTargetEntity,
                ItemID = clickedItem.itemID,
                Amount = 1
            });
        }
    }
}