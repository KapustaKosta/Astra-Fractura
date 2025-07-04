using Unity.Entities;

/// <summary>
/// Тег-компонент для префабов зданий, которые могут стать главным поселением игрока,
/// если будут построены первыми.
/// </summary>
public struct PlayerSettlementCandidateTag : IComponentData { }