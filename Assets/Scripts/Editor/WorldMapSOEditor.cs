using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(WorldMapSO))]
public class WorldMapSOEditor : Editor
{
    private WorldMapSO worldMap;
    private const float CellWidth = 100f;
    private const float CellHeight = 40f;
    private const float Padding = 5f;

    private void OnEnable()
    {
        worldMap = (WorldMapSO)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("World Map Visualization (3D Layout)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag and drop StageSO assets onto the grid cells to assign them.\nThis will automatically set Map Position, Floor Index, Stair Column, and link adjacent stages.", MessageType.Info);

        // Draw 3 Layers
        DrawLayer("Back Layer (奥) [Cols 0-2]", 0);
        DrawLayer("Middle Layer (中) [Cols 3-5]", 3);
        DrawLayer("Front Layer (手前) [Cols 6-8]", 6);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Force Refresh Connections"))
        {
            AutoLinkAllStages(worldMap);
        }
    }

    private void DrawLayer(string title, int xOffset)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        int rows = 7; // 0 (Hub) to 6 (B5F)
        int cols = 3;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.alignment = TextAnchor.MiddleCenter;
        boxStyle.wordWrap = true;

        for (int y = 0; y < rows; y++)
        {
            EditorGUILayout.BeginHorizontal();

            // Format Row Label
            string rowLabel = (y == 0) ? "Hub (0)" : $"B{y}F ({y})";
            EditorGUILayout.LabelField(rowLabel, GUILayout.Width(60));

            for (int x = 0; x < cols; x++)
            {
                int mapX = x + xOffset;
                int mapY = y;
                Vector2Int pos = new Vector2Int(mapX, mapY);

                StageSO stage = worldMap.GetStageByMapPosition(pos);

                // Determine display name and color
                string displayName = "Empty";
                Color startColor = GUI.backgroundColor;

                if (stage != null)
                {
                    displayName = stage.name;
                    if (y == 0) GUI.backgroundColor = Color.cyan; // Hub
                    else GUI.backgroundColor = Color.green; // Normal
                }
                else
                {
                     GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                }

                GUILayout.Box(displayName, boxStyle, GUILayout.Width(CellWidth), GUILayout.Height(CellHeight));
                Rect cellRect = GUILayoutUtility.GetLastRect();

                // Handle Input (Click / Drag&Drop)
                HandleInput(cellRect, pos, stage);

                GUI.backgroundColor = startColor; // Reset
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void HandleInput(Rect rect, Vector2Int pos, StageSO currentStage)
    {
        Event evt = Event.current;

        // Click to Select
        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            if (currentStage != null)
            {
                Selection.activeObject = currentStage;
                EditorGUIUtility.PingObject(currentStage);
                evt.Use();
            }
        }

        // Drag & Drop
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (!rect.Contains(evt.mousePosition)) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is StageSO droppedStage)
                {
                    AssignStageToPosition(droppedStage, pos, worldMap);
                }
            }
            evt.Use();
        }
    }

    private void AssignStageToPosition(StageSO stage, Vector2Int pos, WorldMapSO map)
    {
        Undo.RecordObject(stage, "Assign Stage Position");

        // Update StageSO fields using SerializedObject
        SerializedObject so = new SerializedObject(stage);
        so.FindProperty("mapPosition").vector2IntValue = pos;
        // Removed floorIndex/stairColumn writes as they are now derived properties
        so.ApplyModifiedProperties();

        // Ensure stage is in WorldMapSO list
        List<StageSO> allStages = map.GetAllStages();
        if (!allStages.Contains(stage))
        {
            Undo.RecordObject(map, "Add Stage to WorldMap");
            SerializedObject mapSO = new SerializedObject(map);
            SerializedProperty stagesProp = mapSO.FindProperty("allStages");

            bool alreadyExists = false;
            for(int i=0; i<stagesProp.arraySize; i++)
            {
                if(stagesProp.GetArrayElementAtIndex(i).objectReferenceValue == stage)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if(!alreadyExists)
            {
                stagesProp.InsertArrayElementAtIndex(stagesProp.arraySize);
                stagesProp.GetArrayElementAtIndex(stagesProp.arraySize - 1).objectReferenceValue = stage;
                mapSO.ApplyModifiedProperties();
            }
        }

        // Auto Link
        AutoLinkAllStages(map);
    }

    private void AutoLinkAllStages(WorldMapSO map)
    {
        var stages = map.GetAllStages();
        // Build dictionary map
        var grid = new Dictionary<Vector2Int, StageSO>();
        foreach (var s in stages)
        {
            if (s != null) grid[s.MapPosition] = s;
        }

        int updatedCount = 0;

        foreach (var s in stages)
        {
            if (s == null) continue;
            Vector2Int p = s.MapPosition;
            SerializedObject so = new SerializedObject(s);

            // Planar Connections
            // East/West (Within Layer): Right/Left
            // Col % 3 == 0 (Left), 1 (Center), 2 (Right)
            StageSO right = (p.x % 3 != 2) && grid.ContainsKey(p + Vector2Int.right) ? grid[p + Vector2Int.right] : null; // East
            StageSO left = (p.x % 3 != 0) && grid.ContainsKey(p + Vector2Int.left) ? grid[p + Vector2Int.left] : null;  // West

            // North/South (Between Layers): Up/Down
            // Back(0-2) <-> Mid(3-5) <-> Front(6-8)
            // North ("Up" on map) -> Towards Back (Index - 3)
            // South ("Down" on map) -> Towards Front (Index + 3)
            StageSO north = grid.ContainsKey(p + new Vector2Int(-3, 0)) ? grid[p + new Vector2Int(-3, 0)] : null;
            StageSO south = grid.ContainsKey(p + new Vector2Int(3, 0)) ? grid[p + new Vector2Int(3, 0)] : null;

            // Vertical Connections (Stairs/Floors): DefaultUp/DefaultDown
            // Y is Depth (0=Hub, 6=B6F)

            StageSO stairUp = null;

            // Special Logic for B1F -> Hub
            if (p.y == 1)
            {
                // Back Layer (0-2) -> Hub at (1, 0) [Stage0]
                if (p.x >= 0 && p.x <= 2) stairUp = grid.ContainsKey(new Vector2Int(1, 0)) ? grid[new Vector2Int(1, 0)] : null;

                // Mid Layer (3-5) -> Hub at (4, 0) [Stage3]
                else if (p.x >= 3 && p.x <= 5) stairUp = grid.ContainsKey(new Vector2Int(4, 0)) ? grid[new Vector2Int(4, 0)] : null;

                // Front Layer (6-8) -> Hub at (7, 0) [Stage6]
                else if (p.x >= 6 && p.x <= 8) stairUp = grid.ContainsKey(new Vector2Int(7, 0)) ? grid[new Vector2Int(7, 0)] : null;
            }
            else
            {
                // Standard Vertical Link (B2F -> B1F, etc.)
                stairUp = grid.ContainsKey(new Vector2Int(p.x, p.y - 1)) ? grid[new Vector2Int(p.x, p.y - 1)] : null;
            }

            StageSO stairDown = grid.ContainsKey(new Vector2Int(p.x, p.y + 1)) ? grid[new Vector2Int(p.x, p.y + 1)] : null;

            so.FindProperty("rightStage").objectReferenceValue = right; // East
            so.FindProperty("leftStage").objectReferenceValue = left;   // West
            so.FindProperty("upStage").objectReferenceValue = north;    // North
            so.FindProperty("downStage").objectReferenceValue = south;  // South
            so.FindProperty("defaultUpStage").objectReferenceValue = stairUp;       // Stair Up
            so.FindProperty("defaultDownStage").objectReferenceValue = stairDown;   // Stair Down

            if (so.ApplyModifiedProperties())
            {
                updatedCount++;
            }
        }

        Debug.Log($"Auto-linked connected stages. Updated {updatedCount} stages.");
    }
}
