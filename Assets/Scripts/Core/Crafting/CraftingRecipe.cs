using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject, представляющий рецепт крафта.
/// Содержит только данные о необходимых предметах и результате.
/// Вся логика крафта вынесена в ECS-системы и UI.
/// </summary>
[CreateAssetMenu(fileName = "New Recipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Tooltip("Предмет, который будет создан в результате крафта.")]
    public Item resultItem;
    
    [Tooltip("Количество создаваемых предметов.")]
    public int resultAmount = 1;

    [Tooltip("Список предметов, необходимых для крафта.")]
    public List<Item> requiredItems;

    [Tooltip("Список соответствующего количества для каждого предмета из requiredItems.")]
    public List<int> requiredAmounts;
    
    // Методы CanCraft() и Craft() были удалены, так как класс Inventory больше не существует.
    // Эту логику теперь выполняет CraftingUI, который читает ECS-инвентарь игрока
    // и создает ECS-запросы на добавление/удаление предметов.
}