using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Buildings
{
    /// <summary>
    /// Маркер «это карьер» + настройки взаимодействия.
    /// </summary>
    public struct QuarryTag : IComponentData {}

    /// <summary>
    /// Настройки карьера.
    /// </summary>
    public struct QuarrySettings : IComponentData
    {
        public float InteractionRange; // радиус поиска ближайшего узла
    }

    /// <summary>
    /// Текущее состояние работающего карьера (заполняется при финализации постройки).
    /// </summary>
    public struct QuarryState : IComponentData
    {
        public Entity TargetResourceNode; 
    }

    /// <summary>
    /// Авторинг карьера. ВНИМАНИЕ: не добавляем никаких "Placement/Preview" тэгов здесь,
    /// чтобы они не попадали на финальные здания!
    /// </summary>
    public class QuarryAuthoring : MonoBehaviour
    {
        [Min(0.1f)]
        public float interactionRange = 5f;

        class Baker : Baker<QuarryAuthoring>
        {
            public override void Bake(QuarryAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<QuarryTag>(e);
                AddComponent(e, new QuarrySettings
                {
                    InteractionRange = math.max(0.1f, authoring.interactionRange)
                });
                // ВАЖНО:
                // НЕ добавляем тэгов превью (вроде QuarryPlacementTag) на финальные сущности!
            }
        }
    }
}