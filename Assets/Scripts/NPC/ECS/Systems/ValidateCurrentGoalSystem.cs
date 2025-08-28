using Unity.Entities;
using Unity.Transforms; // Необходимо для доступа к LocalToWorld

/// <summary>
/// Система-сенсор, которая проверяет валидность текущей активной цели NPC.
/// Если целевая сущность (Target) цели перестала существовать, система помечает
/// NPC тегом CurrentGoalInvalidTag, чтобы Арбитр мог прервать задачу.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(NPCTaskArbiterSystem))] // Критически важно: работаем до принятия решений
public partial class ValidateCurrentGoalSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Получаем "Lookup" - это потокобезопасный способ доступа к компонентам других сущностей.
        // Мы будем проверять наличие LocalToWorld, так как у любой видимой сущности он есть.
        var ltwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        Entities
            .WithReadOnly(ltwLookup) // Указываем, что мы будем только читать данные из Lookup
            .ForEach((Entity entity, in ActiveGoal goal) =>
            {
                if (goal.Target != Entity.Null)
                {
                    // Заменяем медленный вызов EntityManager.Exists() на быструю и потокобезопасную
                    // проверку наличия компонента у целевой сущности.
                    bool targetExists = ltwLookup.HasComponent(goal.Target);

                    if (!targetExists)
                    {
                        if (!SystemAPI.HasComponent<CurrentGoalInvalidTag>(entity))
                        {
                            ecb.AddComponent<CurrentGoalInvalidTag>(entity);
                        }
                    }
                    else
                    {
                        if (SystemAPI.HasComponent<CurrentGoalInvalidTag>(entity))
                        {
                            ecb.RemoveComponent<CurrentGoalInvalidTag>(entity);
                        }
                    }
                }
            }).Schedule(); // Теперь мы можем безопасно использовать .Schedule() для максимальной производительности
    }
}