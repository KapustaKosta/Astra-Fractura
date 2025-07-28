using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Mathematics;

/// <summary>
/// Управляет визуальным состоянием одного UI-элемента в очереди крафта.
/// Этот компонент не содержит собственной логики, а только отображает данные,
/// которые ему передает родительский управляющий скрипт.
/// </summary>
public class CraftingQueueItemUI : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Slider progressSlider;

    /// <summary>
    /// Обновляет все дочерние UI-элементы на основе актуальных данных из мира ECS.
    /// Является единственным публичным методом и точкой входа для управления этим компонентом.
    /// </summary>
    /// <param name="data">Структура с данными о конкретном элементе в очереди крафта.</param>
    /// <param name="isCurrentlyCrafting">Флаг, определяющий, активен ли крафт этого предмета (первый в очереди) или он ожидает.</param>
    public void UpdateData(CraftingQueueElement data, bool isCurrentlyCrafting)
    {
        // Для получения имени и иконки обращаемся к глобальному реестру предметов по ID.
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;
        
        var itemData = itemRegistry.GetItemData(data.ResultItemID);
        if (itemData == null) return;

        itemIcon.sprite = itemData.icon;
        itemNameText.text = itemData.itemName;

        // Основная логика, определяющая, в каком из двух состояний находится UI-элемент:
        // "активный крафт" или "ожидание в очереди".
        if (isCurrentlyCrafting)
        {
            // Обновляем UI для отображения прогресса активного крафта.
            statusText.text = $"Осталось: {data.TimeRemaining:F1} с";
            if (progressSlider != null)
            {
                progressSlider.gameObject.SetActive(true);
                // Прогресс вычисляется как отношение прошедшего времени к общему,
                // поэтому мы вычитаем долю оставшегося времени из единицы.
                float progress = 1.0f - (data.TimeRemaining / data.TotalCraftingTime);
                progressSlider.value = math.clamp(progress, 0f, 1f);
            }
        }
        else
        {
            // Обновляем UI для отображения элемента, который ожидает своей очереди.
            statusText.text = "В очереди";
            if (progressSlider != null)
            {
                progressSlider.gameObject.SetActive(false);
            }
        }
    }
}