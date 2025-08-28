using Energy.Core;
using Energy.Core.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Wiring; 

/// <summary>
/// Разметка подсетей с учётом проводов:
/// - Собираем все сущности с NetworkNode (узлы).
/// - Для каждого провода находим владельцев его коннекторов (Parent),
///   и объединяем узлы этих владельцев в одну компоненту связности (Union-Find).
/// - Присваиваем последовательные SubnetId начиная с 1.
/// Триггерится наличием тега NetworkTopologyChanged (создаётся при прокладке провода).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(EnergyDispatchSystem))]
public partial class NetworkDiscoverySystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<NetworkTopologyChanged>();
    }

    protected override void OnUpdate()
    {
        var em = EntityManager;

        // Собираем все узлы сети 
        var nodeQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkNode>());
        var nodes = nodeQuery.ToEntityArray(Allocator.TempJob);
        int n = nodes.Length;

        if (n == 0)
        {
            // Снимаем триггер и выходим.
            em.DestroyEntity(GetEntityQuery(ComponentType.ReadOnly<NetworkTopologyChanged>()));
            nodes.Dispose();
            return;
        }

        // Индексация: Entity -> int (0..n-1)
        var indexOf = new NativeParallelHashMap<Entity, int>(n, Allocator.Temp);
        for (int i = 0; i < n; i++)
            indexOf.TryAdd(nodes[i], i);

        // DSU (union-find)
        var parent = new NativeArray<int>(n, Allocator.Temp);
        for (int i = 0; i < n; i++) parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }
        void Union(int a, int b)
        {
            int ra = Find(a);
            int rb = Find(b);
            if (ra != rb) parent[rb] = ra;
        }

        // Обходим все провода и объединяем владельцев концов 
        var wireQuery = GetEntityQuery(ComponentType.ReadOnly<Wire>());
        var wires = wireQuery.ToComponentDataArray<Wire>(Allocator.TempJob);

        // Быстрые лукапы
        var hasNode = GetComponentLookup<NetworkNode>(true);
        var parents = GetComponentLookup<Parent>(true);

        for (int i = 0; i < wires.Length; i++)
        {
            var w = wires[i];

            // Владелец коннектора = его Parent. Обычно это сущность здания/устройства,
            // на котором висит NetworkNode (генератор/батарея/нагрузка).
            Entity aOwner = Entity.Null;
            if (parents.HasComponent(w.StartConnector))
            {
                var p = parents[w.StartConnector].Value;
                if (hasNode.HasComponent(p)) aOwner = p;
            }

            Entity bOwner = Entity.Null;
            if (parents.HasComponent(w.EndConnector))
            {
                var p = parents[w.EndConnector].Value;
                if (hasNode.HasComponent(p)) bOwner = p;
            }

            if (aOwner != Entity.Null && bOwner != Entity.Null &&
                indexOf.TryGetValue(aOwner, out int ia) &&
                indexOf.TryGetValue(bOwner, out int ib))
            {
                Union(ia, ib);
            }
        }

        // Нумеруем компоненты связности подряд: 1..K 
        var rootToSubnet = new NativeParallelHashMap<int, int>(n, Allocator.Temp);
        int nextId = 1;

        for (int i = 0; i < n; i++)
        {
            int r = Find(i);
            if (!rootToSubnet.TryGetValue(r, out int sid))
            {
                sid = nextId++;
                rootToSubnet.TryAdd(r, sid);
            }

            var node = em.GetComponentData<NetworkNode>(nodes[i]);
            node.SubnetId = sid;
            em.SetComponentData(nodes[i], node);
        }

        // Очистка триггера
        em.DestroyEntity(GetEntityQuery(ComponentType.ReadOnly<NetworkTopologyChanged>()));

        // Dispose
        rootToSubnet.Dispose();
        parent.Dispose();
        indexOf.Dispose();
        wires.Dispose();
        nodes.Dispose();
    }
}
