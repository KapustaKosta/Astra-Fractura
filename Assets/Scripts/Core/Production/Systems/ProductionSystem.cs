using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Energy.Core;
using Energy.Core.Systems;

namespace Game.Production
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnergyDispatchSystem))]
    public partial struct ProductionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState s)
        {
            s.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var reg = SystemAPI.GetSingleton<ProductionRecipeRegistryData>();
            if (!reg.Blob.IsCreated) return;
            ref var recipes = ref reg.Blob.Value.Recipes;

            var workForceLookup = SystemAPI.GetComponentLookup<NPCWorkForce>(true);
            s.Dependency.Complete();

            foreach (var aspect in SystemAPI.Query<ProductionBuildingAspect>().WithAll<ProductionBuildingTag>())
            {
                ref var st = ref aspect.State;
                ref var load = ref aspect.Load;
                ref readonly var usage = ref aspect.Usage;
                var queue = aspect.Queue;

                if (!st.IsOn || queue.IsEmpty)
                {
                    Idle(ref st, ref load);
                    continue;
                }

                var currentTask = queue[0];
                int recipeIndex = ResolveRecipeIndex(ref recipes, currentTask.RecipeID);
                if (recipeIndex == -1)
                {
                    queue.RemoveAt(0);
                    continue;
                }
                ref var recipe = ref recipes[recipeIndex];
                st.ActiveRecipeIndex = recipeIndex;

                if (st.AppliedHammerCost < recipe.HammerCost)
                {
                    st.Status = ProductionStatus.AwaitingManualLabor;
                    load.CurrentKW = 0f;
                    continue;
                }

                bool hasInputs = HasInputs(aspect.InputInventory, ref recipe);
                bool hasSpace = HasSpaceInOutput(aspect.OutputInventory, recipe.OutputItemID);
                if (!hasInputs || !hasSpace)
                {
                    st.Status = ProductionStatus.PausedNoResources;
                    load.CurrentKW = 0f;
                    continue;
                }

                load.CurrentKW = recipe.RequiredKW;
                bool powered = usage.InUsedKW >= load.CurrentKW - 0.0001f;
                if (!powered)
                {
                    st.Status = ProductionStatus.PausedNoPower;
                    continue;
                }

                st.Status = ProductionStatus.Working;
                if (st.RemainingTime <= 0f) st.RemainingTime = recipe.BaseTime;
                st.RemainingTime -= dt;

                if (st.RemainingTime <= 0f)
                {
                    if (ConsumeInputs(aspect.InputInventory, ref recipe))
                    {
                        AddToOutputInventory(aspect.OutputInventory, recipe.OutputItemID, recipe.OutputAmount);
                    }
                    st.RemainingTime = 0f;
                    st.AppliedHammerCost = 0f;

                    currentTask.AmountToProduce--;
                    if (currentTask.AmountToProduce <= 0)
                    {
                        queue.RemoveAt(0);
                    }
                    else
                    {
                        queue[0] = currentTask;
                    }
                }
            }
        }

        static void Idle(ref ProductionBuildingState st, ref ConsumerLoad load)
        {
            st.ActiveRecipeIndex = -1;
            st.RemainingTime = 0f;
            st.AppliedHammerCost = 0f;
            st.Status = ProductionStatus.Idle;
            load.CurrentKW = 0f;
        }

        static int ResolveRecipeIndex(ref BlobArray<ProductionRecipe> recipes, int recipeID)
        {
            for (int i = 0; i < recipes.Length; i++)
                if (recipes[i].RecipeID == recipeID) return i;
            return -1;
        }

        static bool HasInputs(in DynamicBuffer<InputInventorySlot> buf, ref ProductionRecipe r)
        {
            for (int i = 0; i < r.Inputs.Length; i++)
            {
                var input = r.Inputs[i];
                int have = 0;
                foreach (var slot in buf) if (slot.ItemID == input.ItemID) have += slot.Amount;
                if (have < input.Amount) return false;
            }
            return true;
        }

        static bool ConsumeInputs(DynamicBuffer<InputInventorySlot> buf, ref ProductionRecipe r)
        {
            // Эта функция предполагает, что проверка HasInputs уже пройдена
            for (int i = 0; i < r.Inputs.Length; i++)
            {
                var itemID = r.Inputs[i].ItemID;
                var amountToConsume = r.Inputs[i].Amount;
                for (int j = buf.Length - 1; j >= 0 && amountToConsume > 0; j--)
                {
                    if (buf[j].ItemID == itemID)
                    {
                        var element = buf[j];
                        int take = math.min(amountToConsume, element.Amount);
                        element.Amount -= take;
                        amountToConsume -= take;
                        buf[j] = element.Amount <= 0 ? default : element;
                    }
                }
            }
            return true;
        }

        static bool HasSpaceInOutput(in DynamicBuffer<OutputInventorySlot> buf, int itemID)
        {
            foreach (var slot in buf) if (slot.ItemID == 0 || slot.ItemID == itemID) return true;
            return false;
        }

        static void AddToOutputInventory(DynamicBuffer<OutputInventorySlot> buf, int itemID, int amount)
        {

            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].ItemID == itemID)
                {
                    var element = buf[i];
                    element.Amount += amount;
                    buf[i] = element;
                    return;
                }
            }
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].ItemID == 0)
                {
                    buf[i] = new OutputInventorySlot { ItemID = itemID, Amount = amount };
                    return;
                }
            }
        }
    }
}