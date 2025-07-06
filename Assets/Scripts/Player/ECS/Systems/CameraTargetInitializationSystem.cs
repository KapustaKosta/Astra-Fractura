using Unity.Entities;
using UnityEngine;

/// <summary>
/// Однократно выполняемая система, которая находит Cinemachine "прокси-цель"
/// и связывает ее с сущностью игрока. После успешной привязки система отключается.
/// Это обеспечивает правильное позиционирование и ориентацию камеры в игре.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class CameraTargetInitializationSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<CameraProxyData>(); // Требуем синглтон с настройками
    }

    /// <summary>
    /// Вызывается каждый кадр в группе инициализации.
    /// Система ищет GameObject с тегом "CinemachineProxyTarget",
    /// а затем находит сущность игрока, у которой еще не инициализирована CameraTargetData.ProxyTarget,
    /// и устанавливает эту ссылку. После успешной инициализации система отключается.
    /// </summary>
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<CameraProxyData>(out var proxyData)) return;
        
        string tagToFind = proxyData.CinemachineProxyTag.ToString();
        if (string.IsNullOrEmpty(tagToFind)) return;

        GameObject proxyTarget = GameObject.FindWithTag(tagToFind);
        if (proxyTarget == null) return;

        var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        foreach (var (cameraTarget, entity) in SystemAPI.Query<CameraTargetData>().WithNone<PlayerInitializedTag>().WithEntityAccess())
        {
            if (cameraTarget.ProxyTarget == null)
            {
                cameraTarget.ProxyTarget = proxyTarget;
                ecb.AddComponent<PlayerInitializedTag>(entity);

                this.Enabled = false;
            }
        }
    }
}

/// <summary>
/// Тег-компонент, указывающий, что сущность игрока была полностью инициализирована
/// (включая связь с прокси-целью камеры).
/// </summary>
public struct PlayerInitializedTag : IComponentData { }