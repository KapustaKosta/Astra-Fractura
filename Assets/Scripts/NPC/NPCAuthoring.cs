using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для определения NPC в ECS.
/// Позволяет настраивать начальные параметры NPC и добавляет все необходимые компоненты AI и PF.
/// </summary>
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

    [Header("Боевые параметры")]
    [Tooltip("Максимальное здоровье NPC.")]
    public float maxHealth = 100f;

    [Header("Рабочие параметры")]
    [Tooltip("Общий запас 'рабочей силы' (молотков), доступный NPC на один полный производственный цикл.")]
    public float hammerPoolCapacity = 35f;

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
            

            // Компонент с данными NPC из системы AI
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

            // Добавляем новый компонент с запасом рабочей силы
            AddComponent(entity, new NPCWorkForce
            {
                MaxHammerPool = authoring.hammerPoolCapacity,
                CurrentHammerPool = authoring.hammerPoolCapacity // Начинает с полным запасом
            });

            AddComponent(entity, new HealthComponent
            {
                MaxHealth = authoring.maxHealth,
                CurrentHealth = authoring.maxHealth
            });
            
            /// Компонент, хранящий прямую ссылку на GameObject
            AddComponentObject(entity, new GameObjectLink
            {
                Value = authoring.gameObject
            });

            // Компонент "мозга" из системы AI
            AddComponent<NPCBrain>(entity);
            
            // Компоненты, необходимые для системы Pathfinding (PF)
            AddComponent<NPCPathfindingComponent>(entity);
            AddBuffer<NPCPathBufferElement>(entity);
        }
    }
}