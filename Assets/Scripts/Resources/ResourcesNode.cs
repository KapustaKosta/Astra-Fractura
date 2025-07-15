using UnityEngine;
using Unity.Entities;

/// <summary>
/// MonoBehaviour для разрушаемого ресурсного узла.
/// Этот компонент представляет старую, не-ECS логику, но адаптирован на всякий случай,если перейдем к разрушающимся нодам
/// для работы с новой ECS-системой инвентаря.
/// </summary>
public class ResourcesNode : MonoBehaviour
{
    [System.Serializable]
    public class ResourceDrop
    {
        [Tooltip("Какой предмет выпадает")]
        public Item item;
        [Tooltip("Минимальное количество")]
        public int minAmount = 1;
        [Tooltip("Максимальное количество")]
        public int maxAmount = 1;
    }

    [Tooltip("Что может выпасть после разрушения")]
    public ResourceDrop[] drops; 

    [Tooltip("Здоровье ресурсного узла")]
    public float health = 30f;

    [Tooltip("Какой инструмент нужен (null = можно руками)")]
    public Item requiredTool;

    [Tooltip("Множитель урона от правильного инструмента")]
    public float toolEfficiency = 2f;

    [Tooltip("Тип ресурсов, которые содержит узел (для проверки инструмента)")]
    public ResourceType resourceType;

    public void TakeDamage(float damage, Item toolUsed)
    {
        // Проверяем, есть ли у инструмента флаги для добычи этого ресурса
        if (requiredTool != null && (toolUsed == null || !toolUsed.canHarvest.HasFlag(resourceType)))
        {
            #if UNITY_EDITOR
            Debug.Log($"Нужен правильный инструмент для добычи: {resourceType}");
            #endif
            return;
        }

        // Увеличиваем урон при использовании правильного инструмента
        if (toolUsed == requiredTool)
        {
            damage *= toolEfficiency;
        }

        health -= damage;
        
        Debug.Log($"Ресурс {gameObject.name} получил {damage} урона. Осталось здоровья: {health}");

        if (health <= 0)
        {
            Harvest();
        }
    }

    private void Harvest()
    {
        // Проверяем, доступен ли мир ECS
        if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            #if UNITY_EDITOR
            Debug.LogError("ResourcesNode: Не удалось выдать добычу, так как мир ECS не доступен.");
            #endif
            return;
        }

        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Находим сущность игрока, чтобы указать, кому выдать предметы.
        // Мы ищем по уникальному компоненту игрока, например PlayerControllerData.
        var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerControllerData));
        if (playerQuery.IsEmpty)
        {
            #if UNITY_EDITOR
            Debug.LogError("ResourcesNode: Не удалось найти сущность игрока для выдачи добычи.");
            #endif
            return;
        }
        var playerEntity = playerQuery.GetSingletonEntity();
        
        Debug.Log($"Ресурс {gameObject.name} разрушен. Выдача добычи...");

        // Создаем ECS-запросы на добавление каждого предмета из списка добычи
        foreach (var drop in drops)
        {
            if (drop.item == null) continue;

            int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
            if (amount > 0)
            {
                // Создаем новую сущность-запрос
                var requestEntity = entityManager.CreateEntity();
                
                // Добавляем к ней компонент-запрос с данными
                entityManager.AddComponentData(requestEntity, new AddItemRequest
                {
                    TargetInventoryOwner = playerEntity,
                    ItemID = drop.item.itemID,
                    Amount = amount
                });
                #if UNITY_EDITOR
                Debug.Log($"Создан запрос на выдачу {amount}x '{drop.item.itemName}' (ID: {drop.item.itemID}) игроку.");
                #endif
            }
        }
        
        // Уничтожаем GameObject с ресурсом
        Destroy(gameObject);
    }
}