using Unity.Entities;
using UnityEngine;

/// <summary>
/// Определяет намерение игрока атаковать на основе ввода, экипированного оружия и цели.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TargetDetectorSystem))]
public partial class PlayerAttackIntentionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Проверяем общее состояние игры. Атаковать можно только в стандартном режиме.
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
        if (!SystemAPI.HasComponent<InDefaultMode>(gameStateEntity))
        {
            return;
        }

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        float currentTime = (float)SystemAPI.Time.ElapsedTime;

        Entities
            .WithoutBurst()
            .ForEach((Entity playerEntity, ref AttackState attackState, in InputsData inputs, in ActiveEquippedItem equippedItem, in ActiveTarget activeTarget) =>
            {
                // Проверяем, нажал ли игрок кнопку атаки.
                if (!inputs.PrimaryAction) return;
                
                // Цель должна быть валидной и иметь компонент здоровья, иначе атаковать бессмысленно.
                if (!SystemAPI.HasComponent<HealthComponent>(activeTarget.Value)) return;

                // Проверяем, что в руках у игрока действительно оружие.
                var itemData = itemRegistry.GetItemData(equippedItem.ItemID);
                if (itemData == null || itemData.itemType != ItemType.Weapon) return;
                
                // Проверяем кулдаун атаки, чтобы игрок не мог атаковать слишком часто.
                if (currentTime < attackState.LastAttackTime + itemData.attackCooldown) return;
                
                Debug.Log($"[PlayerAttackIntentionSystem] Создан запрос PerformAttackRequest. Атакующий: {playerEntity}, Цель: {activeTarget.Value}");
                
                // Если все проверки пройдены, создаем одноразовую сущность-запрос на атаку.
                // Эту сущность подхватит и обработает DamageSystem.
                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new PerformAttackRequest
                {
                    Attacker = playerEntity,
                    Target = activeTarget.Value
                });
                
                // Обновляем время последней атаки для отсчета следующего кулдауна.
                attackState.LastAttackTime = currentTime;

            }).Run();
    }
}