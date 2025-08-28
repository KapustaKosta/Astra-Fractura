using Energy.Core;
using Game.Workshop; 
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Game.Production
{
    public class ProductionBuildingAuthoring : MonoBehaviour
    {
        [Header("General")]
        public string friendlyName = "Production Building";

        [Tooltip("Укажите тип станции, которому соответствует это здание. Рецепты будут подбираться по этому типу.")]
        public StationType stationType;

        [Header("Initial State")]
        public bool startTurnedOff = true;

        [Header("Storage Settings")]
        public int inputCapacity = 10;
        public int outputCapacity = 10;

        class Baker : Baker<ProductionBuildingAuthoring>
        {
            public override void Bake(ProductionBuildingAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);

                // Проверка, что тип станции указан
                if (a.stationType == null)
                {
                    Debug.LogError($"На здании '{a.friendlyName}' не указан StationType!", a);
                    return;
                }

                AddComponent<ProductionBuildingTag>(e);
                // охраняем StationTypeID в конфигурации
                AddComponent(e, new ProductionConfig { StationTypeID = a.stationType.StationTypeID });

                AddComponent(e, new ProductionBuildingState
                {
                    IsOn = !a.startTurnedOff,
                    SelectedRecipeID = -1, 
                    ActiveRecipeIndex = -1,
                    RemainingTime = 0,
                    Status = ProductionStatus.Idle,
                    AppliedHammerCost = 0f,
                    AssignedWorker = Entity.Null
                });

                AddComponent(e, new ConsumerLoad { CurrentKW = 0f, NetworkId = 0 });
                AddComponent(e, new NetLinkUsage());
                AddComponent(e, new NetworkNode { Name = new FixedString64Bytes(a.friendlyName), SubnetId = 0 });

                if (a.inputCapacity > 0)
                {
                    AddComponent<HasInputInventory>(e);
                    var inBuffer = AddBuffer<InputInventorySlot>(e);
                    inBuffer.ResizeUninitialized(a.inputCapacity);
                }

                if (a.outputCapacity > 0)
                {
                    AddComponent<HasOutputInventory>(e);
                    var outBuffer = AddBuffer<OutputInventorySlot>(e);
                    outBuffer.ResizeUninitialized(a.outputCapacity);
                }

                AddBuffer<ProductionQueueItem>(e);
            }
        }
    }
}