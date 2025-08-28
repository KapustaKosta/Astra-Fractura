// Assets/Editor/MeasureHeightY.cs
using UnityEditor;
using UnityEngine;

public static class MeasureHeightY
{
    [MenuItem("Tools/Conveyor/Print Height Y of Selection")]
    public static void PrintHeightY()
    {
        var sel = Selection.activeTransform;
        if (!sel)
        {
            Debug.LogWarning("[MeasureY] No selection.");
            return;
        }

        // 1) Попробуем простой случай: один MeshFilter у выделенного объекта
        var mf = sel.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            var mesh = mf.sharedMesh;
            float localY = mesh.bounds.size.y;                   // локальная высота по Y меша
            float worldY = localY * sel.lossyScale.y;            // с учётом масштабов родителей
            Debug.Log($"<color=lime>[MeasureY] Single MeshFilter:</color> localY={localY:F4}, worldY≈{worldY:F4}");
        }

        // 2) Общая высота для всей иерархии по оси root.up (если есть Renderers)
        var rends = sel.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            if (!mf) Debug.LogWarning("[MeasureY] No Renderer/MeshFilter found on selection.");
            return;
        }

        // Скомбинированный мировой AABB (Axis-Aligned Bounding Box)
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        // Проекция «вдоль» локальной оси +Y рут-объекта
        Vector3 up = sel.up.normalized; 
        Vector3 c = b.center;
        Vector3 e = b.extents;

        // Находим минимальную и максимальную проекцию 8 углов AABB на ось up
        float minDot = float.PositiveInfinity;
        float maxDot = float.NegativeInfinity;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var corner = c + new Vector3(sx * e.x, sy * e.y, sz * e.z);
                    float d = Vector3.Dot(corner, up); 
                    if (d < minDot) minDot = d;
                    if (d > maxDot) maxDot = d;
                }
        float heightAlongRootY = Mathf.Max(0f, maxDot - minDot);

        Debug.Log($"<color=cyan>[MeasureY] Hierarchy (Renderers):</color> height along root.up ≈ {heightAlongRootY:F4} (world units)");
    }
}