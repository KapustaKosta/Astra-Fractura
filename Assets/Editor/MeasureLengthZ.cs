// Assets/Editor/MeasureLengthZ.cs
using UnityEditor;
using UnityEngine;

public static class MeasureLengthZ
{
    [MenuItem("Tools/Conveyor/Print Length Z of Selection")]
    public static void PrintLengthZ()
    {
        var sel = Selection.activeTransform;
        if (!sel)
        {
            Debug.LogWarning("[MeasureZ] No selection.");
            return;
        }

        // 1) Попробуем простой случай: один MeshFilter у выделенного объекта
        var mf = sel.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            var mesh = mf.sharedMesh;
            float localZ = mesh.bounds.size.z;                   // локальная длина по Z меша
            float worldZ = localZ * sel.lossyScale.z;            // с учётом масштабов родителей
            Debug.Log($"[MeasureZ] Single MeshFilter: localZ={localZ:F4}, worldZ≈{worldZ:F4}");
        }

        // 2) Общая длина для всей иерархии по оси root.forward (если есть Renderers)
        var rends = sel.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            if (!mf) Debug.LogWarning("[MeasureZ] No Renderer/MeshFilter found on selection.");
            return;
        }

        // Скомбинированный мировой AABB
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        // Проекция «вдоль» локальной оси +Z рут-объекта
        Vector3 f = sel.forward.normalized;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        // 8 углов AABB
        float minDot = float.PositiveInfinity;
        float maxDot = float.NegativeInfinity;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {   
                    var corner = c + new Vector3(sx * e.x, sy * e.y, sz * e.z);
                    float d = Vector3.Dot(corner, f);
                    if (d < minDot) minDot = d;
                    if (d > maxDot) maxDot = d;
                }
        float lengthAlongRootZ = Mathf.Max(0f, maxDot - minDot);

        Debug.Log($"[MeasureZ] Hierarchy (Renderers): length along root.forward ≈ {lengthAlongRootZ:F4} (world units)");
    }
}
