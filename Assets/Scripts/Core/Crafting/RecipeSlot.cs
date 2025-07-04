using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecipeSlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI requirements;
    [SerializeField] private Button craftButton;
    
    private CraftingRecipe recipe;
    private CraftingUI craftingUI;
    

    public void Setup(CraftingRecipe recipe, CraftingUI ui)
    {
        this.recipe = recipe;
        craftingUI = ui;

        icon.sprite = recipe.resultItem.icon;
        itemName.text = recipe.resultItem.itemName;

        string reqText = "";
        for (int i = 0; i < recipe.requiredItems.Count; i++)
        {
            reqText += $"{recipe.requiredItems[i].itemName} x{recipe.requiredAmounts[i]}\n";
        }
        requirements.text = reqText;

        craftButton.onClick.AddListener(OnCraftClick);
    }

    private void OnCraftClick()
    {
        craftingUI.TryCraft(recipe);
    }
}