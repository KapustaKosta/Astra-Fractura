using Unity.Entities;
using UnityEngine;
using Unity.Collections;

class NPCAuthoring : MonoBehaviour
{
    [Header("NPC Settings")]
    public string npcName; // Имя NPC
    public int age; // Возраст NPC
    public string skills; // Навыки NPC
    public int organizedness; // Уровень организованности
    public int loyalty; // Уровень преданности
    public int diligence; // Уровень трудолюбия
    class Baker : Baker<NPCAuthoring>
    {
        public override void Bake(NPCAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new NPCComponent
            {
                Name = authoring.npcName ?? string.Empty,
                Age = authoring.age,
                Skills = authoring.skills ?? string.Empty,
                Organizedness = authoring.organizedness,
                Loyalty = authoring.loyalty,
                Diligence = authoring.diligence,
                Target = Entity.Null // Изначально NPC не имеет цели
            });
        }
    }
}

public struct NPCComponent : IComponentData
{
    public FixedString64Bytes Name; // Имя NPC
    public int Age; // Возраст NPC
    public FixedString64Bytes Skills; // Навыки NPC
    public int Organizedness; // Уровень организованности
    public int Loyalty; // Уровень преданности
    public int Diligence; // Уровень трудолюбия

    public Entity Target;
}
