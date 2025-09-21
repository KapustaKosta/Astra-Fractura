using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

/// <summary>
/// Система, которая динамически назначает материалы превью зданий в зависимости от того,
/// можно ли их разместить в текущей позиции.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RegularBuildingPreviewPlacementSystem))]
public partial class PreviewMaterialSystem : SystemBase
{
    private UnityEngine.Rendering.BatchMaterialID validMatID;   // ID материала для валидного размещения.
    private UnityEngine.Rendering.BatchMaterialID invalidMatID; // ID материала для невалидного размещения.
    private bool initialized = false;                           // Флаг инициализации материалов.

    /// <summary>
    /// Вызывается при создании системы. Требует наличия синглтона BuildingSettings.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<BuildingSettings>();
    }

    /// <summary>
    /// Вызывается каждый кадр для обновления материалов превью.
    /// Инициализирует ID материалов при первом запуске, затем применяет
    /// соответствующий материал к превью зданий в зависимости от их тегов валидности.
    /// </summary>
    protected override void OnUpdate()
    {
        var gfx = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        
        // Инициализация материалов при первом запуске или если EntitiesGraphicsSystem еще не готов.
        if (!initialized && gfx != null)
        {
            var authoring = Object.FindFirstObjectByType<BuildingSettingsAuthoring>();
            if (authoring != null)
            {
                // Регистрируем материалы из Authoring-компонента и сохраняем их ID.
                validMatID   = gfx.RegisterMaterial(authoring.validPlacementMaterial);
                invalidMatID = gfx.RegisterMaterial(authoring.invalidPlacementMaterial);

                // Сохраняем полученные MaterialID в синглтоне BuildingSettings для доступа из других систем.
                var bs = SystemAPI.GetSingletonRW<BuildingSettings>();
                bs.ValueRW.ValidPlacementMaterialID   = validMatID;
                bs.ValueRW.InvalidPlacementMaterialID = invalidMatID;

                initialized = true;
            }
            else
            {
                // Если Authoring-компонент не найден, пытаемся взять ID материалов из уже заполненного синглтона.
                var bs = SystemAPI.GetSingleton<BuildingSettings>();
                if (!bs.ValidPlacementMaterialID.Equals(default) &&
                    !bs.InvalidPlacementMaterialID.Equals(default))
                {
                    validMatID   = bs.ValidPlacementMaterialID;
                    invalidMatID = bs.InvalidPlacementMaterialID;
                    initialized  = true;
                }
                else
                {
                    #if UNITY_EDITOR
                    Debug.LogWarning("PreviewMaterialSystem: материалы превью не инициализированы — " +
                                     "проверьте BuildingSettingsAuthoring.");
                    #endif
                }
            }
        }

        // Если инициализация не прошла, прекращаем выполнение OnUpdate.
        if (!initialized) return;

        // Основная логика назначения материалов:

        // 1. Применяем материал для невалидного размещения ко всем превью, имеющим PlacementInvalidTag.
        foreach (var mmi in SystemAPI.Query<RefRW<MaterialMeshInfo>>()
                                     .WithAll<BuildingPreviewTag, PlacementInvalidTag>())
        {
            mmi.ValueRW.MaterialID = invalidMatID;
        }

        // 2. Применяем материал для валидного размещения ко всем превью, имеющим PlacementValidTag,
        // но не имеющим компонента MyOwnColor (чтобы не переопределять кастомный цвет).
        foreach (var mmi in SystemAPI.Query<RefRW<MaterialMeshInfo>>()
                                     .WithAll<BuildingPreviewTag, PlacementValidTag>()
                                     .WithNone<MyOwnColor>())
        {
            mmi.ValueRW.MaterialID = validMatID;
        }

        // 3. Превью с PlacementValidTag и MyOwnColor остаются без изменений, сохраняя свой кастомный цвет.
    }
}