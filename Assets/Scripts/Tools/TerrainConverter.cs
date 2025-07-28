using UnityEngine;

public class TerrainConverter : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    void Start()
    {
        // Можно вызвать ExtractAndSaveMesh() вручную из редактора
    }

    /// <summary>
    /// Извлекает меш из Terrain и возвращает его. Если savePath указан, сохраняет в файл.
    /// </summary>
    public Mesh ExtractMesh(string savePath = null)
    {
        Terrain terrain = GetComponent<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("Terrain component not found!");
            return null;
        }

        TerrainData terrainData = terrain.terrainData;
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, width, height);

        // Корректный расчет масштаба по X и Z
        float scaleX = terrainData.size.x / (width - 1);
        float scaleY = terrainData.size.y;
        float scaleZ = terrainData.size.z / (height - 1);

        Vector3[] vertices = new Vector3[width * height];
        Vector2[] uvs = new Vector2[width * height];
        int[] triangles = new int[(width - 1) * (height - 1) * 6];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                float h = heights[y, x];
                Vector3 v = new Vector3(x * scaleX, h * scaleY, y * scaleZ);
                // Проверка на NaN/Infinity
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                    float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                {
                    Debug.LogError($"Invalid vertex at {i}: {v}");
                    v = Vector3.zero;
                }
                vertices[i] = v;
                uvs[i] = new Vector2((float)x / (width - 1), (float)y / (height - 1));
            }
        }

        int t = 0;
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int i = y * width + x;
                // Первый треугольник
                triangles[t++] = i;
                triangles[t++] = i + width;
                triangles[t++] = i + width + 1;
                // Второй треугольник
                triangles[t++] = i;
                triangles[t++] = i + width + 1;
                triangles[t++] = i + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // На случай больших мешей
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Проверка на "дырки": если треугольников меньше, чем ожидается, вывести предупреждение
        if (t != triangles.Length)
        {
            Debug.LogWarning($"Triangle array not fully filled: {t} of {triangles.Length}");
        }

        if (!string.IsNullOrEmpty(savePath))
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.CreateAsset(mesh, savePath);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"Mesh saved to {savePath}");
#else
            Debug.LogWarning("Saving mesh to file is only supported in the Unity Editor.");
#endif
        }

        return mesh;
    }

    /// <summary>
    /// Публичный метод для вызова из редактора. Сохраняет меш в Assets/ExtractedMeshes/TerrainMesh.asset
    /// </summary>
    public void ExtractAndSaveMesh()
    {
#if UNITY_EDITOR
        string folder = "Assets/ExtractedMeshes";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "ExtractedMeshes");
        }
        string path = folder + "/TerrainMesh.asset";
        ExtractMesh(path);
#else
        Debug.LogWarning("ExtractAndSaveMesh доступен только в редакторе Unity.");
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
