using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Energy.Core;
using Wiring;

namespace Energy.Core.Systems
{
    /// <summary>
    /// Раз в 5 секунд печатает список проводов и какие узлы (сети) они соединяют.
    /// Показывает уровень и ёмкость провода.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NetworkDiscoverySystem))]
    public partial class WireNetDebugSystem : SystemBase
    {
        private float _timer;

        protected override void OnUpdate()
        {
            _timer += SystemAPI.Time.DeltaTime;
            if (_timer < 5f) return;
            _timer = 0f;

            var em = EntityManager;

            var wires = GetEntityQuery(ComponentType.ReadOnly<Wire>())
                .ToComponentDataArray<Wire>(Allocator.Temp);

            var hasNode = GetComponentLookup<NetworkNode>(true);
            var parents = GetComponentLookup<Parent>(true);

            Debug.Log($"[WireDebug] wires={wires.Length}");

            for (int i = 0; i < wires.Length; i++)
            {
                var w = wires[i];

                Entity aOwner = Entity.Null;
                if (parents.HasComponent(w.StartConnector))
                {
                    var p = parents[w.StartConnector].Value;
                    if (hasNode.HasComponent(p)) aOwner = p;
                }

                Entity bOwner = Entity.Null;
                if (parents.HasComponent(w.EndConnector))
                {
                    var p = parents[w.EndConnector].Value;
                    if (hasNode.HasComponent(p)) bOwner = p;
                }

                int netA = aOwner != Entity.Null ? em.GetComponentData<NetworkNode>(aOwner).SubnetId : -1;
                int netB = bOwner != Entity.Null ? em.GetComponentData<NetworkNode>(bOwner).SubnetId : -1;

                string aName = aOwner != Entity.Null
                    ? em.GetComponentData<NetworkNode>(aOwner).Name.ToString()
                    : "<no owner>";
                string bName = bOwner != Entity.Null
                    ? em.GetComponentData<NetworkNode>(bOwner).Name.ToString()
                    : "<no owner>";

                int lvl = w.Level <= 0 ? 1 : w.Level;
                float capKW = WireCapacity.Get(lvl);
                string capStr = float.IsPositiveInfinity(capKW) ? "∞" : $"{capKW:F1}kW";

                Debug.Log($"[WireDebug] {i + 1}: {aName} (net {netA})  <==[L{lvl} {capStr}]==>  {bName} (net {netB})");
            }

            wires.Dispose();
        }
    }
}
