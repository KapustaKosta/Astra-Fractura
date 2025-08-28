using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ConveyorDebugSystem : SystemBase
    {
        private EntityQuery _freeQ;
        private EntityQuery _occQ;

        protected override void OnCreate()
        {
            _freeQ = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ConveyorConnector>(), ComponentType.ReadOnly<LocalToWorld>() },
                None = new[] { ComponentType.ReadOnly<ConveyorOccupiedTag>(), ComponentType.ReadOnly<ConveyorGhostTag>() } // ИСПРАВЛЕНО
            });

            _occQ = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ConveyorConnector>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<ConveyorOccupiedTag>() }, // ИСПРАВЛЕНО
                None = new[] { ComponentType.ReadOnly<ConveyorGhostTag>() }
            });
        }

        protected override void OnUpdate()
        {
            int free = _freeQ.CalculateEntityCount();
            int occ = _occQ.CalculateEntityCount();

            Entity snapTarget = Entity.Null;
            if (SystemAPI.TryGetSingletonEntity<GameState>(out var gs) &&
                SystemAPI.HasComponent<ConveyorState>(gs))
            {
                var st = SystemAPI.GetComponent<ConveyorState>(gs);
                snapTarget = st.StartConnector;
            }
        }
    }
}