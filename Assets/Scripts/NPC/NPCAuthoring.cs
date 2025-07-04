using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для определения NPC в ECS.
/// Позволяет настраивать начальные параметры NPC, такие как имя, возраст,
/// навыки, организованность, лояльность и трудолюбие, в редакторе Unity.
/// </summary>
class NPCAuthoring : MonoBehaviour
{
    /// <summary>
    /// Имя NPC.
    /// </summary>
    [Header("NPC Settings")]
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

    /// <summary>
    /// Baker-класс для преобразования NPCAuthoring в ECS-компоненты.
    /// </summary>
    class Baker : Baker<NPCAuthoring>
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
                Name = authoring.npcName ?? string.Empty,
                Age = authoring.age,
                Skills = authoring.skills ?? string.Empty,
                Organizedness = authoring.organizedness,
                Loyalty = authoring.loyalty,
                Diligence = authoring.diligence,
                Target = Entity.Null
            });
        }
    }
}

/// <summary>
/// ECS-компонент, хранящий данные о NPC.
/// </summary>
public struct NPCComponent : IComponentData
{
    /// <summary>
    /// Имя NPC. Использует FixedString для Burst-совместимости.
    /// </summary>
    public FixedString64Bytes Name;

    /// <summary>
    /// Возраст NPC.
    /// </summary>
    public int Age;

    /// <summary>
    /// Навыки NPC. Использует FixedString для Burst-совместимости.
    /// </summary>
    public FixedString64Bytes Skills;

    /// <summary>
    /// Уровень организованности NPC.
    /// </summary>
    public int Organizedness;

    /// <summary>
    /// Уровень лояльности NPC.
    /// </summary>
    public int Loyalty;

    /// <summary>
    /// Уровень трудолюбия NPC.
    /// </summary>
    public int Diligence;

    /// <summary>
    /// Целевая сущность для NPC (например, ресурсный узел или поселение).
    /// </summary>
    public Entity Target;
}