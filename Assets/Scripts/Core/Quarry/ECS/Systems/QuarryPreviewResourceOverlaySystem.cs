using Unity.Entities;
using Unity.Entities.Graphics;          
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;                 
using UnityEngine.Rendering;

// Вспомогательные компоненты для работы системы
struct OverlayProxyTag : IComponentData {}
struct OverlayOwner    : IComponentData { public Entity Node; }

/// <summary>
/// Система, отвечающая за визуальную подсветку (оверлей) ресурсных узлов,
/// когда игрок размещает карьер и наводит курсор на подходящий узел.
/// Она создает прозрачные "призрачные" копии моделей узла с другим материалом,
/// чтобы визуально выделить цель.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RegularBuildingPreviewValidationSystem))] // Работает после определения цели превью
public sealed partial class QuarryPreviewResourceOverlaySystem : SystemBase
{
    /// <summary>
    /// При создании системы гарантирует наличие синглтона `QuarryPreviewHighlightState`,
    /// который хранит информацию о последнем подсвеченном узле.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<BuildingSettings>();
        RequireForUpdate<BuildingPreviewTag>();

        if (!SystemAPI.HasSingleton<QuarryPreviewHighlightState>())
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var s = ecb.CreateEntity();
            ecb.AddComponent<QuarryPreviewHighlightState>(s, new QuarryPreviewHighlightState
            {
                LastHighlightedNode = Entity.Null
            });
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Выполняется каждый кадр. Определяет текущую цель для подсветки (если есть)
    /// и сравнивает ее с предыдущей. Если цель изменилась, снимает подсветку
    /// со старого узла и применяет к новому.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var stateEntity = SystemAPI.GetSingletonEntity<QuarryPreviewHighlightState>();
        var stateRW = SystemAPI.GetComponentRW<QuarryPreviewHighlightState>(stateEntity);
        var lastHighlightedNode = stateRW.ValueRO.LastHighlightedNode;

