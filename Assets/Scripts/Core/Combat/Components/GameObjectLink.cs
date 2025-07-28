using Unity.Entities;
using UnityEngine;

/// <summary>
/// Компонент-объект (class), хранящий прямую ссылку на GameObject,
/// связанный с этой ECS-сущностью. Используется для уничтожения GameObject при уничтожении сущности.
/// </summary>
public class GameObjectLink : IComponentData
{
    public GameObject Value;
}