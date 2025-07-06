using Unity.Entities;
using UnityEngine;

/// <summary>
/// "Исполнитель". Реагирует на готовое намерение WantsToHarvestTag и выполняет логику добычи.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestIntentionSystem))] // Работает после системы по намерениям
public partial class HarvestingSystem : SystemBase
{
    private float harvestInterval = 0.5f;

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        
        // Ищем игрока с готовым намерением добывать.
        foreach (var (intention, playerState, interactionTarget, entity) in 
                 SystemAPI.Query<RefRO<WantsToHarvestTag>, RefRW<PlayerStateData>, RefRO<InteractionTarget>>()
                     .WithEntityAccess())
        {
            // Проверяем персональный таймер
            if (currentTime < playerState.ValueRO.LastHarvestTime + harvestInterval)
            {
                continue;
            }

            var targetEntity = interactionTarget.ValueRO.Value;

            // Проверяем, что цель все еще является ресурсным узлом, на всякий случай
            if (!SystemAPI.HasComponent<ResourceNode>(targetEntity))
            {
                continue;
            }
            
            
            // Создаем запрос на фактическое добавление ресурса
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new HarvestRequest 
            { 
                Player = entity, 
                TargetResourceNode = targetEntity 
            });
            

            // Добавляем тег для UI, чтобы показать, что процесс идет.
            var resourceNode = SystemAPI.GetComponent<ResourceNode>(targetEntity);
            ecb.AddComponent(entity, new IsHarvestingTag { ResourceType = resourceNode.resourceType });

            // Обновляем персональный таймер, чтобы запустить кулдаун.
            playerState.ValueRW.LastHarvestTime = currentTime;
        }

        // Очищаем UI-тег, если намерения добывать больше нет.
        var query = SystemAPI.QueryBuilder().WithAll<IsHarvestingTag>().WithNone<WantsToHarvestTag>().Build();
        ecb.RemoveComponent<IsHarvestingTag>(query, EntityQueryCaptureMode.AtPlayback);
    }
}
