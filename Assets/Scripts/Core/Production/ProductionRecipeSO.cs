using UnityEngine;
using System.Collections.Generic;
using Game.Workshop;

namespace Game.Production
{
    [CreateAssetMenu(fileName = "REC_", menuName = "Game/Production/Production Recipe")]
    public class ProductionRecipeSO : ScriptableObject
    {
        [Header("General Info")]
        public int RecipeID;
        public string RecipeName;

        [Header("Production Parameters")]
        public StationType RequiredStationType;
        [Min(0f)]
        public float HammerCost;
        [Min(0.01f)]
        public float BaseTime;
        [Min(0f)]
        public float RequiredKW;

        [Header("Ingredients & Products")]
        public List<RecipeIngredient> Ingredients;
        public Item OutputItem;
        [Min(1)]
        public int OutputAmount = 1;

        [System.Serializable]
        public struct RecipeIngredient
        {
            public Item Item;
            [Min(1)]
            public int Amount;
        }

        public Sprite Icon => OutputItem != null ? OutputItem.icon : null;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(RecipeName) && OutputItem != null)
            {
                RecipeName = $"Производство: {OutputItem.itemName}";
            }
            if (RecipeID == 0 && OutputItem != null)
            {
                RecipeID = OutputItem.itemID;
            }
        }
    }
}