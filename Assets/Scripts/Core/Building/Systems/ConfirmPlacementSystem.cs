using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.EventSystems; 
using UnityEngine; 

/// <summary>
/// Система, которая обрабатывает ввод игрока для подтверждения постройки и создает запрос.
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
        RequireForUpdate<InBuildingMode>();
    }

    /// <summary>
    /// Вызывается каждый кадр для обработки ввода игрока.
    /// Если игрок нажимает основную кнопку действия, и курсор не находится над UI,
    /// система проверяет валидность размещения превью здания.
    /// При валидном размещении создает запросы на постройку здания и выход из режима строительства.
    /// </summary>
    protected override void OnUpdate()
    {
        // Проверяем наличие синглтона InputsData.
        if (!SystemAPI.HasSingleton<InputsData>()) return;
        var inputs = SystemAPI.GetSingleton<InputsData>();
        
        // Отслеживаем однократное нажатие основной кнопки действия (например, левая кнопка мыши).
        bool isPressedThisFrame = inputs.PrimaryAction;
        bool justPressed = isPressedThisFrame && !wasPressedLastFrame;
        wasPressedLastFrame = isPressedThisFrame;

        // Если кнопка не была только что нажата, или курсор находится над UI, прерываем выполнение.
        if (!justPressed || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }
        
        // Получаем сущность превью здания. Если ее нет, выходим.
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity)) return;
        
        // Размещаем здание только если превью валидно (т.е. имеет PlacementValidTag).
        if (SystemAPI.HasComponent<PlacementValidTag>(previewEntity))
        {
            var previewTransform = SystemAPI.GetComponent<LocalTransform>(previewEntity);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            
            // Получаем BuildingState из GameState.
            var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
            if (!SystemAPI.HasComponent<BuildingState>(gameStateEntity))
            {
                #if UNITY_EDITOR
                Debug.LogError("ConfirmPlacementSystem: BuildingState не найден на GameStateEntity! Невозможно создать PlaceBuildingRequest.");
                #endif
                return;
            }
            var buildingState = SystemAPI.GetComponent<BuildingState>(gameStateEntity);

            // Создаем сущность запроса на постройку с данными о позиции, ротации, префабе и ID предмета.
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new PlaceBuildingRequest
            {
                Position = previewTransform.Position,
                Rotation = previewTransform.Rotation,
                BuildingPrefabToPlace = buildingState.BuildingPrefabToPlace, 
                ItemIDToConsume = buildingState.BuildingItemID                 
            });

            // Создаем сущность запроса на выход из режима строительства.
            var exitRequestEntity = ecb.CreateEntity();
            ecb.AddComponent(exitRequestEntity, new ExitBuildingModeRequest());
        }
        else
        {
            #if UNITY_EDITOR
            Debug.Log("Невозможно разместить здание: невалидная позиция.");
            #endif
        }
    }
}