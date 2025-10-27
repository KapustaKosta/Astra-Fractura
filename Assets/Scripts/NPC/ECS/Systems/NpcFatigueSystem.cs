using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NpcFatigueSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        Entities
            .WithAll<NPCBrain>() // Обрабатываем только NPC
            .ForEach((Entity entity, ref NpcVitalsComponent vitals, in NpcVitalsConfig config) =>
            {
                bool isWorking = false;
                
                // Проверяем наличие тегов, указывающих на активную работу, а не просто перемещение или бездействие.
                // Это более надежно, чем проверять тип цели, так как цель может быть "Harvest",
                // но NPC еще только идет к ресурсу.
                if (SystemAPI.HasComponent<WantsToHarvestTag>(entity) ||
                    SystemAPI.HasComponent<InsideBuildingTag>(entity) ||
                    SystemAPI.HasComponent<IsAttackingTag>(entity) ||
                    SystemAPI.HasComponent<IsUnloadingTag>(entity))
                {
                    isWorking = true;
                }

                if (isWorking)
                {
                    // Рассчитываем убывание в секунду из минутного значения
                    float decayPerSecond = config.FatigueDecayPerMinuteWhileWorking / 60f;
                    vitals.CurrentFatigue -= decayPerSecond * deltaTime;
                    // Убедимся, что усталость не уходит в минус
                    vitals.CurrentFatigue = math.max(0, vitals.CurrentFatigue);
                }

            }).Schedule();
    }
}