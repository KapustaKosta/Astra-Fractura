// TerrainToPhysicsShapeAuthoring.cs
// Генерирует тайловые Physics Shape (Mesh) для нескольких Terrain'ов c настраиваемым качеством.
// DOTS 1.3.14 / Unity Physics 1.x (нужен импорт Sample "Custom Physics Authoring").
//
// Важно: это редакторская выпечка. В рантайме меши и коллайдеры не строятся.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_PHYSICS_EXISTS // можно добавить Define в Project Settings, но обычно не нужен
using Unity.Physics;
using Unity.Physics.Authoring;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("DOTS/Terrain → Physics Shape (Multi)")]
public class TerrainToPhysicsShapeAuthoring : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("Список исходных Terrain.")]
    public Terrain[] SourceTerrains = Array.Empty<Terrain>();

    [Header("Quality")]
    [Tooltip("Шаг выборки по heightmap. 1 = полное, 2 = половина, 4 = четверть...")]
    [Min(1)] public int SampleStride = 4;

    [Tooltip("Вершин на сторону в одном чанке (включая общий край): напр. 65/97/129/257")]
    [Range(17, 513)] public int ChunkVertsPerSide = 129;

    [Tooltip("Учитывать Terrain holes (если есть API и ресурс дыр).")]
    public bool RespectHoles = true;

    [Tooltip("Вертикальный оффсет (м) для всей сетки.")]
    public float YOffset = 0f;

    [Header("DOTS Physics filter (bit masks)")]
    [Tooltip("Категории, к которым принадлежит террейн в DOTS Physics (CollisionFilter.BelongsTo). По умолчанию бит 3.")]
    public uint BelongsToMask = (1u << 3);

    [Tooltip("С какими категориями должен сталкиваться (CollisionFilter.CollidesWith). По умолчанию — со всеми.")]
    public uint CollidesWithMask = ~0u;

#if UNITY_PHYSICS_EXISTS
    [Header("Optional: Category Names (for nicer UX)")]
    [Tooltip("Physics Category Names asset (Assets → Create → Unity Physics → Physics Category Names)")]
    public PhysicsCategoryNames CategoryNamesAsset;

    [Tooltip("Имена категорий для BelongsTo (из CategoryNamesAsset). Если заполнено — пересчитает BelongsToMask.")]
    public string[] BelongsToByName;

    [Tooltip("Имена категорий для CollidesWith (из CategoryNamesAsset). Если заполнено — пересчитает CollidesWithMask.")]
    public string[] CollidesWithByName;
#endif

    [Header("Scene/Editor")]
    [Tooltip("Unity Layer для созданных чанков (если используете для рендера/отладки/Raycaster'ов в классике).")]
    public int UnityLayerForChunks = 0;

    [Tooltip("Помечать созданные чанки как Static (без устаревших NavMesh-флагов).")]
    public bool MarkStatic = true;

    [Tooltip("Префикс имени дочерних чанков.")]
    public string ChunkNamePrefix = "TerrainPhysChunk";

    [Serializable]
    private class GeneratedTerrainColliderTag : MonoBehaviour { }

