using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;

namespace Game.Production
{
    public class ProductionRecipeRegistryAuthoring : MonoBehaviour
    {
        public ProductionRecipeRegistrySO registry;

        class Baker : Baker<ProductionRecipeRegistryAuthoring>
        {
            public override void Bake(ProductionRecipeRegistryAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.None);
                if (a.registry == null || a.registry.Recipes == null) return;

                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ProductionRecipeRegistryBlob>();

                var recipes = a.registry.Recipes
                    .Where(r => r != null && r.OutputItem != null && r.RequiredStationType != null)
                    .ToList();

                var dst = builder.Allocate(ref root.Recipes, recipes.Count);

                for (int i = 0; i < dst.Length; i++)
                {
                    var src = recipes[i];
                    dst[i].RecipeID = src.RecipeID;
                    dst[i].RecipeName = new FixedString128Bytes(src.RecipeName);
                    dst[i].OutputItemID = src.OutputItem.itemID;
                    dst[i].OutputAmount = math.max(1, src.OutputAmount);
                    dst[i].BaseTime = math.max(0.01f, src.BaseTime);
                    dst[i].HammerCost = math.max(0f, src.HammerCost);
                    dst[i].RequiredKW = math.max(0f, src.RequiredKW);
                    dst[i].RequiredStationTypeID = src.RequiredStationType.StationTypeID;

                    var ingredients = src.Ingredients.Where(ing => ing.Item != null).ToList();
                    var inArr = builder.Allocate(ref dst[i].Inputs, ingredients.Count);
                    for (int j = 0; j < ingredients.Count; j++)
                    {
                        inArr[j] = new RecipeIngredientData { ItemID = ingredients[j].Item.itemID, Amount = math.max(1, ingredients[j].Amount) };
                    }
                }

                var blob = builder.CreateBlobAssetReference<ProductionRecipeRegistryBlob>(Allocator.Persistent);
                builder.Dispose();

                AddComponent(e, new ProductionRecipeRegistryData { Blob = blob });
            }
        }
    }
}