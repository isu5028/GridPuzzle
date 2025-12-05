using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldMapSO))]
public class WorldMapSOEditor : Editor
{
    private WorldMapSO map;
    private SerializedProperty rowsProp;

    private void OnEnable()
    {
        map = (WorldMapSO)target;
        rowsProp = serializedObject.FindProperty("rows");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("World Map Grid", EditorStyles.boldLabel);

        if (GUILayout.Button("Resize Grid"))
        {
            // Simple popup to get new size
            ResizeWindow.Show(map);
        }

        EditorGUILayout.Space();

        if (rowsProp != null && rowsProp.isArray)
        {
            int rowCount = rowsProp.arraySize;
            // Draw rows in serialized order (top array index 0 draws first),
            // but label them so logical Y=0 is the bottom row and increments upward.
            for (int arrayY = 0; arrayY < rowCount; arrayY++)
            {
                int logicalY = (rowCount - 1) - arrayY;
                var row = rowsProp.GetArrayElementAtIndex(arrayY);
                var cols = row.FindPropertyRelative("columns");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Row {logicalY}", GUILayout.Width(60));
                if (cols != null && cols.isArray)
                {
                    for (int x = 0; x < cols.arraySize; x++)
                    {
                        var cell = cols.GetArrayElementAtIndex(x);
                        EditorGUILayout.PropertyField(cell, GUIContent.none, GUILayout.Width(120));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply Adjacency To Stages"))
        {
            ApplyAdjacency();
        }

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void ApplyAdjacency()
    {
        Undo.RegisterCompleteObjectUndo(map, "Apply WorldMap adjacency");

        // iterate grid and set neighbor references
        var rowsField = serializedObject.FindProperty("rows");
        if (rowsField == null) return;

        int rowCount = rowsField.arraySize;
        // Iterate logical Y coordinates (0 = bottom) and map to serialized array indices
        for (int logicalY = 0; logicalY < rowCount; logicalY++)
        {
            int arrayY = (rowCount - 1) - logicalY;
            var row = rowsField.GetArrayElementAtIndex(arrayY);
            var cols = row.FindPropertyRelative("columns");
            if (cols == null) continue;
            int colCount = cols.arraySize;
            for (int x = 0; x < colCount; x++)
            {
                var cell = cols.GetArrayElementAtIndex(x);
                StageSO stage = cell.objectReferenceValue as StageSO;
                if (stage == null) continue;

                StageSO up = GetStageAtSerialized(rowsField, x, logicalY + 1);
                StageSO down = GetStageAtSerialized(rowsField, x, logicalY - 1);
                StageSO left = GetStageAtSerialized(rowsField, x - 1, logicalY);
                StageSO right = GetStageAtSerialized(rowsField, x + 1, logicalY);

                stage.SetAdjacentStages(up, down, left, right);
                EditorUtility.SetDirty(stage);
            }
        }

        EditorUtility.SetDirty(map);
        Debug.Log("Applied adjacency to stages from WorldMapSO");
    }

    private StageSO GetStageAtSerialized(SerializedProperty rowsField, int x, int y)
    {
        if (rowsField == null) return null;
        int rowCount = rowsField.arraySize;
        if (rowCount == 0) return null;
        // Convert logical y (0 = bottom) to serialized array index (0 = top)
        int arrayY = (rowCount - 1) - y;
        if (arrayY < 0 || arrayY >= rowCount) return null;
        var row = rowsField.GetArrayElementAtIndex(arrayY);
        var cols = row.FindPropertyRelative("columns");
        if (cols == null) return null;
        if (x < 0 || x >= cols.arraySize) return null;
        var cell = cols.GetArrayElementAtIndex(x);
        return cell.objectReferenceValue as StageSO;
    }

    // small helper window for resizing
    private class ResizeWindow : EditorWindow
    {
        private static WorldMapSO targetMap;
        private int width = 4;
        private int height = 3;

        public static void Show(WorldMapSO map)
        {
            targetMap = map;
            var w = GetWindow<ResizeWindow>(true, "Resize World Map", true);
            w.width = 8;
            w.height = 8;
            w.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Set grid size", EditorStyles.boldLabel);
            width = EditorGUILayout.IntField("Columns (width)", Mathf.Max(1, width));
            height = EditorGUILayout.IntField("Rows (height)", Mathf.Max(1, height));

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply"))
            {
                ApplyResize();
                Close();
            }
        }

        private void ApplyResize()
        {
            if (targetMap == null) return;
            Undo.RegisterCompleteObjectUndo(targetMap, "Resize WorldMap");

            // rebuild rows array
            var so = new SerializedObject(targetMap);
            var rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = height;
            for (int y = 0; y < height; y++)
            {
                var row = rowsProp.GetArrayElementAtIndex(y);
                var cols = row.FindPropertyRelative("columns");
                cols.arraySize = width;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetMap);
        }
    }
}
