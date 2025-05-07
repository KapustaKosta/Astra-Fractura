using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

// Baker ��� �������������� MonoBehaviour � ECS ���������
public class MyOwnColorAuthoring : MonoBehaviour
{
    [Tooltip("���� ��������� (RGBA)")]
    public Color color = Color.white;

    class Baker : Baker<MyOwnColorAuthoring>
    {
        public override void Bake(MyOwnColorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // ��������� ��������� � ������
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

// ��������� ��� �������� �����
[MaterialProperty("_Color")]
public struct MyOwnColor : IComponentData
{
    public float4 Value;
}