using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using SphereCollider = Unity.Physics.SphereCollider;

/// <summary>
/// Authoring-компонент для определения игрового персонажа в ECS.
/// Позволяет настраивать различные параметры контроллера игрока,
/// такие как скорость движения, параметры прыжка, гравитация,
/// настройки камеры и физические свойства, прямо в редакторе Unity.
/// </summary>
public class PlayerAuthoring : MonoBehaviour
{
    /// <summary>
    /// Скорость движения игрока в обычном режиме.
    /// </summary>
    [Header("Player Settings")]
    public float MoveSpeed = 4.0f;

    /// <summary>
    /// Скорость движения игрока в режиме спринта.
    /// </summary>
    public float SprintSpeed = 6.0f;

    /// <summary>
    /// Скорость изменения скорости движения.
    /// </summary>
    public float SpeedChangeRate = 10.0f;
    [Tooltip("Насколько хорошо игрок управляется в воздухе (0 = нет, 1 = как на земле).")]
    [Range(0f, 1f)] public float AirControlMultiplier = 0.5f;

    /// <summary>
    /// Высота прыжка игрока.
    /// </summary>
    [Header("Jumping and Gravity")]
    public float JumpHeight = 1.2f;

    /// <summary>
    /// Сила гравитации, действующая на игрока.
    /// </summary>
    public float Gravity = -15.0f;

    /// <summary>
    /// Время задержки перед возможностью повторного прыжка.
    /// </summary>
    public float JumpTimeout = 0.1f;

    /// <summary>
    /// Время задержки перед началом падения после прыжка.
    /// </summary>
    public float FallTimeout = 0.15f;
    [Tooltip("Время, в течение которого нажатие прыжка до приземления будет засчитано ('jump buffer').")]
    public float JumpBufferDuration = 0.2f;

    /// <summary>
    /// Максимальная скорость падения игрока.
    /// </summary>
    [Header("Terminal Velocity")]
    public float TerminalVelocity = -53.0f;

    /// <summary>
    /// Смещение центра проверки земли относительно центра игрока.
    /// </summary>
    [Header("Player Grounded")]
    public float GroundedOffset = -0.5f;

    /// <summary>
    /// Радиус сферы для проверки земли.
    /// </summary>
    public float GroundedRadius = 0.4f;

    /// <summary>
    /// Маска слоев, которые считаются землей для проверки заземления.
    /// </summary>
    public LayerMask GroundLayers;
    [Tooltip("Максимальный угол склона, на котором может стоять игрок.")]
    [Range(0f, 90f)] public float MaxSlopeAngle = 45.0f;
    [Tooltip("Небольшая вертикальная скорость, чтобы игрок 'прилипал' к земле.")]
    public float GroundedVerticalVelocity = -0.5f;

    /// <summary>
    /// Маска слоев, которые нужно игнорировать при проверке заземления.
    /// </summary>
    public LayerMask IgnoreLayers;
    
    [Header("Interaction & Targeting")]
    [Tooltip("Максимальная дистанция для взаимодействия (ПКМ).")]
    public float InteractionDistance = 5.0f;
    [Tooltip("Максимальная дистанция для обнаружения цели (для подсветки).")]
    public float TargetingDistance = 10.0f;
    [Tooltip("Слои, с которыми можно взаимодействовать.")]
    public LayerMask InteractableLayers;

    /// <summary>
    /// Скорость вращения игрока/камеры.
    /// </summary>
    [Header("Cinemachine Proxy")]
    public float RotationSpeed = 1.0f;

    [Tooltip("Минимальное значение ввода для обзора (мёртвая зона).")]
    public float LookInputDeadzone = 0.01f;

    /// <summary>
    /// Максимальный угол наклона камеры вверх (по питчу).
    /// </summary>
    public float TopClamp = 90.0f;

    /// <summary>
    /// Минимальный угол наклона камеры вниз (по питчу).
    /// </summary>
    public float BottomClamp = -90.0f;

    /// <summary>
    /// Вертикальное смещение цели камеры от корня игрока.
    /// </summary>
    public float CameraHeightOffset = 1.6f;

    [Header("Movement Tweaks")]
    /// <summary>
    /// Коэффициент демпфирования линейной скорости при движении по земле.
    /// </summary>
    public float GroundDamping = 0.05f;

    /// <summary>
    /// Коэффициент демпфирования линейной скорости при движении в воздухе.
    /// </summary>
    public float AirDamping = 0.01f;
    
    [Tooltip("Пороговое значение для 'прилипания' к целевой скорости.")]
    public float SpeedSnapThreshold = 0.1f;


    /// <summary>
    /// Отображает Gizmo в редакторе Unity для визуализации сферы проверки заземления.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 spherePosition = transform.position + Vector3.up * GroundedOffset;
        Gizmos.DrawWireSphere(spherePosition, GroundedRadius);
    }
}

