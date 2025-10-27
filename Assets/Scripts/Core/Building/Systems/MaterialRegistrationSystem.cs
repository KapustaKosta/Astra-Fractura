using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public struct RenderingReadyTag : IComponentData {}

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateBefore(typeof(PresentationSystemGroup))]
public partial struct MaterialRegistrationSystem : ISystem
{
    private EntityQuery renderingReadyQuery; // Сохраняем запрос, чтобы не создавать его каждый раз

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingSettings>();
        // Создаем запрос один раз при создании системы
        renderingReadyQuery = state.GetEntityQuery(typeof(RenderingReadyTag));
    }

    public void OnUpdate(ref SystemState state)
    {
        // Проверяем, существует ли уже сущность с тегом, безопасным способом.
        // Если да, то все материалы уже зарегистрированы, и систему можно отключить.
        if (!renderingReadyQuery.IsEmpty)
        {
            state.Enabled = false;
            return;
        }

        var em = state.EntityManager;

        var author = Object.FindFirstObjectByType<BuildingSettingsAuthoring>();
        if (author == null)
        {
            Debug.LogError("[MaterialRegistrationSystem] BuildingSettingsAuthoring component not found in the scene!");
            state.Enabled = false;
            return;
        }

        var egs = state.World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();

        if (author.validPlacementMaterial == null) Debug.LogError("[MaterialRegistrationSystem] 'validPlacementMaterial' is not assigned in BuildingSettingsAuthoring inspector!");
        if (author.invalidPlacementMaterial == null) Debug.LogError("[MaterialRegistrationSystem] 'invalidPlacementMaterial' is not assigned in BuildingSettingsAuthoring inspector!");
        if (author.resourceHighlightOverlayMaterial == null) Debug.LogError("[MaterialRegistrationSystem] 'resourceHighlightOverlayMaterial' is not assigned in BuildingSettingsAuthoring inspector! This is required for quarry highlighting.");

        BatchMaterialID validID     = RegisterMaterialSafe(egs, author.validPlacementMaterial);
        BatchMaterialID invalidID   = RegisterMaterialSafe(egs, author.invalidPlacementMaterial);
        BatchMaterialID overlayID   = RegisterMaterialSafe(egs, author.resourceHighlightOverlayMaterial);
        BatchMaterialID rangeMatID  = RegisterMaterialSafe(egs, author.quarryRangeMaterial);
        BatchMeshID     rangeMeshID = RegisterMeshSafe(egs, author.quarryRangeMesh);

        if (!validID.Equals(default))     settings.ValidPlacementMaterialID           = validID;
        if (!invalidID.Equals(default))   settings.InvalidPlacementMaterialID         = invalidID;
        if (!overlayID.Equals(default))   settings.ResourceHighlightOverlayMaterialID = overlayID;
        if (!rangeMatID.Equals(default))  settings.QuarryRangeMaterialID              = rangeMatID;
        if (!rangeMeshID.Equals(default)) settings.QuarryRangeMeshID                  = rangeMeshID;

        settings.ResourceHighlightAlpha = Mathf.Clamp01(author.resourceHighlightAlpha);
        settings.ResourceHighlightColor = new Unity.Mathematics.float4(
            author.resourceHighlightColor.r,
            author.resourceHighlightColor.g,
            author.resourceHighlightColor.b,
            author.resourceHighlightColor.a
        );

        SystemAPI.SetSingleton(settings);

        bool ready = !settings.ValidPlacementMaterialID.Equals(default)
                  && !settings.InvalidPlacementMaterialID.Equals(default);

        if (!ready)
        {
            return; // Ждем, пока рендеринг будет готов зарегистрировать основные материалы
        }

        // Создаем тег, который остановит выполнение этой системы в следующих кадрах
        var e = em.CreateEntity();
        em.AddComponent<RenderingReadyTag>(e);

        Debug.Log($"<color=lime>[MaterialRegistrationSystem] All materials registered successfully. Key IDs:\n" +
                  $"- Valid Placement: {settings.ValidPlacementMaterialID.value}\n" +
                  $"- Invalid Placement: {settings.InvalidPlacementMaterialID.value}\n" +
                  $"- Quarry Overlay: {settings.ResourceHighlightOverlayMaterialID.value}\n" +
                  $"- Quarry Range Mat: {settings.QuarryRangeMaterialID.value}, Mesh: {settings.QuarryRangeMeshID.value}</color>");

        // Отключаем систему после успешного выполнения
        state.Enabled = false;
    }

    private static BatchMaterialID RegisterMaterialSafe(EntitiesGraphicsSystem egs, Material m)
        => m == null ? default : egs.RegisterMaterial(m);

    private static BatchMeshID RegisterMeshSafe(EntitiesGraphicsSystem egs, Mesh mesh)
        => mesh == null ? default : egs.RegisterMesh(mesh);
}