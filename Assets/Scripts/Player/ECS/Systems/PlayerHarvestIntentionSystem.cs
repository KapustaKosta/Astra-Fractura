using Unity.Entities;

/// <summary>
/// Система, отвечающая за определение намерения ИГРОКА начать добычу ресурсов.
/// Она проверяет условия, такие как нажатие кнопки действия и наличие цели-ресурса,
/// и добавляет игроку тег <c>WantsToHarvestTag</c>, если все условия выполнены.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TargetDetectorSystem))]
[UpdateAfter(typeof(InputsSystem))]
public partial class PlayerHarvestIntentionSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для определения намерения добычи у игрока.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();
        
        if (SystemAPI.TryGetSingleton<InputsData>(out var inputs))
        {
            // Запрос для игрока: ищем сущность с тегом игрока и активной целью.
            foreach (var (activeTarget, entity) in 
                     SystemAPI.Query<RefRO<ActiveTarget>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                bool targetIsResource = SystemAPI.HasComponent<ResourceNode>(activeTarget.ValueRO.Value);
                bool alreadyWantsToHarvest = SystemAPI.HasComponent<WantsToHarvestTag>(entity);

                // Добавляем намерение, если нажата кнопка, цель - ресурс, и намерения еще нет.
                if (inputs.PrimaryAction && targetIsResource && !alreadyWantsToHarvest)
                {
                    ecb.AddComponent<WantsToHarvestTag>(entity);
                }
                
                // Удаляем намерение, если кнопка отпущена или цель перестала быть ресурсом,
                // а намерение все еще было.
                if ((!inputs.PrimaryAction || !targetIsResource) && alreadyWantsToHarvest)
                {
                    ecb.RemoveComponent<WantsToHarvestTag>(entity);
                }
            }
        }
    }
}