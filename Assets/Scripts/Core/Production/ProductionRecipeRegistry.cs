using Unity.Collections;
using Unity.Entities;

namespace Game.Production
{
    public struct RecipeIngredientData { public int ItemID; public int Amount; }

    public struct ProductionRecipe
    {
        public int RecipeID;
        public FixedString128Bytes RecipeName;

        public BlobArray<RecipeIngredientData> Inputs;
        public int OutputItemID;
        public int OutputAmount;
        public float HammerCost;
        public float BaseTime;
        public float RequiredKW;
        public int RequiredStationTypeID;
    }

    public struct ProductionRecipeRegistryBlob
    {
        public BlobArray<ProductionRecipe> Recipes;
    }

    public struct ProductionRecipeRegistryData : IComponentData
    {
        public BlobAssetReference<ProductionRecipeRegistryBlob> Blob;
    }
}