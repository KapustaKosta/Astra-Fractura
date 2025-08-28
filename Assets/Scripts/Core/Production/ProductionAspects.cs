using Unity.Entities;
using Energy.Core;

namespace Game.Production
{
    public readonly partial struct ProductionBuildingAspect : IAspect
    {
        public readonly Entity Self;

        // Состояние и конфиг
        readonly RefRW<ProductionBuildingState> _state;
        readonly RefRO<ProductionConfig> _config;

        // Энергия
        readonly RefRW<ConsumerLoad> _load;
        readonly RefRO<NetLinkUsage> _usage;

        // Инвентари и очередь
        readonly DynamicBuffer<InputInventorySlot> _inputInventory;
        readonly DynamicBuffer<OutputInventorySlot> _outputInventory;
        readonly DynamicBuffer<ProductionQueueItem> _queue;

        public ref ProductionBuildingState State => ref _state.ValueRW;
        public ref readonly ProductionConfig Config => ref _config.ValueRO;

        public ref ConsumerLoad Load => ref _load.ValueRW;
        public ref readonly NetLinkUsage Usage => ref _usage.ValueRO;

        public DynamicBuffer<InputInventorySlot> InputInventory => _inputInventory;
        public DynamicBuffer<OutputInventorySlot> OutputInventory => _outputInventory;
        public DynamicBuffer<ProductionQueueItem> Queue => _queue;
    }
}