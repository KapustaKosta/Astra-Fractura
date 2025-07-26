using Unity.Entities;

/// <summary>
/// Тег, указывающий, что сущность готова к финальному уничтожению.
/// Добавляется после того, как с сущности сняты компоненты рендеринга.
/// </summary>
public struct IsReadyForCleanupTag : IComponentData { }