using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система, которая отслеживает состояние боя NPC. Если NPC не получал урон
/// в течение заданного времени, она выводит его из состояния боя, удаляя компонент InCombat.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageSystem))]
public partial class CombatStateSystem : SystemBase
{
    // Константа определяет, через сколько секунд бездействия NPC выйдет из боя.

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().
            CreateCommandBuffer(World.Unmanaged);
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        

        if (!SystemAPI.TryGetSingleton<CombatSystemConfig>(out var config))
        {
            return;
        }
        
        float combatTimeoutDuration = config.CombatTimeoutDuration;
        
        // Запрос ищет всех NPC, которые находятся в бою (имеют компонент InCombat) и еще не мертвы.
        foreach (var (inCombat, entity) in SystemAPI.Query<RefRO<InCombat>>()
                     .WithAll<NPCComponent>()
                     .WithNone<IsDeadTag>()
                     .WithEntityAccess())
        {
            Debug.Log($"[CombatStateSystem] Проверка таймаута для NPC {entity}. Последний урон: " +
                      $"{inCombat.ValueRO.LastDamageTime}, Текущее время: {currentTime}");

            // Главное условие: если с момента последнего удара прошло больше времени, чем заданный таймаут,
            // то мы записываем команду на удаление компонента InCombat.
            if (currentTime > inCombat.ValueRO.LastDamageTime + combatTimeoutDuration)
            {
                Debug.Log($"[CombatStateSystem] Таймаут для NPC {entity} истек. " +
                          $"Записываем команду на удаление InCombat.");
                // Это вернет NPC в его обычное состояние.
                ecb.RemoveComponent<InCombat>(entity);
            }
        }
    }
}