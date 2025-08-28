using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring-компонент, который добавляет тег ItemVisualTag к префабу
/// визуала предмета на конвейере.
/// </summary>
public class ConveyorItemVisualAuthoring : MonoBehaviour
{
    class Baker : Baker<ConveyorItemVisualAuthoring>
    {
        public override void Bake(ConveyorItemVisualAuthoring authoring)
        {
            // ВАЖНО: используем Dynamic, чтобы у сущности был LocalTransform и её можно было двигать
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Conveyor.ItemVisualTag>(entity);
        }
    }
}
