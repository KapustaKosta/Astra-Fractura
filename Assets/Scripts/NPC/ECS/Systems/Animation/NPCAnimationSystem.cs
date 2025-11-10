using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// Система, которая обновляет компонент NPCAnimationState на основе
/// текущего состояния NPC (движение, атака, сбор ресурсов).
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class NPCAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Сбрасываем триггер атаки в начале каждого кадра
        Entities
            .ForEach((ref NPCAnimationState animationState) =>
            {
                animationState.AttackTrigger = false;
            }).ScheduleParallel();

        // Обновление скорости для анимации движения
        Entities
            .ForEach((Entity e, ref NPCAnimationState animationState, in PhysicsVelocity velocity) =>
            {
                var newSpeed = math.length(new float2(velocity.Linear.x, velocity.Linear.z));
                if (animationState.Speed != newSpeed)
                {
                    Debug.Log($"[NPCAnimationSystem] Entity {e}: Speed changed to {newSpeed}");
                }
                animationState.Speed = newSpeed;
            }).ScheduleParallel();

        // Установка флага сбора ресурсов
        Entities
            .WithAll<WantsToHarvestTag>()
            .ForEach((Entity e, ref NPCAnimationState animationState) =>
            {
                if (!animationState.IsHarvesting)
                {
                    Debug.Log($"[NPCAnimationSystem] Entity {e}: IsHarvesting set to TRUE");
                }
                animationState.IsHarvesting = true;
            }).ScheduleParallel();
            
        // Сброс флага сбора ресурсов
        Entities
            .WithNone<WantsToHarvestTag>()
            .ForEach((Entity e, ref NPCAnimationState animationState) =>
            {
                if (animationState.IsHarvesting)
                {
                    Debug.Log($"[NPCAnimationSystem] Entity {e}: IsHarvesting set to FALSE");
                }
                animationState.IsHarvesting = false;
            }).ScheduleParallel();
    }
}
