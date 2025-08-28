using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Conveyor
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPlacementInteractionSystem))] // Важно: работать после того, как Hovered-тег будет выставлен
    public partial struct ConveyorConnectorHighlightSystem : ISystem
    {
        // Определяем константы для всех нужных цветов
        static readonly float4 kColorValid = new float4(0.2f, 1f, 0.2f, 1f);      // Зеленый
        static readonly float4 kColorInvalid = new float4(1f, 0.2f, 0.2f, 1f);    // Красный
        static readonly float4 kColorHover = new float4(1f, 1f, 1f, 1f);         // Белый (для наведения)
        static readonly float4 kColorDefault = new float4(1f, 1f, 1f, 1f);       // Белый (по умолчанию)

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            // Определяем глобальное состояние: нужна ли подсветка вообще
            bool needsHighlighting = false;
            ConveyorState st = default;
            ConveyorConnectorType requiredType = (ConveyorConnectorType)255; // Невалидный тип
            Entity startOwner = Entity.Null;
            Entity hoveredConnector = Entity.Null;

            // Находим коннектор под курсором
            var queryHover = SystemAPI.QueryBuilder().WithAll<HoveredConnectorTag>().Build();
            if (!queryHover.IsEmpty)
            {
                hoveredConnector = queryHover.GetSingletonEntity();
            }

            if (SystemAPI.HasSingleton<InConveyorMode>() && SystemAPI.HasSingleton<ConveyorState>())
            {
                st = SystemAPI.GetSingleton<ConveyorState>();
                if (st.HasStart && em.Exists(st.StartConnector) && em.HasComponent<ConveyorConnector>(st.StartConnector))
                {
                    needsHighlighting = true;
                    var startCc = em.GetComponentData<ConveyorConnector>(st.StartConnector);
                    startOwner = startCc.Owner;
                    // Определяем, какой тип коннектора мы ищем (противоположный стартовому)
                    requiredType = (startCc.Type == ConveyorConnectorType.In) ? ConveyorConnectorType.Out :
                                 (startCc.Type == ConveyorConnectorType.Out) ? ConveyorConnectorType.In :
                                 (ConveyorConnectorType)255; // Bidirectional не может быть стартом
                }
            }

            if (!needsHighlighting)
            {
                // Если мы не в режиме подсветки, возвращаем всем коннекторам цвет по умолчанию
                foreach (var (color, entity) in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>>().WithAll<ConveyorConnector>().WithEntityAccess())
                {
                    ecb.SetComponent(entity, new URPMaterialPropertyBaseColor { Value = kColorDefault });
                    if (em.HasComponent<ConveyorConnectorHighlighted>(entity))
                        ecb.RemoveComponent<ConveyorConnectorHighlighted>(entity);
                }
                return;
            }

            // Главный цикл: проходим по всем коннекторам и решаем, как их красить
            foreach (var (cc, entity) in SystemAPI.Query<RefRO<ConveyorConnector>>().WithAll<URPMaterialPropertyBaseColor>().WithEntityAccess())
            {
                if (entity == st.StartConnector) continue; // Сам стартовый коннектор не красим

                bool isInvalid = (startOwner != Entity.Null && cc.ValueRO.Owner == startOwner) ||
                                 (requiredType != (ConveyorConnectorType)255 && cc.ValueRO.Type != requiredType);

                if (isInvalid)
                {
                    // НЕВАЛИДНАЯ ЦЕЛЬ: красим в красный и снимаем тег подсветки
                    ecb.SetComponent(entity, new URPMaterialPropertyBaseColor { Value = kColorInvalid });
                    if (em.HasComponent<ConveyorConnectorHighlighted>(entity))
                        ecb.RemoveComponent<ConveyorConnectorHighlighted>(entity);
                }
                else
                {
                    // ВАЛИДНАЯ ЦЕЛЬ: красим в зеленый или белый
                    if (entity == hoveredConnector)
                    {
                        ecb.SetComponent(entity, new URPMaterialPropertyBaseColor { Value = kColorHover });
                    }
                    else
                    {
                        ecb.SetComponent(entity, new URPMaterialPropertyBaseColor { Value = kColorValid });
                    }

                    // Добавляем тег, чтобы система взаимодействия разрешила клик
                    if (!em.HasComponent<ConveyorConnectorHighlighted>(entity))
                        ecb.AddComponent<ConveyorConnectorHighlighted>(entity);
                }
            }
        }
    }
}
