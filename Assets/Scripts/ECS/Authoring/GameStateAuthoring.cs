using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring-компонент для создания глобальной сущности-синглтона.
/// </summary>
public class GameStateAuthoring : MonoBehaviour
{
    class Baker : Baker<GameStateAuthoring>
    {
        public override void Bake(GameStateAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            // Добавляем маркер, чтобы легко находить эту сущность
            AddComponent<GameState>(entity);
            
            // Устанавливаем начальное состояние игры
            AddComponent<InDefaultMode>(entity);
        }
    }
}