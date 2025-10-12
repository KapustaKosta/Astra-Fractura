using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RegularBuildingPreviewValidationSystem))]
public sealed partial class PreviewMaterialSystem : SystemBase
{
    private bool _initialized;
    private UnityEngine.Rendering.BatchMaterialID _validID;
    private UnityEngine.Rendering.BatchMaterialID _invalidID;

    protected override void OnCreate()
    {
        RequireForUpdate<BuildingSettings>();
        RequireForUpdate<BuildingPreviewTag>();
        _initialized = false;
    }

    protected override void OnUpdate()
    {
        if (!_initialized)
        {
            var auth = Object.FindFirstObjectByType<BuildingSettingsAuthoring>();
            if (auth != null && auth.validPlacementMaterial != null && auth.invalidPlacementMaterial != null)
            {
                var gfx = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                _validID = gfx.RegisterMaterial(auth.validPlacementMaterial);
                _invalidID = gfx.RegisterMaterial(auth.invalidPlacementMaterial);

                var bs = SystemAPI.GetSingletonRW<BuildingSettings>();
                bs.ValueRW.ValidPlacementMaterialID = _validID;
                bs.ValueRW.InvalidPlacementMaterialID = _invalidID;
                _initialized = true;
            }
        }
        if (!_initialized) return;

        // 1. Находим единственный экземпляр превью
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewRootEntity))
        {
            return;
        }

        // 2. Определяем, какой материал нужно применить, по тегам на корневом объекте
        bool isInvalid = SystemAPI.HasComponent<PlacementInvalidTag>(previewRootEntity);
        var targetMaterialID = isInvalid ? _invalidID : _validID;

        // 3. Используем очередь для безопасного обхода всей иерархии (корень + все потомки)
        var queue = new NativeQueue<Entity>(Allocator.Temp);
        queue.Enqueue(previewRootEntity);

        while (queue.TryDequeue(out var currentEntity))
        {
            // в прогрессе
            if (SystemAPI.HasComponent<MaterialMeshInfo>(currentEntity) && 
                SystemAPI.HasComponent<RenderBounds>(currentEntity))
            {
                var mmi = SystemAPI.GetComponentRW<MaterialMeshInfo>(currentEntity);
                mmi.ValueRW.MaterialID = targetMaterialID;  
            }

            // Добавляем дочерние сущности в очередь для обработки
            if (SystemAPI.HasBuffer<Child>(currentEntity))
            {
                var children = SystemAPI.GetBuffer<Child>(currentEntity);
                foreach (var child in children)
                {
                    queue.Enqueue(child.Value);
                }
            }
        }
    }
}