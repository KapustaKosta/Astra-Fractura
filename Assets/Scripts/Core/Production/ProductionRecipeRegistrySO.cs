using UnityEngine;
using System.Collections.Generic;

namespace Game.Production
{
    [CreateAssetMenu(fileName = "ProductionRecipeRegistry", menuName = "Game/Production/Recipe Registry")]
    public class ProductionRecipeRegistrySO : ScriptableObject
    {
        [Tooltip("Перетащите сюда все ассеты рецептов (ProductionRecipeSO) из папки проекта.")]
        public List<ProductionRecipeSO> Recipes;
    }
}