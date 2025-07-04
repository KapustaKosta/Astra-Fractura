using UnityEngine;
using UnityEngine.UI;

public class CraftingUI : MonoBehaviour
{
    [SerializeField] private Transform recipesParent;
    [SerializeField] private GameObject recipePrefab;
    [SerializeField] private Inventory inventory;

    void Start()
    {
        RefreshRecipes();
    }

    public void RefreshRecipes()
    {
        foreach (Transform child in recipesParent)
        {
            Destroy(child.gameObject);
        }

        CraftingRecipe[] recipes = Resources.LoadAll<CraftingRecipe>("CraftingRecipes");
        
        foreach (var recipe in recipes)
        {
            GameObject slot = Instantiate(recipePrefab, recipesParent);
            RecipeSlot recipeSlot = slot.GetComponent<RecipeSlot>();
            recipeSlot.Setup(recipe, this);
        }
    }

    public void TryCraft(CraftingRecipe recipe)
    {
        if (recipe.CanCraft(inventory))
        {
            inventory.CraftItem(recipe);
            RefreshRecipes();
        }
        else
        {
            Debug.Log("Не хватает ресурсов!");
        }
    }
}