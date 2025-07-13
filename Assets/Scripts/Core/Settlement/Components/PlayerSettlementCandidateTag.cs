using Unity.Entities;

/// <summary>
/// Компонент-маркер, помечающий сущность поселения как потенциального кандидата 
/// на роль главного поселения игрока.
/// </summary>
/// <remarks>
/// <para>
/// Этот компонент добавляется к поселению при его создании, если в <see cref="SettlementAuthoring"/> 
/// установлено <c>canBecomePlayerSettlement = true</c>.
/// </para>
/// </remarks>
public struct PlayerSettlementCandidateTag : IComponentData { }