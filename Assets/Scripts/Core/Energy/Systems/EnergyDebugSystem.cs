using System.Collections.Generic;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Energy.Core.Systems
{
    /// <summary>
    /// Телеметрия энергосистемы раз в 5 секунд:
    ///  - по подсетям: спрос, генераторы (count/online/rated/output), батареи (count/cap/avgSoC/flow);
    ///  - по узлам: использование портов In/Out относительно лимита (NetLinkUsage/NetLinkCapacity).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnergyDispatchSystem))]
    public partial class EnergyDebugSystem : SystemBase
    {
        private double _nextLogTime;

        protected override void OnCreate()
        {
            _nextLogTime = 0;
        }

        protected override void OnUpdate()
        {
            double t = SystemAPI.Time.ElapsedTime;
            if (t < _nextLogTime) return;
            _nextLogTime = t + 5.0;

            var demandByNet = new Dictionary<int, float>();
            var genRatedByNet = new Dictionary<int, float>();
            var genOutputByNet = new Dictionary<int, float>();
            var genOnlineByNet = new Dictionary<int, int>();
            var genCountByNet = new Dictionary<int, int>();
            var batCapByNet = new Dictionary<int, float>();
            var batSocEnergyByNet = new Dictionary<int, float>();
            var batCountByNet = new Dictionary<int, int>();
            var batFlowByNet = new Dictionary<int, float>();

            int gensTotal = 0, gensWithNet = 0, gensOnlineTotal = 0;
            int batsTotal = 0, batsWithNet = 0;
            int loadsTotal = 0, loadsWithNet = 0;

            // Loads
            Entities.WithoutBurst().ForEach((in ConsumerLoad load, in NetworkNode node) =>
            {
                loadsWithNet++;
                Acc(ref demandByNet, node.SubnetId, math.max(0f, load.CurrentKW));
            }).Run();

            // Generators
            Entities.WithoutBurst().ForEach((in GeneratorComponent gen, in NetworkNode node) =>
            {
                gensWithNet++;
                Acc(ref genCountByNet, node.SubnetId, 1);
                if (gen.IsOnline)
                {
                    Acc(ref genOnlineByNet, node.SubnetId, 1);
                    gensOnlineTotal++;
                }
                Acc(ref genRatedByNet, node.SubnetId, math.max(0f, gen.RatedKW));
                Acc(ref genOutputByNet, node.SubnetId, math.max(0f, gen.CurrentKW));
            }).Run();

            // Batteries
            Entities.WithoutBurst().ForEach((in BatteryComponent bat, in NetworkNode node) =>
            {
                batsWithNet++;
                Acc(ref batCountByNet, node.SubnetId, 1);
                float cap = math.max(0f, bat.CapacityKWh);
                Acc(ref batCapByNet, node.SubnetId, cap);
                Acc(ref batSocEnergyByNet, node.SubnetId, cap * math.saturate(bat.SoC));
                Acc(ref batFlowByNet, node.SubnetId, bat.CurrentKW);
            }).Run();

            // Totals
            Entities.WithoutBurst().ForEach((in GeneratorComponent _) => { gensTotal++; }).Run();
            Entities.WithoutBurst().ForEach((in BatteryComponent _) => { batsTotal++; }).Run();
            Entities.WithoutBurst().ForEach((in ConsumerLoad _) => { loadsTotal++; }).Run();

            // Nodes port usage
            var nodeCaps = new List<NodePortLine>(64);
            Entities.WithoutBurst().ForEach((Entity e, in NetworkNode node, in NetLinkUsage usage) =>
            {
                float cap = float.PositiveInfinity;
                if (SystemAPI.HasComponent<NetLinkCapacity>(e))
                    cap = SystemAPI.GetComponent<NetLinkCapacity>(e).MaxKW;

                nodeCaps.Add(new NodePortLine
                {
                    Net = node.SubnetId,
                    Label = $"Node#{e.Index}",
                    InUsed = usage.InUsedKW,
                    OutUsed = usage.OutUsedKW,
                    Cap = cap
                });
            }).Run();

            var sb = new StringBuilder(2048);
            sb.AppendLine($"[EnergyDebug] t={t:F1}s");
            sb.AppendLine(
                $" Entities: Gens total={gensTotal} (withNet={gensWithNet}, online={gensOnlineTotal}, noNet={gensTotal - gensWithNet}); " +
                $"Bats total={batsTotal} (withNet={batsWithNet}, noNet={batsTotal - batsWithNet}); " +
                $"Loads total={loadsTotal} (withNet={loadsWithNet}, noNet={loadsTotal - loadsWithNet})"
            );

            var nets = new HashSet<int>();
            AddKeys(nets, demandByNet);
            AddKeys(nets, genRatedByNet);
            AddKeys(nets, genOutputByNet);
            AddKeys(nets, genOnlineByNet);
            AddKeys(nets, genCountByNet);
            AddKeys(nets, batCapByNet);
            AddKeys(nets, batSocEnergyByNet);
            AddKeys(nets, batCountByNet);
            AddKeys(nets, batFlowByNet);
            foreach (var npl in nodeCaps) nets.Add(npl.Net);

            if (nets.Count == 0)
            {
                sb.AppendLine(" No networks found yet (SubnetId map is empty). Проверь работу NetworkDiscoverySystem.");
            }
            else
            {
                foreach (var net in SortAscending(nets))
                {
                    demandByNet.TryGetValue(net, out var demand);
                    genRatedByNet.TryGetValue(net, out var genRated);
                    genOutputByNet.TryGetValue(net, out var genOut);
                    genCountByNet.TryGetValue(net, out var gCount);
                    genOnlineByNet.TryGetValue(net, out var gOnline);

                    batCapByNet.TryGetValue(net, out var cap);
                    batSocEnergyByNet.TryGetValue(net, out var socEnergy);
                    batCountByNet.TryGetValue(net, out var bCount);
                    batFlowByNet.TryGetValue(net, out var batFlow);

                    float avgSoC = (cap > 1e-6f) ? math.clamp(socEnergy / cap, 0f, 1f) : 0f;

                    sb.AppendLine(
                        $" Net {net}: " +
                        $"Demand={demand:F2} kW | " +
                        $"Gen: count={gCount}, online={gOnline}, rated={genRated:F2} kW, output={genOut:F2} kW | " +
                        $"Bat: count={bCount}, cap={cap:F2} kWh, avgSoC={(avgSoC * 100f):F1}%, flow={batFlow:F2} kW");

                    int printed = 0;
                    for (int i = 0; i < nodeCaps.Count && printed < 8; i++)
                    {
                        var c = nodeCaps[i];
                        if (c.Net != net) continue;
                        string capStr = float.IsPositiveInfinity(c.Cap) ? "∞" : c.Cap.ToString("F1");
                        sb.AppendLine($"  • {c.Label}: In {c.InUsed:F2}/{capStr} kW | Out {c.OutUsed:F2}/{capStr} kW");
                        printed++;
                    }
                    if (printed == 0) sb.AppendLine("  • (нет данных по использованию портов)");

                    if (demand > 0 && gCount == 0 && bCount == 0)
                        sb.AppendLine("  ! В сети нет ни генераторов, ни батарей.");

                    if (demand > 0 && gCount > 0 && genOut <= 1e-5f)
                        sb.AppendLine("  ! Генераторы есть, но отдают 0 кВт. Проверить: IsOnline, RatedKW>0, SubnetId, лимиты портов.");

                    if (demand > 0 && genOut < demand && bCount > 0 && math.abs(batFlow) <= 1e-5f)
                        sb.AppendLine("  ! Батареи не реагируют на дефицит/профицит. Проверить: MaxDis/MaxCharge, SoC/Capacity, лимиты портов, dt.");
                }
            }

            Debug.Log(sb.ToString());
        }

        private struct NodePortLine
        {
            public int Net;
            public string Label;
            public float InUsed;
            public float OutUsed;
            public float Cap;
        }

        private static void Acc(ref Dictionary<int, float> dict, int key, float delta)
        {
            if (!dict.TryGetValue(key, out var cur)) dict[key] = delta;
            else dict[key] = cur + delta;
        }

        private static void Acc(ref Dictionary<int, int> dict, int key, int delta)
        {
            if (!dict.TryGetValue(key, out var cur)) dict[key] = delta;
            else dict[key] = cur + delta;
        }

        private static void AddKeys<T>(HashSet<int> target, Dictionary<int, T> src)
        {
            foreach (var k in src.Keys) target.Add(k);
        }

        private static IEnumerable<int> SortAscending(HashSet<int> set)
        {
            var list = new List<int>(set);
            list.Sort();
            return list;
        }
    }
}
