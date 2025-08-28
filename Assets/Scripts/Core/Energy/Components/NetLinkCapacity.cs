using Unity.Entities;

namespace Energy.Core
{
    /// <summary> Суммарный лимит порта узла (кВт). Используем как In и Out лимит. </summary>
    public struct NetLinkCapacity : IComponentData
    {
        public float MaxKW;
    }

    /// <summary>
    /// Использование портов узла сети.
    /// InUsedKW/OutUsedKW — локальные потоки (заряд/разряд батареи, генерация, потребление).
    /// TransitInKW/TransitOutKW — транзит через узел (проходящий поток к другим устройствам).
    /// </summary>
    public struct NetLinkUsage : IComponentData
    {
        public float InUsedKW;      // локальный вход (заряд батареи, вход нагрузки)
        public float OutUsedKW;     // локальный выход (разряд батареи, выход генератора)

        public float TransitInKW;   // транзит, вошедший в узел
        public float TransitOutKW;  // транзит, вышедший из узла
    }


}
