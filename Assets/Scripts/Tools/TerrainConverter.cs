using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class TerrainConverter : MonoBehaviour
{
    [Header("Quality")]
    [Min(1)] public int SampleStride = 4;          // шаг выборки по сетке heightmap
    [Range(0, 8)] public int SmoothRadius = 1;     // радиус бокс-блюра
    [Range(0, 8)] public int SmoothPasses = 1;     // количество проходов сглаживания

    [Header("Seams")]
    [Tooltip("Не трогать полосу по периметру (шириной SmoothRadius).")]
    public bool PreserveOuterBorder = true;

    [Tooltip("Жёстко копировать кромки из соседних террейнов, если они заданы.")]
    public bool LockBordersToNeighbors = true;

    [Header("Save")]
    public string SaveFolder = "Assets/ExtractedMeshes";

    public Mesh ExtractMesh(string savePath = null)
    {
        var t = GetComponent<Terrain>();
        if (!t || !t.terrainData)
        {
            Debug.LogError("[TerrainConverter] Terrain component not found!");
            return null;
        }

        return ExtractMeshFromTerrain(
            t, SampleStride, SmoothRadius, SmoothPasses,
            PreserveOuterBorder, LockBordersToNeighbors, savePath);
    }

    public static Mesh ExtractMeshFromTerrain(
        Terrain terrain,
        int sampleStride,
        int smoothRadius,
        int smoothPasses,
        bool preserveBorder,
        bool lockBordersToNeighbors,
        string savePath = null)
    {
        if (!terrain || !terrain.terrainData)
        {
            Debug.LogError("[TerrainConverter] Terrain is null.");
            return null;
        }

        var td = terrain.terrainData;
        int res = td.heightmapResolution;                // количество сэмплов по стороне
        Vector3 size = td.size;                          // физический размер террейна
        float[,] heights = td.GetHeights(0, 0, res, res);// [z,x] в диапазоне 0..1

        // Сглаживание только внутри, без затрагивания кромки R
        if (smoothRadius > 0 && smoothPasses > 0)
            BoxBlur_InPlace_InteriorOnly(heights, res, smoothRadius, smoothPasses, preserveBorder);

        // Жёстко синхронизируем кромки с соседями (если есть)
        if (lockBordersToNeighbors)
            SyncBordersWithNeighbors(terrain, heights);

        // Сэмплируем только в узлах сетки (без билинейной интерполяции) 
        int stride = Mathf.Max(1, sampleStride);
        int steps = (res - 1) / stride;      // целое число полных шагов
        int outW = steps + 1;                 // последняя вершина = res-1
        int outH = outW;

        var verts = new Vector3[outW * outH];
        var uvs = new Vector2[outW * outH];
        var norms = new Vector3[outW * outH];

        // шаг между сэмплами в метрах
        float sx = size.x / (res - 1);
        float sz = size.z / (res - 1);
        float sy = size.y;

        for (int j = 0; j < outH; j++)
        {
            int zi = (j == outH - 1) ? (res - 1) : (j * stride);
            float z = zi * sz;
            float nz = (float)zi / (res - 1);

            for (int i = 0; i < outW; i++)
            {
                int xi = (i == outW - 1) ? (res - 1) : (i * stride);
                float x = xi * sx;
                float nx = (float)xi / (res - 1);

                float h01 = heights[zi, xi];
                float y = h01 * sy;

                int idx = j * outW + i;
                verts[idx] = new Vector3(x, y, z);
                uvs[idx] = new Vector2(nx, nz);
                norms[idx] = ComputeNormalFromHeights(heights, xi, zi, res, sx, sz, sy);
            }
        }

        // Индексы
        var tris = new int[(outW - 1) * (outH - 1) * 6];
        int tPtr = 0;
        for (int y = 0; y < outH - 1; y++)
        {
            for (int x = 0; x < outW - 1; x++)
            {
                int i00 = y * outW + x;
                int i10 = y * outW + (x + 1);
                int i01 = (y + 1) * outW + x;
                int i11 = (y + 1) * outW + (x + 1);

                // Виндим как в исходнике
                tris[tPtr++] = i00; tris[tPtr++] = i11; tris[tPtr++] = i10;
                tris[tPtr++] = i00; tris[tPtr++] = i01; tris[tPtr++] = i11;
            }
        }

        // Сборка меша
        var mesh = new Mesh();
        if (verts.Length >= 65535) mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0, true);
        mesh.SetUVs(0, new List<Vector2>(uvs));
        mesh.SetNormals(new List<Vector3>(norms));
        mesh.RecalculateBounds();

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(savePath))
        {
            EnsureFolder(Path.GetDirectoryName(savePath));
            AssetDatabase.CreateAsset(mesh, savePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TerrainConverter] Mesh saved to {savePath}");
        }
