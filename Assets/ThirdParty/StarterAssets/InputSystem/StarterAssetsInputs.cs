using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace StarterAssets
{
    /// <summary>
    /// Этот класс является чистым приемником сообщений от Player Input.
    /// Он НЕ хранит состояние, а просто генерирует C#-события.
    /// На эти события подпишется наша ECS-система для обработки ввода.
    /// </summary>
    public class StarterAssetsInputs : MonoBehaviour
    {
        /// <summary>
        /// Событие, вызываемое при изменении входных данных движения.
        /// </summary>
        public static event Action<Vector2> onMove;

        /// <summary>
        /// Событие, вызываемое при изменении входных данных обзора (камеры).
        /// </summary>
        public static event Action<Vector2> onLook;

        /// <summary>
        /// Событие, вызываемое при изменении состояния кнопки спринта.
        /// </summary>
        public static event Action<bool> onSprint;
        
        /// <summary>
        /// Событие, вызываемое при однократном нажатии кнопки прыжка.
        /// </summary>
        public static event Action onJump;

        /// <summary>
        /// Событие, вызываемое при однократном нажатии кнопки инвентаря.
        /// </summary>
        public static event Action onInventory;

        /// <summary>
        /// Событие, вызываемое при однократном нажатии правой кнопки мыши (или эквивалентного вторичного действия).
        /// </summary>
        public static event Action onRightClick;

        /// <summary>
        /// Событие, вызываемое при изменении состояния основного действия (например, ЛКМ).
        /// </summary>
        public static event Action<bool> onPrimaryAction; 

        /// <summary>
        /// Событие, вызываемое при изменении состояния кнопки поворота.
        /// true — кнопка зажата, false — отпущена.
        /// </summary>
        public static event Action<bool> onRotate;

#if ENABLE_INPUT_SYSTEM

        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки ввода поворота.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnRotate(InputValue value)
        {
            onRotate?.Invoke(value.isPressed);
        }

        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки ввода движения.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnMove(InputValue value)
        {
            onMove?.Invoke(value.Get<Vector2>());
        }

        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки ввода обзора.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnLook(InputValue value)
        {
            onLook?.Invoke(value.Get<Vector2>());
        }

        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки ввода спринта.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnSprint(InputValue value)
        {
            onSprint?.Invoke(value.isPressed);
        }

        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки ввода прыжка.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                onJump?.Invoke();
            }
        }
        
        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки ввода инвентаря.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnInventory(InputValue value)
        {
            if (value.isPressed)
            {
                onInventory?.Invoke();
            }
        }

        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки ввода правой кнопки мыши.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnRightClick(InputValue value)
        {
            if (value.isPressed)
            {
                onRightClick?.Invoke();
            }
        }

        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки основного действия (ЛКМ).
        /// Важно: в отличие от Jump/Inventory, он передает состояние `isPressed`, 
        /// чтобы мы знали, когда кнопка зажата, а когда отпущена.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnFire(InputValue value) 
        {
            onPrimaryAction?.Invoke(value.isPressed);
        }

#endif
    }
}