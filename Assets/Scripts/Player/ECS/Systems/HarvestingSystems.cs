using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestIntentionSystem))]
public partial class HarvestingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        var controllerData = SystemAPI.GetSingleton<PlayerControllerData>();
        
        foreach (var (intention, playerState, interactionTarget, entity) in 
                 SystemAPI.Query<RefRO<WantsToHarvestTag>, RefRW<PlayerStateData>, RefRO<InteractionTarget>>()
                     .WithEntityAccess())
        {
            if (currentTime < playerState.ValueRO.LastHarvestTime + controllerData.HarvestInterval)
            {
                continue;
            }

            var targetEntity = interactionTarget.ValueRO.Value;

            if (!SystemAPI.HasComponent<ResourceNode>(targetEntity))
            {
                continue;
            }
            
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new HarvestRequest 
            { 
                Player = entity, 
                TargetResourceNode = targetEntity 
            });
            
            // --- DEBUG ---
            Debug.Log($"<color=yellow>[HarvestingSystem]</color> Кулдаун прошел. Создаю HarvestRequest для игрока {entity} на цель {targetEntity}.");
            // --- END DEBUG ---

            var resourceNode = SystemAPI.GetComponent<ResourceNode>(targetEntity);
            ecb.AddComponent(entity, new IsHarvestingTag { ResourceType = resourceNode.resourceType });

            playerState.ValueRW.LastHarvestTime = currentTime;
        }

        var query = SystemAPI.QueryBuilder().WithAll<IsHarvestingTag>().WithNone<WantsToHarvestTag>().Build();
        ecb.RemoveComponent<IsHarvestingTag>(query, EntityQueryCaptureMode.AtPlayback);
    }
}