using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Синхронизирует Transform GameObject'а с позицией и вращением его сущности.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class NPCVisualsSyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Проходим по всем сущностям, у которых есть ссылка на GameObject
        Entities
            .ForEach((GameObjectLink link, in LocalToWorld ltw) =>
            {
                if (link != null && link.Value != null)
                {
                    // Напрямую обновляем позицию и вращение GameObject'а
                    link.Value.transform.position = ltw.Position;
                    link.Value.transform.rotation = ltw.Rotation;
                }
            }).WithoutBurst().Run();
    }
}
