using Unity.Entities;
using UnityEngine;

/// <summary>
/// Обрабатывает запросы на атаку, находя урон оружия и уменьшая здоровье цели.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerAttackIntentionSystem))]
public partial class DamageSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Получаем "Lookup" для компонента Health. 
        var healthLookup = GetComponentLookup<HealthComponent>(false);
        
        // Получаем доступ к реестру предметов. Если он еще не загрузился, выходим.
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        // Командный буфер для отложенных изменений, таких как добавление или изменение компонентов.
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        float currentTime = (float)SystemAPI.Time.ElapsedTime;

        Entities
            .WithoutBurst() 
            .ForEach((in PerformAttackRequest request) =>
            {
                // Убеждаемся, что цель все еще существует, имеет здоровье и еще не мертва.
                if (!healthLookup.HasComponent(request.Target) || SystemAPI.HasComponent<IsDeadTag>(request.Target))
                {
                    return;
                }
                
                // Проверяем, что у атакующего все еще есть активный предмет.
                if (!SystemAPI.HasComponent<ActiveEquippedItem>(request.Attacker))
                {
                    return;
                }
                
                // Получаем данные об оружии атакующего из реестра.
                var attackerItem = SystemAPI.GetComponent<ActiveEquippedItem>(request.Attacker);
                var itemData = itemRegistry.GetItemData(attackerItem.ItemID);

                // Если у предмета нет данных или это не оружие, урон не наносится.
                if (itemData == null || itemData.itemType != ItemType.Weapon) return;

                // Наносим урон, изменяя значение здоровья цели.
                var targetHealth = healthLookup[request.Target];
                targetHealth.CurrentHealth -= itemData.weaponDamage;
                healthLookup[request.Target] = targetHealth; 
                
                // Если атакован NPC, необходимо перевести его в боевое состояние.
                if (SystemAPI.HasComponent<NPCComponent>(request.Target))
                {
                    var newCombatState = new InCombat { LastDamageTime = currentTime };

                    // Если у NPC уже есть компонент InCombat (это повторный удар), мы обновляем время.
                    if (SystemAPI.HasComponent<InCombat>(request.Target))
                    {
                        ecb.SetComponent(request.Target, newCombatState);
                    }
                    // Если компонента нет (это первый удар), мы его добавляем.
                    else
                    {
                        ecb.AddComponent(request.Target, newCombatState);
                    }
                }
                
            }).Run();
    }
}