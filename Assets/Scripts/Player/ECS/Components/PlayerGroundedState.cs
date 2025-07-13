using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// ECS-компонент, который хранит текущее состояние заземления персонажа,
/// определенное физическим запросом ECS.
/// </summary>
public struct PlayerGroundedState : IComponentData
{
    /// <summary>
    /// Флаг, указывающий, находится ли игрок на земле.
    /// </summary>
    public bool IsGrounded;

    /// <summary>
    /// Нормаль поверхности, на которой стоит игрок.
    /// </summary>
    public float3 SurfaceNormal;

    /// <summary>
    /// Сущность, с которой игрок соприкасается как с землей.
    /// </summary>
    public Entity GroundEntity;
}