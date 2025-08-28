using Unity.Entities;
using Energy.Core;
using Game.Production;
using Unity.Mathematics;
using Unity.Collections;

namespace Game.Workshop
{
    // Аспект цеха (все поля readonly по требованиям генератора)
    public readonly partial struct WorkshopAspect : IAspect
    {
        public readonly Entity Self;
        public readonly RefRW<WorkshopState> State;
        public readonly RefRW<ConsumerLoad> Load;
        public readonly DynamicBuffer<StationSlot> Slots;
        public readonly DynamicBuffer<WorkshopProductionQueueItem> Queue;

        // Буферы инвентаря: поле остаётся readonly, для записи берём локальную копию
        private readonly DynamicBuffer<InputInventorySlot> _input;
        private readonly DynamicBuffer<OutputInventorySlot> _output;
        private readonly DynamicBuffer<WorkshopWIPBufferElement> _wip;



        public bool TryTakeFromInput(int itemID, int amount)
        {
            var input = _input;
            int available = 0;
            for (int i = 0; i < input.Length; i++)
                if (input[i].ItemID == itemID) available += input[i].Amount;

            if (available < amount) return false;

            int need = amount;
            for (int i = input.Length - 1; i >= 0 && need > 0; i--)
            {
                if (input[i].ItemID != itemID) continue;
                var slot = input[i];
                int take = math.min(need, slot.Amount);
                slot.Amount -= take;
                need -= take;
                input[i] = slot.Amount <= 0 ? default : slot;
            }
            return true;
        }

        public bool TryTakeFromWIP(int itemID, int amount)
        {
            var wip = _wip;
            int available = 0;
            for (int i = 0; i < wip.Length; i++)
                if (wip[i].ItemID == itemID) available += wip[i].Amount;

            if (available < amount) return false;

            int need = amount;
            for (int i = wip.Length - 1; i >= 0 && need > 0; i--)
            {
                if (wip[i].ItemID != itemID) continue;
                var slot = wip[i];
                int take = math.min(need, slot.Amount);
                slot.Amount -= take;
                need -= take;
                wip[i] = slot.Amount <= 0 ? default : slot;
            }
            return true;
        }

        public bool TryAddToOutput(int itemID, ref int amount)
        {
            var output = _output;
            const int MaxStack = 1000;
            int orig = amount;

            for (int i = 0; i < output.Length && amount > 0; i++)
            {
                if (output[i].ItemID != itemID) continue;
                var slot = output[i];
                int space = MaxStack - slot.Amount;
                if (space <= 0) continue;
                int add = math.min(space, amount);
                slot.Amount += add;
                amount -= add;
                output[i] = slot;
            }

            for (int i = 0; i < output.Length && amount > 0; i++)
            {
                if (output[i].ItemID != 0) continue;
                var slot = output[i];
                int add = math.min(MaxStack, amount);
                slot.ItemID = itemID;
                slot.Amount = add;
                amount -= add;
                output[i] = slot;
            }

            return amount < orig;
        }


        public bool TryAddToWIP(int itemID, ref int amount)
        {
            var wip = _wip;
            const int MaxStack = 1000; // Или другое значение, если нужно
            int orig = amount;

            // Сначала добавляем в существующие стаки
            for (int i = 0; i < wip.Length && amount > 0; i++)
            {
                if (wip[i].ItemID != itemID) continue;
                var slot = wip[i];
                int space = MaxStack - slot.Amount;
                if (space <= 0) continue;
                int add = math.min(space, amount);
                slot.Amount += add;
                amount -= add;
                wip[i] = slot;
            }

            // Затем добавляем в пустые слоты
            for (int i = 0; i < wip.Length && amount > 0; i++)
            {
                if (wip[i].ItemID != 0) continue;
                var slot = wip[i];
                int add = math.min(MaxStack, amount);
                slot.ItemID = itemID;
                slot.Amount = add;
                amount -= add;
                wip[i] = slot;
            }

            return amount < orig;
        }
    }

        // Аспект станции
        public readonly partial struct StationAspect : IAspect
    {
        public readonly Entity Self;
        public readonly RefRO<StationConfig> Config;
        public readonly RefRW<StationState> State;
        public readonly DynamicBuffer<StationOutputBufferElement> OutputBuffer;

        public int GetRecipeIndex(ref BlobArray<ProductionRecipe> recipes)
        {
            for (int i = 0; i < recipes.Length; i++)
                if (recipes[i].RecipeID == State.ValueRO.SelectedRecipeID) return i;
            return -1;
        }

        public bool HasSpaceInOutput(int maxAmount)
        {
            var buf = OutputBuffer;
            int total = 0;
            for (int i = 0; i < buf.Length; i++) total += buf[i].Amount;
            return total < maxAmount;
        }
    }

    public static class WorkshopAspectExtensions
    {
        public static bool HasInInput(this WorkshopAspect aspect, int itemID, int amount, ref SystemState state)
        {
            if (!state.EntityManager.HasBuffer<InputInventorySlot>(aspect.Self)) return false;
            var buffer = state.EntityManager.GetBuffer<InputInventorySlot>(aspect.Self);
            int count = 0;
            for (int i = 0; i < buffer.Length; i++)
                if (buffer[i].ItemID == itemID) count += buffer[i].Amount;
            return count >= amount;
        }
    }
}