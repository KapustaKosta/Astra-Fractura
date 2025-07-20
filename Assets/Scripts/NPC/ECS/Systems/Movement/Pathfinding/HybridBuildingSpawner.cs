using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Спавнер гибридных строений, создающий GameObject-объекты на основе ECS-сущностей.
/// Используется для интеграции ECS-логики с традиционными Unity-объектами.
/// </summary>
public class HybridBuildingSpawner : MonoBehaviour
{
    /// <summary>
    /// Связывает Item с префабом строения для спавна.
    /// Настраивается в инспекторе Unity.
    /// </summary>
    [System.Serializable]
    public class BuildingPrefabEntry
    {
        public Item item;     // Тип строения (определяется через Item)
        public GameObject prefab; // Префаб для спавна
    }

    /// <summary>
    /// Массив связей между типами строений и их префабами.
    /// Настраивается в инспекторе Unity.
    /// </summary>
    public BuildingPrefabEntry[] buildingPrefabs;

    /// <summary>
    /// Метод Update вызывается каждый кадр.
    /// Ищет сущности с меткой SpawnHybridBuildingTag и создает соответствующие GameObject-объекты.
    /// </summary>
    void Update()
    {
        // Получаем доступ к ECS World и EntityManager
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
            return;
        
        var entityManager = world.EntityManager;

        // Создаем запрос на сущности, требующие спавна
        var query = entityManager.CreateEntityQuery(
            typeof(SpawnHybridBuildingTag), 
            typeof(LocalTransform)
        );

        // Получаем массив подходящих сущностей
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            // Дополнительная проверка наличия трансформа
            if (!entityManager.HasComponent<LocalTransform>(entity))
                continue;

            // Получаем данные о спавне и позицию
            var spawnData = entityManager.GetComponentData<SpawnHybridBuildingTag>(entity);
            var transformData = entityManager.GetComponentData<LocalTransform>(entity);

            // Находим соответствующий префаб по ID предмета
            GameObject prefab = null;
            foreach (var entry in buildingPrefabs)
            {
                if (entry == null || entry.item == null)
                    continue;
                
                if (entry.item.itemID == spawnData.BuildingItemID)
                {
                    prefab = entry.prefab;
                    break;
                }
            }

            // Пропускаем, если префаб не найден
            if (prefab == null)
                continue;

            // Создаем объект в указанной позиции и повороте
            var go = Instantiate(prefab, transformData.Position, transformData.Rotation);

            // Удаляем ненужные компоненты для гибридной интеграции:
            // Сохраняем только Transform, MeshRenderer, MeshFilter и NavMeshObstacle
            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp is Transform || 
                    comp is MeshRenderer || 
                    comp is MeshFilter || 
                    comp is UnityEngine.AI.NavMeshObstacle)
                    continue;
                
                Destroy(comp);
            }

            // Удаляем метку спавна, чтобы избежать повторного создания
            entityManager.RemoveComponent<SpawnHybridBuildingTag>(entity);
        }
    }
}