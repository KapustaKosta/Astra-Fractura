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
    
    [Header("Crafting Time")]
    [Tooltip("Время, необходимое для крафта предмета в секундах.")]
    [Min(0.1f)]
    public float craftingTime = 1.0f;
    
}