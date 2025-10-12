using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Система, определяющая момент, когда NPC достиг цели сбора ресурсов. Когда NPC
/// находится в пределах радиуса остановки плюс буфер для взаимодействия, он
/// прекращает движение (сбрасывает HasTarget) и получает тег IsAtHarvestTargetTag.
/// Это позволяет другим системам (например, ротации и добычи) реагировать на то,
/// что агент находится у источника и должен стоять на месте, повернувшись лицом к
/// ресурсу.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCPathFollowSystem))]
[UpdateBefore(typeof(NPCLocalAvoidanceSystem))]
public partial class HarvestArrivalSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Получаем глобальные настройки ИИ для буфера взаимодействия при сборе
        var settings = SystemAPI.GetSingleton<AISettings>();
        // Используем структурные изменения, поскольку необходимо добавлять и
        // удалять компоненты в рантайме (IsAtHarvestTargetTag)
        Entities
            .WithStructuralChanges()
            .ForEach((Entity entity, ref NPCMovementComponent movement, in LocalTransform transform, in ActiveGoal goal) =>
            {
                // Если цель не Harvest или цель отсутствует, убираем тег и выходим
                if (goal.Type != GoalType.Harvest || goal.Target == Entity.Null)
                {
                    if (EntityManager.HasComponent<IsAtHarvestTargetTag>(entity))
                        EntityManager.RemoveComponent<IsAtHarvestTargetTag>(entity);
                    return;
                }

                // Убедимся, что целевая сущность имеет трансформ
                if (!EntityManager.HasComponent<LocalTransform>(goal.Target))
                {
                    if (EntityManager.HasComponent<IsAtHarvestTargetTag>(entity))
                        EntityManager.RemoveComponent<IsAtHarvestTargetTag>(entity);
                    return;
                }

                // Вычисляем расстояние до цели по плоскости XZ
                var targetTransform = EntityManager.GetComponentData<LocalTransform>(goal.Target);
                float3 toTarget = targetTransform.Position - transform.Position;
                float2 to2D = new float2(toTarget.x, toTarget.z);
                float distance = math.length(to2D);

                // Пороговое расстояние: радиус остановки NPC. Мы не добавляем
                // дополнительный буфер здесь, чтобы NPC останавливались только
                // непосредственно у источника, исключая преждевременную остановку
                // в пределах зоны прибытия.
                float threshold = movement.StoppingDistance;

                // Если в пределах порога, считаем NPC достигшим точки сбора
                if (distance <= threshold)
                {
                    // Устанавливаем флаг отсутствия цели, чтобы локальное избегание и движение
                    // прекратили двигать NPC
                    movement.HasTarget = false;

                    // Сбрасываем желаемые и финальные скорости, чтобы NPC не продолжал движение.
                    movement.PreferredVelocity = float3.zero;
                    movement.TargetVelocity    = float3.zero;

                    // Добавляем тег, если его нет, для оповещения других систем
                    if (!EntityManager.HasComponent<IsAtHarvestTargetTag>(entity))
                        EntityManager.AddComponent<IsAtHarvestTargetTag>(entity);
                }
                else
                {
                    // Если NPC ушёл из зоны сбора, удаляем тег (при возобновлении пути)
                    if (EntityManager.HasComponent<IsAtHarvestTargetTag>(entity))
                        EntityManager.RemoveComponent<IsAtHarvestTargetTag>(entity);
                }
            }).Run();
    }
}