using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Wiring
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConnectorPickingSystem))]
    public partial class WireConnectorHighlightSystem : SystemBase
    {
        static readonly float4 kColorFree = new float4(1f, 1f, 1f, 1f);
        static readonly float4 kColorOccupied = new float4(1f, 0.2f, 0.2f, 1f);
        static readonly float4 kColorValidTarget = new float4(0.2f, 1f, 0.2f, 1f);
        static readonly float4 kColorStart = new float4(0.2f, 0.6f, 1f, 1f);
        static readonly float4 kColorDefault = new float4(0.5f, 0.5f, 0.5f, 1f);
        static readonly float4 kColorRemoval = new float4(1f, 0.2f, 0.2f, 1f);

        protected override void OnUpdate()
        {
            // Если мы не в режиме проводов, ничего не делаем. Очисткой займется другая система.
            if (!SystemAPI.HasSingleton<InWirePlacementMode>())
            {
                return;
            }

            bool isPlacing = SystemAPI.TryGetSingleton<PendingWire>(out var pendingPlacement);
            bool isRemoving = SystemAPI.TryGetSingleton<PendingWireRemoval>(out var pendingRemoval);

            // Главный цикл: устанавливаем правильный цвет для каждого видимого коннектора.
            foreach (var (color, entity) in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>>()
                     .WithAll<WireConnector>().WithNone<DisableRendering>().WithEntityAccess())
            {
                float4 targetColor;

                if (isRemoving)
                {
                    var wireData = SystemAPI.GetComponent<Wire>(pendingRemoval.WireToRemove);
                    Entity otherEnd = (wireData.StartConnector == pendingRemoval.FirstConnector) ? wireData.EndConnector : wireData.StartConnector;

                    targetColor = (entity == pendingRemoval.FirstConnector || entity == otherEnd) ? kColorRemoval : kColorDefault;
                }
                else if (isPlacing)
                {
                    if (entity == pendingPlacement.StartConnector)
                    {
                        targetColor = kColorStart;
                    }
                    else
                    {
                        bool isOccupied = SystemAPI.GetBuffer<ConnectedWires>(entity).Length > 0;
                        targetColor = isOccupied ? kColorOccupied : kColorValidTarget;
                    }
                }
                else
                {
                    bool isOccupied = SystemAPI.GetBuffer<ConnectedWires>(entity).Length > 0;
                    targetColor = isOccupied ? kColorOccupied : kColorFree;
                }

                color.ValueRW.Value = targetColor;
            }

            // Отдельная подсветка самого визуала провода при удалении.
            if (isRemoving && SystemAPI.Exists(pendingRemoval.WireVisual) && SystemAPI.HasComponent<URPMaterialPropertyBaseColor>(pendingRemoval.WireVisual))
            {
                var visualColor = SystemAPI.GetComponentRW<URPMaterialPropertyBaseColor>(pendingRemoval.WireVisual);
                visualColor.ValueRW.Value = kColorRemoval;
            }
        }
    }
}