using UnityEngine;
using StarterAssets;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform slotsParent;
    public GameObject slotPrefab;
    public GameObject inventoryPanel;

    [Header("Player Input")]
    public FirstPersonController fpsController;
    private StarterAssetsInputs _input;

    private Inventory inventory;
    private InventorySlot[] slots;
    private bool isOpen = false;

    void Start()
    {
        inventory = Inventory.Instance;
        _input = FindObjectOfType<StarterAssetsInputs>();
        fpsController = FindObjectOfType<FirstPersonController>();

        InitializeSlots();
        SetInventoryState(false);
    }

    void InitializeSlots()
    {
        slots = new InventorySlot[inventory.space];
        for (int i = 0; i < inventory.space; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotsParent);
            InventorySlot slotComponent = slot.GetComponent<InventorySlot>();
            slots[i] = slotComponent;

            // Подписываемся на событие
            slotComponent.OnSlotClicked += HandleSlotClicked;
        }
    }

    void Update()
    {
        if (_input != null && _input.inventory)
        {
            ToggleInventory();
            _input.inventory = false; // Сбрасываем флаг
        }
    }

    public void ToggleInventory()
    {
        SetInventoryState(!isOpen);
    }

    private void SetInventoryState(bool state)
    {
        isOpen = state;
        inventoryPanel.SetActive(state);

        // Управление курсором
        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = state;

        // Блокировка управления персонажем
        if (fpsController != null)
        {
            fpsController.enabled = !state;
        }

        // Блокировка вращения камеры
        if (_input != null)
        {
            _input.cursorLocked = !state;
            _input.cursorInputForLook = !state;
        }

        if (state) UpdateUI();
    }

    void UpdateUI()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < inventory.items.Count)
                slots[i].SetupSlot(inventory.items[i].item, inventory.items[i].amount);
            else
                slots[i].ClearSlot();
        }
    }

    private void HandleSlotClicked(Item clickedItem)
    {
        // Скрываем инвентарь, если выбран предмет типа Building
        if (clickedItem.itemType == ItemType.Building)
        {
            ToggleInventory();
        }
    }

    private void OnDestroy()
    {
        // Отписываемся от событий для всех слотов
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    slot.OnSlotClicked -= HandleSlotClicked;
                }
            }
        }
    }
}
