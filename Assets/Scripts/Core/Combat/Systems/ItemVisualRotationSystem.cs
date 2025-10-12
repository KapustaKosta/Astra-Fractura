// Assets/Scripts/Core/Items/Systems/ItemVisualRotationSystem.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Маркирует визуал предмета, который должен крутиться вокруг своей оси.
/// </summary>
public struct ItemVisualRotator : IComponentData
{
    /// <summary>Скорость вращения в градусах в секунду.</summary>
    public float Speed;
}
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class ItemVisualRotationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        Entities
            .WithNone<Disabled>()
            .ForEach((ref LocalTransform transform, in ItemVisualRotator rotator) =>
            {
                var rot = quaternion.AxisAngle(math.up(), math.radians(rotator.Speed * dt));
                transform.Rotation = math.mul(transform.Rotation, rot);
            })
            .ScheduleParallel();
    }
}