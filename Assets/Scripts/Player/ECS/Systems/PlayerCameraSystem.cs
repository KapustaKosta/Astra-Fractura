using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// ECS-система, управляющая поведением камеры игрока.
/// Обновляет вращение игрока по горизонтали (рыскание) и питч (наклон) CinemachineProxyTarget.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerCameraSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр.
    /// Обновляет поворот игрока и CinemachineProxyTarget на основе ввода игрока.
    /// </summary>
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        Entities
            .WithoutBurst()
            .WithAll<PlayerInitializedTag>()
            .ForEach((ref LocalTransform playerTransform, ref PlayerStateData stateData, in PlayerControllerData controllerData, in InputsData inputs, in CameraTargetData cameraTarget) =>
            {
                var proxy = cameraTarget.ProxyTarget;
                if (proxy == null) return;

                // Горизонтальное вращение (Рыскание)
                if (math.abs(inputs.look.x) > 0.01f)
                {
                    float rotationVelocity = inputs.look.x * controllerData.RotationSpeed;
                    if (!inputs.isMouseControl)
                    {
                        rotationVelocity *= deltaTime;
                    }
                    
                    playerTransform = playerTransform.RotateY(math.radians(rotationVelocity));
                }

                // Вертикальное вращение (Питч)
                if (math.abs(inputs.look.y) > 0.01f)
                {
                    float pitchDelta = inputs.look.y * controllerData.RotationSpeed;
                    if (!inputs.isMouseControl)
                    {
                        pitchDelta *= deltaTime;
                    }

                    stateData.cinemachineTargetPitch += pitchDelta;
                    stateData.cinemachineTargetPitch = ClampAngle(
                        stateData.cinemachineTargetPitch,
                        controllerData.BottomClamp,
                        controllerData.TopClamp);
                }

                // Обновление прокси Cinemachine
                float3 proxyPosition = playerTransform.Position + new float3(0, controllerData.CameraHeightOffset, 0);

                Quaternion playerHorizontalRotation = playerTransform.Rotation;
                Quaternion cameraPitchRotation = Quaternion.Euler(stateData.cinemachineTargetPitch, 0.0f, 0.0f);
                
                proxy.transform.rotation = playerHorizontalRotation * cameraPitchRotation;
                proxy.transform.position = proxyPosition;

            }).Run();
    }
    
    /// <summary>
    /// Ограничивает угол в заданном диапазоне.
    /// </summary>
    /// <param name="lfAngle">Входной угол.</param>
    /// <param name="lfMin">Минимальное значение угла.</param>
    /// <param name="lfMax">Максимальное значение угла.</param>
    /// <returns>Ограниченный угол.</returns>
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}