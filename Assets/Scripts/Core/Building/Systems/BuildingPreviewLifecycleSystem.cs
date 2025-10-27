using Unity.Entities;
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
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);

        // Проверяем существование синглтона GameState.
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;

        bool inBuildMode = SystemAPI.HasComponent<InBuildingMode>(gs);
        bool hasPreview  = SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity);

        if (inBuildMode && !hasPreview)
        {
            if (SystemAPI.HasComponent<BuildingState>(gs))
            {
                var st = SystemAPI.GetComponent<BuildingState>(gs);
                if (st.BuildingPrefabToPlace != Entity.Null)
                {
                    var rootPrefab = ResolvePrefabRoot(st.BuildingPrefabToPlace);
                    var g          = ecb.Instantiate(rootPrefab);

                    ecb.AddComponent<BuildingPreviewTag>(g);
                    ecb.AddComponent<NeedsPreviewSetupTag>(g);

                    // Карьер — проверяем и исходный префаб, и корень
                    bool isQuarry =
                        SystemAPI.HasComponent<QuarryTag>(st.BuildingPrefabToPlace) ||
                        SystemAPI.HasComponent<QuarryTag>(rootPrefab);

                    if (isQuarry)
                    {
                        ecb.AddComponent<QuarryPlacementTag>(g);
                        ecb.AddComponent<QuarryPreviewTarget>(g);
                        ecb.SetComponentEnabled<QuarryPreviewTarget>(g, false);
                        ecb.AddComponent<NeedsRangeVisSetup>(g);
                        ecb.AddComponent<AllowRenderingTag>(g); // разрешаем рендер превью
                    }
                    else
                    {
                        ecb.AddComponent<AllowRenderingTag>(g); // обычные превью тоже рисуем
                    }
                }
            }
        }
        else if (!inBuildMode && hasPreview)
        {
            ecb.DestroyEntity(previewEntity);
        }
    }

    private Entity ResolvePrefabRoot(Entity anyPrefabEntity)
    {
        while (SystemAPI.HasComponent<Parent>(anyPrefabEntity))
        {
            anyPrefabEntity = SystemAPI.GetComponent<Parent>(anyPrefabEntity).Value;
        }
        return anyPrefabEntity;
    }
}
