using Unity.Entities;

namespace Energy.Core
{
    /// <summary> Ограничения на скорость изменения мощности генератора (кВт/с). </summary>
    public struct GeneratorRamp : IComponentData
    {
        public float UpKWps;   // рост
        public float DownKWps; // спад
    }

    /// <summary> Ограничения на скорость изменения мощности батареи (кВт/с). </summary>
    public struct BatteryRamp : IComponentData
    {
        public float ChargeKWps;    // скорость заряда (модуль)
        public float DischargeKWps; // скорость разряда
    }
}
