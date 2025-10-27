using Unity.Entities;
using UnityEngine;

namespace Conveyor
{
    /// Печатает сводную статистику по визуалам/транзитам и помогает ловить расхождения.
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ConveyorVisualDebugSystem : SystemBase
    {
        protected override void OnUpdate()
        {
#if !UNITY_EDITOR
            return;
#else
            if (!SystemAPI.TryGetSingleton<ConveyorVisualDebugConfig>(out var cfg) || !cfg.Enable)
                return;

            // Быстрые подсчёты
            var qTransit = SystemAPI.QueryBuilder().WithAll<ItemInTransit>().Build();
            var qSpawnable = SystemAPI.QueryBuilder().WithAll<ItemInTransit>().WithNone<HasVisualTag>().Build();
            var qVisuals = SystemAPI.QueryBuilder().WithAll<ItemVisualTag>().Build();

            int nTransit = qTransit.CalculateEntityCount();
            int nSpawnable = qSpawnable.CalculateEntityCount();
            int nVisuals = qVisuals.CalculateEntityCount();


            var hasTransitLookup = GetComponentLookup<ItemInTransit>(true);

            int ownerMissing = 0;


            Entities
                .WithAll<ItemVisualTag>()
                .WithReadOnly(hasTransitLookup)
                .ForEach((in VisualFor link) =>
                {
                    if (link.LogicalEntity == Entity.Null || !hasTransitLookup.HasComponent(link.LogicalEntity))
                        ownerMissing++;
                })
                .Run();


            Debug.Log($"<color=#87CEFA>[Conveyor/Visual Debug]</color> " +
                      $"transit={nTransit} spawnable(no visual)={nSpawnable} visuals={nVisuals} " +
                      $"ownerMissing={ownerMissing}");
#endif
        }
    }
}