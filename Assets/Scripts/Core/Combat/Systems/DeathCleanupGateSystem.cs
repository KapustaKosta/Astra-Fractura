using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DeathSystem))]
public partial struct DeathCleanupGateSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DeathTimer>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float dt = SystemAPI.Time.DeltaTime;

        // Тикаем таймеры
        foreach (var timerRW in SystemAPI.Query<RefRW<DeathTimer>>())
            timerRW.ValueRW.TimeSinceDeath += dt;

        foreach (var traceRW in SystemAPI.Query<RefRW<DeathTrace>>())
            traceRW.ValueRW.Elapsed += dt;

        // Лишь инициируем DropRequested при наступлении "момента диспауна"
        foreach (var (timerRW, entity) in SystemAPI.Query<RefRW<DeathTimer>>()
                     .WithAll<IsDeadTag>()
                     .WithEntityAccess())
        {
            ref var timer = ref timerRW.ValueRW;

            if (timer.TimeSinceDeath >= timer.TraceDuration && timer.DropDone == 0)
            {
                if (!SystemAPI.HasComponent<DropRequested>(entity))
                    ecb.AddComponent<DropRequested>(entity);

                timer.DropDone = 1; // чтобы не повторять
            }

            // Больше ничего не делаем — выключит и отметит к чистке DropOnDeathSystem
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}