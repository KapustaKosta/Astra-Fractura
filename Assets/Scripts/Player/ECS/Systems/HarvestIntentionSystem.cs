using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Система проверяет, хочет ли игрок добывать ресурсы,
/// и устанавливает соответствующий тег-намерение.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TargetDetectorSystem))] // Работает после того, как цель определена
public partial class HarvestIntentionSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр. Анализирует ввод и цель, чтобы установить тег WantsToHarvestTag.
    /// </summary>
    protected override void OnUpdate()
    {
        var inputs = SystemAPI.GetSingleton<InputsData>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerControllerData>();

        // Всегда очищаем старое намерение перед новой проверкой.
        ecb.RemoveComponent<WantsToHarvestTag>(playerEntity);
        
        // Получаем сущность GameState для проверки текущего режима
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        // Проверка условий:
        // 1. Нажата ли кнопка?
        // 2. Есть ли у игрока вообще цель?
        // 3. Является ли эта цель ресурсом?
        // 4. Не находимся ли мы над элементом UI?
        // 5. НЕ находимся ли мы в режиме строительства? (Добавлено)
        if (inputs.PrimaryAction &&
            SystemAPI.HasComponent<InteractionTarget>(playerEntity) &&
            SystemAPI.HasComponent<ResourceNode>(SystemAPI.GetComponent<InteractionTarget>(playerEntity).Value) &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) &&
            !SystemAPI.HasComponent<InBuildingMode>(gameStateEntity)) // Добавлена проверка InBuildingMode
        {
            // Все условия выполнены. Устанавливаем намерение.
            ecb.AddComponent<WantsToHarvestTag>(playerEntity);
        }
    }
}