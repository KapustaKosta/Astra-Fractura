using Unity.Entities;
using UnityEngine;
using Energy.Core; // WireCapacity
using Wiring;     // WireSettings (синглтон из твоего WireSettingsAuthoring)

namespace Energy.Core.Systems
{
    /// <summary>
    /// Бридж: переносит глобальные ёмкости из WireSettings (L1..L3) в WireCapacity,
    /// чтобы BuildLinkCapacitySystem/дебаг брали кВт из единого места.
    /// Если синглтона нет — ничего не делает (сработают WireItem/дефолты).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(BuildLinkCapacitySystem))] // важно: лимиты узлов считаются после
    public partial class WireSettingsToCapacityBridgeSystem : SystemBase
    {
        // Чтобы не перезаписывать каждый кадр без нужды
        private bool _appliedOnce = false;
        private float _l1, _l2, _l3;

        protected override void OnUpdate()
        {
            // Пытаемся прочитать синглтон WireSettings
            if (!SystemAPI.TryGetSingleton<WireSettings>(out var ws))
            {
                // Нет синглтона — подстрахуйся дефолтами (или оставь то, что уже выставили WireItem’ы)
                if (!_appliedOnce)
                    WireCapacity.EnsureDefaultsIfEmpty();
                return;
            }

            // Если значения не менялись — выходим
            if (_appliedOnce && Mathf.Approximately(ws.Level1Capacity, _l1)
                             && Mathf.Approximately(ws.Level2Capacity, _l2)
                             && Mathf.Approximately(ws.Level3Capacity, _l3))
                return;

            // Лог для ясности один раз/при изменении
            Debug.Log($"[WireBridge] Apply WireSettings → WireCapacity: L1={ws.Level1Capacity} kW, L2={ws.Level2Capacity} kW, L3={ws.Level3Capacity} kW");

            // Записываем в реестр (очищаем, чтобы не мешали старые значения)
            WireCapacity.Clear();
            WireCapacity.Set(1, Mathf.Max(0f, ws.Level1Capacity));
            WireCapacity.Set(2, Mathf.Max(0f, ws.Level2Capacity));
            WireCapacity.Set(3, Mathf.Max(0f, ws.Level3Capacity));



            _appliedOnce = true;
            _l1 = ws.Level1Capacity;
            _l2 = ws.Level2Capacity;
            _l3 = ws.Level3Capacity;
        }
    }
}
