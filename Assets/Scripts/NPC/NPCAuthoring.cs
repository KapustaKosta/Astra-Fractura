using Unity.Entities;
using UnityEngine;

class NPCAuthoring : MonoBehaviour
{
    class Baker : Baker<NPCAuthoring>
    {
        public override void Bake(NPCAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new npc { age = 25 });
        }
    }
}

public struct npc : IComponentData
{
    public float age;
}
