using Unity.Entities;

/// <summary>
/// Система, отвечающая за определение намерения игрока начать добычу ресурсов.
/// Она проверяет условия, такие как нажатие кнопки действия и наличие цели-ресурса,
/// и добавляет игроку тег <c>WantsToHarvestTag</c>, если все условия выполнены.
/// Работает только в стандартном игровом режиме.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TargetDetectorSystem))]
[UpdateAfter(typeof(InputsSystem))]
public partial class HarvestIntentionSystem : SystemBase
{
    /// <summary>
    /// Вызывается при создании системы. Гарантирует, что система будет обновляться,
    /// только когда игра находится в режиме по умолчанию (<c>InDefaultMode</c>).
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<InDefaultMode>();
    }
    
    /// <summary>
    /// Вызывается каждый кадр для проверки и установки намерения добычи.
    /// Управляет жизненным циклом тега <c>WantsToHarvestTag</c> на сущности игрока.
    /// </summary>
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerControllerData>(out var playerEntity)) return;

        var inputs = SystemAPI.GetSingleton<InputsData>();
        
        // Если основная кнопка действия отпущена, а у игрока все еще есть тег намерения, удаляем его.
        if (!inputs.PrimaryAction && SystemAPI.HasComponent<WantsToHarvestTag>(playerEntity))
        {
            EntityManager.RemoveComponent<WantsToHarvestTag>(playerEntity);
        }
        
        // Если основная кнопка действия нажата и у игрока еще нет тега намерения,
        // проверяем условия для его добавления.
        if (inputs.PrimaryAction && !SystemAPI.HasComponent<WantsToHarvestTag>(playerEntity))
        {
            // Проверяем, есть ли у игрока цель и является ли эта цель ресурсным узлом.
            if (SystemAPI.HasComponent<InteractionTarget>(playerEntity) &&
                SystemAPI.HasComponent<ResourceNode>(SystemAPI.GetComponent<InteractionTarget>(playerEntity).Value))
            {
                var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                ecb.AddComponent<WantsToHarvestTag>(playerEntity);
            }
        }
    }
}