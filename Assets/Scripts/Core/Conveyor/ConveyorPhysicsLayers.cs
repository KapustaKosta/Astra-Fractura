using Unity.Physics;

namespace Conveyor
{
    /// <summary>
    /// Единая точка правды по физическим маскам/категориям конвейерной подсистемы.
    /// </summary>
    public static class ConveyorPhysicsLayers
    {
        /// <summary>
        /// Unity Layer Index, — 12.
        /// </summary>
        public const int ConnectorUnityLayer = 12;

        /// <summary>
        /// DOTS Physics категория (битовая маска), соответствующая Unity Layer #12.
        /// </summary>
        public const uint ConnectorCategory = 1u << ConnectorUnityLayer;

        /// <summary>
        /// Фильтр "бить лучом только по коннекторам".
        /// </summary>
        public static CollisionFilter ConnectorOnlyFilter => new CollisionFilter
        {
            BelongsTo = ~0u,                // кто кидает луч — неважно
            CollidesWith = ConnectorCategory,  // попадать только по коннекторам
            GroupIndex = 0
        };
    }
}
