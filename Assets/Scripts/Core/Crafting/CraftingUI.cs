using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// Управляет UI-окном крафта.
/// Этот компонент является частью гибридной ECS-архитектуры: он читает данные из мира ECS для отображения
/// и отправляет сущности-запросы для выполнения действий, но не содержит основной игровой логики.
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

    // Хранит ссылки на все созданные UI-слоты для последующего обновления.
    private List<RecipeSlot> activeSlots = new List<RecipeSlot>();

    void Start()
    {
        TryInitialize();
        if (isInitialized)
        {
            RefreshRecipes();
        }
    }
    
    /// <summary>
    /// Вызывается каждый кадр после основного цикла Update.
    /// Используется для обновления визуального состояния слотов (например, доступности кнопок),
    /// чтобы UI всегда отражал актуальное состояние инвентаря игрока после всех изменений за кадр.
    /// </summary>
    void LateUpdate()
    {
        // Прекращаем выполнение, если окно неактивно или система не готова.
        if (!gameObject.activeInHierarchy || !isInitialized) return;

        // Проходим по каждому отображаемому рецепту и обновляем его статус.
        foreach (var slot in activeSlots)
        {
            if (slot != null && slot.GetRecipe() != null)
            {
                // Проверяем, может ли игрок создать предмет по этому рецепту прямо сейчас.
                bool canCraft = CanCraft(slot.GetRecipe());
                // Обновляем визуальное состояние слота (например, делаем кнопку серой, если крафт недоступен).
                slot.SetCraftableStatus(canCraft);
            }
        }
    }
    
    /// <summary>
    /// Выполняет отложенную инициализацию, получая ссылки на EntityManager и сущность игрока.
    /// Этот подход необходим в гибридной архитектуре, так как ECS-мир может быть не готов в момент вызова Start().
    /// </summary>
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
    
    /// <summary>
    /// Загружает все рецепты из папки Resources и создает для каждого из них UI-элемент.
    /// </summary>
    public void RefreshRecipes()
    {
        if (!isInitialized) return;
        
        // Очищаем старые UI-элементы перед обновлением.
        foreach (Transform child in recipesParent)
        {
            Destroy(child.gameObject);
        }
        activeSlots.Clear();

        // Загружаем все ассеты типа CraftingRecipe по указанному пути.
        CraftingRecipe[] recipes = Resources.LoadAll<CraftingRecipe>(recipesPath);
        
        // Создаем и настраиваем UI-слот для каждого найденного рецепта.
        foreach (var recipe in recipes)
        {
            GameObject slotGO = Instantiate(recipePrefab, recipesParent);
            RecipeSlot recipeSlot = slotGO.GetComponent<RecipeSlot>();
            recipeSlot.Setup(recipe, this);
            activeSlots.Add(recipeSlot); 
        }
    }
    
    /// <summary>
    /// Выполняет проверку наличия необходимых ресурсов в инвентаре игрока.
    /// Это операция только для чтения, она не изменяет данные.
    /// </summary>
    private bool CanCraft(CraftingRecipe recipe)
    {
        if (!isInitialized || !entityManager.HasBuffer<InventoryItemElement>(playerEntity)) return false;

        // Получаем временную копию инвентаря игрока для безопасной итерации.
        var playerInventory = entityManager.GetBuffer<InventoryItemElement>(playerEntity).ToNativeArray(Allocator.Temp);
        
        // Проверяем каждое требование из рецепта.
        for (int i = 0; i < recipe.requiredItems.Count; i++)
        {
            int requiredAmount = recipe.requiredAmounts[i];
            int currentAmount = 0;
            
            // Суммируем количество нужного предмета по всем слотам инвентаря.
            foreach (var item in playerInventory)
            {
                if (item.ItemID == recipe.requiredItems[i].itemID)
                {
                    currentAmount += item.Amount;
                }
            }

            // Если хотя бы одного ресурса не хватает, прекращаем проверку.
            if (currentAmount < requiredAmount)
            {
                playerInventory.Dispose();
                return false;
            }
        }
        
        // Очищаем временный массив и возвращаем успешный результат.
        playerInventory.Dispose();
        return true;
    }

    /// <summary>
    /// Пытается инициировать процесс крафта.
    /// Метод не выполняет логику крафта напрямую, а создает сущность-запрос,
    /// которую затем обработают соответствующие ECS-системы.
    /// </summary>
    public void TryCraft(CraftingRecipe recipe)
    {
        if (CanCraft(recipe))
        {
            // Создаем пустую сущность, которая будет служить запросом.
            var craftRequestEntity = entityManager.CreateEntity();
            
            // Добавляем к ней компонент-запрос с основной информацией о крафте.
            entityManager.AddComponentData(craftRequestEntity, new StartCraftingRequest
            {
                Crafter = playerEntity,
                ResultItemID = recipe.resultItem.itemID,
                ResultAmount = recipe.resultAmount,
                CraftingTime = recipe.craftingTime
            });
            
            // Добавляем к запросу динамический буфер со списком требуемых ингредиентов.
            var requiredItemsBuffer = entityManager.AddBuffer<RequiredCraftingItem>(craftRequestEntity);
            for (int i = 0; i < recipe.requiredItems.Count; i++)
            {
                requiredItemsBuffer.Add(new RequiredCraftingItem
                {
                    ItemID = recipe.requiredItems[i].itemID,
                    Amount = recipe.requiredAmounts[i]
                });
            }
        }
        else
        {
            // Если ресурсов не хватает, создаем другую сущность-запрос на отображение уведомления для игрока.
            var notificationEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(notificationEntity, new UINotificationRequest
            {
                Message = "Недостаточно ресурсов для крафта!"
            });
        }
    }
}