#endif
        return mesh;
    }


    // Сглаживаем только внутреннюю область [R .. res-1-R], не читая и не записывая кромку
    private static void BoxBlur_InPlace_InteriorOnly(float[,] data, int res, int radius, int passes, bool preserveBorder)
    {
        if (radius <= 0 || passes <= 0) return;

        int R = preserveBorder ? Mathf.Min(radius, res / 2) : 0;
        var tmp = new float[res, res];

        for (int p = 0; p < passes; p++)
        {
            // Горизонталь (внутренняя полоса)
            for (int z = R; z < res - R; z++)
            {
                // копируем кромки строки как есть
                for (int x = 0; x < R; x++) tmp[z, x] = data[z, x];
                for (int x = res - R; x < res; x++) tmp[z, x] = data[z, x];

                for (int x = R; x < res - R; x++)
                {
                    int x0 = Mathf.Max(R, x - radius);
                    int x1 = Mathf.Min(res - 1 - R, x + radius);
                    float sum = 0f;
                    int cnt = 0;
                    for (int k = x0; k <= x1; k++) { sum += data[z, k]; cnt++; }
                    tmp[z, x] = sum / cnt;
                }
            }

            // Вертикаль (внутренняя полоса)
            for (int x = 0; x < res; x++)
            {
                // копируем верх/низ как есть
                for (int z = 0; z < R; z++) data[z, x] = data[z, x];
                for (int z = res - R; z < res; z++) data[z, x] = data[z, x];

                for (int z = R; z < res - R; z++)
                {
                    int z0 = Mathf.Max(R, z - radius);
                    int z1 = Mathf.Min(res - 1 - R, z + radius);
                    float sum = 0f;
                    int cnt = 0;
                    for (int k = z0; k <= z1; k++) { sum += tmp[k, x]; cnt++; }
                    data[z, x] = sum / cnt;
                }
            }
            // Внешняя кромка уже не тронута вообще
        }
    }

    // Центральные разности по высотам -> нормаль
    private static Vector3 ComputeNormalFromHeights(float[,] h, int xi, int zi, int res, float sx, float sz, float sy)
    {
        int xl = Mathf.Max(0, xi - 1);
        int xr = Mathf.Min(res - 1, xi + 1);
        int zd = Mathf.Max(0, zi - 1);
        int zu = Mathf.Min(res - 1, zi + 1);

        float dHx = (h[zi, xr] - h[zi, xl]) * sy / (2f * sx);
        float dHz = (h[zu, xi] - h[zd, xi]) * sy / (2f * sz);

        var n = new Vector3(-dHx, 1f, -dHz);
        return n.normalized;
    }

    // Выравниваем кромки по соседям (копируем 1 столбец/строку)
    private static void SyncBordersWithNeighbors(Terrain t, float[,] h)
    {
        var td = t.terrainData;
        int res = td.heightmapResolution;

        // ЛЕВЫЙ сосед: его правый столбец -> наш левый столбец (x=0)
        if (t.leftNeighbor && t.leftNeighbor.terrainData && t.leftNeighbor.terrainData.heightmapResolution == res)
        {
            var nh = t.leftNeighbor.terrainData.GetHeights(res - 1, 0, 1, res); // [z,0]
            for (int z = 0; z < res; z++) h[z, 0] = nh[z, 0];
        }

        // ПРАВЫЙ сосед: его левый столбец -> наш правый столбец (x=res-1)
        if (t.rightNeighbor && t.rightNeighbor.terrainData && t.rightNeighbor.terrainData.heightmapResolution == res)
        {
            var nh = t.rightNeighbor.terrainData.GetHeights(0, 0, 1, res);
            for (int z = 0; z < res; z++) h[z, res - 1] = nh[z, 0];
        }

        // ВЕРХНИЙ сосед: его нижняя строка -> наша верхняя строка (z=0)
        if (t.topNeighbor && t.topNeighbor.terrainData && t.topNeighbor.terrainData.heightmapResolution == res)
        {
            var nh = t.topNeighbor.terrainData.GetHeights(0, res - 1, res, 1); // [0..res-1,0]
            for (int x = 0; x < res; x++) h[0, x] = nh[0, x];
        }

        // НИЖНИЙ сосед: его верхняя строка -> наша нижняя строка (z=res-1)
        if (t.bottomNeighbor && t.bottomNeighbor.terrainData && t.bottomNeighbor.terrainData.heightmapResolution == res)
        {
            var nh = t.bottomNeighbor.terrainData.GetHeights(0, 0, res, 1);
            for (int x = 0; x < res; x++) h[res - 1, x] = nh[0, x];
        }
    }

#if UNITY_EDITOR
    public void ExtractAndSaveMesh()
    {
        var t = GetComponent<Terrain>();
        if (!t || !t.terrainData)
        {
            Debug.LogError("[TerrainConverter] Terrain component not found!");
            return;
        }

        string safeName = SanitizeName(t.name);
        string fileName = $"Terrain_{safeName}_LOD{SampleStride}_R{SmoothRadius}P{SmoothPasses}" +
                          $"{(PreserveOuterBorder ? "_SEAM" : "")}{(LockBordersToNeighbors ? "_LOCK" : "")}.asset";
        EnsureFolder(SaveFolder);
        string path = $"{SaveFolder}/{fileName}";

        ExtractMeshFromTerrain(t, SampleStride, SmoothRadius, SmoothPasses, PreserveOuterBorder, LockBordersToNeighbors, path);
    }

    private static string SanitizeName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
#endif
}
