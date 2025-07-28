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
    public bool rotate;
    public bool analogMovement;
    public bool isMouseControl;
    public bool secondaryActionDown;
    public bool PrimaryAction; 
    
    /// <summary>
    /// Хранит цифру 1-8, если соответствующая клавиша была нажата в этом кадре. Иначе 0.
    /// </summary>
    public int QuickbarDigitKeyPressed;
    /// <summary>
    /// Хранит дельту прокрутки колеса мыши за этот кадр.
    /// </summary>
    public float QuickbarScrollDelta;
}