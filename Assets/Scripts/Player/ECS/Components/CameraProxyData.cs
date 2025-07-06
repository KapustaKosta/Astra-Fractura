using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Синглтон-компонент, хранящий тег прокси-цели для камеры.
/// </summary>
public struct CameraProxyData : IComponentData
{
    public FixedString64Bytes CinemachineProxyTag;
}