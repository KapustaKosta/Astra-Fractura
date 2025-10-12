using Unity.Entities;

/// <summary>
/// Система, которая отслеживает здоровье игрока и обрабатывает его смерть.
/// При смерти игрока добавляет ему DeadTag, чтобы отключить другие системы,
/// и создает запрос на отображение UI смерти.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerDeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // Ищем живого игрока (с PlayerTag, но без DeadTag)
        foreach (var (health, entity) in SystemAPI.Query<HealthComponent>().WithAll<PlayerTag>().WithNone<DeadTag>().WithEntityAccess())
        {
            if (health.CurrentHealth <= 0)
            {
                // 1. Помечаем игрока как мертвого
                ecb.AddComponent<DeadTag>(entity);

                // 2. Создаем запрос на показ UI
                var uiRequestEntity = ecb.CreateEntity();
                ecb.AddComponent<ShowDeathUIRequest>(uiRequestEntity);

                // 3. Переключаем состояние игры в режим UI, чтобы остановить ввод и взаимодействие
                var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
                if (SystemAPI.HasComponent<InDefaultMode>(gameStateEntity))
                {
                    ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
                    ecb.AddComponent<InUIMode>(gameStateEntity);
                }
            }
        }
    }
}