using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;
    public Button slotButton;

    private Item currentItem;
    private int currentAmount;

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

        // Выбираем предмет в инвентаре
        Inventory.Instance.SelectItem(currentItem);

        switch (currentItem.itemType)
        {
            case ItemType.Consumable:
                // Для расходников сразу используем
                Inventory.Instance.Remove(currentItem);
                Debug.Log($"Использован: {currentItem.itemName}");
                break;
                
            case ItemType.Building:
                // Для зданий активируем режим строительства
                GameModeManager.Instance.SetBuildingMode(currentItem);
                break;
        }
    }
}