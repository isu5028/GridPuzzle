using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldMapSO))]
public class WorldMapSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Use Root Stage to define the entry point.\nUse Stair Connections in each StageSO to define the map structure.", MessageType.Info);
    }
}
