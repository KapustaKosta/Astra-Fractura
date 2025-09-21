using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Energy.Core; // Убедимся, что пространство имен подключено

namespace Energy.Core.Authoring
{
    public class GeneratorAuthoring : MonoBehaviour
    {
        public string friendlyName = "Generator";
        [Min(0)] public float ratedKW = 5f;
        public bool isOnline = true;
        [Tooltip("Необязательно: приоритет/уровень для диспетчеризации")] public int level = 1;

        class Baker : Baker<GeneratorAuthoring>
        {
            public override void Bake(GeneratorAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.None);
                var name = new FixedString64Bytes(a.friendlyName);

                AddComponent(e, new GeneratorComponent
                {
                    Name = name,
                    RatedKW = Mathf.Max(0f, a.ratedKW),
                    CurrentKW = 0f,
                    IsOnline = a.isOnline,
                    Level = a.level,
                    NetworkId = 0 // Оставляем для совместимости, если другие системы его используют
                });

                // Любой энергетический объект является узлом сети
                AddComponent(e, new NetworkNode { Name = name, SubnetId = 0 });
            }
        }
    }
}