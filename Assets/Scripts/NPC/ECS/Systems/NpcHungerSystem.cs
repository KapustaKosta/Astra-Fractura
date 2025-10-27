using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NpcHungerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        Entities
            .WithAll<NPCBrain>() // Обрабатываем только NPC
            .ForEach((ref NpcVitalsComponent vitals, in NpcVitalsConfig config) =>
            {
                // Рассчитываем убывание в секунду из минутного значения
                float decayPerSecond = config.HungerDecayPerMinute / 60f;
                vitals.CurrentHunger -= decayPerSecond * deltaTime;
                // Убедимся, что голод не уходит в минус
                vitals.CurrentHunger = math.max(0, vitals.CurrentHunger);

            }).ScheduleParallel();
    }
}