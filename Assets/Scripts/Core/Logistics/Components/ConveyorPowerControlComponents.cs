using Unity.Entities;

namespace Conveyor
{
    public struct UserEnabledRouteTag : IComponentData { }

    public struct RoutePowerScaling : IComponentData
    {
        public float SpeedMultiplier;
    }
}