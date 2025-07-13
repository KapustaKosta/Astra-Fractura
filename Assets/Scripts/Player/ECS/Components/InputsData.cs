using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// ECS компонент, хранящий данные ввода от игрока для использования системами.
/// </summary>
public struct InputsData : IComponentData
{
    public float2 move;
    public float2 look;
    public bool jump;
    public bool sprint;
    public bool analogMovement;
    public bool isMouseControl;
    public bool secondaryActionDown;
    public bool PrimaryAction; 
}