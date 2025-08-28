using Unity.Entities;
using Unity.Collections;

namespace Energy.Core
{
    // Генератор (ДГ, солнечная установка и т.п.)
    public struct GeneratorComponent : IComponentData
    {
        public FixedString64Bytes Name;
        public float RatedKW;   // Установленная мощность (kW)
        public float CurrentKW; // Фактическая мощность (kW) — ставится системой
        public bool IsOnline;  // Включён ли агрегат
        public int Level;     // Условный уровень/приоритет (для диспетчеризации)
        public int NetworkId; // Дублируем для быстрых запросов
    }
}