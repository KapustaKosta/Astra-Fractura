using Energy.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Energy.Core.Systems.EnergyDispatchSystem))]
    public partial struct ConveyorPowerRequestSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (load, demand) in SystemAPI.Query<RefRW<ConsumerLoad>, RefRO<ConveyorEnergyDemand>>().WithAll<ActiveRouteTag>())
            {
                load.ValueRW.CurrentKW = demand.ValueRO.RequiredKW;
            }
            foreach (var load in SystemAPI.Query<RefRW<ConsumerLoad>>().WithAll<ConveyorEnergyDemand>().WithNone<ActiveRouteTag>())
            {
                load.ValueRW.CurrentKW = 0f;
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Energy.Core.Systems.EnergyDispatchSystem))]
    public partial class ConveyorPowerFeedbackSystem : SystemBase
    {
        private NativeHashMap<int, float> _previousRatios;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkNode>();
            _previousRatios = new NativeHashMap<int, float>(32, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_previousRatios.IsCreated) _previousRatios.Dispose();
        }

        protected override void OnUpdate()
        {
            var demandByNet = new NativeHashMap<int, float>(32, Allocator.Temp);
            var supplyByNet = new NativeHashMap<int, float>(32, Allocator.Temp);
            var keySet = new NativeHashSet<int>(32, Allocator.Temp);

            Entities.ForEach((in ConsumerLoad load, in NetworkNode node) => {
                if (node.SubnetId == 0) return;
                demandByNet.Increment(node.SubnetId, load.CurrentKW);
                keySet.Add(node.SubnetId);
            }).Run();
            Entities.ForEach((in GeneratorComponent gen, in NetworkNode node) => {
                if (node.SubnetId == 0) return;
                if (gen.IsOnline && gen.CurrentKW > 0.001f) {
                    supplyByNet.Increment(node.SubnetId, gen.CurrentKW);
                }
                keySet.Add(node.SubnetId);
            }).Run();
            Entities.ForEach((in BatteryComponent bat, in NetworkNode node) => {
                if (node.SubnetId == 0) return;
                if (bat.CurrentKW > 0.001f) {
                    supplyByNet.Increment(node.SubnetId, bat.CurrentKW);
                }
                keySet.Add(node.SubnetId);
            }).Run();

            var powerRatioByNet = new NativeHashMap<int, float>(keySet.Count, Allocator.Temp);
            var keys = keySet.ToNativeArray(Allocator.Temp);

            foreach (var key in keys)
            {
                supplyByNet.TryGetValue(key, out float totalSupply);
                demandByNet.TryGetValue(key, out float totalDemand);
                
                float ratio = (totalSupply < 0.001f) ? 0f : ((totalDemand < 0.001f) ? 1f : math.saturate(totalSupply / totalDemand));
                powerRatioByNet.TryAdd(key, ratio);

                _previousRatios.TryGetValue(key, out float oldRatio);
                if (math.abs(oldRatio - ratio) > 0.01f) {
                    // Debug.Log($"<color=orange>[DEBUG/EnergyFeedback]</color> PowerRatio для сети #{key} изменился: {oldRatio:F2} -> {ratio:F2} (Supply: {totalSupply:F2}, Demand: {totalDemand:F2})");
                    _previousRatios[key] = ratio;
                }
            }
            
            var networkNodeLookup = SystemAPI.GetComponentLookup<NetworkNode>(true);
            var connectorLookup = SystemAPI.GetComponentLookup<ConveyorConnector>(true);

             Entities
                .WithReadOnly(powerRatioByNet)
                .WithReadOnly(networkNodeLookup)
                .WithReadOnly(connectorLookup)
                .ForEach((Entity e, ref RoutePowerStatus status, ref NetworkNode node, ref RouteNetworkInfo networkInfo, in RouteDefinition routeDef) => {
                    
                    if (connectorLookup.TryGetComponent(routeDef.StartConnector, out var startConn))
                    {
                         if (networkNodeLookup.TryGetComponent(startConn.Owner, out var startNode))
                             networkInfo.StartSubnetId = startNode.SubnetId;
                         else
                             networkInfo.StartSubnetId = 0;
                    }

                    if (connectorLookup.TryGetComponent(routeDef.EndConnector, out var endConn))
                    {
                         if (networkNodeLookup.TryGetComponent(endConn.Owner, out var endNode))
                             networkInfo.EndSubnetId = endNode.SubnetId;
                         else
                             networkInfo.EndSubnetId = 0;
                    }

                    float startRatio = powerRatioByNet.TryGetValue(networkInfo.StartSubnetId, out var r1) ? r1 : 0f;
                    float endRatio = powerRatioByNet.TryGetValue(networkInfo.EndSubnetId, out var r2) ? r2 : 0f;

                    int bestSubnetId;
                    float finalRatio;

                    if (startRatio >= endRatio)
                    {
                        finalRatio = startRatio;
                        bestSubnetId = networkInfo.StartSubnetId;
                    }
                    else
                    {
                        finalRatio = endRatio;
                        bestSubnetId = networkInfo.EndSubnetId;
                    }

                    if (math.abs(status.PowerRatio - finalRatio) > 0.01f && finalRatio > 0.01f) {
                         Debug.Log($"<color=#9966CC>[DEBUG/EnergyFeedback]</color> Маршрут {e}: StartNet[#{networkInfo.StartSubnetId}] Ratio={startRatio:F2}, EndNet[#{networkInfo.EndSubnetId}] Ratio={endRatio:F2}. <color=white>FinalRatio={finalRatio:F2}, BestNet=#{bestSubnetId}</color>");
                    }

                    status.PowerRatio = finalRatio;
                    node.SubnetId = bestSubnetId;

                }).Run();

            keys.Dispose();
        }
    }
    
    public static class NativeHashMapExtensionsForFeedbackFinalV3
    {
        public static void Increment(this NativeHashMap<int, float> map, int key, float value)
        {
            if (map.TryGetValue(key, out float current))
            {
                map[key] = current + value;
            }
            else
            {
                map.TryAdd(key, value);
            }
        }
    }
}