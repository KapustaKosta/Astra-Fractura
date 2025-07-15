using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Отвечает за процесс "тиков" добычи. Когда сущность с тегом WantsToHarvestTag находится
/// в радиусе действия цели, эта система с заданной периодичностью создает запросы на валидацию добычи.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestGoalExecutionSystem))]
public partial class HarvestingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Получаем командный буфер для создания новых сущностей-запросов.
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Получаем текущее время для проверки интервала добычи.
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        
        // Используем GetComponentLookup для безопасного доступа к данным о трансформации из параллельного кода.
        var worldTransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        Entities
            .WithReadOnly(worldTransformLookup)
            // Система работает только с сущностями, у которых есть намерение добывать и активная цель.
            .WithAll<WantsToHarvestTag, ActiveTarget>()
            .ForEach((Entity entity, ref HarvesterState harvesterState, in HarvesterSettings harvesterSettings, in ActiveTarget activeTarget) =>
            {
                var targetEntity = activeTarget.Value;

                // Проверяем, что и добытчик, и его цель все еще существуют в мире и имеют позицию.
                if (targetEntity == Entity.Null || !worldTransformLookup.HasComponent(targetEntity) || !worldTransformLookup.HasComponent(entity))
                {
                    // Если цель или сам добытчик невалидны, отменяем намерение добывать.
                    ecb.RemoveComponent<WantsToHarvestTag>(entity);
                    ecb.RemoveComponent<ActiveTarget>(entity);
                    return;
                }
                
                // Получаем мировые позиции добытчика и цели.
                var harvesterTransform = worldTransformLookup[entity];
                var targetTransform = worldTransformLookup[targetEntity];

                // Проверяем, находится ли добытчик в пределах допустимого радиуса взаимодействия.
                if (math.distancesq(harvesterTransform.Position, targetTransform.Position) > harvesterSettings.InteractionRange * harvesterSettings.InteractionRange)
                {
                    // Если добытчик оказался слишком далеко, просто прекращаем обработку.
                    // Для NPC система HarvestGoalExecutionSystem вернет его обратно к цели.
                    // Для игрока это означает, что он отошел от ресурса.
                    return;
                }

                // Проверяем, прошел ли необходимый интервал времени с последней попытки добычи.
                if (currentTime < harvesterState.LastHarvestTime + harvesterSettings.HarvestInterval)
                {
                    return;
                }
                
                // Если все проверки пройдены, создаем одноразовую сущность-запрос на валидацию.
                // Сама добыча (добавление предмета) произойдет в другой системе.
                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new ValidateHarvestAttemptRequest
                {
                    Harvester = entity,
                    TargetResourceNode = targetEntity
                });

                // Обновляем время последней добычи, чтобы перезапустить таймер интервала.
                harvesterState.LastHarvestTime = currentTime;

            }).Schedule();
    }
}