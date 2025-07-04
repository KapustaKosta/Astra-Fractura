using Unity.Entities;
using UnityEngine;

/// <summary>
/// Простой Authoring-компонент, единственная задача которого -
/// добавить в ECS-мир сущность с компонентом GameState,
/// чтобы она стала доступна как синглтон.
/// </summary>
public class GameStateAuthoring : MonoBehaviour
{
    class Baker : Baker<GameStateAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания" данных из MonoBehaviour в ECS-сущности.
        /// Создает сущность GameState и инициализирует ее значения по умолчанию.
        /// </summary>
        /// <param name="authoring">Экземпляр GameStateAuthoring.</param>
        public override void Bake(GameStateAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new GameState
            {
                CurrentMode = GameMode.Default,
                BuildingPrefabToPlace = Entity.Null,
                BuildingItemID = 0
            });
        }
    }
}