using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainConverter))]
public class TerrainConverterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TerrainConverter converter = (TerrainConverter)target;
        if (GUILayout.Button("Extract and Save Terrain Mesh"))
        {
            converter.ExtractAndSaveMesh();
        }
    }
}
