using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Burst;
using UnityEngine;


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

        // Определяем, находится ли игра в режиме строительства и существует ли уже сущность превью.
        bool inBuild   = SystemAPI.HasComponent<InBuildingMode>(gs);
        bool haveGhost = SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var ghost);

        // Логика создания превью: если в режиме строительства, но превью еще нет.
        if (inBuild && !haveGhost)
        {
            var st = SystemAPI.GetComponent<BuildingState>(gs);
            // Если нет выбранного префаба для строительства, прерываем.
            if (st.BuildingPrefabToPlace == Entity.Null) return;

            var g = ecb.Instantiate(st.BuildingPrefabToPlace); // Создаем сущность превью из префаба.
            ecb.AddComponent<BuildingPreviewTag>(g);           
            ecb.RemoveComponent<Parent>(g);                    // Удаляем Parent, чтобы превью имело свой собственный Transform.
            ecb.AddComponent<NeedsPreviewSetupTag>(g);         // Добавляем тег для последующей настройки коллайдера
        }
        // Логика удаления превью: если не в режиме строительства, но превью существует.
        else if (!inBuild && haveGhost)
        {
            ecb.DestroyEntity(ghost); 
        }
    }
}


