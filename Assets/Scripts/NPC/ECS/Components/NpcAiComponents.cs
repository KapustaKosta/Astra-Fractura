using Unity.Entities;



/// <summary>
/// Перечисление всех возможных типов глобальных целей для NPC.
/// </summary>
public enum GoalType { Idle, Harvest, ReturnToBase, Flee, MaintainWorkshop, Attack, Wander } // ДОБАВЛЕНО: Wander

/// <summary>
/// Компонент текущей цели NPC. Хранит информацию о типе цели, 
/// целевой сущности и контекстных данных для принятия решений.
/// Является unmanaged компонентом для работы с Burst и Jobs.
/// </summary>
public struct ActiveGoal : IComponentData
{
    /// <summary>
    /// Тип текущей цели (Idle, Harvest и т.д.)
    /// </summary>
    public GoalType Type;

    /// <summary>
    /// Целевая сущность для достижения цели (например, объект для сбора)
    /// </summary>
    public Entity Target;

    /// <summary>
    /// ID предмета, связанного с текущей целью (например, собираемый ресурс)
    /// </summary>
    public int RelevantItemID;

    /// <summary>
    /// Оценка текущей цели - используется для выбора наиболее приоритетной цели
    /// </summary>
    public float CurrentGoalScore;
}

/// <summary>
/// Маркерный компонент, указывающий на сущность с ИИ-логикой NPC.
/// Используется для фильтрации сущностей в системах ИИ.
/// </summary>
public struct NPCBrain : IComponentData { }

/// <summary>
/// Таймер ожидания для цели "Бродить".
/// </summary>
public struct WanderWaitTimer : IComponentData
{
    public float Value;
}

/// <summary>
/// Доступное действие для NPC. Хранится в динамическом буфере 
/// и используется для оценки возможных целей поведения.
/// </summary>
public struct AvailableAction : IBufferElementData
{
    /// <summary>
    /// Тип доступного действия (соответствует GoalType)
    /// </summary>
    public GoalType Type;

    /// <summary>
    /// Базовый приоритет действия - используется для расчета общей оценки цели
    /// </summary>
    public float BaseScore;
}

/// <summary>
/// Запрос на перемещение к целевой сущности.
/// Используется для связи между системой ИИ и системой перемещения.
/// </summary>
public struct MoveToRequest : IComponentData
{
    /// <summary>
    /// Целевая сущность для перемещения
    /// </summary>
    public Entity TargetEntity;

    /// <summary>
    /// Расстояние остановки - на каком расстоянии от цели считать перемещение завершенным
    /// </summary>
    public float StoppingDistance;
}

/// <summary>
/// Конфигурационные настройки ИИ, определяющие параметры поведения NPC.
/// Хранится в разделяемой сущности сцены для глобального доступа.
/// </summary>
public struct AISettings : IComponentData
{
    /// <summary>
    /// Радиус поиска целей для NPC
    /// </summary>
    public float AISearchRadius;
    
    public int PlayerCollisionLayer;

    /// <summary>
    /// Слой коллизий для ресурсов - используется для фильтрации объектов поиска
    /// </summary>
    public int ResourceCollisionLayer;

    /// <summary>
    /// Приоритет назначения сбора ресурсов при взаимодействии с игроком
    /// </summary>
    public float PlayerAssignHarvestPriority;

    /// <summary>
    /// Приоритет возврата к базе при взаимодействии с игроком
    /// </summary>
    public float PlayerAssignReturnPriority;

    /// <summary>
    /// Скорость поворота NPC (в градусах в секунду)
    /// </summary>
    public float RotationSpeed;

    /// <summary>
    /// Буфер взаимодействия для сбора ресурсов - дополнительное расстояние для активации взаимодействия
    /// </summary>
    public float HarvestInteractionRangeBuffer;

    /// <summary>
    /// Буфер остановки для возврата к базе - дополнительное расстояние для завершения перемещения
    /// </summary>
    public float ReturnToBaseStoppingDistanceBuffer;
}

/// <summary>
/// Компонент точки прибытия - указывает на специальную точку назначения
/// для NPC в безопасных зонах (например, у базы или укрытия).
/// </summary>
public struct ArrivalPointComponent : IComponentData
{
    /// <summary>
    /// Сущность точки прибытия - целевая позиция для завершения перемещения
    /// </summary>
    public Entity ArrivalTarget;
}
