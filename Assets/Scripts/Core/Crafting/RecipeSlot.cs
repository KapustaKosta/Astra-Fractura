using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Управляет визуальным представлением и поведением одного слота с рецептом в UI-окне крафта.
/// Этот компонент является "представлением" (View), которое получает данные и делегирует действия
/// родительскому контроллеру CraftingUI.
/// </summary>
public class RecipeSlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI requirements;
    [SerializeField] private Button craftButton;
    [SerializeField] private TextMeshProUGUI craftingTimeText;

    // Хранит ссылку на ScriptableObject рецепта, который представляет этот слот.
    private CraftingRecipe recipe;
    // Хранит ссылку на родительский контроллер для обратных вызовов при действиях пользователя.
    private CraftingUI craftingUI;
    // Кэширует оригинальный цвет иконки для восстановления после изменения состояния.
    private Color originalIconColor;

    /// <summary>
    /// Кэширует начальное состояние компонента перед первым вызовом Update.
    /// </summary>
    private void Awake()
    {
        if (icon != null)
        {
            // Сохраняем исходный цвет иконки, чтобы иметь возможность вернуть его,
            // когда слот снова станет доступным для крафта.
            originalIconColor = icon.color;
        }
    }

    /// <summary>
    /// Инициализирует слот, заполняя все его UI-элементы данными из указанного рецепта.
    /// Этот метод вызывается извне родительским контроллером CraftingUI.
    /// </summary>
    /// <param name="recipe">ScriptableObject рецепта для отображения.</param>
    /// <param name="ui">Ссылка на родительский контроллер CraftingUI.</param>
    public void Setup(CraftingRecipe recipe, CraftingUI ui)
    {
        this.recipe = recipe;
        this.craftingUI = ui;

        icon.sprite = recipe.resultItem.icon;
        itemName.text = recipe.resultItem.itemName;

        // Динамически формируем строку с перечнем всех требуемых ресурсов.
        string reqText = "";
        for (int i = 0; i < recipe.requiredItems.Count; i++)
        {
            reqText += $"{recipe.requiredItems[i].itemName} x{recipe.requiredAmounts[i]}\n";
        }
        requirements.text = reqText;

        if (craftingTimeText != null)
        {
            craftingTimeText.text = $"{recipe.craftingTime:F1} с";
        }

        // Подписываемся на событие нажатия кнопки, чтобы инициировать крафт.
        craftButton.onClick.AddListener(OnCraftClick);
    }

    /// <summary>
    /// Обработчик события нажатия на кнопку крафта.
    /// Делегирует логику попытки крафта родительскому контроллеру.
    /// </summary>
    private void OnCraftClick()
    {
        craftingUI.TryCraft(recipe);
    }

    /// <summary>
    /// Обновляет визуальное состояние слота в зависимости от того,
    /// может ли игрок в данный момент создать предмет по этому рецепту.
    /// </summary>
    /// <param name="canCraft">Флаг, указывающий на доступность крафта.</param>
    public void SetCraftableStatus(bool canCraft)
    {
        if (craftButton != null)
        {
            // Включаем или отключаем интерактивность кнопки.
            craftButton.interactable = canCraft;
        }

        if (icon != null)
        {
            // Изменяем цвет иконки для наглядной обратной связи:
            // серый цвет для недоступного рецепта, оригинальный — для доступного.
            icon.color = canCraft ? originalIconColor : Color.gray;
        }
    }

    /// <summary>
    /// Возвращает ScriptableObject рецепта, связанный с этим слотом.
    /// Используется родительским контроллером для выполнения проверок.
    /// </summary>
    public CraftingRecipe GetRecipe()
    {
        return recipe;
    }
}