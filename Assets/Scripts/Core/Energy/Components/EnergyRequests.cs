using Unity.Entities;

namespace Energy.Core
{
    public struct OpenGeneratorUIRequest : IComponentData
    {
        public Entity Target;
    }

    public struct OpenBatteryUIRequest : IComponentData
    {
        public Entity Target;
    }

    // -1 => показать все сети
    public struct OpenNetworkOverviewUIRequest : IComponentData
    {
        public int NetworkId;
    }

    public struct ToggleGeneratorRequest : IComponentData
    {
        public Entity Target;
        public bool DesiredOn;
    }
}