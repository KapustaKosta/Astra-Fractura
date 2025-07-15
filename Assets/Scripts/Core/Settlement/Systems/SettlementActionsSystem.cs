using Unity.Entities;
using UnityEngine; 

/// <summary>
/// Обрабатывает действия, инициированные от имени поселения, например, найм NPC.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SettlementActionsSystem : SystemBase
{
    /// <summary>
    /// Гарантирует, что система будет активна, только когда в мире появится главное поселение игрока.
    /// </summary>
    protected override void OnCreate()
    {
        // Система будет работать, только если в мире есть сущность с тегом поселения.
        RequireForUpdate<PlayerSettlementTag>();
    }

    /// <summary>
    /// Вызывается каждый кадр для обработки запросов найма и назначения задач.
    /// </summary>
    protected override void OnUpdate()
    {
        // Нет смысла распараллеливать эту систему, так как она всегда работает
        // с одним-единственным поселением и небольшим количеством запросов.
        // Используем один CommandBuffer для всех операций.
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Получаем прямой доступ на запись к компоненту нашего синглтона.
        var settlementData = SystemAPI.GetSingletonRW<SettlementComponent>();

        // Обработка запросов на найм NPC
        Entities
            .WithoutBurst()
            .ForEach((in HireNPCRequest request) =>
            {
                // Проверяем, существует ли NPC и не нанят ли он уже
                if (!SystemAPI.Exists(request.NPCToHire) || SystemAPI.HasComponent<NPCHiredTag>(request.NPCToHire))
                {
                    return; 
                }
                
                // Проверяем, есть ли место в поселении
                if (settlementData.ValueRO.NPCs.Length < settlementData.ValueRO.NPCs.Capacity)
                {
                    // Найм: добавляем NPC в список поселения и ставим ему тег
                    settlementData.ValueRW.NPCs.Add(request.NPCToHire);
                    settlementData.ValueRW.Population += 1;
                    ecb.AddComponent<NPCHiredTag>(request.NPCToHire);
                }
                else
                {
                    #if UNITY_EDITOR
                    Debug.LogWarning($"Недостаточно места в поселении для найма NPC {request.NPCToHire.Index}.");
                    #endif
                }
                
            })
            .Run();
    }
}