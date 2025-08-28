using Unity.Entities;

/// <summary>
/// Тег-состояние. Указывает, что NPC в данный момент не может выполнять добычу.
/// Причины могут быть разные: инвентарь полон, склад полон, нет доступных ресурсов.
/// Используется GoalDefinition'ами, чтобы не рассматривать цель добычи.
/// </summary>
public struct HarvestingBlockedTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что NPC временно не может разгрузиться на базе.
/// Обычно добавляется после неудачной попытки передачи предметов на склад, который заполнен.
/// Предотвращает циклическое поведение (подошел к базе -> не смог разгрузиться -> отошел -> подошел снова).
/// </summary>
public struct UnloadingBlockedTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что NPC, выполнявший цель "Вернуться на базу",
/// по какой-то причине потерял все предметы в инвентаре.
/// Это сигнал для Арбитра, что текущая цель бессмысленна и ее нужно прервать.
/// </summary>
public struct MissingRequiredItemForReturnTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что инвентарь NPC полностью пуст.
/// Добавляется и удаляется системой NPCInventoryStatusSystem.
/// </summary>
public struct NPCInventoryEmptyTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что инвентарь NPC полностью заполнен (нет пустых слотов
/// и все стаки достигли максимума). Добавляется и удаляется системой NPCInventoryStatusSystem.
/// </summary>
public struct NPCInventoryFullTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что NPC физически находится в пределах
/// радиуса взаимодействия со своей целью для добычи.
/// </summary>
public struct IsAtHarvestTargetTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что NPC находится в процессе выполнения
/// действия по разгрузке инвентаря.
/// </summary>
public struct IsUnloadingTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что система поиска пути не смогла построить маршрут к текущей цели.
/// Используется Арбитром для временной дисквалификации цели и предотвращения "зависания" NPC.
/// </summary>
public struct MovementFailedTag : IComponentData { }

/// <summary>
/// Тег-состояние, указывающий, что текущая цель NPC стала невыполнимой
/// (например, целевой объект был уничтожен). Арбитр использует этот тег,
/// чтобы принудительно прервать текущую задачу и запустить переоценку.
/// </summary>
public struct CurrentGoalInvalidTag : IComponentData { }

/// <summary>
/// Тег-состояние. Указывает, что NPC находится внутри здания для выполнения задачи.
/// Используется для скрытия модели NPC и блокировки его стандартного поведения.
/// </summary>
public struct InsideBuildingTag : IComponentData { }

