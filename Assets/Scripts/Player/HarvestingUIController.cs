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

        // Проверяем, есть ли у игрока тег IsHarvestingTag
        bool isHarvesting = entityManager.HasComponent<IsHarvestingTag>(playerEntity);

        // Синхронизируем видимость текстового поля
        if (harvestText.gameObject.activeSelf != isHarvesting)
        {
            harvestText.gameObject.SetActive(isHarvesting);
        }

        // Если добываем - обновляем текст
        if (isHarvesting)
        {
            var harvestingTag = entityManager.GetComponentData<IsHarvestingTag>(playerEntity);
            UpdateHarvestText(harvestingTag.ResourceType);
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