        // Цель подсветки берём из превью-контекста карьера
        Entity currentTargetNode = Entity.Null;
        if (SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var preview) &&
            SystemAPI.HasComponent<QuarryPlacementTag>(preview) &&
            SystemAPI.IsComponentEnabled<QuarryPreviewTarget>(preview))
        {
            currentTargetNode = SystemAPI.GetComponent<QuarryPreviewTarget>(preview).TargetNode;
        }

        // Переключаем подсветку, только если цель изменилась.
        if (lastHighlightedNode != currentTargetNode)
        {
            if (lastHighlightedNode != Entity.Null && SystemAPI.Exists(lastHighlightedNode))
                ClearOverlay(ecb, lastHighlightedNode);

            if (currentTargetNode != Entity.Null && SystemAPI.Exists(currentTargetNode))
                ApplyOverlay(ecb, currentTargetNode);

            stateRW.ValueRW.LastHighlightedNode = currentTargetNode;
        }
    }

    /// <summary>
    /// Применяет эффект оверлея к указанному узлу, создавая прокси-сущности для всех его рендереров.
    /// </summary>
    private void ApplyOverlay(EntityCommandBuffer ecb, Entity node)
    {
        if (!SystemAPI.HasSingleton<BuildingSettings>()) return;
        var s = SystemAPI.GetSingleton<BuildingSettings>();
        if (s.ResourceHighlightOverlayMaterialID.Equals(default)) return;

        var color = new float4(
            s.ResourceHighlightColor.x,
            s.ResourceHighlightColor.y,
            s.ResourceHighlightColor.z,
            math.saturate(s.ResourceHighlightAlpha)
        );

        int createdProxies = 0;

        // Обрабатываем всех дочерних рендереров узла
        if (SystemAPI.HasBuffer<Child>(node))
        {
            var children = SystemAPI.GetBuffer<Child>(node);
            for (int i = 0; i < children.Length; i++)
            {
                createdProxies += EnsureProxyForRenderer(
                    ecb, node, children[i].Value, s.ResourceHighlightOverlayMaterialID, color);
            }
        }
        // Обрабатываем сам узел, если на нём тоже есть рендерер
        createdProxies += EnsureProxyForRenderer(
            ecb, node, node, s.ResourceHighlightOverlayMaterialID, color);

        if (createdProxies > 0 && !SystemAPI.HasComponent<HighlightedResourceNodeTag>(node))
        {
            ecb.AddComponent<HighlightedResourceNodeTag>(node, new HighlightedResourceNodeTag());
        }
    }

    /// <summary>
    /// Создает одну прокси-сущность для одного рендерера, если она еще не создана.
    /// </summary>
    private int EnsureProxyForRenderer(
        EntityCommandBuffer ecb,
        Entity ownerNode,
        Entity renderEnt,
        BatchMaterialID overlayMat,
        float4 color)
    {
        if (!SystemAPI.HasComponent<MaterialMeshInfo>(renderEnt)) return 0;
        
        // Проверяем, не создан ли уже прокси для этой пары (узел + рендерер)
        foreach (var (parent, owner) in SystemAPI.Query<RefRO<Parent>, RefRO<OverlayOwner>>()
                                                 .WithAll<OverlayProxyTag>())
        {
            if (owner.ValueRO.Node == ownerNode && parent.ValueRO.Value == renderEnt)
                return 0;
        }

        var proxy = ecb.CreateEntity();

        // Настраиваем иерархию и базовые компоненты
        ecb.AddComponent<OverlayProxyTag>(proxy, new OverlayProxyTag());
        ecb.AddComponent<OverlayOwner>(proxy, new OverlayOwner { Node = ownerNode });
        ecb.AddComponent<Parent>(proxy, new Parent { Value = renderEnt });
        ecb.AddComponent<LocalTransform>(proxy, LocalTransform.Identity);
        ecb.AddComponent<LocalToWorld>(proxy, new LocalToWorld());

        // Копируем данные о меше и подменяем материал на оверлейный
        var srcMMI = SystemAPI.GetComponent<MaterialMeshInfo>(renderEnt);
        var proxyMMI = srcMMI;
        proxyMMI.MaterialID = overlayMat;
        ecb.AddComponent<MaterialMeshInfo>(proxy, proxyMMI);
        ecb.AddComponent<URPMaterialPropertyBaseColor>(proxy, new URPMaterialPropertyBaseColor { Value = color });

        // Копируем границы рендера для корректного отсечения (culling)
        if (SystemAPI.HasComponent<RenderBounds>(renderEnt))
        {
            ecb.AddComponent<RenderBounds>(proxy, SystemAPI.GetComponent<RenderBounds>(renderEnt));
        }

        // Копируем RenderFilterSettings, так как это важный shared-компонент
        if (EntityManager.HasComponent<RenderFilterSettings>(renderEnt))
        {
            var srcFilter = EntityManager.GetSharedComponentManaged<RenderFilterSettings>(renderEnt);
            ecb.AddSharedComponentManaged(proxy, srcFilter);
        }

        return 1;
    }

    /// <summary>
    /// Удаляет все прокси-сущности, связанные с указанным узлом, и снимает тег подсветки.
    /// </summary>
    private void ClearOverlay(EntityCommandBuffer ecb, Entity node)
    {
        // Ищем и уничтожаем все прокси, принадлежащие этому узлу
        foreach (var (owner, proxyEntity) in SystemAPI.Query<RefRO<OverlayOwner>>()
                                                      .WithAll<OverlayProxyTag>()
                                                      .WithEntityAccess())
        {
            if (owner.ValueRO.Node == node)
                ecb.DestroyEntity(proxyEntity);
        }

        if (SystemAPI.HasComponent<HighlightedResourceNodeTag>(node))
            ecb.RemoveComponent<HighlightedResourceNodeTag>(node);
    }
}