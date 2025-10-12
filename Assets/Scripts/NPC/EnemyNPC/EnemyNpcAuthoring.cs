using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;

/// <summary>
/// Авторинг вражеского NPC. Набор компонентов зеркалирует дружественного NPC:
/// боевые теги/статы + движение/избежание + Pathfinding.
/// ⚠ Физические компоненты НЕ добавляются здесь — берутся из вашего уже настроенного Physics Body/Shape.
/// </summary>
public class EnemyNpcAuthoring : MonoBehaviour
{
    [Header("Perception/Targeting")]
    public float AggroRadius = 12f;

    [Header("Attack")]
    public float AttackRange = 1.8f;
    public float AttackCooldown = 0.8f;
    public float Damage = 10f;
    
    [Header("Health")]
    public float MaxHealth = 50f;

    [Header("Movement (как в NPCAuthoring)")]
    public float MoveSpeed = 3.5f;
    public float RotationSpeed = 120f;
    public float StoppingDistance = 0.5f;
    public float VelocityZeroingThreshold = 0.001f;

    [Header("Avoidance")]
    public float AvoidanceRadius = 0.4f;
    public float AvoidanceWeight = 2.0f;

    public class Baker : Baker<EnemyNpcAuthoring>
    {
        public override void Bake(EnemyNpcAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);

            // Вражеские теги/боевые статы 
            AddComponent<HostileNPCTag>(e);
            AddComponent(e, new EnemyStats
            {
                AggroRadius    = a.AggroRadius,
                AttackRange    = a.AttackRange,
                AttackCooldown = a.AttackCooldown,
                Damage         = a.Damage
            });
            AddComponent<AttackState>(e);


            // Добавляем NPCComponent, чтобы враг считался валидной целью для атаки и смерти
            AddComponent(e, new NPCComponent
            {
                Name = new FixedString64Bytes("Hostile NPC"), 
            });
            
            AddComponent(e, new HealthComponent
            {
                MaxHealth = a.MaxHealth,
                CurrentHealth = a.MaxHealth
            });

            // Движение/избежание
            AddComponent(e, new NPCBaseMovementStats
            {
                Speed            = a.MoveSpeed,
                RotationSpeed    = a.RotationSpeed,
                StoppingDistance = a.StoppingDistance
            });

            AddComponent(e, new NPCMovementComponent
            {
                HasTarget                   = false,
                TargetPosition              = float3.zero,
                Speed                       = a.MoveSpeed,
                RotationSpeed               = a.RotationSpeed,
                StoppingDistance            = a.StoppingDistance,
                VelocityZeroingThresholdSq  = a.VelocityZeroingThreshold * a.VelocityZeroingThreshold,
                CurrentDesiredMoveDirection = float3.zero,
                PreferredVelocity           = float3.zero,
                TargetVelocity              = float3.zero
            });

            AddComponent(e, new AvoidanceData
            {
                Radius = math.max(0.1f, a.AvoidanceRadius),
                Weight = a.AvoidanceWeight
            });

            // Путь/буфер пути
            AddComponent<NPCBrain>(e);
            AddComponent<NPCPathfindingComponent>(e);
            AddBuffer<NPCPathBufferElement>(e);
            
            AddComponent(e, new PhysicsGravityFactor
            {
                Value = 1f // Стандартная гравитация
            });
            
        }
    }
}

public struct EnemyStats : IComponentData
{
    public float AggroRadius;
    public float AttackRange;
    public float AttackCooldown;
    public float Damage;
}

public struct EnemySeenPlayer : IComponentData
{
    public Entity Player;
}
