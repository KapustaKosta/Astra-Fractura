// Assets/Scripts/Core/Energy/Wires/WireItemCapacityBootstrap.cs
using System.Collections.Generic;
using UnityEngine;

namespace Energy.Core
{
    /// <summary>
    /// Bootstrap: переносит capacity по уровням из списка WireItem → WireCapacity.
    /// Повесь этот компонент на любой GameObject на сцене и укажи wireItems.
    /// </summary>
    public class WireItemCapacityBootstrap : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private List<WireItem> wireItems;

        [Header("Behavior")]
        [Tooltip("Если включено — значения из WireItem перезапишут уже имеющиеся (в т.ч. из WireSettings). " +
                 "Если выключено — добавим только отсутствующие уровни, не трогая заданные ранее.")]
        public bool overrideExisting = false;

        [Tooltip("Если после применения таблица пустая — заполнить дефолтами (L1=2, L2=5, L3=15, L4=40 кВт).")]
        public bool fillDefaultsIfEmpty = true;

        private void Awake()
        {
            int applied = 0;

            if (overrideExisting)
                WireCapacity.Clear();

            if (wireItems != null && wireItems.Count > 0)
            {
                foreach (var wi in wireItems)
                {
                    if (wi == null) continue;
                    if (wi.wireLevel < 1) continue;

                    // Если не перезаписываем — пропускаем уже заданные уровни
                    if (!overrideExisting && WireCapacity.HasLevel(wi.wireLevel))
                        continue;

                    WireCapacity.Set(wi.wireLevel, Mathf.Max(0f, wi.capacityKW));
                    applied++;
                }
            }

            if (fillDefaultsIfEmpty)
                WireCapacity.EnsureDefaultsIfEmpty();

            Debug.Log($"[WireBootstrap] Applied {applied} levels from WireItem list. " +
                      $"Registry size: {WireCapacity.LevelCount} (override={overrideExisting}, defaults={(fillDefaultsIfEmpty ? "on" : "off")}).");
        }
    }

    /// <summary>
    /// Глобальный реестр «уровень провода → кВт». Источник правды для расчёта лимитов узлов/дебага.
    /// </summary>
    public static class WireCapacity
    {
        private static readonly Dictionary<int, float> _byLevel = new();

        /// <summary>Всего записанных уровней.</summary>
        public static int LevelCount => _byLevel.Count;

        public static void Clear() => _byLevel.Clear();

        /// <summary>Установить ёмкость уровня (перезаписывает, если уже был).</summary>
        public static void Set(int level, float kw)
        {
            if (level < 1) return;
            _byLevel[level] = kw;
        }

        /// <summary>Получить ёмкость уровня. Если не найден — +∞ (что сразу видно в дебаге).</summary>
        public static float Get(int level)
        {
            if (level < 1) level = 1;
            return _byLevel.TryGetValue(level, out var kw) ? kw : float.PositiveInfinity;
        }

        /// <summary>Проверить, есть ли запись для уровня.</summary>
        public static bool HasLevel(int level) => _byLevel.ContainsKey(Mathf.Max(1, level));

        /// <summary>
        /// Если таблица пуста — проставляем разумные значения по умолчанию.
        /// Подгони под свой баланс по желанию.
        /// </summary>
        public static void EnsureDefaultsIfEmpty()
        {
            if (_byLevel.Count > 0) return;
            Set(1, 2f);
            Set(2, 5f);
            Set(3, 15f);
            Set(4, 40f);
        }
    }
}
