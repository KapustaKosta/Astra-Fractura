using Unity.Entities;

/// <summary>
/// Одноразовый тег-синглтон. Его наличие в мире сигнализирует,
/// что топология сети изменилась (добавлен/удален провод или узел),
/// и требуется полный пересчет всех подсетей.
/// </summary>
public struct NetworkTopologyChanged : IComponentData { }