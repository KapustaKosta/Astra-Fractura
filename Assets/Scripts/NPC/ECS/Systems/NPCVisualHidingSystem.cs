using Unity.Entities;
using Unity.Rendering; 
using Unity.Transforms;

/// <summary>
/// Управляет визуальным скрытием NPC, когда он "входит" в здание.
/// Рекурсивно находит дочерние сущности с любым типом рендерера (статичным или анимированным)
/// и добавляет/удаляет им компонент DisableRendering.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NPCVisualHidingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var childBufferLookup = SystemAPI.GetBufferLookup<Child>(true);

        // Запрос для СКРЫТИЯ NPC
        Entities
            .WithAll<InsideBuildingTag>()
            .WithNone<VisualsHiddenTag>()
            .WithReadOnly(childBufferLookup)
            .WithoutBurst()
            .ForEach((Entity npcEntity) =>
            {
                HideVisualsRecursively(ecb, npcEntity, EntityManager, childBufferLookup);
                ecb.AddComponent<VisualsHiddenTag>(npcEntity);

            }).Run();

        // Запрос для ПОКАЗА NPC
        Entities
            .WithAll<VisualsHiddenTag>()
            .WithNone<InsideBuildingTag>()
            .WithReadOnly(childBufferLookup)
            .WithoutBurst()
            .ForEach((Entity npcEntity) =>
            {
                ShowVisualsRecursively(ecb, npcEntity, EntityManager, childBufferLookup);
                ecb.RemoveComponent<VisualsHiddenTag>(npcEntity);

            }).Run();
    }

    /// <summary>
    /// Рекурсивно обходит иерархию и добавляет DisableRendering всем сущностям, имеющим визуальное представление.
    /// </summary>
    private void HideVisualsRecursively(EntityCommandBuffer ecb, Entity currentEntity, EntityManager em,
        [Unity.Collections.ReadOnly] BufferLookup<Child> childBufferLookup)
    {

        // Проверяем наличие MaterialMeshInfo, который есть и у статичных, и у skinned-мешей.
        if (em.HasComponent<MaterialMeshInfo>(currentEntity))
        {
            ecb.AddComponent<DisableRendering>(currentEntity);
        }


        if (childBufferLookup.HasBuffer(currentEntity))
        {
            var children = childBufferLookup[currentEntity];
            foreach (var child in children)
            {
                HideVisualsRecursively(ecb, child.Value, em, childBufferLookup);
            }
        }
    }

    /// <summary>
    /// Рекурсивно обходит иерархию и удаляет DisableRendering у всех сущностей, имеющих визуальное представление.
    /// </summary>
    private void ShowVisualsRecursively(EntityCommandBuffer ecb, Entity currentEntity, EntityManager em,
        [Unity.Collections.ReadOnly] BufferLookup<Child> childBufferLookup)
    {

        // Проверяем наличие MaterialMeshInfo, который есть и у статичных, и у skinned-мешей.
        if (em.HasComponent<MaterialMeshInfo>(currentEntity))
        {
            if (em.HasComponent<DisableRendering>(currentEntity))
            {
                ecb.RemoveComponent<DisableRendering>(currentEntity);
            }
        }


        if (childBufferLookup.HasBuffer(currentEntity))
        {
            var children = childBufferLookup[currentEntity];
            foreach (var child in children)
            {
                ShowVisualsRecursively(ecb, child.Value, em, childBufferLookup);
            }
        }
    }
}

/// <summary>
/// Внутренний тег для системы, чтобы отслеживать, чьи визуалы уже были скрыты.
/// </summary>
public struct VisualsHiddenTag : IComponentData { }