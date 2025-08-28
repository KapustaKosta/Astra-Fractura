// Assets/Scripts/Tools/TerrainConverterEditor.cs
using UnityEditor;
using UnityEngine;
using System.IO;

[CustomEditor(typeof(TerrainConverter))]
public class TerrainConverterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // рисует все поля компонента, включая LockBordersToNeighbors
        // см. UnityEditor.Editor.DrawDefaultInspector docs. 
        // https://docs.unity3d.com/ScriptReference/Editor.DrawDefaultInspector.html

        var converter = (TerrainConverter)target;

        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

            if (GUILayout.Button("Extract & Save Mesh (this Terrain)"))
            {
                converter.ExtractAndSaveMesh();
            }

            if (GUILayout.Button("Batch: Extract & Save for ALL Terrains in Open Scenes"))
            {
                var terrains = GameObject.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
                int ok = 0;

                foreach (var t in terrains)
                {
                    if (t == null || t.terrainData == null) continue;

                    string safe = SanitizeName(t.name);
                    string fileName =
                        $"Terrain_{safe}_LOD{converter.SampleStride}_R{converter.SmoothRadius}P{converter.SmoothPasses}" +
                        (converter.PreserveOuterBorder ? "_SEAM" : "") +
                        (converter.LockBordersToNeighbors ? "_LOCK" : "") +
                        ".asset";

                    string path = $"{converter.SaveFolder}/{fileName}";

                    // ВАЖНО: добавили аргумент lockBordersToNeighbors перед path
                    TerrainConverter.ExtractMeshFromTerrain(
                        t,
                        converter.SampleStride,
                        converter.SmoothRadius,
                        converter.SmoothPasses,
                        converter.PreserveOuterBorder,
                        converter.LockBordersToNeighbors,
                        path
                    );
                    ok++;
                }

                Debug.Log($"[TerrainConverter] Exported {ok} terrain mesh asset(s).");
            }
        }

        EditorGUILayout.HelpBox(
            "Качество:\n" +
            "• SampleStride снижает плотность сетки (LOD).\n" +
            "• SmoothRadius / SmoothPasses мягко сглаживают высоты (внутри).\n" +
            "• PreserveOuterBorder — кромка без сглаживания.\n" +
            "• LockBordersToNeighbors — кромки жёстко копируются из соседей.",
            MessageType.Info);
    }

    private static string SanitizeName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
