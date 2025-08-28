using Unity.Entities;
using Unity.Rendering;

namespace Conveyor
{
    /// <summary>
    /// Система, которая отслеживает "осиротевшие" коннекторы. Если сегмент,
    /// к которому был подключен коннектор, удаляется, эта система
    /// возвращает коннектор в исходное, свободное состояние.
    /// Работает как "сборщик мусора" для некорректных ссылок.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class ConnectorCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);


            foreach (var (connector, entity) in SystemAPI.Query<RefRW<ConveyorConnector>>()
                .WithEntityAccess())
            {
                // Пропускаем коннекторы, которые уже свободны.
                if (connector.ValueRO.ConnectedSegment == Entity.Null)
                    continue;

                // Если сущность, к которой был привязан коннектор, больше не существует - это "осиротевший" коннектор.
                if (!SystemAPI.Exists(connector.ValueRO.ConnectedSegment))
                {
                    // Сбрасываем коннектор в состояние "свободен"
                    connector.ValueRW.ConnectedSegment = Entity.Null;

                    // Снимаем тег занятости, если он был
                    if (SystemAPI.HasComponent<ConveyorOccupiedTag>(entity))
                    {
                        ecb.RemoveComponent<ConveyorOccupiedTag>(entity);
                    }

                    // Убеждаемся, что он снова станет видимым для игрока в режиме строительства.
                    // Это ключевое исправление: коннектор мог остаться выключенным ('Disabled').
                    if (SystemAPI.HasComponent<Disabled>(entity))
                    {
                        ecb.RemoveComponent<Disabled>(entity);
                    }
                    // Также проверяем DisableRendering на всякий случай
                    if (SystemAPI.HasComponent<DisableRendering>(entity))
                    {
                        ecb.RemoveComponent<DisableRendering>(entity);
                    }
                }
            }
        }
    }
}