using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Находит "чистые" сущности NPC, у которых еще нет визуального представления,
/// и создает для них GameObject из префаба.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class NPCVisualsInstantiationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var visualPrefab = Resources.Load<GameObject>("Prefabs/NPC_Visual_Prefab");
        if (visualPrefab == null)
        {
            Debug.LogError("Не удалось загрузить NPC_Visual_Prefab! Убедитесь, что он лежит в Assets/Resources/Prefabs/");
            Enabled = false;
            return;
        }

        // Используем WithStructuralChanges для добавления управляемого компонента (класса)
        Entities
            .WithAll<NPCComponent>()
            .WithNone<GameObjectLink>() // Ищем только тех, у кого еще нет ссылки
            .WithStructuralChanges()
            .ForEach((Entity e, in LocalToWorld ltw) =>
            {
                // Создаем обычный GameObject
                var newVisualGO = Object.Instantiate(visualPrefab, ltw.Position, ltw.Rotation);
                newVisualGO.name = $"NPC_Visual_{e.Index}";

                // Инициализируем его анимационный контроллер
                var animController = newVisualGO.GetComponent<NPCAnimationController>();
                if (animController != null)
                {
                    animController.Init(e, World);
                }

                // Добавляем на СУЩНОСТЬ компонент-ссылку на созданный GameObject.
                // Это и есть "метка", что мы обработали эту сущность.
                EntityManager.AddComponentObject(e, new GameObjectLink { Value = newVisualGO });

            }).WithoutBurst().Run();

        // Отключаем систему после первого успешного выполнения, чтобы избежать повторной работы.
        Enabled = false;
    }
}