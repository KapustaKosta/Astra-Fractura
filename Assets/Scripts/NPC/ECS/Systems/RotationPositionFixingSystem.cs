using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public partial struct FixRotationPositionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, rotationPositionFixing, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<RotationPositionFixing>>().WithEntityAccess())
        {
            if (rotationPositionFixing.ValueRW.isFixed) continue;

            transform.ValueRW.Rotation = math.mul(
                quaternion.RotateZ(math.radians(90f)),
                transform.ValueRW.Rotation
            );

            transform.ValueRW.Position -= new float3(0, 1f, 0);

            rotationPositionFixing.ValueRW.isFixed = true;
        }
    }
}