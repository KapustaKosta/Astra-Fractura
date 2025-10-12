using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

/// <summary>
/// Authoring-компонент для привязки пользовательского цвета из MonoBehaviour к ECS-компоненту.
/// Привязываем к _BaseColor, т.к. URP Lit использует именно это имя свойства.
/// </summary>
public class MyOwnColorAuthoring : MonoBehaviour
{
    [Tooltip("Цвет (RGBA) для сущности")]
    public Color color = Color.white;

    class Baker : Baker<MyOwnColorAuthoring>
    {
        public override void Bake(MyOwnColorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MyOwnColor
            {
                Value = new float4(authoring.color.r, authoring.color.g, authoring.color.b, authoring.color.a)
            });
        }
    }
}

/// <summary>
/// Компонент ECS, хранящий пользовательский цвет.
/// </summary>
[MaterialProperty("_BaseColor")]
public struct MyOwnColor : IComponentData
{
    public float4 Value;
}