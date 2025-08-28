// Assets/Scripts/Core/Energy/Systems/EnergyDispatchSystem.cs
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Energy.Core;

namespace Energy.Core.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EnergyDispatchSystem : SystemBase
    {
        private const bool ChargeWithSurplus = true;
        private const bool ShowTransit = true; // включена визуализация транзита отдельными полями

        #region DTO
        struct GenInfo
        {
            public Entity Entity;
            public Entity Owner;
            public int NetId;
            public float RatedKW;
            public float CurrentKW;
            public bool Online;
            public float RampUp;
            public float RampDown;
        }

        struct BatInfo
        {
            public Entity Entity;
            public Entity Owner;
            public int NetId;
            public float CapacityKWh;
            public float SoC;
            public float CurrentKW;        // + разряд, - заряд
            public float MaxChargeKW;
            public float MaxDischargeKW;
            public float RampCharge;
            public float RampDischarge;
        }

        struct LoadInfo
        {
            public Entity Entity;
            public Entity Owner;
            public int NetId;
            public float DemandKW;
        }
        #endregion

        #region helpers
        private Entity ResolveNodeOwner(Entity e)
        {
            if (e == Entity.Null || !EntityManager.Exists(e)) return Entity.Null;
            if (EntityManager.HasComponent<NetworkNode>(e)) return e;

            int guard = 16;
            var cur = e;
            while (guard-- > 0 && EntityManager.Exists(cur))
            {
                if (EntityManager.HasComponent<NetworkNode>(cur)) return cur;
                if (!EntityManager.HasComponent<Parent>(cur)) break;
                cur = EntityManager.GetComponentData<Parent>(cur).Value;
            }
            return Entity.Null;
        }

        private float GetNodeCap(Entity node)
        {
            if (node == Entity.Null || !EntityManager.Exists(node)) return 0f;
            return EntityManager.HasComponent<NetLinkCapacity>(node)
                ? EntityManager.GetComponentData<NetLinkCapacity>(node).MaxKW
                : 0f;
        }

        private static float Ramp(float current, float target, float upKWps, float downKWps, float dt)
        {
            if (!float.IsFinite(upKWps)) upKWps = float.PositiveInfinity;
            if (!float.IsFinite(downKWps)) downKWps = float.PositiveInfinity;

            float delta = target - current;
            float rate = (delta >= 0f ? upKWps : downKWps);
            float maxStep = rate * dt;

            // ∞ — безлимитная скорость: можно мгновенно прийти к цели
            if (!float.IsFinite(maxStep)) return target;

            // 0 или отрицательная скорость — НЕ менять текущее значение
            if (maxStep <= 0f) return current;

            float step = math.clamp(delta, -maxStep, +maxStep);
            float res = current + step;
            if (math.abs(res) < 1e-4f && math.abs(target) < 1e-4f) res = 0f;
            return res;
        }

        private void FinalizeTransitAtNodes()
        {
            foreach (var usage in SystemAPI.Query<RefRW<NetLinkUsage>>())
            {
                var u = usage.ValueRW;
                float edgeIn = u.TransitInKW;
                float edgeOut = u.TransitOutKW;
                float localIn = u.InUsedKW;
                float localOut = u.OutUsedKW;
                u.TransitInKW = math.max(0f, edgeIn - localIn);
                u.TransitOutKW = math.max(0f, edgeOut - localOut);
                usage.ValueRW = u;
            }
        }
        private static void DictAdd(Dictionary<Entity, float> d, Entity k, float v)
        {
            if (d.TryGetValue(k, out var old)) d[k] = old + v;
            else d[k] = v;
        }
        private static float DictGet(Dictionary<Entity, float> d, Entity k, float def = 0f)
            => d.TryGetValue(k, out var v) ? v : def;

        private void SafeZeroUsageAll()
        {
            foreach (var usage in SystemAPI.Query<RefRW<NetLinkUsage>>())
                usage.ValueRW = new NetLinkUsage();
        }

        private void SafeSetInUsage(Entity node, float value)
        {
            if (!EntityManager.Exists(node)) return;
            float cap = GetNodeCap(node);
            if (float.IsFinite(cap)) value = math.min(value, cap);

            if (EntityManager.HasComponent<NetLinkUsage>(node))
            {
                var rw = SystemAPI.GetComponentRW<NetLinkUsage>(node);
                var u = rw.ValueRO; u.InUsedKW = value; rw.ValueRW = u;
            }
            else
            {
                EntityManager.AddComponentData(node, new NetLinkUsage { InUsedKW = value });
            }
        }

        private void SafeSetOutUsage(Entity node, float value)
        {
            if (!EntityManager.Exists(node)) return;
            float cap = GetNodeCap(node);
            if (float.IsFinite(cap)) value = math.min(value, cap);

            if (EntityManager.HasComponent<NetLinkUsage>(node))
            {
                var rw = SystemAPI.GetComponentRW<NetLinkUsage>(node);
                var u = rw.ValueRO; u.OutUsedKW = value; rw.ValueRW = u;
            }
            else
            {
                EntityManager.AddComponentData(node, new NetLinkUsage { OutUsedKW = value });
            }
        }

        private void SafeSetTransit(Entity node, float inVal, float outVal)
        {
            if (!EntityManager.Exists(node)) return;
            float cap = GetNodeCap(node);
            if (float.IsFinite(cap))
            {
                inVal = math.min(inVal, cap);
                outVal = math.min(outVal, cap);
            }

            if (EntityManager.HasComponent<NetLinkUsage>(node))
            {
                var rw = SystemAPI.GetComponentRW<NetLinkUsage>(node);
                var u = rw.ValueRO;
                u.TransitInKW = inVal;
                u.TransitOutKW = outVal;
                rw.ValueRW = u;
            }
            else
            {
                EntityManager.AddComponentData(node, new NetLinkUsage { TransitInKW = inVal, TransitOutKW = outVal });
            }
        }
        #endregion

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            float dtHours = dt / 3600f;

            SafeZeroUsageAll();

            var gens = new List<GenInfo>(16);
            var bats = new List<BatInfo>(16);
            var loads = new List<LoadInfo>(16);

            // Генераторы 
            foreach (var (genRO, ent) in SystemAPI.Query<RefRO<GeneratorComponent>>().WithEntityAccess())
            {
                var g = genRO.ValueRO;

                // ДЕФОЛТ: если компонента GeneratorRamp нет — идём к номиналу за ~5 секунд.
                float defaultRate = math.max(0.001f, math.max(1e-6f, g.RatedKW) / 5f);
                float ru = defaultRate, rd = defaultRate;

                if (SystemAPI.HasComponent<GeneratorRamp>(ent))
                {
                    var r = SystemAPI.GetComponent<GeneratorRamp>(ent);
                    ru = math.max(0f, r.UpKWps);
                    rd = math.max(0f, r.DownKWps);
                }

                gens.Add(new GenInfo
                {
                    Entity = ent,
                    Owner = ResolveNodeOwner(ent),
                    NetId = g.NetworkId,
                    RatedKW = math.max(0f, g.RatedKW),
                    CurrentKW = g.CurrentKW,
                    Online = g.IsOnline,
                    RampUp = ru,
                    RampDown = rd
                });
            }

            // Батареи 
            foreach (var (batRO, ent) in SystemAPI.Query<RefRO<BatteryComponent>>().WithEntityAccess())
            {
                var b = batRO.ValueRO;

                // ДЕФОЛТ: плавно выходим на план за ~5 секунд от предельной мощности
                float defCharge = math.max(0.001f, math.max(1e-6f, b.MaxChargeKW) / 5f);
                float defDis = math.max(0.001f, math.max(1e-6f, b.MaxDischargeKW) / 5f);
                float rc = defCharge, rd = defDis;

                if (SystemAPI.HasComponent<BatteryRamp>(ent))
                {
                    var r = SystemAPI.GetComponent<BatteryRamp>(ent);
                    rc = math.max(0f, r.ChargeKWps);
                    rd = math.max(0f, r.DischargeKWps);
                }

                bats.Add(new BatInfo
                {
                    Entity = ent,
                    Owner = ResolveNodeOwner(ent),
                    NetId = b.NetworkId,
                    CapacityKWh = math.max(1e-6f, b.CapacityKWh),
                    SoC = math.saturate(b.SoC),
                    CurrentKW = b.CurrentKW,
                    MaxChargeKW = math.max(0f, b.MaxChargeKW),
                    MaxDischargeKW = math.max(0f, b.MaxDischargeKW),
                    RampCharge = rc,
                    RampDischarge = rd
                });
            }

            // Нагрузки 
            foreach (var (loadRO, ent) in SystemAPI.Query<RefRO<ConsumerLoad>>().WithEntityAccess())
            {
                var l = loadRO.ValueRO;
                loads.Add(new LoadInfo
                {
                    Entity = ent,
                    Owner = ResolveNodeOwner(ent),
                    NetId = l.NetworkId,
                    DemandKW = math.max(0f, l.CurrentKW)
                });
            }

            var nets = new HashSet<int>();
            foreach (var x in gens) nets.Add(x.NetId);
            foreach (var x in bats) nets.Add(x.NetId);
            foreach (var x in loads) nets.Add(x.NetId);

            foreach (var netId in nets)
            {
                // Не фильтруем по Online — нужно уметь плавно гасить выключающиеся генераторы
                var gList = gens.FindAll(g => g.NetId == netId && g.RatedKW > 1e-6f);
                var bList = bats.FindAll(b => b.NetId == netId);
                var lList = loads.FindAll(l => l.NetId == netId && l.DemandKW > 1e-6f);

                var usedIn = new Dictionary<Entity, float>(16);
                var usedOut = new Dictionary<Entity, float>(16);

                float totalDemand = 0f, totalLoadCapIn = 0f;
                foreach (var L in lList)
                {
                    totalDemand += L.DemandKW;
                    totalLoadCapIn += math.min(GetNodeCap(L.Owner), L.DemandKW);
                }
                float deliverableDemand = math.min(totalDemand, totalLoadCapIn);

                // Генераторы: идём к цели через Ramp()
                float actualGenKW = 0f;
                for (int i = 0; i < gList.Count; i++)
                {
                    var gi = gList[i];
                    float cap = GetNodeCap(gi.Owner);

                    float target = gi.Online ? math.min(gi.RatedKW, cap) : 0f;

                    float applied = Ramp(gi.CurrentKW, target, gi.RampUp, gi.RampDown, dt);
                    applied = math.clamp(applied, 0f, cap);

                    if (applied > 1e-4f)
                    {
                        DictAdd(usedOut, gi.Owner, applied);
                    }

                    var rw = SystemAPI.GetComponentRW<GeneratorComponent>(gi.Entity);
                    rw.ValueRW.CurrentKW = applied;

                    gi.CurrentKW = applied; gList[i] = gi;

                    if (gi.Online)
                    {
                        actualGenKW += applied;
                    }
                }

                // Первая раздача генерации нагрузкам (пропорционально входной способности)
                float supplyForLoads = math.min(deliverableDemand, actualGenKW);
                float capSum = 0f; foreach (var L in lList) capSum += math.min(GetNodeCap(L.Owner), L.DemandKW);
                if (supplyForLoads > 1e-6f && capSum > 1e-6f)
                {
                    float k = supplyForLoads / capSum;
                    foreach (var L in lList)
                    {
                        float capIn = math.min(GetNodeCap(L.Owner), L.DemandKW);
                        float give = capIn * k;
                        if (give > 1e-6f) DictAdd(usedIn, L.Owner, give);
                    }
                }

                // Headroom — что ещё можем довезти до нагрузок
                var headroom = new Dictionary<Entity, float>(lList.Count);
                float totalHeadroom = 0f;
                foreach (var L in lList)
                {
                    float capIn = math.min(GetNodeCap(L.Owner), L.DemandKW);
                    float already = DictGet(usedIn, L.Owner, 0f);
                    float rest = math.max(0f, capIn - already);
                    if (rest > 1e-6f) { headroom[L.Owner] = rest; totalHeadroom += rest; }
                }

                // Дефицит покрываем разрядом батарей (с рампом)
                float deficit = math.max(0f, deliverableDemand - actualGenKW);
                var batTouched = new bool[bList.Count];
                float batDisKW = 0f;

                if (deficit > 1e-6f && bList.Count > 0)
                {
                    var disCaps = new float[bList.Count];
                    float totalDis = 0f;
                    for (int b = 0; b < bList.Count; b++)
                    {
                        var bi = bList[b];
                        float cap = math.min(GetNodeCap(bi.Owner), bi.MaxDischargeKW);
                        if (bi.SoC <= 1e-5f) cap = 0f;
                        disCaps[b] = math.max(0f, cap); totalDis += disCaps[b];
                    }

                    float plan = math.min(deficit, totalDis);
                    if (plan > 1e-6f && totalDis > 1e-6f)
                    {
                        float k = plan / totalDis;
                        for (int b = 0; b < bList.Count; b++)
                        {
                            var bi = bList[b];
                            float want = disCaps[b] * k;
                            if (want <= 1e-8f) continue;

                            float applied = Ramp(bi.CurrentKW, +want, bi.RampDischarge, bi.RampDischarge, dt);
                            applied = math.clamp(applied, 0f, disCaps[b]);

                            DictAdd(usedOut, bi.Owner, applied);

                            bi.SoC = math.saturate(bi.SoC - (applied * dtHours) / bi.CapacityKWh);
                            var brw = SystemAPI.GetComponentRW<BatteryComponent>(bi.Entity);
                            brw.ValueRW.SoC = bi.SoC; brw.ValueRW.CurrentKW = applied;

                            bi.CurrentKW = applied; bList[b] = bi;
                            batTouched[b] = true; batDisKW += applied;
                        }
                    }
                }

                // Раздаём разряд батарей в оставшиеся входные «окна» нагрузок
                if (batDisKW > 1e-6f && totalHeadroom > 1e-6f)
                {
                    float k = batDisKW / totalHeadroom;
                    foreach (var kv in headroom)
                    {
                        float add = kv.Value * k;
                        if (add > 1e-6f) DictAdd(usedIn, kv.Key, add);
                    }
                }

                // Заряжаем батареи из излишков генерации (с рампом)
                float deliveredToLoads = 0f;
                foreach (var L in lList) deliveredToLoads += DictGet(usedIn, L.Owner, 0f);
                float surplus = math.max(0f, actualGenKW - deliveredToLoads);

                if (ChargeWithSurplus && surplus > 1e-6f && bList.Count > 0)
                {
                    var chCaps = new float[bList.Count];
                    float totalCh = 0f;
                    for (int b = 0; b < bList.Count; b++)
                    {
                        var bi = bList[b];
                        float cap = math.min(GetNodeCap(bi.Owner), bi.MaxChargeKW);
                        if (bi.SoC >= 1f - 1e-5f) cap = 0f;
                        chCaps[b] = math.max(0f, cap); totalCh += chCaps[b];
                    }

                    float plan = math.min(surplus, totalCh);
                    if (plan > 1e-6f && totalCh > 1e-6f)
                    {
                        float k = plan / totalCh;
                        for (int b = 0; b < bList.Count; b++)
                        {
                            var bi = bList[b];
                            float want = chCaps[b] * k;
                            if (want <= 1e-8f) continue;

                            float applied = Ramp(bi.CurrentKW, -want, bi.RampCharge, bi.RampCharge, dt);
                            applied = math.clamp(applied, -chCaps[b], 0f);

                            DictAdd(usedIn, bi.Owner, -applied);

                            bi.SoC = math.saturate(bi.SoC + ((-applied) * dtHours) / bi.CapacityKWh);
                            var brw = SystemAPI.GetComponentRW<BatteryComponent>(bi.Entity);
                            brw.ValueRW.SoC = bi.SoC; brw.ValueRW.CurrentKW = applied;

                            bi.CurrentKW = applied; bList[b] = bi;
                            batTouched[b] = true;
                        }
                    }
                }

                // Если бат не трогали — плавно тянем её к нулю
                for (int b = 0; b < bList.Count; b++)
                {
                    if (batTouched[b]) continue;
                    var bi = bList[b];

                    float rate = float.PositiveInfinity;
                    if (float.IsFinite(bi.RampCharge) && float.IsFinite(bi.RampDischarge))
                        rate = math.max(0f, math.min(bi.RampCharge, bi.RampDischarge));
                    if (!float.IsFinite(rate) || rate <= 0f)
                        rate = math.max(bi.RampCharge, bi.RampDischarge);

                    float newVal = Ramp(bi.CurrentKW, 0f, rate, rate, dt);
                    if (math.abs(newVal) < 1e-4f) newVal = 0f;

                    if (math.abs(newVal - bi.CurrentKW) > 1e-6f)
                    {
                        var brw = SystemAPI.GetComponentRW<BatteryComponent>(bi.Entity);
                        brw.ValueRW.CurrentKW = newVal;
                        bi.CurrentKW = newVal; bList[b] = bi;
                    }
                }

                // Визуализация транзита (по желанию)
                if (ShowTransit)
                {
                    float flowToLoads = 0f;
                    foreach (var L in lList) flowToLoads += DictGet(usedIn, L.Owner, 0f);

                    if (flowToLoads > 1e-6f)
                    {
                        var candidates = new HashSet<Entity>();
                        foreach (var bi in bList) candidates.Add(bi.Owner);
                        foreach (var gi in gList) candidates.Remove(gi.Owner);
                        foreach (var li in lList) candidates.Remove(li.Owner);

                        if (candidates.Count > 0)
                        {
                            float per = flowToLoads / candidates.Count;
                            foreach (var node in candidates)
                                SafeSetTransit(node, per, per);
                        }
                    }
                }

                foreach (var kv in usedIn) SafeSetInUsage(kv.Key, kv.Value);
                foreach (var kv in usedOut) SafeSetOutUsage(kv.Key, kv.Value);

                FinalizeTransitAtNodes();
            }
        }
    }
}