/// <summary>
/// Baker-класс для преобразования PlayerAuthoring в ECS-компоненты.
/// </summary>
public partial class PlayerBaker : Baker<PlayerAuthoring>
{
    /// <summary>
    /// Выполняет процесс "запекания" данных из MonoBehaviour в ECS-сущности.
    /// Этот Baker больше не отвечает за создание инвентаря.
    /// </summary>
    /// <param name="authoring">Экземпляр PlayerAuthoring.</param>
    public override void Bake(PlayerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent<PlayerTag>(entity);
        
        AddComponent(entity, new PlayerGroundCheckData
        {
            IgnoreLayers = authoring.IgnoreLayers.value
        });
        AddComponent<PlayerGroundedState>(entity);

        var sphereGeometry = new SphereGeometry
        {
            Center = float3.zero,
            Radius = authoring.GroundedRadius
        };
        var bakedCollider = SphereCollider.Create(sphereGeometry);

        AddComponent(entity, new PlayerControllerData
        {
            MoveSpeed = authoring.MoveSpeed,
            SprintSpeed = authoring.SprintSpeed,
            SpeedChangeRate = authoring.SpeedChangeRate,
            JumpHeight = authoring.JumpHeight,
            Gravity = authoring.Gravity,
            JumpTimeout = authoring.JumpTimeout,
            FallTimeout = authoring.FallTimeout,
            TerminalVelocity = authoring.TerminalVelocity,
            RotationSpeed = authoring.RotationSpeed,
            TopClamp = authoring.TopClamp,
            BottomClamp = authoring.BottomClamp,
            GroundedOffset = authoring.GroundedOffset,
            GroundedRadius = authoring.GroundedRadius,
            GroundLayers = authoring.GroundLayers.value,
            CameraHeightOffset = authoring.CameraHeightOffset,
            GroundDamping = authoring.GroundDamping,
            AirDamping = authoring.AirDamping,
            GroundCheckSphereCollider = bakedCollider,
            MaxSlopeCosine = math.cos(math.radians(authoring.MaxSlopeAngle)),
            AirControlMultiplier = authoring.AirControlMultiplier,
            JumpBufferDuration = authoring.JumpBufferDuration,
            InteractionDistance = authoring.InteractionDistance,
            InteractableLayers = authoring.InteractableLayers.value,
            TargetingDistance = authoring.TargetingDistance,
            GroundedVerticalVelocity = authoring.GroundedVerticalVelocity,
            LookInputDeadzone = authoring.LookInputDeadzone,
            SpeedSnapThreshold = authoring.SpeedSnapThreshold
        });

        AddComponent(entity, new PlayerGroundCheckColliderCleanup { Collider = bakedCollider }); 

        AddComponent(entity, new PlayerStateData
        {
            currentSpeed = 0f,
            verticalVelocity = 0f,
            cinemachineTargetPitch = 0f,
            jumpTimeoutDelta = authoring.JumpTimeout,
            fallTimeoutDelta = authoring.FallTimeout,
            isGrounded = true,
        });

        AddComponent(entity, new AttackState { LastAttackTime = -1f });
        
        AddComponent(entity, new ActiveQuickbarSlot { Index = 0 }); // Начинаем с первого слота (индекс 0)

        AddComponent<InputsData>(entity);
        AddComponent<InventoryInputData>(entity);
        AddComponentObject(entity, new CameraTargetData { ProxyTarget = null });
    }
}

/// <summary>
/// Компонент очистки, используемый для корректной утилизации BlobAssetReference Collider,
/// связанного с проверкой земли игрока.
/// </summary>
public struct PlayerGroundCheckColliderCleanup : ICleanupComponentData
{
    /// <summary>
    /// Ссылка на BlobAssetReference Collider, который необходимо утилизировать.
    /// </summary>
    public BlobAssetReference<Unity.Physics.Collider> Collider;
}

/// <summary>
/// Система, отвечающая за очистку BlobAssetReference Collider
/// после удаления сущности игрока или компонента.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerGroundCheckColliderCleanupSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр. Находит сущности с компонентом PlayerGroundCheckColliderCleanup,
    /// утилизирует связанный коллайдер и удаляет компонент очистки.
    /// </summary>
    protected override void OnUpdate()
    {
        Entities
            .WithStructuralChanges()
            .ForEach((Entity entity, in PlayerGroundCheckColliderCleanup cleanup) =>
            {
                if (cleanup.Collider.IsCreated)
                {
                    cleanup.Collider.Dispose();
                }
                EntityManager.RemoveComponent<PlayerGroundCheckColliderCleanup>(entity);
            })
            .Run();
    }
}