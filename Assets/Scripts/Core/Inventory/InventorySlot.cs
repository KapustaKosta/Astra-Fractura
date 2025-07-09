using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;
using Unity.Entities;

/// <summary>
/// Управляет отображением и функциональностью отдельного слота инвентаря в UI.
/// Реализует интерфейсы для обработки событий Drag-and-Drop и кликов.
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerDownHandler
{
    [Tooltip("Изображение иконки предмета в слоте.")]
    public Image icon;

    [Tooltip("Текстовое поле для отображения количества предметов.")]
    public TextMeshProUGUI amountText;

    [Tooltip("Кнопка, представляющая сам слот. Используется для визуальных состояний.")]
    public Button slotButton;

    /// <summary>
    /// Ссылка на ScriptableObject предмета, который в данный момент находится в слоте.
    /// </summary>
    public Item CurrentItem => currentItem;
    /// <summary>
    /// Текущее количество предметов в слоте.
    /// </summary>
    public int CurrentAmount => currentAmount;

    /// <summary>
    /// Сущность-владелец инвентаря, к которому относится этот слот.
    /// </summary>
    internal Entity ownerEntity;
    /// <summary>
    /// Индекс этого слота в буфере инвентаря.
    /// </summary>
    internal int slotIndex;

    /// <summary>
    /// Событие, вызываемое при клике левой кнопкой мыши по слоту.
    /// </summary>
    public event Action<Item> OnSlotClicked;
    
    private Item currentItem;
    private int currentAmount;

    private void Awake()
    {
        // Эта проверка остается на всякий случай, она срабатывает самой первой.
        if (icon == null || amountText == null || slotButton == null)
        {
            Debug.LogError($"[InventorySlot AWAKE CHECK] Одна или несколько ссылок (Icon, AmountText, SlotButton) не установлены для слота '{this.gameObject.name}'!", this.gameObject);
        }
    }

    /// <summary>
    /// Инициализирует слот данными о предмете, его количестве и владельце.
    /// Если предмет null или количество 0, слот очищается.
    /// </summary>
    /// <param name="newItem">ScriptableObject предмета для отображения.</param>
    /// <param name="amount">Количество предмета.</param>
    /// <param name="owner">Сущность-владелец инвентаря.</param>
    /// <param name="index">Индекс слота.</param>
    public void InitializeSlot(Item newItem, int amount, Entity owner, int index)
    {
        ownerEntity = owner;
        slotIndex = index;

        if (newItem == null || amount <= 0)
        {
            ClearSlot();
            return;
        }

        currentItem = newItem;
        currentAmount = amount;

        if (icon == null || amountText == null)
        {
            Debug.LogError($"[InventorySlot SETUP CHECK] FATAL: Ссылки на UI элементы (Icon, AmountText) равны NULL на слоте '{this.gameObject.name}'.", this.gameObject);
            return;
        }

        if (currentItem.icon == null)
        {
            Debug.LogWarning($"[InventorySlot] У предмета '{currentItem.itemName}' (ID: {currentItem.itemID}) в ассете не назначен спрайт. Иконка будет скрыта.");
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

    /// <summary>
    /// Очищает слот, сбрасывая данные о предмете и обновляя UI.
    /// Делает слот неинтерактивным.
    /// </summary>
    public void ClearSlot()
    {
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

    /// <summary>
    /// Обрабатывает клик по слоту (реализация IPointerClickHandler).
    /// Вызывает событие OnSlotClicked при клике левой кнопкой мыши.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
        {
            if (currentItem != null)
            {
                OnSlotClicked?.Invoke(currentItem);
            }
        }
    }

    /// <summary>
    /// Обрабатывает нажатие кнопки мыши на слоте (реализация IPointerDownHandler).
    /// Используется для инициализации операции Drag-and-Drop.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (currentItem != null)
        {
            DragAndDropHandler.Instance?.SetSourceSlot(this);
        }
    }

    /// <summary>
    /// Вызывается в начале операции перетаскивания (реализация IBeginDragHandler).
    /// Определяет, какой кнопкой мыши начато перетаскивание, и передает эту информацию.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem != null)
        {
            bool isSplitDrag = eventData.button == PointerEventData.InputButton.Right;
            DragAndDropHandler.Instance?.OnBeginDrag(eventData, isSplitDrag);
        }
    }

    /// <summary>
    /// Вызывается каждый кадр во время перетаскивания (реализация IDragHandler).
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (currentItem != null)
        {
            DragAndDropHandler.Instance?.OnDrag(eventData);
        }
    }

    /// <summary>
    /// Вызывается при завершении операции перетаскивания (реализация IEndDragHandler).
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        DragAndDropHandler.Instance?.OnEndDrag(eventData);
    }

    /// <summary>
    /// Вызывается, когда другой объект "брошен" на этот слот (реализация IDropHandler).
    /// Создает ECS-запрос на перемещение или разделение стака.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        var handler = DragAndDropHandler.Instance;
        if (handler == null) return;
        
        InventorySlot source = handler.GetSourceSlot();
        if (source != null && source != this && source.CurrentItem != null)
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var requestEntity = entityManager.CreateEntity();

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