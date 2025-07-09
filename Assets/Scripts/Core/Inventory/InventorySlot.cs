using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Управляет отображением и функциональностью отдельного слота инвентаря в UI.
/// Добавлены "параноидальные" проверки для отладки скрытых NullReferenceException.
/// </summary>
public class InventorySlot : MonoBehaviour
{
    [Tooltip("Изображение иконки предмета в слоте.")]
    public Image icon;

    [Tooltip("Текстовое поле для отображения количества предметов.")]
    public TextMeshProUGUI amountText;

    [Tooltip("Кнопка, представляющая сам слот.")]
    public Button slotButton;

    private Item currentItem;
    
    public event Action<Item> OnSlotClicked;

    private void Awake()
    {
        // Эта проверка остается на всякий случай, она срабатывает самой первой.
        if (icon == null || amountText == null || slotButton == null)
        {
            Debug.LogError($"[InventorySlot AWAKE CHECK] Одна или несколько ссылок (Icon, AmountText, SlotButton) не установлены для слота '{this.gameObject.name}'!", this.gameObject);
        }
    }
    
    /// <summary>
    /// Настраивает внешний вид слота на основе предоставленного предмета и его количества.
    /// </summary>
    public void SetupSlot(Item item, int amount = 1)
    {
        currentItem = item;
        
        // 1. Проверяем, передан ли нам вообще предмет.
        if (item == null)
        {
            ClearSlot();
            return;
        }

        // 2. САМАЯ ВАЖНАЯ ПРОВЕРКА: проверяем ссылки на компоненты UI.
        if (icon == null) 
        {
            Debug.LogError($"[InventorySlot SETUP CHECK] FATAL: Ссылка на 'Icon' (Image) равна NULL на слоте '{this.gameObject.name}'. Не могу установить спрайт.", this.gameObject);
            return; // Прекращаем выполнение, чтобы избежать ошибки
        }
        if (amountText == null)
        {
            Debug.LogError($"[InventorySlot SETUP CHECK] FATAL: Ссылка на 'Amount Text' равна NULL на слоте '{this.gameObject.name}'. Не могу установить текст.", this.gameObject);
        }
        
        // 3. Проверяем, есть ли у самого предмета иконка.
        if (item.icon == null)
        {
            Debug.LogWarning($"[InventorySlot] У предмета '{item.itemName}' (ID: {item.itemID}) в ассете не назначен спрайт (Icon is null). Иконка будет скрыта.");
            icon.enabled = false; // Скрываем Image, если нет спрайта
        }
        else
        {
            icon.sprite = item.icon;
            icon.enabled = true;
        }

        // Обновляем текст количества
        if (amountText != null)
        {
            amountText.text = (amount > 1 && item.maxStack > 1) ? amount.ToString() : "";
        }
        
        // Включаем кнопку
        if (slotButton != null)
        {
            slotButton.interactable = true;
        }
    }

    /// <summary>
    /// Очищает слот инвентаря.
    /// </summary>
    public void ClearSlot()
    {
        currentItem = null;

        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (amountText != null)
        {
            amountText.text = "";
        }

        if (slotButton != null)
        {
            slotButton.interactable = false;
        }
    }
    
    /// <summary>
    /// Вызывается Unity при нажатии на кнопку слота.
    /// </summary>
    public void OnSlotClick()
    {
        if (currentItem == null) return;
        
        OnSlotClicked?.Invoke(currentItem);
    }
}