#if UNITY_EDITOR
    [ContextMenu("Find Terrains In Scene")]
    public void FindTerrainsInScene()
    {
        SourceTerrains = GameObject.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Debug.Log($"[TerrainToPhysicsShapeAuthoring] Found {SourceTerrains.Length} terrains.");
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Bake Physics Shapes (rebuild all)")]
    public void BakeNow()
    {
        if (SourceTerrains == null || SourceTerrains.Length == 0)
        {
            Debug.LogError("[TerrainToPhysicsShapeAuthoring] No SourceTerrains. Use 'Find Terrains In Scene' or fill by hand.");
            return;
        }

        if (SampleStride < 1) SampleStride = 1;
        if (ChunkVertsPerSide < 3) ChunkVertsPerSide = 3;

#if UNITY_PHYSICS_EXISTS
        // Если указаны имена и есть Asset — пересчитаем маски
        TryRebuildMasksFromNames();
#endif

        RemoveOldChildren();

        int totalChunks = 0;
        foreach (var terrain in SourceTerrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            totalChunks += GenerateChunksForTerrain(terrain, totalChunks /*просто для индекса/имени*/);
        }

        Debug.Log($"[TerrainToPhysicsShapeAuthoring] Bake complete. Chunks: {totalChunks}.");
    }

    private void RemoveOldChildren()
    {
        var toRemove = new List<GameObject>();
        foreach (Transform child in transform)
            if (child.GetComponent<GeneratedTerrainColliderTag>() != null)
                toRemove.Add(child.gameObject);

        foreach (var go in toRemove)
            Undo.DestroyObjectImmediate(go);
    }

    private int GenerateChunksForTerrain(Terrain terrain, int runningIndex)
    {
        var td = terrain.terrainData;
        var tPos = terrain.transform.position;
        Vector3 size = td.size;

        int hmRes = td.heightmapResolution;
        int samplesPerSide = 1 + (hmRes - 1) / SampleStride;

        int stride = SampleStride;
        int cellsPerSide = samplesPerSide - 1;
        int chunkCells = Mathf.Max(2, ChunkVertsPerSide - 1);

        // Данные
        float[,] heights = td.GetHeights(0, 0, hmRes, hmRes);

        bool[,] holes = null;
        int holesRes = 0;
        if (RespectHoles)
        {
            try
            {
                holesRes = td.holesResolution;
                if (holesRes > 0)
                    holes = td.GetHoles(0, 0, holesRes, holesRes);
            }
            catch { holes = null; holesRes = 0; } 
        }

        int created = 0;

        for (int cellZ0 = 0, chunkZ = 0; cellZ0 < cellsPerSide; cellZ0 += chunkCells, chunkZ++)
        {
            int cellZCount = Mathf.Min(chunkCells, cellsPerSide - cellZ0);
            int vZCount = cellZCount + 1;

            for (int cellX0 = 0, chunkX = 0; cellX0 < cellsPerSide; cellX0 += chunkCells, chunkX++)
            {
                int cellXCount = Mathf.Min(chunkCells, cellsPerSide - cellX0);
                int vXCount = cellXCount + 1;

                var mesh = BuildChunkMesh(
                    td, heights, holes, holesRes,
                    transform, tPos, size, hmRes, stride,
                    cellX0, cellZ0, vXCount, vZCount, YOffset);

                string goName = $"{ChunkNamePrefix}_{runningIndex + created:D4}_{chunkX:D2}_{chunkZ:D2}";
                var go = new GameObject(goName);
                Undo.RegisterCreatedObjectUndo(go, "Create Terrain Physics Chunk");
                go.transform.SetParent(this.transform, false);
                go.layer = UnityLayerForChunks;

                if (MarkStatic)
                {
                    var flags = StaticEditorFlags.BatchingStatic
                              | StaticEditorFlags.OccluderStatic
                              | StaticEditorFlags.OccludeeStatic
                              | StaticEditorFlags.ReflectionProbeStatic
                              | StaticEditorFlags.ContributeGI;
                    GameObjectUtility.SetStaticEditorFlags(go, flags);
                }

                go.AddComponent<GeneratedTerrainColliderTag>();

#if UNITY_PHYSICS_EXISTS
                // ЯВНО создаём статическое тело + шейп (Mesh)
                var body = go.AddComponent<PhysicsBodyAuthoring>();
                body.MotionType = BodyMotionType.Static;

                var shape = go.AddComponent<PhysicsShapeAuthoring>();
                shape.SetMesh(mesh);                 // наш сгенерённый меш
                shape.ForceUnique = true;            // чтобы при желании менять фильтр/материал на конкретном чанке в рантайме
                shape.BelongsTo = BelongsToMask;     // DOTS-фильтр
                shape.CollidesWith = CollidesWithMask;
#else
                // Фоллбэк на случай отсутствия sample'ов: создадим MeshCollider (статический).
                // (Но для проектной логики DOTS-слоёв просили именно PhysicsShape — импортируйте sample!)
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
#endif
                created++;
            }
        }

        return created;
    }

    private static Mesh BuildChunkMesh(
        TerrainData td,
        float[,] heights,
        bool[,] holes, int holesRes,
        Transform holder,
        Vector3 terrainWorldPos,
        Vector3 terrainSize,
        int hmRes,
        int stride,
        int cellX0, int cellZ0,
        int vXCount, int vZCount,
        float yOffset)
    {
        float stepX_M = terrainSize.x * stride / (hmRes - 1);
        float stepZ_M = terrainSize.z * stride / (hmRes - 1);

        int vXStart = cellX0;
        int vZStart = cellZ0;

        var verts = new Vector3[vXCount * vZCount];

        for (int iz = 0; iz < vZCount; iz++)
        {
            int hz = (vZStart + iz) * stride;
            float zLocal = (vZStart + iz) * stepZ_M;
            for (int ix = 0; ix < vXCount; ix++)
            {
                int hx = (vXStart + ix) * stride;
                float xLocal = (vXStart + ix) * stepX_M;

                float h01 = heights[hz, hx]; // 0..1
                float yWorld = h01 * terrainSize.y + terrainWorldPos.y + yOffset;

                float xWorld = terrainWorldPos.x + xLocal;
                float zWorld = terrainWorldPos.z + zLocal;

                verts[iz * vXCount + ix] = holder.InverseTransformPoint(new Vector3(xWorld, yWorld, zWorld));
            }
        }

        var tris = new List<int>(vXCount * vZCount * 6);
        for (int cz = 0; cz < vZCount - 1; cz++)
        {
            for (int cx = 0; cx < vXCount - 1; cx++)
            {
                if (holes != null && holesRes > 0)
                {
                    float normX = ((vXStart + cx) + 0.5f) * stride / (hmRes - 1);
                    float normZ = ((vZStart + cz) + 0.5f) * stride / (hmRes - 1);
                    int hx = Mathf.Clamp(Mathf.FloorToInt(normX * holesRes), 0, holesRes - 1);
                    int hz = Mathf.Clamp(Mathf.FloorToInt(normZ * holesRes), 0, holesRes - 1);
                    if (!holes[hz, hx]) continue; // false = отверстие
                }

                int i00 = cz * vXCount + cx;
                int i10 = cz * vXCount + (cx + 1);
                int i01 = (cz + 1) * vXCount + cx;
                int i11 = (cz + 1) * vXCount + (cx + 1);

                tris.Add(i00); tris.Add(i11); tris.Add(i10);
                tris.Add(i00); tris.Add(i01); tris.Add(i11);
            }
        }

        var mesh = new Mesh();
        if (verts.Length > 65000)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

#if UNITY_PHYSICS_EXISTS
    private void TryRebuildMasksFromNames()
    {
        if (CategoryNamesAsset == null) return;

        if (BelongsToByName != null && BelongsToByName.Length > 0)
            BelongsToMask = BuildMaskFromNames(CategoryNamesAsset, BelongsToByName);

        if (CollidesWithByName != null && CollidesWithByName.Length > 0)
            CollidesWithMask = BuildMaskFromNames(CategoryNamesAsset, CollidesWithByName);
    }

    private static uint BuildMaskFromNames(PhysicsCategoryNames names, string[] wanted)
    {
        if (names == null || wanted == null) return 0u;
        uint mask = 0u;
        var list = names.CategoryNames;
        for (int w = 0; w < wanted.Length; w++)
        {
            string s = wanted[w];
            if (string.IsNullOrEmpty(s)) continue;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], s, StringComparison.OrdinalIgnoreCase))
                {
                    mask |= 1u << i;
                    break;
                }
            }
        }
        return mask;
    }
#endif

#endif // UNITY_EDITOR
}

#if UNITY_EDITOR
[CustomEditor(typeof(TerrainToPhysicsShapeAuthoring))]
public class TerrainToPhysicsShapeAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!(target as TerrainToPhysicsShapeAuthoring).isActiveAndEnabled))
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Find Terrains In Scene"))
                (target as TerrainToPhysicsShapeAuthoring).FindTerrainsInScene();
            if (GUILayout.Button("Bake Physics Shapes"))
                (target as TerrainToPhysicsShapeAuthoring).BakeNow();
            GUILayout.EndHorizontal();
        }

        EditorGUILayout.HelpBox(
            "Печёт статические Physics Shape (Mesh) чанки для всех указанных Terrain’ов.\n" +
            "DOTS-фильтр задаётся через BelongsTo/CollidesWith (битовые маски) либо именами из Physics Category Names.",
            MessageType.Info);
    }
}
#endif
