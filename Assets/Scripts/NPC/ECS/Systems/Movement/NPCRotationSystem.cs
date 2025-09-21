using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics.Systems;

// Поворот после физики, чтобы не трогать трансформ в середине шага физики
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]                  
public partial class NPCRotationSystem : SystemBase
{
    [BurstCompile]
    private partial struct FaceMoveDirJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(ref LocalTransform transform, in NPCMovementComponent move)
        {
            float2 xz = new float2(move.TargetVelocity.x, move.TargetVelocity.z);
            if (math.lengthsq(xz) < 0.01f) return;

            float3 forward = math.normalize(new float3(move.TargetVelocity.x, 0, move.TargetVelocity.z));
            quaternion targetRot = quaternion.LookRotationSafe(forward, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot, math.saturate(move.RotationSpeed * DeltaTime));
        }
    }

    protected override void OnUpdate()
    {
        new FaceMoveDirJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
    }
}