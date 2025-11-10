using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для определения NPC в ECS.
/// Настраивает стартовые параметры и добавляет набор компонентов:
/// AI, движение, избегание, Pathfinding + буфер пути.
/// </summary>
[DisallowMultipleComponent]
public class NPCAuthoring : MonoBehaviour
{
    [Header("Базовая информация о NPC")]
    public string npcName = "Unnamed NPC";
    public int age = 25;
    public string skills = "Worker";
    public int organizedness = 50;
    public int loyalty = 50;
    public int diligence = 50;

    [Header("Боевые параметры")]
    [Tooltip("Максимальное здоровье NPC.")]
    public float maxHealth = 100f;

    [Header("Рабочие параметры")]
    [Tooltip("Общий запас 'рабочей силы' (молотков) на цикл.")]
    public float hammerPoolCapacity = 35f;

    [Header("Параметры движения")]
    [Tooltip("Скорость передвижения NPC в м/с.")]
    public float moveSpeed = 3.5f;
    [Tooltip("Скорость поворота NPC в радианах/сек.")]
    public float rotationSpeed = 120f;
    [Tooltip("Расстояние до цели, на котором NPC прекратит движение.")]
    public float stoppingDistance = 0.5f;
    [Tooltip("Порог обнуления скорости движения.")]
    public float velocityZeroingThreshold = 0.001f;

    [Header("Параметры избегания")]
    [Tooltip("Радиус 'личного пространства' для ORCA/сепарации.")]
    public float avoidanceRadius = 0.4f;
    [Tooltip("Вес реакции на силы избегания.")]
    public float avoidanceWeight = 2.0f;

    public class Baker : Baker<NPCAuthoring>
    {
        public override void Bake(NPCAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(e, new NPCComponent
            {
                Name          = new FixedString64Bytes(a.npcName ?? string.Empty),
                Age           = a.age,
                Skills        = new FixedString128Bytes(a.skills ?? string.Empty),
                Organizedness = a.organizedness,
                Loyalty       = a.loyalty,
                Diligence     = a.diligence,
                Target             = Entity.Null,
                AssignedWorkshop   = Entity.Null
            });

            AddComponent(e, new NPCWorkForce
            {
                MaxHammerPool     = a.hammerPoolCapacity,
                CurrentHammerPool = a.hammerPoolCapacity
            });

            AddComponent(e, new HealthComponent
            {
                MaxHealth     = a.maxHealth,
                CurrentHealth = a.maxHealth
            });

            // Избежание
            AddComponent(e, new AvoidanceData
            {
                Radius = math.max(0.1f, a.avoidanceRadius),
                Weight = a.avoidanceWeight
            });

            // Базовые статы движения
            AddComponent(e, new NPCBaseMovementStats
            {
                Speed            = a.moveSpeed,
                RotationSpeed    = a.rotationSpeed,
                StoppingDistance = a.stoppingDistance
            });

            // Текущее движение
            AddComponent(e, new NPCMovementComponent
            {
                HasTarget                   = false,
                TargetPosition              = float3.zero,
                Speed                       = a.moveSpeed,
                RotationSpeed               = a.rotationSpeed,
                StoppingDistance            = a.stoppingDistance,
                VelocityZeroingThresholdSq  = a.velocityZeroingThreshold * a.velocityZeroingThreshold,
                CurrentDesiredMoveDirection = float3.zero,
                PreferredVelocity           = float3.zero,
                TargetVelocity              = float3.zero
            });

            // Мозг/Pathfinding
            AddComponent<NPCBrain>(e);
            AddComponent<NPCPathfindingComponent>(e);
            AddBuffer<NPCPathBufferElement>(e);

            // Анимация
            AddComponent<NPCAnimationState>(e);
        }
    }
}
