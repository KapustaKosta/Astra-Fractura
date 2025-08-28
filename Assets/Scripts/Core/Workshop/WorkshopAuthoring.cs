using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Energy.Core;
using Game.Production;
using static Unity.Mathematics.math;

namespace Game.Workshop
{
    public class WorkshopAuthoring : MonoBehaviour
    {
        [Header("General")]
        public string friendlyName = "Workshop";
        public bool startTurnedOff = false;
        [Range(1, 16)] public int level = 1;

        [Header("Stations")]
        [Range(1, 16)] public int slotCount = 3;

        [Header("Workshop Inventories")]
        [Tooltip("Слоты входного инвентаря ЦЕХА.")] public int inputCapacity = 16;
        [Tooltip("Слоты буферного инвентаря ЦЕХА (WIP).")] public int wipCapacity = 16;
        [Tooltip("Слоты выходного инвентаря ЦЕХА.")] public int outputCapacity = 16;

        class Baker : Baker<WorkshopAuthoring>
        {
            public override void Bake(WorkshopAuthoring a)
            {
                var ws = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<WorkshopTag>(ws);
                AddComponent(ws, new WorkshopConfig
                {
                    Level = (byte)clamp(a.level, 1, 255),
                    SlotCount = (byte)clamp(a.slotCount, 1, 255),
                    InputCapacity = (short)clamp(a.inputCapacity, 0, 32767),
                    OutputCapacity = (short)clamp(a.outputCapacity, 0, 32767),
                    WipCapacity = (short)clamp(a.wipCapacity, 0, 32767)
                });
                AddComponent(ws, new WorkshopState { IsOn = !a.startTurnedOff, RequiredWorkers = 0 });

                AddComponent(ws, new ConsumerLoad { CurrentKW = 0f });
                AddComponent(ws, new NetLinkUsage());
                AddComponent(ws, new NetworkNode { Name = new FixedString64Bytes(a.friendlyName), SubnetId = 0 });

                if (a.inputCapacity > 0)
                {
                    AddComponent<HasInputInventory>(ws);
                    var inBuf = AddBuffer<InputInventorySlot>(ws);
                    inBuf.ResizeUninitialized(a.inputCapacity);
                    for (int i = 0; i < a.inputCapacity; i++) inBuf[i] = default;
                }
                if (a.outputCapacity > 0)
                {
                    AddComponent<HasOutputInventory>(ws);
                    var outBuf = AddBuffer<OutputInventorySlot>(ws);
                    outBuf.ResizeUninitialized(a.outputCapacity);
                    for (int i = 0; i < a.outputCapacity; i++) outBuf[i] = default;
                }

                if (a.wipCapacity > 0)
                {
                    var wipBuf = AddBuffer<WorkshopWIPBufferElement>(ws);
                    wipBuf.ResizeUninitialized(a.wipCapacity);
                    for (int i = 0; i < a.wipCapacity; i++) wipBuf[i] = default;
                }
                else
                {
                    AddBuffer<WorkshopWIPBufferElement>(ws);
                }

                AddBuffer<AssignedWorker>(ws);
                AddBuffer<WorkshopProductionQueueItem>(ws);

                var slots = AddBuffer<StationSlot>(ws);
                slots.ResizeUninitialized(a.slotCount);

                for (int i = 0; i < a.slotCount; i++)
                {
                    var st = CreateAdditionalEntity(TransformUsageFlags.None);
                    AddComponent<StationTag>(st);
                    AddComponent(st, new StationOwner { Workshop = ws, SlotIndex = i });
                    AddComponent(st, new StationConfig { StationTypeID = -1 });
                    AddComponent(st, new StationState
                    {
                        Status = StationStatus.Empty,
                        SelectedRecipeID = -1,
                        Enabled = 0
                    });
                    AddBuffer<StationOutputBufferElement>(st);

                    slots[i] = new StationSlot { Station = st, Order = (byte)i };
                }
            }
        }
    }
}