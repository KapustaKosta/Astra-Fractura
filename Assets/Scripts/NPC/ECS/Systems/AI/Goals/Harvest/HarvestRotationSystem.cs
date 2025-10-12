using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics.Systems;

/// <summary>
/// Дополнительная система ротации для NPC, которые находятся у цели сбора ресурсов.
/// Когда сущность имеет тег IsAtHarvestTargetTag и активную цель Harvest, NPC
/// поворачивается к источнику ресурса, чтобы демонстрировать ориентированное
/// взаимодействие. Эта система работает после стандартной системы поворота,
/// поэтому она переопределяет результат, если необходимо.
/// </summary>
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateAfter(typeof(NPCRotationSystem))]
public partial class HarvestRotationSystem : SystemBase
{
    // Буфер для получения мирового трансформа цели. Используем LocalToWorld,
    // чтобы избежать aliasing с записью LocalTransform текущего NPC. Чтение
    // LocalToWorld безопасно, так как этот компонент обновляется другой системой
    // и не модифицируется здесь.
    private ComponentLookup<LocalToWorld> _targetLocalToWorld;

    protected override void OnCreate()
    {
        // Инициализируем lookup для LocalToWorld с чтением
        _targetLocalToWorld = GetComponentLookup<LocalToWorld>(true);
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        // Обновляем lookup перед использованием в джобе
        _targetLocalToWorld.Update(this);

        // Планируем джоб на поворот к источнику. Джоб будет выполняться параллельно
        // для всех NPC, которые достигли точки сбора (IsAtHarvestTargetTag)
        new FaceHarvestJob
        {
            DeltaTime       = dt,
            TargetLocalToWorld = _targetLocalToWorld
        }.ScheduleParallel();
    }

    [BurstCompile]
    private partial struct FaceHarvestJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public ComponentLookup<LocalToWorld> TargetLocalToWorld;

        public void Execute(ref LocalTransform transform,
                            in NPCMovementComponent movement,
                            in ActiveGoal goal,
                            in IsAtHarvestTargetTag atHarvest)
        {
            // Обрабатываем только сущности с целью Harvest и наличием цели
            if (goal.Type != GoalType.Harvest || goal.Target == Entity.Null)
                return;
            // Убеждаемся, что существует мировая матрица цели
            if (!TargetLocalToWorld.HasComponent(goal.Target))
                return;

            // Получаем позицию цели через LocalToWorld
            var targetTf = TargetLocalToWorld[goal.Target];
            float3 toTarget = targetTf.Position - transform.Position;
            float3 forward  = new float3(toTarget.x, 0f, toTarget.z);

            if (math.lengthsq(forward) < 1e-6f)
                return;

            forward = math.normalize(forward);
            // Получаем скорость поворота из динамического компонента движения
            float rotSpeed = movement.RotationSpeed;
            // Ориентация на цель с плавным изменением
            quaternion targetRot = quaternion.LookRotationSafe(forward, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot,
                                             math.saturate(rotSpeed * DeltaTime));
        }
    }
}