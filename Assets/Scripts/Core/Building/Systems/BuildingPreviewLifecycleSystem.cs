﻿using Unity.Entities;
using Unity.Transforms;


/// <summary>
/// Система управляет созданием и удалением сущности превью здания (ghost)
/// в зависимости от текущего состояния игры (режим строительства).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnterBuildingModeSystem))]
public partial class BuildingPreviewLifecycleSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Проверяем существование синглтона GameState.
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;

        bool inBuildMode = SystemAPI.HasComponent<InBuildingMode>(gs);
        bool hasPreview = SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity);

        if (inBuildMode && !hasPreview)
        {
            if (SystemAPI.HasComponent<BuildingState>(gs))
            {
                var st = SystemAPI.GetComponent<BuildingState>(gs);
                if (st.BuildingPrefabToPlace != Entity.Null)
                {
                    var rootPrefab = ResolvePrefabRoot(st.BuildingPrefabToPlace);
                    var g = ecb.Instantiate(rootPrefab);

                    ecb.AddComponent<BuildingPreviewTag>(g);
                    ecb.AddComponent<NeedsPreviewSetupTag>(g);
                    ecb.AddComponent<BuildingHeightOffset>(g);
                    ecb.AddComponent<BuildingPreviewLink>(g);
                    ecb.AddComponent<PreviewGroundPosition>(g);

                    ecb.RemoveComponent<Parent>(g);
                }
            }
        }
        else if (!inBuildMode && hasPreview)
        {
            if (SystemAPI.HasComponent<BuildingPreviewLink>(previewEntity))
            {
                var link = SystemAPI.GetComponent<BuildingPreviewLink>(previewEntity);
                if (link.FoundationPreviewEntity != Entity.Null && EntityManager.Exists(link.FoundationPreviewEntity))
                {
                    ecb.DestroyEntity(link.FoundationPreviewEntity);
                }
            }
            ecb.DestroyEntity(previewEntity);
        }
    }

    private Entity ResolvePrefabRoot(Entity anyPrefabEntity)
    {
        var e = anyPrefabEntity;
        while (SystemAPI.HasComponent<Parent>(e))
        {
            e = SystemAPI.GetComponent<Parent>(e).Value;
        }
        return e;
    }
}