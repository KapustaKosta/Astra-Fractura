using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TargetDetectorSystem))]
public partial class HarvestIntentionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var inputs = SystemAPI.GetSingleton<InputsData>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<PlayerControllerData>(out var playerEntity)) return;

        ecb.RemoveComponent<WantsToHarvestTag>(playerEntity);
        
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        if (inputs.PrimaryAction &&
            SystemAPI.HasComponent<InteractionTarget>(playerEntity) &&
            SystemAPI.HasComponent<ResourceNode>(SystemAPI.GetComponent<InteractionTarget>(playerEntity).Value) &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) &&
            !SystemAPI.HasComponent<InBuildingMode>(gameStateEntity))
        {
            var targetEntity = SystemAPI.GetComponent<InteractionTarget>(playerEntity).Value;
            
            // --- DEBUG ---
            Debug.Log($"<color=cyan>[HarvestIntentionSystem]</color> Условия выполнены. Игрок {playerEntity} хочет добывать цель {targetEntity}. Добавляю WantsToHarvestTag.");
            // --- END DEBUG ---

            ecb.AddComponent<WantsToHarvestTag>(playerEntity);
        }
    }
}