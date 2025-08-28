using Unity.Entities;

namespace Energy.Core
{
    // Нагрузка (агрегированная или точечная)
    public struct ConsumerLoad : IComponentData
    {
        public float CurrentKW; // Потребление (kW)
        public int NetworkId;
    }
}