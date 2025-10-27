using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Система инициализации, которая гарантирует, что в мире существует ровно один
/// экземпляр синглтона `QuarryPreviewHighlightState`.
/// Она выполняется один раз при запуске в группе `InitializationSystemGroup`.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class QuarryPreviewStateBootstrapSystem : SystemBase
{
    /// <summary>
    /// При создании проверяет количество сущностей с `QuarryPreviewHighlightState`.
    /// Если их нет, создает одну. Если больше одной, удаляет лишние.
    /// </summary>
    protected override void OnCreate()
    {
        var em = EntityManager;
        using var q = em.CreateEntityQuery(ComponentType.ReadOnly<QuarryPreviewHighlightState>());
        var count = q.CalculateEntityCount();

        if (count == 0)
        {
            // Если синглтона нет, создаем его.
            var e = em.CreateEntity(typeof(QuarryPreviewHighlightState));
            em.SetComponentData(e, new QuarryPreviewHighlightState { LastHighlightedNode = Entity.Null });
        }
        else if (count > 1)
        {
            // Если по какой-то причине создалось несколько, удаляем все, кроме первого.
            using var es = q.ToEntityArray(Allocator.Temp);
            var keep = es[0];
            for (int i = 1; i < es.Length; i++) em.DestroyEntity(es[i]);
            // И сбрасываем состояние на всякий случай.
            em.SetComponentData(keep, new QuarryPreviewHighlightState { LastHighlightedNode = Entity.Null });
        }
    }

    /// <summary>
    /// Система не должна выполняться каждый кадр, поэтому метод OnUpdate пуст.
    /// </summary>
    protected override void OnUpdate() { /* ничего */ }
}