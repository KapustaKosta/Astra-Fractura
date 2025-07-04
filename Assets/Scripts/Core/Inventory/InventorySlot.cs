using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Управляет отображением и функциональностью отдельного слота инвентаря в пользовательском интерфейсе.
/// </summary>
public class InventorySlot : MonoBehaviour
{
    /// <summary>
    /// Изображение иконки предмета в слоте.
    /// </summary>
    public Image icon;

    /// <summary>
    /// Текстовое поле для отображения количества предметов.
    /// </summary>
    public TextMeshProUGUI amountText;

    /// <summary>
    /// Кнопка, представляющая сам слот.
    /// </summary>
    public Button slotButton;

    private Item currentItem;
    
    /// <summary>
    /// Событие, вызываемое при клике по слоту инвентаря, передает Item, который был кликнут.
    /// </summary>
    public event Action<Item> OnSlotClicked;

    /// <summary>
    /// Настраивает внешний вид слота на основе предоставленного предмета и его количества.
    /// </summary>
    /// <param name="item">Предмет для отображения в слоте.</param>
    /// <param name="amount">Количество предмета (по умолчанию 1).</param>
    public void SetupSlot(Item item, int amount = 1)
    {
        currentItem = item;
        
        if (item == null)
        {
            ClearSlot();
            return;
        }

        icon.sprite = item.icon;
        icon.enabled = true;
        amountText.text = (amount > 1 && item.maxStack > 1) ? amount.ToString() : "";
        slotButton.interactable = true;
    }

    /// <summary>
    /// Очищает слот инвентаря, скрывая иконку и текст, и делая кнопку неактивной.
    /// </summary>
    public void ClearSlot()
    {
        currentItem = null;
        icon.sprite = null;
        icon.enabled = false;
        amountText.text = "";
        slotButton.interactable = false;
    }
    
    /// <summary>
    /// Вызывается Unity при нажатии на кнопку слота.
    /// Генерирует событие OnSlotClicked с текущим предметом.
    /// </summary>
    public void OnSlotClick()
    {
        if (currentItem == null) return;
        
        OnSlotClicked?.Invoke(currentItem);
    }
}