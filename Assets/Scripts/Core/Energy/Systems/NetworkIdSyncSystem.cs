using Unity.Entities;

namespace Energy.Core.Systems
{
    /// <summary>
    /// В редакторе и дев-сборках копирует фактический SubnetId в поле NetworkId компонентов
    /// (чтобы в инспекторе было видно корректную сеть у генераторов/батарей/нагрузок).
    /// Никакой логики — чисто визуальная синхронизация для дебага.
    /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NetworkDiscoverySystem))]
    public partial class NetworkIdSyncSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.WithoutBurst().ForEach((ref GeneratorComponent gen, in NetworkNode node) =>
            {
                gen.NetworkId = node.SubnetId;
            }).Run();

            Entities.WithoutBurst().ForEach((ref BatteryComponent bat, in NetworkNode node) =>
            {
                bat.NetworkId = node.SubnetId;
            }).Run();

            Entities.WithoutBurst().ForEach((ref ConsumerLoad load, in NetworkNode node) =>
            {
                load.NetworkId = node.SubnetId;
            }).Run();
        }
    }
#endif
}
