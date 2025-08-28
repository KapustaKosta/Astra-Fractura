using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Подтверждает установку обычных (НЕ конвейерных) построек по ЛКМ.
/// Вместо прямого создания здания, эта система создает PlaceBuildingRequest,
/// который будет обработан FinalizeBuildingSystem для корректного списания ресурсов.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingPlacementSystem))]
public partial class ConfirmPlacementSystem : SystemBase
{
    private bool wasPressedLastFrame; // Флаг для отслеживания состояния кнопки между кадрами.
    
    /// <summary>
    /// Вызывается при создании системы. Требует наличия компонента InBuildingMode для обновления.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
        RequireForUpdate<BuildingPreviewTag>(); // Работаем только когда есть превью
    }

    /// <summary>
    /// Вызывается каждый кадр для обработки ввода игрока.
    /// Если игрок нажимает основную кнопку действия, и курсор не находится над UI,
    /// система проверяет валидность размещения превью здания.
    /// При валидном размещении создает запросы на постройку здания и выход из режима строительства.
    /// </summary>
    protected override void OnUpdate()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var preview) || !SystemAPI.Exists(preview))
            return;

        // Если это конвейерное превью — выходим (у него своя логика)
        if (SystemAPI.HasComponent<SnapToEndpointTag>(preview))
            return;

        bool isValid = SystemAPI.HasComponent<PlacementValidTag>(preview) &&
                       !SystemAPI.HasComponent<PlacementInvalidTag>(preview);
        if (!isValid)
            return;

        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs) ||
            !SystemAPI.HasComponent<BuildingState>(gs))
            return;

        var st = SystemAPI.GetComponent<BuildingState>(gs);
        if (st.BuildingPrefabToPlace == Entity.Null)
            return;

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);


        var lt = SystemAPI.GetComponent<LocalTransform>(preview);

        var requestEntity = ecb.CreateEntity();
        ecb.AddComponent(requestEntity, new PlaceBuildingRequest
        {
            Position = lt.Position,
            Rotation = lt.Rotation,
            BuildingPrefabToPlace = st.BuildingPrefabToPlace,
            ItemIDToConsume = st.BuildingItemID
        });


        ecb.DestroyEntity(preview);
    }
}