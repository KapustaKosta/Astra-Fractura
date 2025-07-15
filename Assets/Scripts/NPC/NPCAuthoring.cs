using Unity.Entities;
using UnityEngine;
using Unity.Collections;

public class NPCAuthoring : MonoBehaviour
{
    [Header("Базовая информация о NPC")]
    public string npcName;

    /// <summary>
    /// Возраст NPC.
    /// </summary>
    public int age;

    /// <summary>
    /// Навыки NPC (строковое представление).
    /// </summary>
    public string skills;

    /// <summary>
    /// Уровень организованности NPC.
    /// </summary>
    public int organizedness;

    /// <summary>
    /// Уровень лояльности NPC.
    /// </summary>
    public int loyalty;

    /// <summary>
    /// Уровень трудолюбия NPC.
    /// </summary>
    public int diligence;
    
    public class Baker : Baker<NPCAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания" данных из MonoBehaviour в ECS-сущности.
        /// Создает и добавляет компонент NPCComponent к сущности NPC.
        /// </summary>
        /// <param name="authoring">Экземпляр NPCAuthoring.</param>
        public override void Bake(NPCAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new NPCComponent
            {
                Name = new FixedString64Bytes(authoring.npcName ?? string.Empty),
                Age = authoring.age,
                Skills = new FixedString128Bytes(authoring.skills ?? string.Empty),
                Organizedness = authoring.organizedness,
                Loyalty = authoring.loyalty,
                Diligence = authoring.diligence,
                Target = Entity.Null
            });

            AddComponent<NPCBrain>(entity);
            
        }
    }
}