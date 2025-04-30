using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class InventorySlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;
    public Button slotButton;

    private Item currentItem;
    private int currentAmount;

    // Событие для уведомления об активации слота
    public event Action<Item> OnSlotClicked;

    public void SetupSlot(Item item, int amount = 1)
    {
        currentItem = item;
        currentAmount = amount;

        if (item == null)
        {
            ClearSlot();
            return;
        }

        icon.sprite = item.icon;
        icon.enabled = true;

        if (amount > 1 && item.maxStack > 1)
            amountText.text = amount.ToString();
        else
            amountText.text = "";

        slotButton.interactable = true;
    }

    public void ClearSlot()
    {
        currentItem = null;
        currentAmount = 0;

        icon.sprite = null;
        icon.enabled = false;
        amountText.text = "";
        slotButton.interactable = false;
    }

    public void OnSlotClick()
    {
        if (currentItem == null) return;

        Inventory.Instance.selectedItem = currentItem;

        // Вызываем событие
        OnSlotClicked?.Invoke(currentItem);

        // Локальная логика для обработки типов предметов
        switch (currentItem.itemType)
        {
            case ItemType.Consumable:
                Inventory.Instance.Remove(currentItem);
                Debug.Log($"Использован: {currentItem.itemName}");
                break;

            case ItemType.Building:
                GameModeManager.Instance.SetBuildingMode(currentItem);
                break;
        }
    }
}
