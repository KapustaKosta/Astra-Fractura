using Unity.Entities;
using Unity.Mathematics;

public struct Dying : IComponentData
{
    public float StartTime;       // Когда началась "анимация падения"
    public float FallDuration;    // Сколько "падать" (сек)
    public float DespawnDelay;    // Пауза после падения до отключения/чистки (сек)
    public float3 FallAxis;       // Ось падения (для визуала, опционально)
    public float  FallAngleDeg;   // Угол падения, обычно ~90°
}

public struct DeadCleanupReady : IComponentData {}     // Готов к Disable/очистке
public struct DropRequested    : IComponentData {}     // Сделать дроп

public struct WorldItem : IComponentData
{
    public int ItemID;
    public int Count;
}

public struct DroppedItemTag : IComponentData {}       // Для фильтрации
public struct FrozenOnDeath  : IComponentData {}       // Заморозка логики

/// <summary>
/// [NEW] Таймер до отключения и последующей чистки (тело лежит на земле).
/// </summary>
public struct AwaitCleanupCountdown : IComponentData
{
    public float Start;
    public float Delay;
}

public struct DeathDebugTag : IComponentData
{
    public float StartTime;  // Время смерти для расчёта интервала
}

public struct DeathTimer : IComponentData
{
    public float TimeSinceDeath;
    public float TraceDuration;
    public byte  DropDone;
    public byte  DespawnRequested;
}

public struct DeathTrace : IComponentData
{
    public float Elapsed;
    public float Duration;
    public float3 HitDirXZ;
}

public struct DeathFinalizedTag : IComponentData { }


/// <summary>
/// Тикет на отложенный спавн лута. Живёт отдельно от трупа.
/// </summary>
public struct LootSpawnTicket : IComponentData
{
    public float3 Position;     // Точка дропа
    public int    DelayFrames;  // Сколько кадров подождать (1–2)
    public uint   SeedBase;     // База для рандома импульса
}

/// <summary>
/// Буфер предметов для тикета отложенного спавна.
/// </summary>
public struct LootItemElement : IBufferElementData
{
    public int ItemID;
    public int Amount;
}

