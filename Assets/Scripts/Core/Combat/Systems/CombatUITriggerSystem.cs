using Unity.Entities;

/// <summary>
/// Система, которая управляет отображением боевого UI.
/// Показывает UI, когда игрок с оружием в руках наводится на NPC,
/// и поддерживает его видимость, пока NPC находится в состоянии боя.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DeathSystem))] 
public partial class CombatUITriggerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity)) return;

        // Сначала определяем, на какую валидную цель смотрит игрок в данный момент.
        Entity hoveredTarget = Entity.Null;
        if (SystemAPI.HasComponent<ActiveTarget>(playerEntity) && SystemAPI.HasComponent<ActiveEquippedItem>(playerEntity))
        {
            var activeTarget = SystemAPI.GetComponent<ActiveTarget>(playerEntity);
            var equippedItem = SystemAPI.GetComponent<ActiveEquippedItem>(playerEntity);
            var itemRegistry = ItemRegistry.Instance;

            if (itemRegistry != null)
            {
                var itemData = itemRegistry.GetItemData(equippedItem.ItemID);
                // Цель считается валидной для боевого UI, если это живой NPC, а у игрока в руках оружие.
                if (SystemAPI.HasComponent<NPCComponent>(activeTarget.Value) && 
                    !SystemAPI.HasComponent<IsDeadTag>(activeTarget.Value) &&
                    itemData != null && itemData.itemType == ItemType.Weapon)
                {
                    hoveredTarget = activeTarget.Value;
                }
            }
        }

        // Далее управляем синглтоном ActiveCombatTarget, который включает и выключает UI.
        bool hasActiveCombatTarget = SystemAPI.TryGetSingletonEntity<ActiveCombatTarget>(out var singletonEntity);
        Entity currentTargetEntity = hasActiveCombatTarget ? 
            SystemAPI.GetComponent<ActiveCombatTarget>(singletonEntity).TargetEntity : Entity.Null;

        // Если игрок навёл курсор на новую валидную цель.
        if (hoveredTarget != Entity.Null)
        {
            // Если UI еще не показан или показан для другой цели, мы создаем или обновляем синглтон.
            if (!hasActiveCombatTarget || currentTargetEntity != hoveredTarget)
            {
                var newSingletonData = new ActiveCombatTarget { TargetEntity = hoveredTarget };
                if (hasActiveCombatTarget)
                {
                    ecb.SetComponent(singletonEntity, newSingletonData); // Обновляем существующий синглтон.
                }
                else
                {
                    var newSingletonEntity = ecb.CreateEntity();
                    ecb.AddComponent(newSingletonEntity, newSingletonData); // Создаем новый.
                }
            }
        }
        // Если игрок не смотрит на валидную цель.
        else
        {
            // Если UI в данный момент показан, нам нужно решить, скрывать ли его.
            if (hasActiveCombatTarget)
            {
                // Мы не хотим, чтобы UI исчезал моментально, как только игрок отвел взгляд в пылу боя.
                // Поэтому проверяем состояние текущей цели, на которую указывает UI.
                bool targetIsDead = SystemAPI.HasComponent<IsDeadTag>(currentTargetEntity);
                bool targetIsInCombat = SystemAPI.HasComponent<InCombat>(currentTargetEntity);
                
                // Скрываем UI (уничтожая синглтон) только если цель умерла,
                // либо если игрок на нее не смотрит и при этом цель уже не находится в бою.
                if (targetIsDead || !targetIsInCombat)
                {
                    ecb.DestroyEntity(singletonEntity);
                }
            }
        }
    }
}