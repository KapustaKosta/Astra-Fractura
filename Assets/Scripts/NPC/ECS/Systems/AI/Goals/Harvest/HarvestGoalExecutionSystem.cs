// HarvestGoalExecutionSystem.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Исполнение цели "Сбор ресурсов".
/// Добыча стартует ТОЛЬКО после реального прибытия и полной остановки.
/// Используем IsAtHarvestTargetTag как единственный валидный триггер старта,
/// и дополнительно проверяем отсутствие движения (HasTarget==false и скорости ~0).
/// Введён гистерезис, чтобы не дёргать состояние при мелких колебаниях.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCTaskCleanupSystem))]
public partial class HarvestGoalExecutionSystem : SystemBase
{
    // Небольшие пороги, чтобы отсечь «дрожание»
    private const float VelEpsSqr = 0.01f;   // ~|v|<0.1
    private const float Hys = 0.15f;         // гистерезис по дистанции

    protected override void OnUpdate()
    {
        var em = EntityManager;

        Entities
        .WithStructuralChanges()
        .ForEach((Entity e,
                  ref NPCMovementComponent move,
                  in ActiveGoal goal,
                  in HarvesterSettings hs,
                  in LocalToWorld npcLTW) =>
        {
            // Базовая валидация цели
            if (goal.Type != GoalType.Harvest || goal.Target == Entity.Null || !em.HasComponent<LocalToWorld>(goal.Target))
            {
                if (em.HasComponent<WantsToHarvestTag>(e)) em.RemoveComponent<WantsToHarvestTag>(e);
                if (em.HasComponent<ActiveTarget>(e))      em.RemoveComponent<ActiveTarget>(e);
                return;
            }

            var tgtLTW = em.GetComponentData<LocalToWorld>(goal.Target);
            float2 dXZ = new float2(tgtLTW.Position.x - npcLTW.Position.x,
                                    tgtLTW.Position.z - npcLTW.Position.z);
            float dist = math.length(dXZ);

            bool isAtTag = em.HasComponent<IsAtHarvestTargetTag>(e); // ставится на прибытии (каноничный триггер) :contentReference[oaicite:4]{index=4}

            // «Реально движется?» — если есть цель или скорости не близки к нулю
            bool moving =
                move.HasTarget ||                                  // ведёт путь / не завершил следование :contentReference[oaicite:5]{index=5}
                math.lengthsq(move.TargetVelocity.xz) > VelEpsSqr || // задаётся avoid-системой :contentReference[oaicite:6]{index=6}
                math.lengthsq(move.PreferredVelocity.xz) > VelEpsSqr;

            bool harvesting = em.HasComponent<WantsToHarvestTag>(e);

            // Условия запуска: ТОЛЬКО после прибытия и полной остановки
            if (!harvesting)
            {
                if (isAtTag && !moving)
                {
                    // Жёстко фиксируем остановку (на случай расхождения систем)
                    move.HasTarget         = false;
                    move.PreferredVelocity = float3.zero;
                    move.TargetVelocity    = float3.zero;

                    em.AddComponent<WantsToHarvestTag>(e);
                    if (!em.HasComponent<ActiveTarget>(e))
                        em.AddComponentData(e, new ActiveTarget { Value = goal.Target });
                }
                // иначе — ещё не прибыли/не остановились: гарантируем отсутствие флага добычи
                else
                {
                    if (em.HasComponent<WantsToHarvestTag>(e)) em.RemoveComponent<WantsToHarvestTag>(e);
                    if (em.HasComponent<ActiveTarget>(e))      em.RemoveComponent<ActiveTarget>(e);
                }
            }
            else
            {
                // Уже добываем — держим состояние, пока не начнём явное движение
                // или не выйдем за InteractionRange + гистерезис
                float stopDist = hs.InteractionRange + Hys;
                bool leftArea  = dist > stopDist;
                if (moving || leftArea)
                {
                    // Сброс добычи, возобновление похода (если цель ещё активна — другой системы)
                    if (em.HasComponent<WantsToHarvestTag>(e)) em.RemoveComponent<WantsToHarvestTag>(e);
                    if (em.HasComponent<ActiveTarget>(e))      em.RemoveComponent<ActiveTarget>(e);
                }
            }
        })
        .Run();
    }
}
