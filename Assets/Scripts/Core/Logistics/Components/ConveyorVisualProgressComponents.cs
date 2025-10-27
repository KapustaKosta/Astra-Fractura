using Unity.Entities;

namespace Conveyor
{
    public struct ConveyorVisualProgress : IComponentData
    {
        public float TotalDistanceTraveled;
        public float Speed;
        public float TotalLength;
        
        public int CurrentJointIndex;
        public float DistanceOnSegment;
    }
    
    public struct ConveyorVisualNeedsInitTag : IComponentData { }
}