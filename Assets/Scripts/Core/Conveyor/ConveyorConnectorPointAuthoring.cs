using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Conveyor.Authoring
{
    public sealed class ConveyorConnectorPointAuthoring : MonoBehaviour
    {
        [Header("Connector")]
        public ConveyorConnectorType Type = ConveyorConnectorType.Bidirectional;

        [Header("Ownership")]
        [Tooltip("ОБЯЗАТЕЛЬНО: Перетащите сюда корневой GameObject здания, которому принадлежит этот коннектор.")]
        public BuildingNameAuthoring ownerAuthoring; 
    }

    public class ConveyorConnectorPointBaker : Baker<ConveyorConnectorPointAuthoring>
    {
        public override void Bake(ConveyorConnectorPointAuthoring authoring)
        {

            Entity ownerEntity;

            if (authoring.ownerAuthoring != null)
            {
                // 1. Получаем сущность из явно указанного объекта-владельца.
                ownerEntity = GetEntity(authoring.ownerAuthoring.gameObject, TransformUsageFlags.Dynamic);
            }
            else
            {
                // 2. Аварийный вариант, если ссылка не установлена.
                //    Оставляем предупреждение, чтобы помочь найти проблему в редакторе.
                Debug.LogError($"[ConveyorConnectorPointBaker] На коннекторе '{authoring.name}' " +
                               $"не установлена ссылка 'Owner Authoring' в инспекторе! " +
                               $"Владелец будет определен неверно. Перетащите объект с BuildingNameAuthoring в это поле.", authoring.gameObject);

                // Используем сам объект коннектора как временного владельца, чтобы избежать падений.
                ownerEntity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
            }


            var connectorEntity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);

            AddComponent(connectorEntity, new ConveyorConnector
            {
                Type = authoring.Type,
                Owner = ownerEntity, // Теперь здесь гарантированно правильная сущность
                LocalPosition = authoring.transform.localPosition
            });

            AddComponent<LocalToWorld>(connectorEntity);
            AddComponent(connectorEntity, new Parent { Value = ownerEntity });
            AddComponent(connectorEntity, new Unity.Rendering.URPMaterialPropertyBaseColor { Value = new float4(1, 1, 1, 1) });
        }
    }
}