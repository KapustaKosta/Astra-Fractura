using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для настроек, связанных с камерой,
/// в частности, для хранения тега объекта-цели Cinemachine.
/// </summary>
public class CameraProxyAuthoring : MonoBehaviour
{
    [Tooltip("Тег GameObject'а, который используется как прокси-цель для Cinemachine.")]
    public string CinemachineProxyTag = "CinemachineProxyTarget";

    class Baker : Baker<CameraProxyAuthoring>
    {
        public override void Bake(CameraProxyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new CameraProxyData
            {
                CinemachineProxyTag = new FixedString64Bytes(authoring.CinemachineProxyTag)
            });
        }
    }
}
