using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RegularBuildingPreviewValidationSystem))]
public sealed partial class PreviewMaterialSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<BuildingSettings>();
        RequireForUpdate<BuildingPreviewTag>();
    }

    protected override void OnUpdate()
    {
        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        
        // Добавим проверку на случай, если MaterialRegistrationSystem еще не отработала.
        if (settings.ValidPlacementMaterialID == default || settings.InvalidPlacementMaterialID == default)
        {
            return;
        }

        // 1. Находим единственный экземпляр превью
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewRootEntity))
        {
            return;
        }

        // 2. Определяем, какой материал нужно применить, по тегам на корневом объекте
        bool isInvalid = SystemAPI.HasComponent<PlacementInvalidTag>(previewRootEntity);
        var targetMaterialID = isInvalid ? settings.InvalidPlacementMaterialID : settings.ValidPlacementMaterialID;

        // 3. Используем очередь для безопасного обхода всей иерархии (корень + все потомки)
        var queue = new NativeQueue<Entity>(Allocator.Temp);
        queue.Enqueue(previewRootEntity);

        while (queue.TryDequeue(out var currentEntity))
        {
            if (SystemAPI.HasComponent<MaterialMeshInfo>(currentEntity))
            {
                var mmi = SystemAPI.GetComponentRW<MaterialMeshInfo>(currentEntity);
                mmi.ValueRW.MaterialID = targetMaterialID;
            }

            // Добавляем дочерние сущности в очередь для обработки
            if (SystemAPI.HasBuffer<Child>(currentEntity))
            {
                foreach (var child in SystemAPI.GetBuffer<Child>(currentEntity))
                {
                    queue.Enqueue(child.Value);
                }
            }
        }
    }
}