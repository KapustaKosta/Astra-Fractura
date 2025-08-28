using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring-компонент для создания сущностей зданий в ECS.
/// Позволяет связывать GameObject-префабы зданий с их ID предметов
/// для использования в системе строительства.
/// </summary>
public class BuildingAuthoring : MonoBehaviour
{
    /// <summary>
    /// GameObject-префаб, который будет преобразован в ECS-сущность.
    /// </summary>
    public GameObject buildingPrefab;

    /// <summary>
    /// Ссылка на ScriptableObject Item, который представляет данное здание в инвентаре.
    /// </summary>
    public Item item;

    /// <summary>
    /// Baker-класс для преобразования BuildingAuthoring в ECS-компоненты.
    /// </summary>
    public class Baker : Baker<BuildingAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания" данных из MonoBehaviour в ECS-сущности.
        /// </summary>
        /// <param name="authoring">Экземпляр BuildingAuthoring.</param>
        public override void Bake(BuildingAuthoring authoring)
        {
            if (authoring.buildingPrefab == null)
                return;

            Entity prefabEntity = GetEntity(authoring.buildingPrefab, TransformUsageFlags.Dynamic);

            var registryEntity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddComponent(registryEntity, new BuildingPrefabReference
            {
                EntityPrefab = prefabEntity,
                ItemID = authoring.item.itemID
            });
        }
    }
}

/// <summary>
/// ECS-компонент, который хранит ссылку на Entity-префаб здания
/// и соответствующий ID предмета. Используется для разрешения ID предмета
/// в реальные ECS-префабы для инстанцирования.
/// </summary>
public struct BuildingPrefabReference : IComponentData
{
    /// <summary>
    /// ECS-сущность, представляющая префаб здания.
    /// </summary>
    public Entity EntityPrefab;

    /// <summary>
    /// ID предмета, соответствующего этому зданию.
    /// </summary>
    public int ItemID;
}