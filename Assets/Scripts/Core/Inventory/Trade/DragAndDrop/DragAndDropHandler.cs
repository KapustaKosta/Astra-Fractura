using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Unity.Mathematics;

/// <summary>
/// Singleton-обработчик для управления визуальным представлением Drag-and-Drop операции.
/// РЕАЛИЗАЦИЯ ИСПРАВЛЕНА для корректной работы с любым типом Canvas, включая Screen Space - Overlay.
/// </summary>
public class DragAndDropHandler : MonoBehaviour
{
    public static DragAndDropHandler Instance { get; private set; }

    [Tooltip("Image-компонент, который будет отображать иконку перетаскиваемого предмета.")]
    [SerializeField] private Image draggedIconImage;

    [Tooltip("Текстовый компонент для отображения количества перетаскиваемых предметов.")]
    [SerializeField] private TextMeshProUGUI draggedAmountText;

    private RectTransform iconRectTransform;
    private CanvasGroup iconCanvasGroup;

    private Item currentlyDraggedItem;
    private int currentlyDraggedAmount;
    private InventorySlot sourceSlot;

    private bool isSplittingStack = false;
    private CursorLockMode previousLockMode;
    private bool wasCursorVisible;
    private bool isDragging = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (draggedIconImage == null)
        {
            #if UNITY_EDITOR
            Debug.LogError("[DragAndDropHandler] Не назначен компонент Image для DragAndDropHandler.", this);
            #endif
            return;
        }

        iconRectTransform = draggedIconImage.GetComponent<RectTransform>();
        
        iconCanvasGroup = draggedIconImage.GetComponent<CanvasGroup>();
        if (iconCanvasGroup == null)
        {
            iconCanvasGroup = draggedIconImage.gameObject.AddComponent<CanvasGroup>();
        }
        iconCanvasGroup.blocksRaycasts = false;

        draggedIconImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Начинает операцию перетаскивания, вызывается из InventorySlot.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData, bool isSplit)
    {
        if (sourceSlot == null || sourceSlot.CurrentItem == null || isDragging)
            return;

        isSplittingStack = isSplit;
        int originalAmount = sourceSlot.CurrentAmount;
        
        if (isSplittingStack && originalAmount > 1)
        {
            currentlyDraggedAmount = (int)math.ceil(originalAmount / 2.0f);
        }
        else
        {
            isSplittingStack = false; 
            currentlyDraggedAmount = originalAmount;
        }

        isDragging = true;
        currentlyDraggedItem = sourceSlot.CurrentItem;

        previousLockMode = Cursor.lockState;
        wasCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        draggedIconImage.gameObject.SetActive(true);
        draggedIconImage.transform.SetAsLastSibling(); 

        draggedIconImage.sprite = currentlyDraggedItem.icon;
        if (draggedAmountText != null)
        {
            draggedAmountText.text = (currentlyDraggedAmount > 1 && currentlyDraggedItem.maxStack > 1)
                ? currentlyDraggedAmount.ToString()
                : string.Empty;
        }

        UpdateIconPosition(eventData.position);
    }

    /// <summary>
    /// Обновляет позицию иконки, вызывается из InventorySlot.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            UpdateIconPosition(eventData.position);
        }
    }

    /// <summary>
    /// Завершает операцию перетаскивания, вызывается из InventorySlot.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Cursor.lockState = previousLockMode;
        Cursor.visible = wasCursorVisible;
        
        isSplittingStack = false;
        isDragging = false;
        currentlyDraggedItem = null;
        currentlyDraggedAmount = 0;
        sourceSlot = null;
        draggedIconImage.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Обновляет позицию иконки, напрямую присваивая ей позицию курсора.
    /// </summary>
    /// <param name="screenPosition">Текущая позиция курсора из PointerEventData.</param>
    private void UpdateIconPosition(Vector2 screenPosition)
    {
        iconRectTransform.position = screenPosition;
    }

    /// <summary>
    /// Сохраняет исходный слот, из которого был начат драг.
    /// </summary>
    public void SetSourceSlot(InventorySlot slot)
    {
        if (!isDragging)
        {
            sourceSlot = slot;
        }
    }
    
    public bool IsSplitting() => isSplittingStack;
    public int GetDraggedAmount() => currentlyDraggedAmount;
    public InventorySlot GetSourceSlot() => sourceSlot;
}