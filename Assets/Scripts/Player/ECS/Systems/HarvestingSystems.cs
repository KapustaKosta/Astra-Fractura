using Unity.Entities;

/// <summary>
/// Система, которая обрабатывает намерение игрока добывать ресурсы.
/// Она отвечает за соблюдение интервала (cooldown) между попытками добычи
/// и создает одноразовые <c>HarvestRequest</c> для фактического получения ресурсов.
/// Также управляет состоянием игрока, добавляя и удаляя тег <c>IsHarvestingTag</c>.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestIntentionSystem))] 
public partial class HarvestingSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для обработки процесса добычи.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        
        if (!SystemAPI.TryGetSingleton<PlayerControllerData>(out var controllerData))
        {
            return;
        }
        
        // Перебираем всех игроков, у которых есть намерение добывать (WantsToHarvestTag).
        foreach (var (intention, playerState, interactionTarget, entity) in 
                 SystemAPI.Query<RefRO<WantsToHarvestTag>, RefRW<PlayerStateData>, RefRO<InteractionTarget>>()
                     .WithEntityAccess())
        {
            // Проверяем, прошел ли необходимый интервал времени с последней добычи.
            // Это предотвращает слишком частую генерацию запросов.
            if (currentTime < playerState.ValueRO.LastHarvestTime + controllerData.HarvestInterval)
            {
                continue;
            }

            var targetEntity = interactionTarget.ValueRO.Value;

            // Дополнительная проверка на случай, если цель перестала быть ресурсным узлом.
            if (!SystemAPI.HasComponent<ResourceNode>(targetEntity))
            {
                continue;
            }
            
            // Создаем сущность-запрос, которую обработает другая система для добавления ресурса в инвентарь.
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new HarvestRequest 
            { 
                Player = entity, 
                TargetResourceNode = targetEntity 
            });
            
            // Добавляем игроку тег состояния, указывающий, что он находится в процессе добычи.
            // Это может использоваться, например, для проигрывания анимаций или отображения UI.
            var resourceNode = SystemAPI.GetComponent<ResourceNode>(targetEntity);
            ecb.AddComponent(entity, new IsHarvestingTag { ResourceType = resourceNode.resourceType });

            // Обновляем время последней добычи, чтобы перезапустить кулдаун.
            playerState.ValueRW.LastHarvestTime = currentTime;
        }

        // Логика очистки: если у игрока есть тег IsHarvestingTag, но он больше не выражает
        // намерения добывать, мы убираем тег.
        var query = SystemAPI.QueryBuilder().WithAll<IsHarvestingTag>().WithNone<WantsToHarvestTag>().Build();
        ecb.RemoveComponent<IsHarvestingTag>(query, EntityQueryCaptureMode.AtPlayback);
    }
}