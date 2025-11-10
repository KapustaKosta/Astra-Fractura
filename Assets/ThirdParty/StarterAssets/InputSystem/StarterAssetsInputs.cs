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
    /// Событие, вызываемое при нажатии клавиши исследования (например, TAB).
    /// </summary>
    public static event Action onResearch;

        /// <summary>
        /// Событие, вызываемое при однократном нажатии правой кнопки мыши (или эквивалентного вторичного действия).
        /// </summary>
        public static event Action onRightClick;

        /// <summary>
        /// Событие, вызываемое при изменении состояния основного действия (например, ЛКМ).
        /// </summary>
        public static event Action<bool> onPrimaryAction; 
        
        /// <summary>
        /// Событие, вызываемое при прокрутке колеса мыши. Передает значение Y.
        /// </summary>
        public static event Action<float> onQuickbarScroll;
        
        /// <summary>
        /// Событие, вызываемое при нажатии цифровой клавиши (1-8). Передает номер клавиши.
        /// </summary>
        public static event Action<int> onQuickbarDigit;

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
        /// Метод, вызываемый компонентом Player Input для обработки запроса исследования.
        /// </summary>
        /// <param name="value">Значение ввода.</param>
        public void OnResearch(InputValue value)
        {
            if (value.isPressed)
            {
                onResearch?.Invoke();
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
        
        /// <summary>
        /// Метод, вызываемый компонентом Player Input для обработки скролла.
        /// </summary>
        public void OnQuickbarScroll(InputValue value)
        {
            float scrollValue = value.Get<Vector2>().y;
            if (scrollValue != 0)
            {
                onQuickbarScroll?.Invoke(scrollValue);
            }
        }
        
        /// <summary>
        /// Методы, вызываемые компонентом Player Input для обработки нажатий цифровых клавиш.
        /// </summary>
        public void OnQuickbarAlpha1(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(1); }
        public void OnQuickbarAlpha2(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(2); }
        public void OnQuickbarAlpha3(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(3); }
        public void OnQuickbarAlpha4(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(4); }
        public void OnQuickbarAlpha5(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(5); }
        public void OnQuickbarAlpha6(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(6); }
        public void OnQuickbarAlpha7(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(7); }
        public void OnQuickbarAlpha8(InputValue value) { if (value.isPressed) onQuickbarDigit?.Invoke(8); }

#endif
    }
}