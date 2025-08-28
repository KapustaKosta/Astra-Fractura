using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Energy.Core; // Убедимся, что пространство имен подключено

namespace Energy.Core.Authoring
{
    public class ConsumerAuthoring : MonoBehaviour
    {
        public string friendlyName = "Consumer";
        [Min(0)] public float consumptionKW = 1f;


        class Baker : Baker<ConsumerAuthoring>
        {
            public override void Bake(ConsumerAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.None);
                var name = new FixedString64Bytes(a.friendlyName);

                AddComponent(e, new ConsumerLoad
                {
                    CurrentKW = Mathf.Max(0f, a.consumptionKW),
                    NetworkId = 0 // Оставляем для совместимости
                });

                // Любой энергетический объект является узлом сети
                // ИЗМЕНЕНИЕ: Используем SubnetId и присваиваем начальное значение 0.
                AddComponent(e, new NetworkNode { Name = name, SubnetId = 0 });
            }
        }
    }
}