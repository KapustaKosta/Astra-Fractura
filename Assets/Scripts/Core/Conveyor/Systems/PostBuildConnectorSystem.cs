using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorFinalizeSystem))]
    public partial class PostBuildConnectorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);

            foreach (var (req, newSegmentsBuffer, requestEntity) in SystemAPI
                         .Query<RefRO<PostBuildConnectorUpdateRequest>, DynamicBuffer<NewlyBuiltConveyorSegmentRef>>()
                         .WithEntityAccess())
            {
                var reqData = req.ValueRO;
                if (newSegmentsBuffer.Length == 0 ||
                    !EntityManager.Exists(reqData.StartConnector) ||
                    !EntityManager.Exists(reqData.EndConnector))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                var startConnectorClicked = reqData.StartConnector;
                var endConnectorClicked = reqData.EndConnector;

                var startConnData = EntityManager.GetComponentData<ConveyorConnector>(startConnectorClicked);
                var endConnData = EntityManager.GetComponentData<ConveyorConnector>(endConnectorClicked);

                // определить истинное направление 
                Entity trueStartConnector;
                Entity trueEndConnector;
                bool isReversed = false;

                if (startConnData.Type == ConveyorConnectorType.Out && endConnData.Type == ConveyorConnectorType.In)
                {
                    trueStartConnector = startConnectorClicked;
                    trueEndConnector = endConnectorClicked;
                }
                else if (startConnData.Type == ConveyorConnectorType.In && endConnData.Type == ConveyorConnectorType.Out)
                {
                    trueStartConnector = endConnectorClicked;   // настоящий старт — Out
                    trueEndConnector = startConnectorClicked; // настоящий конец — In
                    isReversed = true;
                    Debug.Log("<color=yellow>[PostBuildSystem]</color> Обнаружена постройка в обратном порядке — разворачиваем цепь.");
                }
                else
                {
                    // допускаем bidir/прочие — оставляем порядок кликов
                    trueStartConnector = startConnectorClicked;
                    trueEndConnector = endConnectorClicked;
                }

                // связный список сегментов по направлению
                ecb.AddComponent<ConveyorOccupiedTag>(trueStartConnector);
                ecb.AddComponent<ConveyorOccupiedTag>(trueEndConnector);

                if (!isReversed)
                {
                    for (int i = 0; i < newSegmentsBuffer.Length - 1; i++)
                        ecb.AddComponent(newSegmentsBuffer[i].Value, new ConveyorLink { NextSegment = newSegmentsBuffer[i + 1].Value });
                }
                else
                {
                    for (int i = newSegmentsBuffer.Length - 1; i > 0; i--)
                        ecb.AddComponent(newSegmentsBuffer[i].Value, new ConveyorLink { NextSegment = newSegmentsBuffer[i - 1].Value });
                }

                // привязать крайние сегменты к коннекторам 
                var startConn = EntityManager.GetComponentData<ConveyorConnector>(trueStartConnector);
                var endConn = EntityManager.GetComponentData<ConveyorConnector>(trueEndConnector);

                startConn.ConnectedSegment = isReversed ? newSegmentsBuffer[^1].Value : newSegmentsBuffer[0].Value;
                endConn.ConnectedSegment = isReversed ? newSegmentsBuffer[0].Value : newSegmentsBuffer[^1].Value;

                ecb.SetComponent(trueStartConnector, startConn);
                ecb.SetComponent(trueEndConnector, endConn);

                // запуск пересчёта ТОЛЬКО от истинного старта 
                var recalcRequest = ecb.CreateEntity();
                ecb.AddComponent(recalcRequest, new RecalculateRoutesForNetworkRequest
                {
                    SourceBuilding = startConn.Owner
                });

                // очистка
                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}

