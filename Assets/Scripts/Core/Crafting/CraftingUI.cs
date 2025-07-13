using UnityEngine;
using Unity.Entities;
using Unity.Collections;

/// <summary>
/// UI для крафта. Теперь работает с ECS-инвентарем игрока.
/// </summary>
public class CraftingUI : MonoBehaviour
{
    [SerializeField] private Transform recipesParent;
    [SerializeField] private GameObject recipePrefab;
    [Header("Data Settings")]
    [Tooltip("Путь к папке с рецептами внутри папки Resources")]
    [SerializeField] private string recipesPath = "CraftingRecipes";

    private EntityManager entityManager;
    private Entity playerEntity;
    private bool isInitialized = false;

    void Start()
    {
        TryInitialize();
        if (isInitialized)
        {
            RefreshRecipes();
        }
    }
    
    private void TryInitialize()
    {
        if (isInitialized) return;
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerControllerData));
            if (!playerQuery.IsEmpty)
            {
                playerEntity = playerQuery.GetSingletonEntity();
                isInitialized = true;
            }
        }
    }
    
    public void RefreshRecipes()
    {
        if (!isInitialized) return;
        
        foreach (Transform child in recipesParent)
        {
            Destroy(child.gameObject);
        }

        CraftingRecipe[] recipes = Resources.LoadAll<CraftingRecipe>(recipesPath);
        
        foreach (var recipe in recipes)
        {
            GameObject slot = Instantiate(recipePrefab, recipesParent);
            RecipeSlot recipeSlot = slot.GetComponent<RecipeSlot>();
            // Передаем this для обратного вызова
            recipeSlot.Setup(recipe, this);
        }
    }
    
    /// <summary>
    /// Проверяет, можно ли создать предмет по рецепту, читая инвентарь игрока из ECS.
    /// </summary>
    private bool CanCraft(CraftingRecipe recipe)
    {
        if (!isInitialized || !entityManager.HasBuffer<InventoryItemElement>(playerEntity)) return false;

        var playerInventory = entityManager.GetBuffer<InventoryItemElement>(playerEntity).ToNativeArray(Allocator.Temp);
        
        for (int i = 0; i < recipe.requiredItems.Count; i++)
        {
            int requiredAmount = recipe.requiredAmounts[i];
            int currentAmount = 0;
            
            // Суммируем все предметы нужного типа в инвентаре
            foreach (var item in playerInventory)
            {
                if (item.ItemID == recipe.requiredItems[i].itemID)
                {
                    currentAmount += item.Amount;
                }
            }

            if (currentAmount < requiredAmount)
            {
                playerInventory.Dispose();
                return false;
            }
        }
        
        playerInventory.Dispose();
        return true;
    }

    public void TryCraft(CraftingRecipe recipe)
    {
        if (CanCraft(recipe))
        {
            // Создаем запросы на удаление требуемых предметов
            for (int i = 0; i < recipe.requiredItems.Count; i++)
            {
                var removeRequest = entityManager.CreateEntity();
                entityManager.AddComponentData(removeRequest, new RemoveItemRequest
                {
                    TargetInventoryOwner = playerEntity,
                    ItemID = recipe.requiredItems[i].itemID,
                    Amount = recipe.requiredAmounts[i]
                });
            }
            
            // Создаем запрос на добавление результирующего предмета
            var addRequest = entityManager.CreateEntity();
            entityManager.AddComponentData(addRequest, new AddItemRequest
            {
                TargetInventoryOwner = playerEntity,
                ItemID = recipe.resultItem.itemID,
                Amount = recipe.resultAmount
            });
            
            // UI обновится не сразу, а в следующем кадре, когда сработает InventorySystem.
            // Можно обновить его принудительно, если нужно мгновенное отображение.
        }
        else
        {
            Debug.Log("Не хватает ресурсов!");
        }
    }
}