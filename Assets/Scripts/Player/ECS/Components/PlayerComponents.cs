
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;

/// <summary>
/// ECS-компонент, хранящий конфигурируемые данные для контроллера игрока,
/// такие как скорости, параметры прыжка, гравитация и настройки камеры.
/// </summary>
public struct PlayerControllerData : IComponentData
{

    public float MoveSpeed;
    
    public float SprintSpeed;
    
    public float SpeedChangeRate;
    
    public float JumpHeight;
    
    public float Gravity;

    /// <summary>
    /// Время задержки перед возможностью повторного прыжка.
    /// </summary>
    public float JumpTimeout;

    /// <summary>
    /// Время задержки перед началом падения после прыжка.
    /// </summary>
    public float FallTimeout;

    /// <summary>
    /// Скорость вращения игрока/камеры.
    /// </summary>
    public float RotationSpeed;

    /// <summary>
    /// Максимальный угол наклона камеры вверх (по питчу).
    /// </summary>
    public float TopClamp;

    /// <summary>
    /// Минимальный угол наклона камеры вниз (по питчу).
    /// </summary>
    public float BottomClamp;

    /// <summary>
    /// Смещение центра проверки земли относительно центра игрока.
    /// </summary>
    public float GroundedOffset;

    /// <summary>
    /// Радиус сферы для проверки земли.
    /// </summary>
    public float GroundedRadius;

    /// <summary>
    /// Маска слоев, которые считаются землей для проверки заземления.
    /// </summary>
    public int GroundLayers;

    /// <summary>
    /// Максимальная скорость падения игрока.
    /// </summary>
    public float TerminalVelocity;

    /// <summary>
    /// Вертикальное смещение цели камеры относительно игрока.
    /// </summary>
    public float CameraHeightOffset;

    /// <summary>
    /// Коэффициент демпфирования линейной скорости при движении по земле.
    /// </summary>
    public float GroundDamping;

    /// <summary>
    /// Коэффициент демпфирования линейной скорости при движении в воздухе.
    /// </summary>
    public float AirDamping;

    /// <summary>
    /// Ссылка на BlobAssetReference Collider для коллайдера сферы проверки земли.
    /// </summary>
    public BlobAssetReference<Unity.Physics.Collider> GroundCheckSphereCollider;
}

/// <summary>
/// ECS-компонент-объект, содержащий ссылку на GameObject-прокси,
/// используемый Cinemachine для отслеживания камеры.
/// </summary>
public class CameraTargetData : IComponentData
{
    public GameObject ProxyTarget;
}

/// <summary>
/// ECS-компонент, хранящий динамические данные состояния игрока,
/// такие как текущая скорость, вертикальная скорость, состояние прыжка/падения
/// и параметры камеры.
/// </summary>
public struct PlayerStateData : IComponentData
{
    public float currentSpeed;
    
    public float verticalVelocity;
    
    public float cinemachineTargetPitch;
    
    public float jumpTimeoutDelta;
    
    public float fallTimeoutDelta;
    
    public bool isGrounded;
}


/// <summary>
/// ECS-компонент, хранящий данные о слоях, которые нужно игнорировать
/// при проверке заземления игрока.
/// </summary>
public struct PlayerGroundCheckData : IComponentData
{
    /// <summary>
    /// Маска слоев, которые следует игнорировать при проверке земли.
    /// </summary>
    public int IgnoreLayers;
}