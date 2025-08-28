using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Energy.Core;
using Wiring; // Wire

namespace Energy.Core.Systems
{
    /// <summary>
    /// Суммирует пропускную способность по всем проводам и записывает NetLinkCapacity.MaxKW у владельцев коннекторов.
    /// Безопасно: сначала копим в map, затем применяем через ECB (никаких структурных изменений при чтении Lookups).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NetworkDiscoverySystem))]
    public partial class BuildLinkCapacitySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // Подстраховка: если bootstrap не выставил таблицу — используем дефолты
            WireCapacity.EnsureDefaultsIfEmpty();

            // Сбрасываем текущие значения у существующих NetLinkCapacity (это не структурное изменение)
            Entities.ForEach((ref NetLinkCapacity cap) => { cap.MaxKW = 0f; }).Run();

            // Read-only lookups
            var parents = GetComponentLookup<Parent>(true);
            var hasNode = GetComponentLookup<NetworkNode>(true);

            // Собираем провода
            var wireQuery = GetEntityQuery(ComponentType.ReadOnly<Wire>());
            var wires = wireQuery.ToComponentDataArray<Wire>(Allocator.Temp);

            // Копим суммы по владельцам концов
            var capByOwner = new NativeParallelHashMap<Entity, float>(math.max(8, wires.Length * 2), Allocator.Temp);

            for (int i = 0; i < wires.Length; i++)
            {
                var w = wires[i];
                int lvl = w.Level <= 0 ? 1 : w.Level;
                float capKW = WireCapacity.Get(lvl);

                if (parents.HasComponent(w.StartConnector))
                {
                    var owner = parents[w.StartConnector].Value;
                    if (hasNode.HasComponent(owner))
                        AddOrAcc(ref capByOwner, owner, capKW);
                }

                if (parents.HasComponent(w.EndConnector))
                {
                    var owner = parents[w.EndConnector].Value;
                    if (hasNode.HasComponent(owner))
                        AddOrAcc(ref capByOwner, owner, capKW);
                }
            }

            // Применяем через ECB
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var owners = capByOwner.GetKeyArray(Allocator.Temp);
            for (int k = 0; k < owners.Length; k++)
            {
                var owner = owners[k];
                capByOwner.TryGetValue(owner, out var cap);

                if (!em.HasComponent<NetLinkCapacity>(owner))
                    ecb.AddComponent(owner, new NetLinkCapacity { MaxKW = cap });
                else
                    ecb.SetComponent(owner, new NetLinkCapacity { MaxKW = cap });
            }
            ecb.Playback(em);
            ecb.Dispose();

            // Уборка
            owners.Dispose();
            capByOwner.Dispose();
            wires.Dispose();
        }

        private static void AddOrAcc(ref NativeParallelHashMap<Entity, float> map, Entity key, float add)
        {
            if (map.TryGetValue(key, out var cur))
            {
                map.Remove(key);
                map.TryAdd(key, cur + add);
            }
            else map.TryAdd(key, add);
        }
    }
}
