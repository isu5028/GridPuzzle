using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(WorldMapSO))]
public class WorldMapSOEditor : Editor
{
    private bool showMapPreview = true;
    private const float CELL_SIZE = 50f;
    private const float CELL_SPACING = 5f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        WorldMapSO worldMap = (WorldMapSO)target;

        // マッププレビュー
        showMapPreview = EditorGUILayout.Foldout(showMapPreview, "階層マッププレビュー", true);
        if (showMapPreview)
        {
            DrawMapPreview(worldMap);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "階層マップの構造:\n" +
            "• ハブ (1, 0): ゲーム開始地点\n" +
            "• 3列の階段: 左(0), 中央(1), 右(2)\n" +
            "• 各列6階層: B1F(1) ~ B6F(6)\n\n" +
            "各StageSOの「ワールドマップ位置設定」でMapPositionとStairColumnを設定してください。",
            MessageType.Info
        );
    }

    private void DrawMapPreview(WorldMapSO worldMap)
    {
        EditorGUILayout.BeginVertical("Box");

        var allStages = worldMap.GetAllStages();

        // ステージをマップ位置でグループ化
        Dictionary<Vector2Int, StageSO> stageMap = new Dictionary<Vector2Int, StageSO>();
        foreach (var stage in allStages)
        {
            if (stage != null)
            {
                stageMap[stage.MapPosition] = stage;
            }
        }

        // ハブ（中央上部）
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        DrawStageCell(new Vector2Int(1, 0), stageMap, worldMap.RootStage);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // ダンジョン（3列×6行）
        for (int row = 1; row <= 6; row++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int col = 0; col < 3; col++)
            {
                Vector2Int pos = new Vector2Int(col, row);
                DrawStageCell(pos, stageMap, null);

                if (col < 2) GUILayout.Space(CELL_SPACING);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawStageCell(Vector2Int pos, Dictionary<Vector2Int, StageSO> stageMap, StageSO hubOverride)
    {
        StageSO stage = hubOverride;
        if (stage == null)
        {
            stageMap.TryGetValue(pos, out stage);
        }

        // セルの色を決定
        Color cellColor;
        string label;

        if (pos.y == 0)
        {
            cellColor = new Color(0.3f, 0.8f, 0.3f); // ハブは緑
            label = "HUB";
        }
        else if (stage != null)
        {
            cellColor = new Color(0.5f, 0.7f, 0.9f); // 設定済みは青
            label = $"B{pos.y}F";
        }
        else
        {
            cellColor = new Color(0.5f, 0.5f, 0.5f); // 未設定はグレー
            label = $"({pos.x},{pos.y})";
        }

        // ボタンとして描画（クリックでステージを選択）
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = cellColor;

        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 10;
        style.fontStyle = FontStyle.Bold;

        string displayName = stage != null ? $"{label}\n{stage.name}" : label;

        if (GUILayout.Button(displayName, style, GUILayout.Width(CELL_SIZE), GUILayout.Height(CELL_SIZE)))
        {
            if (stage != null)
            {
                Selection.activeObject = stage;
                EditorGUIUtility.PingObject(stage);
            }
        }

        GUI.backgroundColor = oldBg;
    }
}
