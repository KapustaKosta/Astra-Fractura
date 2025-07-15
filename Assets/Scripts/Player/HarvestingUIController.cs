using UnityEngine;
using Unity.Entities;
using TMPro;

/// <summary>
/// Управляет UI-элементом, который показывает информацию о текущей добыче.
/// Полностью управляется данными из ECS, читая состояние игрока каждый кадр.
/// </summary>
public class HarvestingUIController : MonoBehaviour
{
    [Header("UI & Data References")]
    [Tooltip("Текстовый элемент для отображения статуса добычи")]
    [SerializeField] private TextMeshProUGUI harvestText;
    
    [Tooltip("ScriptableObject для сопоставления типа ресурса с названием предмета")]
    [SerializeField] private ResourceItemMapping resourceItemMapping;

    private EntityManager entityManager;
    private Entity playerEntity;
    private bool isInitialized;

    /// <summary>
    /// Вызывается при старте. Гарантирует, что текст изначально скрыт.
    /// </summary>
    void Start()
    {
        if (harvestText != null)
        {
            harvestText.gameObject.SetActive(false);
        }
        else
        {
            enabled = false;
        }
    }

    /// <summary>
    /// Вызывается каждый кадр. Пытается инициализироваться, а затем синхронизирует состояние UI с состоянием игрока в ECS.
    /// </summary>
    void Update()
    {
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }

        // Проверяем, существует ли сущность игрока
        if (!entityManager.Exists(playerEntity))
        {
            isInitialized = false; 
            if (harvestText.gameObject.activeSelf) harvestText.gameObject.SetActive(false);
            return;
        }


        // Ищем намерение добывать (WantsToHarvestTag).
        bool isTryingToHarvest = entityManager.HasComponent<WantsToHarvestTag>(playerEntity);

        // Синхронизируем видимость текстового поля
        if (harvestText.gameObject.activeSelf != isTryingToHarvest)
        {
            harvestText.gameObject.SetActive(isTryingToHarvest);
        }

        // Если UI должен быть виден, обновляем текст
        if (isTryingToHarvest)
        {
            // Чтобы узнать, ЧТО добывается, нам нужна цель из компонента ActiveTarget.
            if (entityManager.HasComponent<ActiveTarget>(playerEntity))
            {
                var targetEntity = entityManager.GetComponentData<ActiveTarget>(playerEntity).Value;
                
                // Убедимся, что цель существует и является ресурсом
                if (entityManager.HasComponent<ResourceNode>(targetEntity))
                {
                    var resourceType = entityManager.GetComponentData<ResourceNode>(targetEntity).resourceType;
                    UpdateHarvestText(resourceType);
                }
                else
                {
                    // Если цель по какой-то причине невалидна, прячем UI, чтобы избежать ошибок
                    harvestText.gameObject.SetActive(false);
                }
            }
            else
            {
                // Если есть намерение, но нет цели - это некорректное состояние. Прячем UI.
                harvestText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Пытается найти EntityManager и сущность игрока.
    /// </summary>
    private void TryInitialize()
    {
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
    /// Обновляет текстовое поле на основе типа добываемого ресурса.
    /// </summary>
    private void UpdateHarvestText(ResourceCollectionType resourceType)
    {
        if (harvestText != null && resourceItemMapping != null)
        {
            Item item = resourceItemMapping.GetItemByResourceType(resourceType);
            string textToDisplay = $"Добывается: {(item != null ? item.itemName : resourceType.ToString())}";
            harvestText.text = textToDisplay;
        }
    }
}