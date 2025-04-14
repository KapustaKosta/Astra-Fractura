using UnityEngine;
using StarterAssets; // Для StarterAssetsInputs

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform slotsParent;
    public GameObject slotPrefab;
    public GameObject inventoryPanel;

    [Header("Player Input")]
    public FirstPersonController fpsController; // Контроллер из Starter Assets
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
            slots[i] = slot.GetComponent<InventorySlot>();
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
}