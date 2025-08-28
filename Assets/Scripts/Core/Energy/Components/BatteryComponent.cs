using Unity.Entities;
using Unity.Collections;

namespace Energy.Core
{
    // Аккумуляторный блок / BESS
    public struct BatteryComponent : IComponentData
    {
        public FixedString64Bytes Name;
        public float CapacityKWh;     // Ёмкость (kWh)
        public float SoC;             // 0..1
        public float CurrentKW;       // + разрядка в сеть; − заряд из сети
        public float MaxChargeKW;     // Ограничение на заряд (kW)
        public float MaxDischargeKW;  // Ограничение на разряд (kW)
        public int NetworkId;
    }
}