using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

public struct VisualUpAxis : IComponentData
{
    // 0 = X, 1 = Y, 2 = Z; Sign: +1 или -1
    public sbyte Axis;
    public sbyte Sign;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
public partial class InitDroppedVisualsSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Получаем синглтон с настройками
        if (!SystemAPI.TryGetSingleton<CombatSystemConfig>(out var config))
            return;

        Entities
            .WithoutBurst()
            .ForEach((Entity visual, ref PendingDroppedVisualInit init) =>
            {
                // 1) Базовый LT
                var lt = LocalTransform.FromPosition(init.Position);

                // 2) Встать «ровно»: сопоставить локальную ось префаба с мировым Up
                float3 localUp = new float3(0, 1, 0);
                if (SystemAPI.HasComponent<VisualUpAxis>(visual))
                {
                    var up = SystemAPI.GetComponent<VisualUpAxis>(visual);
                    localUp = up.Axis switch
                    {
                        0 => new float3(1, 0, 0),
                        1 => new float3(0, 1, 0),
                        2 => new float3(0, 0, 1),
                        _ => new float3(0, 1, 0)
                    };
                    if (up.Sign < 0) localUp = -localUp;
                }
                var corr = FromToRotationSafe(localUp, math.up()); // выравниваем «ось вверх»
                lt.Rotation = math.mul(corr, lt.Rotation);

                if (SystemAPI.HasComponent<LocalTransform>(visual))
                    ecb.SetComponent(visual, lt);
                else
                    ecb.AddComponent(visual, lt);

                // 3) Инициализация крутилки
                if (SystemAPI.HasComponent<ItemVisualRotator>(visual))
                    ecb.SetComponent(visual, new ItemVisualRotator { Speed = config.DefaultItemRotatorSpeed });
                else
                    ecb.AddComponent(visual, new ItemVisualRotator { Speed = config.DefaultItemRotatorSpeed });

                // 4) Линки логика <-> визуал
                if (SystemAPI.Exists(init.Logical))
                {
                    if (SystemAPI.HasComponent<LogicalItemHasVisual>(init.Logical))
                        ecb.SetComponent(init.Logical, new LogicalItemHasVisual { VisualEntity = visual });
                    else
                        ecb.AddComponent(init.Logical, new LogicalItemHasVisual { VisualEntity = visual });
                }

                if (SystemAPI.HasComponent<VisualFor>(visual))
                    ecb.SetComponent(visual, new VisualFor { LogicalEntity = init.Logical });
                else
                    ecb.AddComponent(visual, new VisualFor { LogicalEntity = init.Logical });

                // 5) Лёгкий импульс при наличии физики
                if (SystemAPI.HasComponent<PhysicsVelocity>(visual))
                {
                    var rng = Unity.Mathematics.Random.CreateFromIndex(init.Seed);
                    float3 dir = rng.NextFloat3Direction(); dir.y = math.abs(dir.y);
                    
                    // Используем значения из конфига
                    float impulse = rng.NextFloat(config.DroppedItemImpulseRange.x, config.DroppedItemImpulseRange.y);
                    float angularImpulse = rng.NextFloat(config.DroppedItemAngularVelocityRange.x, config.DroppedItemAngularVelocityRange.y);
                    
                    ecb.SetComponent(visual, new PhysicsVelocity
                    {
                        Linear  = dir * impulse,
                        Angular = rng.NextFloat3Direction() * angularImpulse
                    });
                }

                // Готово
                ecb.RemoveComponent<PendingDroppedVisualInit>(visual);
            })
            .Run();
    }

    // Кватернион, поворачивающий вектор 'from' в 'to' (обе нормализуем)
    static quaternion FromToRotationSafe(float3 from, float3 to)
    {
        float3 f = math.normalize(from);
        float3 t = math.normalize(to);
        float d = math.dot(f, t);

        if (d >= 1f - 1e-6f) return quaternion.identity;
        if (d <= -1f + 1e-6f)
        {
            float3 axis = math.normalize(math.cross(new float3(1, 0, 0), f));
            if (math.lengthsq(axis) < 1e-6f)
                axis = math.normalize(math.cross(new float3(0, 1, 0), f));
            return quaternion.AxisAngle(axis, math.PI);
        }

        float s = math.sqrt((1f + d) * 2f);
        float invs = 1f / s;
        float3 c = math.cross(f, t);
        return new quaternion(c.x * invs, c.y * invs, c.z * invs, 0.5f * s);
    }
}