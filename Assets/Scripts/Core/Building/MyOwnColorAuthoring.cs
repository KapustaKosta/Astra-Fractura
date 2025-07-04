using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

// Baker для преобразования MonoBehaviour в ECS компонент
public class MyOwnColorAuthoring : MonoBehaviour
{
    [Tooltip("Цвет материала (RGBA)")]
    public Color color = Color.white;

    class Baker : Baker<MyOwnColorAuthoring>
    {
        public override void Bake(MyOwnColorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем компонент с цветом
            AddComponent(entity, new MyOwnColor
            {
                Value = new float4(
                    authoring.color.r,
                    authoring.color.g,
                    authoring.color.b,
                    authoring.color.a)
            });
        }
    }
}

// Компонент для хранения цвета
[MaterialProperty("_Color")]
public struct MyOwnColor : IComponentData
{
    public float4 Value;
}