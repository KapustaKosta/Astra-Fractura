using Unity.Entities;
using UnityEngine;
using Energy.Core; 

namespace Energy.Core.Authoring
{

    [DisallowMultipleComponent]
    public sealed class GeneratorRampAuthoring : MonoBehaviour
    {
        [Header("Generator ramp (kW per second)")]
        [Tooltip("Максимальная скорость роста выдачи генератора, кВт/с.")]
        [Min(0f)] public float upKWps = 5f;

        [Tooltip("Максимальная скорость снижения выдачи генератора, кВт/с.")]
        [Min(0f)] public float downKWps = 10f;

        sealed class Baker : Baker<GeneratorRampAuthoring>
        {
            public override void Bake(GeneratorRampAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new GeneratorRamp
                {
                    UpKWps = Mathf.Max(0f, authoring.upKWps),
                    DownKWps = Mathf.Max(0f, authoring.downKWps)
                });
            }
        }
    }


    [DisallowMultipleComponent]
    public sealed class BatteryRampAuthoring : MonoBehaviour
    {
        [Header("Battery ramp (kW per second)")]
        [Tooltip("Максимальная скорость ЗАРЯДА (модуль), кВт/с. Чем больше — тем быстрее уходит в минус CurrentKW.")]
        [Min(0f)] public float chargeKWps = 3f;

        [Tooltip("Максимальная скорость РАЗРЯДА, кВт/с.")]
        [Min(0f)] public float dischargeKWps = 6f;

        sealed class Baker : Baker<BatteryRampAuthoring>
        {
            public override void Bake(BatteryRampAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BatteryRamp
                {
                    ChargeKWps = Mathf.Max(0f, authoring.chargeKWps),
                    DischargeKWps = Mathf.Max(0f, authoring.dischargeKWps)
                });
            }
        }
    }
}
