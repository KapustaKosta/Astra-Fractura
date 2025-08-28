using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using Energy.Core; // Убедимся, что пространство имен подключено

namespace Energy.Core.Authoring
{
    public class BatteryAuthoring : MonoBehaviour
    {
        public string friendlyName = "Battery";
        [Min(0)] public float capacityKWh = 20f;
        [Range(0f, 1f)] public float initialSoC = 0.5f;
        [Min(0)] public float maxChargeKW = 5f;
        [Min(0)] public float maxDischargeKW = 5f;

        class Baker : Baker<BatteryAuthoring>
        {
            public override void Bake(BatteryAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.None);
                var cap = math.max(0f, a.capacityKWh);
                var soc = math.saturate(a.initialSoC);
                var name = new FixedString64Bytes(a.friendlyName);

                AddComponent(e, new BatteryComponent
                {
                    Name = name,
                    CapacityKWh = cap,
                    SoC = soc,
                    CurrentKW = 0f,
                    MaxChargeKW = math.max(0f, a.maxChargeKW),
                    MaxDischargeKW = math.max(0f, a.maxDischargeKW),
                    NetworkId = 0 // Оставляем для совместимости
                });

                // Любой энергетический объект является узлом сети
                // ИЗМЕНЕНИЕ: Используем SubnetId и присваиваем начальное значение 0.
                AddComponent(e, new NetworkNode { Name = name, SubnetId = 0 });
            }
        }
    }
}