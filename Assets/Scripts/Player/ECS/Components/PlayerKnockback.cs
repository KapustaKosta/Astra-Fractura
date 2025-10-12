using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Компонент-состояние. Добавляется к игроку, когда он получает
/// импульс отталкивания (кнокбек). Система движения игрока
/// будет игнорировать ввод, пока этот компонент существует.
/// </summary>
public struct PlayerKnockback : IComponentData
{
    /// <summary>
    /// Текущая скорость кнокбека. Будет затухать со временем.
    /// </summary>
    public float3 Velocity;

    /// <summary>
    /// Коэффициент затухания. Каждый кадр скорость будет умножаться на это значение.
    /// Например, 0.95f.
    /// </summary>
    public float Damping;
}