using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

static class QuarryPreviewDebug
{
    // Включай/выключай детальные логи тут:
    public static bool Enabled = true;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Log(string msg)
    {
        if (Enabled) Debug.Log($"[QuarryPreview] {msg}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Warn(string msg)
    {
        if (Enabled) Debug.LogWarning($"[QuarryPreview] {msg}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Err(string msg)
    {
        if (Enabled) Debug.LogError($"[QuarryPreview] {msg}");
    }
}



[UpdateInGroup(typeof(SimulationSystemGroup))]
public sealed partial class QuarryTargetHighlightSystem : SystemBase
{
    private Entity _lastNode;

    protected override void OnCreate()
    {
        RequireForUpdate<BuildingSettings>();
        RequireForUpdate<BuildingPreviewTag>();
        _lastNode = Entity.Null;
    }

    protected override void OnUpdate()
    {
        var em   = EntityManager;
        var bs   = SystemAPI.GetSingleton<BuildingSettings>();
        var hiID = bs.ResourceHighlightMaterialID;

        // 1) Снять подсветку с прошлого узла, если он был
        if (_lastNode != Entity.Null)
        {
            if (em.Exists(_lastNode))
                RestoreNodeMaterial(_lastNode, em);
            _lastNode = Entity.Null;
        }

        // 2) Найти превью карьера
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity)) return;
        if (!SystemAPI.HasComponent<QuarryPlacementTag>(previewEntity)) return;

        // 3) Проверить, есть ли у него цель для подсветки
        if (!SystemAPI.HasComponent<QuarryPreviewTarget>(previewEntity) || !SystemAPI.IsComponentEnabled<QuarryPreviewTarget>(previewEntity))
        {
            // Цели нет - подсвечивать нечего
            return;
        }

        var node = em.GetComponentData<QuarryPreviewTarget>(previewEntity).TargetNode;
        if (node == Entity.Null || !em.Exists(node))
        {
            // Цель некорректна
            return;
        }
        
        Debug.Log($"<color=blue>[Highlight]</color> Quarry preview has a valid target: Node {node.Index}. Attempting to highlight.");

        // Сохранить исходный материал один раз
        if (!em.HasComponent<ResourceOriginalMaterial>(node))
        {
            if (em.HasComponent<MaterialMeshInfo>(node))
            {
                var mmi = em.GetComponentData<MaterialMeshInfo>(node);
                em.AddComponentData(node, new ResourceOriginalMaterial { Value = mmi.MaterialID });
                Debug.Log($"<color=blue>[Highlight]</color> Stored original material ({mmi.MaterialID}) for node {node.Index}.");
            }
        }

        // Применить материал подсветки
        if (em.HasComponent<MaterialMeshInfo>(node))
        {
             var mmi = em.GetComponentData<MaterialMeshInfo>(node);
             if (mmi.MaterialID != hiID)
             {
                 mmi.MaterialID = hiID;
                 em.SetComponentData(node, mmi);
                 Debug.Log($"<color=blue>[Highlight]</color> Applied highlight material ({hiID}) to node {node.Index}.");
             }
        }

        _lastNode = node;
    }
    
    protected override void OnStopRunning()
    {
        var em = EntityManager;
        if (_lastNode != Entity.Null && em.Exists(_lastNode))
        {
            Debug.Log($"<color=blue>[Highlight]</color> Exiting build mode. Restoring material for last highlighted node {_lastNode.Index}.");
            RestoreNodeMaterial(_lastNode, em);
        }
        _lastNode = Entity.Null;
    }

    private static void RestoreNodeMaterial(Entity node, EntityManager em)
    {
        if (!em.HasComponent<ResourceOriginalMaterial>(node) || !em.HasComponent<MaterialMeshInfo>(node)) return;

        var orig = em.GetComponentData<ResourceOriginalMaterial>(node);
        var mmi  = em.GetComponentData<MaterialMeshInfo>(node);
        
        if (mmi.MaterialID != orig.Value)
        {
            mmi.MaterialID = orig.Value;
            em.SetComponentData(node, mmi);
            Debug.Log($"<color=blue>[Highlight]</color> Restored original material ({orig.Value}) for node {node.Index}.");
        }
        
        em.RemoveComponent<ResourceOriginalMaterial>(node);
    }
}