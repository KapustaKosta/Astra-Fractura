using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Conveyor
{
#if UNITY_EDITOR
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ConveyorDebugSystem : SystemBase
    {
        private double _nextLogTime = 0.0;

        protected override void OnUpdate()
        {
            foreach(var (ltw, runtimeLength)
                    in SystemAPI.Query<RefRO<LocalToWorld>, RefRO<ConveyorSegmentRuntimeLength>>())
            {
                var position = ltw.ValueRO.Position;
                var forward = ltw.ValueRO.Forward;
                var length = runtimeLength.ValueRO.Value;

                float3 startPoint = position - forward * (length / 2f);
                float3 endPoint = position + forward * (length / 2f);
                
                Debug.DrawLine(startPoint, endPoint, Color.cyan);
                Debug.DrawRay(startPoint, Vector3.up * 0.5f, Color.yellow);
                Debug.DrawRay(endPoint, Vector3.up * 0.5f, Color.red);
            }
            
            if (SystemAPI.Time.ElapsedTime < _nextLogTime)
            {
                return;
            }
            _nextLogTime = SystemAPI.Time.ElapsedTime + 1.0; 

            var itemQuery = SystemAPI.QueryBuilder().WithAll<LocalToWorld, ItemVisualTag, ConveyorVisualProgress>().Build();
            if (itemQuery.IsEmpty) return;
            
            using (var items = itemQuery.ToEntityArray(Allocator.Temp))
            {
                if (items.Length > 0)
                {
                    Entity firstItemEntity = items[0];
                    var itemLtw = SystemAPI.GetComponent<LocalToWorld>(firstItemEntity);
                    var itemProgress = SystemAPI.GetComponent<ConveyorVisualProgress>(firstItemEntity);
                    
                    Debug.Log($"<color=yellow>[ConveyorItem] Pos: {itemLtw.Position}," +
                              $" Progress: (Joint: {itemProgress.CurrentJointIndex}," +
                              $" DistOnSeg: {itemProgress.DistanceOnSegment:F2})" +
                              $" Speed: {itemProgress.Speed:F2}</color>");
                }
            }
        }
    }
#endif
}