using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Energy.Core;

/// <summary>
/// Authoring-компонент для префаба карьера. Позволяет настраивать параметры
/// карьера в инспекторе Unity и "запекает" их в ECS-компоненты для использования в игре.
/// </summary>
public class QuarryAuthoring : MonoBehaviour
{
    [Tooltip("Радиус, в котором карьер ищет ближайший ресурсный узел при постройке.")]
    [Min(0.1f)]
    public float interactionRange = 5f;

    [Tooltip("Базовый интервал добычи ресурса (в секундах).")]
    [Min(0.1f)]
    public float harvestInterval = 10f;

    [Tooltip("Потребление энергии в киловаттах (кВт) при работе.")]
    [Min(0f)]
    public float energyConsumptionKW = 50f;

    /// <summary>
    /// Класс-бейкер, который преобразует данные из MonoBehaviour в ECS-компоненты.
    /// </summary>
    class Baker : Baker<QuarryAuthoring>
    {
        /// <summary>
        /// Метод "запекания". Добавляет на сущность карьера все необходимые для его работы компоненты.
        /// </summary>
        public override void Bake(QuarryAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);

            // Основные компоненты карьера
            AddComponent<QuarryTag>(e);
            
            AddComponent(e, new QuarrySettings
            {
                InteractionRange = math.max(0.1f, authoring.interactionRange),
                HarvestInterval = math.max(0.1f, authoring.harvestInterval),
                EnergyConsumptionKW = math.max(0f, authoring.energyConsumptionKW)
            });
            
            AddComponent(e, new QuarryState
            {
                IsOnline = false // Изначально карьер выключен
            });
            
            // Компоненты для интеграции с энергосистемой
            
            // 1. Делает карьер узлом электросети, чтобы к нему могли подключаться провода.
            AddComponent(e, new NetworkNode 
            { 
                Name = "Quarry", 
                SubnetId = 0 
            });

            // 2. Делает карьер потребителем энергии.
            // Изначально потребление равно 0; система QuarryHarvestingSystem будет его изменять.
            AddComponent(e, new ConsumerLoad 
            { 
                CurrentKW = 0f, 
                NetworkId = 0 
            });
        }
    }
}