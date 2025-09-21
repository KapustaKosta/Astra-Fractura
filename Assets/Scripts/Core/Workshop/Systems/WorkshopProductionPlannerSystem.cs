﻿using Energy.Core;
using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    /// <summary>
    /// Система-планировщик, которая проверяет, возможно ли выполнить текущий
    /// производственный заказ в цехе, исходя из имеющихся ресурсов.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorkshopPlannerSystem : ISystem
    {
        /// <summary>
        /// Вызывается при создании системы для установки необходимых зависимостей.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        /// <summary>
        /// Основной метод обновления. Срабатывает при запросе на пересчет плана.
        /// Рекурсивно проверяет, хватает ли ресурсов для производства финального
        /// продукта из очереди, учитывая необходимость крафта промежуточных компонентов.
        /// Помечает цех тегом Feasible (выполнимо) или Unfeasible (невыполнимо).
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var nameLookup = SystemAPI.GetComponentLookup<NetworkNode>(true);
            var inputLookup = SystemAPI.GetBufferLookup<InputInventorySlot>(true);
            var wipLookup = SystemAPI.GetBufferLookup<WorkshopWIPBufferElement>(true);


            foreach (var (queue, workshopEntity) in SystemAPI.Query<DynamicBuffer<WorkshopProductionQueueItem>>()
                .WithAll<WorkshopTag>()
                .WithAny<RequestRecalculatePlan, WorkshopChainChangedTag>() 
                .WithEntityAccess())

            {
                ecb.RemoveComponent<ProductionPlanIsFeasibleTag>(workshopEntity);
                ecb.RemoveComponent<ProductionPlanIsUnfeasibleTag>(workshopEntity);

                bool canProduce = false;
                if (!queue.IsEmpty)
                {
                    var availableResources = new NativeHashMap<int, int>(128, Allocator.Temp);
                    if (inputLookup.HasBuffer(workshopEntity))
                    {
                        foreach (var item in inputLookup[workshopEntity])
                            if (item.ItemID != 0) availableResources.Increment(item.ItemID, item.Amount);
                    }
                    if (wipLookup.HasBuffer(workshopEntity))
                    {
                        foreach (var item in wipLookup[workshopEntity])
                            if (item.ItemID != 0) availableResources.Increment(item.ItemID, item.Amount);
                    }

                    var currentTask = queue[0];
                    int amountToProduce = currentTask.AmountToProduce == -1 ? 1 : currentTask.AmountToProduce;

                    if (amountToProduce > 0)
                    {
                        canProduce = CanPotentiallyProduce(
                            currentTask.FinalRecipeID,
                            amountToProduce,
                            ref registry,
                            ref availableResources
                        );
                    }
                    availableResources.Dispose();
                }

                if (canProduce)
                {
                    ecb.AddComponent(workshopEntity, new ProductionPlanIsFeasibleTag());
                }
                else
                {
                    ecb.AddComponent(workshopEntity, new ProductionPlanIsUnfeasibleTag());
                }

                ecb.RemoveComponent<RequestRecalculatePlan>(workshopEntity);
                ecb.RemoveComponent<WorkshopChainChangedTag>(workshopEntity); 
            }
        }

        /// <summary>
        /// Рекурсивная функция для проверки возможности производства предмета.
        /// </summary>
        private bool CanPotentiallyProduce(int recipeIdToProduce, int amountNeeded, ref ProductionRecipeRegistryBlob registry, ref NativeHashMap<int, int> availableResources)
        {
            int recipeIndex = FindRecipeIndex(ref registry, recipeIdToProduce);
            if (recipeIndex == -1) { return false; }
            ref var recipe = ref registry.Recipes[recipeIndex];

            var requiredInputs = new NativeHashMap<int, int>(recipe.Inputs.Length, Allocator.Temp);
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                requiredInputs.Increment(recipe.Inputs[i].ItemID, recipe.Inputs[i].Amount * amountNeeded);
            }

            foreach (var kvp in requiredInputs)
            {
                int inputItemId = kvp.Key;
                int totalInputNeeded = kvp.Value;
                availableResources.TryGetValue(inputItemId, out int amountOnHand);
                if (amountOnHand >= totalInputNeeded)
                {
                    availableResources[inputItemId] = amountOnHand - totalInputNeeded;
                    continue;
                }
                if (amountOnHand > 0) availableResources[inputItemId] = 0;

                int amountToCraft = totalInputNeeded - amountOnHand;
                if (!CanPotentiallyProduce(inputItemId, amountToCraft, ref registry, ref availableResources))
                {
                    requiredInputs.Dispose();
                    return false;
                }
            }
            requiredInputs.Dispose();
            return true;
        }

        /// <summary>
        /// Вспомогательный метод для поиска индекса рецепта по его ID.
        /// </summary>
        private int FindRecipeIndex(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++)
            {
                if (registry.Recipes[i].RecipeID == recipeID) return i;
            }
            return -1;
        }
    }
}