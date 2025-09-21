using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;
using Unity.Entities;

public class InventorySlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerDownHandler
{
    public Image icon;
    public TextMeshProUGUI amountText;
    public Button slotButton;
    public Image slotBackground;

    public Item CurrentItem => currentItem;
    public int CurrentAmount => currentAmount;
    public InventoryType SlotInventoryType { get; private set; }

    internal Entity ownerEntity;
    internal int slotIndex;

    public event Action<InventorySlot> OnSlotClicked;

    private Item currentItem;
    private int currentAmount;

    private void Awake()
    {
        if (icon == null || amountText == null || slotButton == null)
        {
            Debug.LogError($"Одна или несколько ссылок (Icon, AmountText, SlotButton) не установлены для слота '{this.gameObject.name}'!", this.gameObject);
        }
    }

    public void InitializeSlot(Item newItem, int amount, Entity owner, int index, InventoryType inventoryType)
    {
        ownerEntity = owner;
        slotIndex = index;
        SlotInventoryType = inventoryType;

        // НОВАЯ ПРОВЕРКА: Логируем, что пришло на вход
        if (slotIndex == 0) // Логируем только для проблемного слота
        {
            string itemName = newItem != null ? newItem.name : "NULL";
                //Debug.Log($"<color=lime>[Slot #{slotIndex}]</color> InitializeSlot called with Item: '{itemName}', Amount: {amount}");
        }

        if (newItem == null || amount <= 0)
        {
            ClearSlot();
            return;
        }

        currentItem = newItem;
        currentAmount = amount;

        if (icon == null || amountText == null) return;

        if (currentItem.icon == null)
        {
            icon.enabled = false;
        }
        else
        {
            icon.sprite = currentItem.icon;
            icon.enabled = true;
        }

        amountText.text = (currentAmount > 1 && currentItem.maxStack > 1) ? currentAmount.ToString() : string.Empty;

        if (slotButton != null)
        {
            slotButton.interactable = true;
        }
    }

    public void ClearSlot()
    {
        // НОВАЯ ПРОВЕРКА: Логируем, когда слот очищается
        if (slotIndex == 0)
        {
            Debug.Log($"<color=lime>[Slot #{slotIndex}]</color> <B>ClearSlot called!</B> The slot will become non-interactive.");
        }

        currentItem = null;
        currentAmount = 0;

        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (amountText != null)
        {
            amountText.text = string.Empty;
        }

        if (slotButton != null)
        {
            slotButton.interactable = false;
        }
    }

    public void SetHighlightStatus(bool isActive, QuickbarSettings settings)
    {
        if (slotBackground != null && settings != null)
        {
            slotBackground.color = isActive ? settings.activeSlotColor : settings.inactiveSlotColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
        {
            OnSlotClicked?.Invoke(this);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (currentItem != null)
        {
            DragAndDropHandler.Instance?.SetSourceSlot(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem != null)
        {
            bool isSplitDrag = eventData.button == PointerEventData.InputButton.Right;
            DragAndDropHandler.Instance?.OnBeginDrag(eventData, isSplitDrag);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (currentItem != null)
        {
            DragAndDropHandler.Instance?.OnDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragAndDropHandler.Instance?.OnEndDrag(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var handler = DragAndDropHandler.Instance;
        if (handler == null) return;

        InventorySlot source = handler.GetSourceSlot();
        if (source != null && source != this && source.CurrentItem != null)
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var requestEntity = entityManager.CreateEntity();

            entityManager.AddComponentData(requestEntity, new DestinationInventoryHint { Type = this.SlotInventoryType });

            if (handler.IsSplitting())
            {
                entityManager.AddComponentData(requestEntity, new SplitStackRequest
                {
                    SourceInventoryOwner = source.ownerEntity,
                    SourceSlotIndex = source.slotIndex,
                    DestinationInventoryOwner = this.ownerEntity,
                    DestinationSlotIndex = this.slotIndex,
                    AmountToMove = handler.GetDraggedAmount()
                });
            }
            else
            {
                entityManager.AddComponentData(requestEntity, new MoveItemRequest
                {
                    SourceInventoryOwner = source.ownerEntity,
                    SourceSlotIndex = source.slotIndex,
                    DestinationInventoryOwner = this.ownerEntity,
                    DestinationSlotIndex = this.slotIndex,
                    ItemID = source.CurrentItem.itemID,
                    Amount = source.CurrentAmount
                });
            }
        }
    }
}