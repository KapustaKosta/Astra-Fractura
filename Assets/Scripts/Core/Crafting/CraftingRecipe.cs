using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Recipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    public Item resultItem;
    public int resultAmount = 1;
    public List<Item> requiredItems;
    public List<int> requiredAmounts;
    
    public bool CanCraft(Inventory inventory)
    {
        for (int i = 0; i < requiredItems.Count; i++)
        {
            if (!inventory.HasItem(requiredItems[i], requiredAmounts[i]))
                return false;
        }
        return true;
    }
    
    public void Craft(Inventory inventory)
    {
        if (!CanCraft(inventory)) return;
        
        foreach (var item in requiredItems)
        {
            inventory.Remove(item, requiredAmounts[requiredItems.IndexOf(item)]);
        }
        
        inventory.Add(resultItem, resultAmount);
    }
}