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

    /// <summary>
    /// Маска слоев, которые нужно игнорировать при проверке заземления.
    /// </summary>
    public LayerMask IgnoreLayers;

    /// <summary>
    /// Скорость вращения игрока/камеры.
    /// </summary>
    [Header("Cinemachine Proxy")]
    public float RotationSpeed = 1.0f;

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

    /// <summary>
    /// Коэффициент демпфирования линейной скорости при движении по земле.
    /// </summary>
    public float GroundDamping = 0.05f;

    /// <summary>
    /// Коэффициент демпфирования линейной скорости при движении в воздухе.
    /// </summary>
    public float AirDamping = 0.01f;

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
/// Создает и добавляет все необходимые компоненты для контроллера игрока в ECS-сущность.
/// </summary>
public partial class PlayerBaker : Baker<PlayerAuthoring>
{
    /// <summary>
    /// Выполняет процесс "запекания" данных из MonoBehaviour в ECS-сущности.
    /// Добавляет PlayerGroundCheckData, PlayerGroundedState, PlayerControllerData,
    /// PlayerStateData, InputsData, InventoryInputData, CameraTargetData и RigidBodyPushData.
    /// Также создает коллайдер сферы для проверки земли и добавляет компонент очистки для него.
    /// </summary>
    /// <param name="authoring">Экземпляр PlayerAuthoring.</param>
    public override void Bake(PlayerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

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
            GroundCheckSphereCollider = bakedCollider
        });

        AddComponent(entity, new PlayerGroundCheckColliderCleanup { Collider = bakedCollider }); 

        AddComponent(entity, new PlayerStateData
        {
            currentSpeed = 0f,
            verticalVelocity = 0f,
            cinemachineTargetPitch = 0f,
            jumpTimeoutDelta = authoring.JumpTimeout,
            fallTimeoutDelta = authoring.FallTimeout,
            isGrounded = true
        });

        AddComponent(entity, new InputsData());
        AddComponent(entity, new InventoryInputData());
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