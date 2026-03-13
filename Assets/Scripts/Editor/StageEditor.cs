using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(StageSO))]
public class StageEditor : Editor
{
    private StageSO stage;
    private bool showGridEditor = true;
    private bool showEnemyList = true;
    private ReorderableList enemyReorderableList;
    private ReorderableList stairConnectionsReorderableList;

    // エディタの状態
    private enum EditMode
    {
        Wall,       // 壁の配置
        Floor,      // 床の配置
        SyncMap,    // SyncMapの配置
        Player,     // プレイヤー位置
        Enemy,      // 敵の配置
        EraseUnit,  // Unit削除
        Tutorial,   // チュートリアル配置
        StairUp,    // 上階段配置
        StairDown,  // 下階段配置
        BrokenStairUp, // 壊れた上階段配置
        Crate,      // Crate配置
        StairLink   // 階段リンク設定（追加）
    }

    private EditMode currentMode = EditMode.Wall;
    private EnemyType currentEnemyType = EnemyType.Slime;
    private int currentMaxHP = 10;
    private Direction currentEnemyDirection = Direction.Up;

    private int currentAmmo = 3; // Default ammo for Skeleton
    private bool currentDropsCoin = false;
    private int currentTutorialIndex = 1;

    // グリッド描画設定
    private const float CELL_SIZE = 30f;
    private const float GRID_PADDING = 10f;
    private Vector2 scrollPosition;

    // 色設定
    private readonly Color wallColor = new Color(0.3f, 0.3f, 0.3f);
    private readonly Color floorColor = new Color(0.9f, 0.9f, 0.9f);
    private readonly Color playerColor = new Color(0.2f, 0.8f, 0.2f);
    private readonly Color enemyColor = new Color(0.8f, 0.2f, 0.2f);
    private readonly Color syncMapColor = new Color(0.0f, 1.0f, 1.0f, 0.5f); // シアン半透明
    private readonly Color tutorialColor = new Color(1f, 0.64f, 0f, 0.5f); // オレンジ半透明
    private readonly Color stairUpColor = new Color(0.3f, 0.5f, 0.9f, 0.7f); // 青（上へ）
    private readonly Color stairDownColor = new Color(0.9f, 0.4f, 0.3f, 0.7f); // 赤（下へ）
    private readonly Color brokenStairUpColor = new Color(0.5f, 0.6f, 0.7f, 0.7f); // グレー青（壊れた上階段）
    private readonly Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    private readonly Color highlightColor = new Color(1f, 0f, 1f, 0.5f); // マゼンタ半透明
    private readonly Color stairLinkColor = new Color(0.5f, 0.0f, 1.0f, 0.7f); // 紫（リンク）
    private readonly Color crateColor = new Color(0.55f, 0.40f, 0.20f, 0.8f); // 茶色

    private void OnEnable()
    {
        stage = (StageSO)target;
        stage.InitializeWallData();
        SetupReorderableList();
        serializedObject.Update();
    }

    private void SetupReorderableList()
    {
        SetupEnemyReorderableList();
        SetupStairConnectionsReorderableList();
    }

    private void SetupEnemyReorderableList()
    {
        // StageSO のフィールド名に合わせる
        SerializedProperty enemyListProp = serializedObject.FindProperty("enemyStartData");

        enemyReorderableList = new ReorderableList(
            serializedObject,
            enemyListProp,
            draggable: true,
            displayHeader: true,
            displayAddButton: false,
            displayRemoveButton: false
        );

        enemyReorderableList.drawHeaderCallback = (Rect rect) =>
        {
            float xPos = rect.x;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 40, rect.height), "順");
            xPos += 45;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 80, rect.height), "位置");
            xPos += 85;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 80, rect.height), "種類");
            xPos += 85;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 80, rect.height), "方向");
            xPos += 85;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 40, rect.height), "HP");
            xPos += 45;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 40, rect.height), "弾数");
            xPos += 45;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 40, rect.height), "coin");
            xPos += 40;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 50, rect.height), "削除");
        };

        enemyReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            SerializedProperty element = enemyReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            rect.y += 2;
            float height = EditorGUIUtility.singleLineHeight;
            float xPos = rect.x;

            EditorGUI.LabelField(new Rect(xPos, rect.y, 40, height), $"{index + 1}");
            xPos += 45;

            SerializedProperty posProp = element.FindPropertyRelative("Position");
            EditorGUI.PropertyField(new Rect(xPos, rect.y, 80, height), posProp, GUIContent.none);
            xPos += 85;

            SerializedProperty typeProp = element.FindPropertyRelative("EnemyType");
            EditorGUI.PropertyField(new Rect(xPos, rect.y, 80, height), typeProp, GUIContent.none);
            xPos += 85;

            SerializedProperty directionProp = element.FindPropertyRelative("Direction");
            EditorGUI.PropertyField(new Rect(xPos, rect.y, 80, height), directionProp, GUIContent.none);
            xPos += 85;

            SerializedProperty maxHPProp = element.FindPropertyRelative("MaxHP");
            EditorGUI.PropertyField(new Rect(xPos, rect.y, 40, height), maxHPProp, GUIContent.none);
            xPos += 45;

            // Ammo (Only for Skeleton or Bomb)
            SerializedProperty typeVal = element.FindPropertyRelative("EnemyType");
            int enemyTypeIndex = typeVal.enumValueIndex;
            if (enemyTypeIndex == (int)EnemyType.Skeleton || enemyTypeIndex == (int)EnemyType.Bomb)
            {
                SerializedProperty ammoProp = element.FindPropertyRelative("Ammo");
                EditorGUI.PropertyField(new Rect(xPos, rect.y, 40, height), ammoProp, GUIContent.none);
            }
            else
            {
                EditorGUI.LabelField(new Rect(xPos, rect.y, 40, height), "-");
            }
            xPos += 45;

            // DropsCoin Toggle
            SerializedProperty dropsCoinProp = element.FindPropertyRelative("DropsCoin");
            dropsCoinProp.boolValue = EditorGUI.Toggle(new Rect(xPos + 10, rect.y, 20, height), dropsCoinProp.boolValue);
            xPos += 40;

            if (GUI.Button(new Rect(xPos, rect.y, 50, height), "削除"))
            {
                enemyReorderableList.serializedProperty.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(stage);
            }
        };

        enemyReorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 4;

        // 選択が変更されたときにグリッドを再描画
        enemyReorderableList.onSelectCallback = (list) =>
        {
            Repaint();
        };

        // リストの並び替えが完了したときにActionIndexを更新
        enemyReorderableList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
        {
            serializedObject.ApplyModifiedProperties();
            stage.UpdateEnemyActionIndices();
            serializedObject.Update();
            EditorUtility.SetDirty(stage);
        };
    }

    private void SetupStairConnectionsReorderableList()
    {
        SerializedProperty connectionsProp = serializedObject.FindProperty("stairConnections");

        stairConnectionsReorderableList = new ReorderableList(
            serializedObject,
            connectionsProp,
            draggable: true,
            displayHeader: true,
            displayAddButton: true,
            displayRemoveButton: true
        );

        stairConnectionsReorderableList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "階段接続設定 (座標 -> 遷移先ステージ)");
        };

        stairConnectionsReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            SerializedProperty element = connectionsProp.GetArrayElementAtIndex(index);
            rect.y += 2;

            float xPos = rect.x;
            float height = EditorGUIUtility.singleLineHeight;

            // Position
            float posWidth = 100;
            EditorGUI.LabelField(new Rect(xPos, rect.y, 40, height), "Pos:");
            xPos += 40;
            EditorGUI.PropertyField(new Rect(xPos, rect.y, posWidth, height), element.FindPropertyRelative("Position"), GUIContent.none);
            xPos += posWidth + 10;

            // Target Stage
            EditorGUI.LabelField(new Rect(xPos, rect.y, 60, height), "Target:");
            xPos += 60;
            EditorGUI.PropertyField(new Rect(xPos, rect.y, rect.width - xPos, height), element.FindPropertyRelative("TargetStage"), GUIContent.none);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(10);
        DrawStageInfo();

        EditorGUILayout.Space(10);
        DrawEditModeSelector();

        EditorGUILayout.Space(10);
        DrawGridEditor();

        EditorGUILayout.Space(10);
        DrawEnemyList();

        EditorGUILayout.Space(10);
        DrawUtilityButtons();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(stage);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawStageInfo()
    {
        EditorGUILayout.LabelField("ステージ情報", EditorStyles.boldLabel);

        // Name is now derived from Asset Name
        EditorGUILayout.LabelField("ステージ名", stage.name);

        SerializedProperty widthProp = serializedObject.FindProperty("width");
        SerializedProperty heightProp = serializedObject.FindProperty("height");
        SerializedProperty syncMapOffsetProp = serializedObject.FindProperty("syncMapOffset");

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(widthProp, new GUIContent("幅"));
        EditorGUILayout.PropertyField(heightProp, new GUIContent("高さ"));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(syncMapOffsetProp, new GUIContent("SyncMap Offset"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            stage.InitializeWallData();
        }

        // Display Derived Info
        string floorDisplay = stage.FloorIndex == 0 ? "1F" : $"B{stage.FloorIndex}F";
        EditorGUILayout.LabelField($"グリッドサイズ: {stage.Width} × {stage.Height} | フロア: {floorDisplay}");

        EditorGUILayout.Space(10);

        // --- ワールドマップ位置設定 ---
        EditorGUILayout.LabelField("ワールドマップ位置設定", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("Box");

        SerializedProperty mapPositionProp = serializedObject.FindProperty("mapPosition");

        // Show raw MapPosition but interpreted
        EditorGUILayout.PropertyField(mapPositionProp, new GUIContent("マップ位置 (MapPosition)", "X=Column(0-8), Y=Floor(0-6)"));

        // StairColumn Interpretation (Column 0-8)
        // We allow changing Column via a friendly dropdown that updates MapPosition.X
        string[] columnOptions = {
            "0 - 奥の左 (Back Left)", "1 - 奥の中央 (Back Center)", "2 - 奥の右 (Back Right)",
            "3 - 中の左 (Mid Left)", "4 - 中の中央 (Mid Center)", "5 - 中の右 (Mid Right)",
            "6 - 手前の左 (Front Left)", "7 - 手前の中央 (Front Center)", "8 - 手前の右 (Front Right)"
        };

        int currentX = mapPositionProp.vector2IntValue.x;
        int currentY = mapPositionProp.vector2IntValue.y;

        int newX = EditorGUILayout.Popup(new GUIContent("階段列 (Layer/Column)", "マップ位置のX座標に対応"), Mathf.Clamp(currentX, 0, 8), columnOptions);

        // Floor Selection via IntField that updates MapPosition.Y
        int newY = EditorGUILayout.IntField(new GUIContent("深さ (Floor/Depth)", "マップ位置のY座標に対応 (0=Hub)"), currentY);

        if (newX != currentX || newY != currentY)
        {
            mapPositionProp.vector2IntValue = new Vector2Int(newX, newY);
        }

        // HelpBox
        string layerName = newX <= 2 ? "Back (奥)" : (newX <= 5 ? "Mid (中)" : "Front (手前)");
        EditorGUILayout.HelpBox(
            $"Current Setting:\nLayer: {layerName}\nFloor: {(newY==0?"Hub":$"B{newY}F")}\n\nAdjusting these values updates MapPosition.",
            MessageType.Info
        );

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- 階層マップ設定 ---
        EditorGUILayout.LabelField("階層マップ設定 (上下階接続)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("Box");

        SerializedProperty defaultUpProp = serializedObject.FindProperty("defaultUpStage");
        SerializedProperty defaultDownProp = serializedObject.FindProperty("defaultDownStage");

        EditorGUILayout.PropertyField(defaultUpProp, new GUIContent("上の階 (Default StairUp)", "上り階段のデフォルト移動先"));
        EditorGUILayout.PropertyField(defaultDownProp, new GUIContent("下の階 (Default StairDown)", "下り階段のデフォルト移動先"));

        EditorGUILayout.HelpBox("※通常ステージでは、配置された階段(StairUp/Down)は自動的に上記のデフォルト設定に接続されます。\n" +
                                "明示的な接続先が必要な場合(Hubなど)のみ、以下のリストに追加してください。", MessageType.Info);

        EditorGUILayout.Space(5);

        // 階段接続リストの描画
        EditorGUILayout.LabelField("特殊接続 / HUB設定 (Overrides)", EditorStyles.boldLabel);
        if (stairConnectionsReorderableList != null)
        {
            stairConnectionsReorderableList.DoLayoutList();
        }
        else
        {
            EditorGUILayout.HelpBox("Stair Connections List not initialized", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // --- 平面マップ設定 (Legacy/Optional) ---
        EditorGUILayout.LabelField("平面マップ設定 (隣接ステージ)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("Box");

        SerializedProperty upStageProp = serializedObject.FindProperty("upStage");
        SerializedProperty downStageProp = serializedObject.FindProperty("downStage");
        SerializedProperty rightStageProp = serializedObject.FindProperty("rightStage");
        SerializedProperty leftStageProp = serializedObject.FindProperty("leftStage");

        EditorGUILayout.PropertyField(upStageProp, new GUIContent("North"));
        EditorGUILayout.PropertyField(downStageProp, new GUIContent("South"));
        EditorGUILayout.PropertyField(rightStageProp, new GUIContent("East"));
        EditorGUILayout.PropertyField(leftStageProp, new GUIContent("West"));

        EditorGUILayout.EndVertical();
    }

    private void DrawEditModeSelector()
    {
        EditorGUILayout.LabelField("編集モード", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Toggle(currentMode == EditMode.Wall, "壁配置", "Button"))
            currentMode = EditMode.Wall;

        if (GUILayout.Toggle(currentMode == EditMode.Floor, "床配置", "Button"))
            currentMode = EditMode.Floor;

        if (GUILayout.Toggle(currentMode == EditMode.SyncMap, "SyncMap", "Button"))
            currentMode = EditMode.SyncMap;

        if (GUILayout.Toggle(currentMode == EditMode.Player, "プレイヤー", "Button"))
            currentMode = EditMode.Player;

        if (GUILayout.Toggle(currentMode == EditMode.Enemy, "敵配置", "Button"))
            currentMode = EditMode.Enemy;

        if (GUILayout.Toggle(currentMode == EditMode.EraseUnit, "Unit削除", "Button"))
            currentMode = EditMode.EraseUnit;

        if (GUILayout.Toggle(currentMode == EditMode.Tutorial, "Tutorial", "Button"))
            currentMode = EditMode.Tutorial;

        if (GUILayout.Toggle(currentMode == EditMode.StairUp, "階段↑", "Button"))
            currentMode = EditMode.StairUp;

        if (GUILayout.Toggle(currentMode == EditMode.StairDown, "階段↓", "Button"))
            currentMode = EditMode.StairDown;

        if (GUILayout.Toggle(currentMode == EditMode.BrokenStairUp, "壊階段↑", "Button"))
            currentMode = EditMode.BrokenStairUp;

        if (GUILayout.Toggle(currentMode == EditMode.Crate, "Crate", "Button"))
            currentMode = EditMode.Crate;

        EditorGUILayout.EndHorizontal();

        // プレイヤーの初期方向を選択するためのフィールド
        if (currentMode == EditMode.Player)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("プレイヤーの初期方向:", GUILayout.Width(150));
            stage.SetPlayerDirection((Direction)EditorGUILayout.EnumPopup(stage.PlayerStartDirection));
            EditorGUILayout.EndHorizontal();
        }

        // 敵配置モードの場合、敵の種類を選択
        if (currentMode == EditMode.Enemy)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("敵の種類:", GUILayout.Width(80));
            currentEnemyType = (EnemyType)EditorGUILayout.EnumPopup(currentEnemyType);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("敵の最大HP:", GUILayout.Width(150));
            currentMaxHP = EditorGUILayout.IntField(currentMaxHP);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("敵の初期方向:", GUILayout.Width(150));
            currentEnemyDirection = (Direction)EditorGUILayout.EnumPopup(currentEnemyDirection);
            EditorGUILayout.EndHorizontal();

            if (currentEnemyType == EnemyType.Skeleton || currentEnemyType == EnemyType.Bomb)
            {
                EditorGUILayout.BeginHorizontal();
                string ammoLabel = currentEnemyType == EnemyType.Bomb ? "起爆までのターン:" : "初期弾数(Ammo):";
                EditorGUILayout.LabelField(ammoLabel, GUILayout.Width(150));
                currentAmmo = EditorGUILayout.IntField(currentAmmo);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("コインを落とす:", GUILayout.Width(150));
            currentDropsCoin = EditorGUILayout.Toggle(currentDropsCoin);
            EditorGUILayout.EndHorizontal();
        }

        if (currentMode == EditMode.Tutorial)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Index:", GUILayout.Width(50));
            currentTutorialIndex = EditorGUILayout.IntField(currentTutorialIndex);
            EditorGUILayout.EndHorizontal();
        }

        // 現在の編集モードを表示
        string modeText = "";
        switch (currentMode)
        {
            case EditMode.Wall: modeText = "壁を配置"; break;
            case EditMode.Floor: modeText = "床を配置"; break;
            case EditMode.SyncMap: modeText = "SyncMapマスを配置"; break;
            case EditMode.Player: modeText = "プレイヤーの初期位置を設定"; break;
            case EditMode.Enemy: modeText = $"{currentEnemyType}敵を配置"; break;
            case EditMode.EraseUnit: modeText = "プレイヤーまたは敵をクリックして削除"; break;
            case EditMode.Tutorial: modeText = "チュートリアルセル(Index)を配置"; break;
            case EditMode.StairUp: modeText = "上階段を配置（上の階へ移動）"; break;
            case EditMode.StairDown: modeText = "下階段を配置（下の階へ移動）"; break;
            case EditMode.BrokenStairUp: modeText = "壊れた上階段を配置（通行不能）"; break;
            case EditMode.StairLink: modeText = "階段をクリックして接続設定（StairConnections）を追加・選択"; break;
        }

        EditorGUILayout.HelpBox(modeText, MessageType.Info);
    }

    private void DrawGridEditor()
    {
        showGridEditor = EditorGUILayout.Foldout(showGridEditor, "グリッドエディタ", true);

        if (!showGridEditor) return;

        EditorGUILayout.BeginVertical("Box");

        float gridWidth = stage.Width * CELL_SIZE + GRID_PADDING * 2;
        float gridHeight = stage.Height * CELL_SIZE + GRID_PADDING * 2;

        // スクロール可能な領域
        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.Height(Mathf.Min(gridHeight + 20, 600))
        );

        Rect gridRect = GUILayoutUtility.GetRect(gridWidth, gridHeight);

        // グリッドの描画
        if (Event.current.type == EventType.Repaint)
        {
            DrawGrid(gridRect);
        }

        // マウスクリックの処理
        HandleGridInput(gridRect);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 凡例
        DrawLegend();
    }

    private void DrawGrid(Rect gridRect)
    {
        Rect cellRect = new Rect(0, 0, CELL_SIZE, CELL_SIZE);

        // グリッドセルを描画（下から上に描画してY座標を正しく表示）
        for (int y = stage.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < stage.Width; x++)
            {
                cellRect.x = gridRect.x + GRID_PADDING + x * CELL_SIZE;
                cellRect.y = gridRect.y + GRID_PADDING + (stage.Height - 1 - y) * CELL_SIZE;

                // セルの背景色
                CellType cellType = stage.GetWallAt(x, y);
                Color cellColor = floorColor;
                if (cellType == CellType.Wall) cellColor = wallColor;
                else if (cellType == CellType.SyncMap) cellColor = syncMapColor;
                else if (cellType == CellType.Tutorial) cellColor = tutorialColor;
                else if (cellType == CellType.StairUp) cellColor = stairUpColor;
                else if (cellType == CellType.StairDown) cellColor = stairDownColor;
                else if (cellType == CellType.BrokenStairUp) cellColor = brokenStairUpColor;

                EditorGUI.DrawRect(cellRect, cellColor);

                // 選択中の敵のセルをハイライト
                if (enemyReorderableList != null && stage.EnemyStartData != null && enemyReorderableList.index >= 0 && enemyReorderableList.index < stage.EnemyStartData.Count)
                {
                    var selectedEnemy = stage.EnemyStartData[enemyReorderableList.index];
                    if (selectedEnemy.Position.x == x && selectedEnemy.Position.y == y)
                    {
                        EditorGUI.DrawRect(cellRect, highlightColor);
                    }
                }

                // プレイヤー位置の描画
                if (stage.PlayerStartPosition.x == x && stage.PlayerStartPosition.y == y)
                {
                    Rect playerRect = new Rect(
                        cellRect.x + CELL_SIZE * 0.1f,
                        cellRect.y + CELL_SIZE * 0.1f,
                        CELL_SIZE * 0.8f,
                        CELL_SIZE * 0.8f
                    );
                    EditorGUI.DrawRect(playerRect, playerColor);

                    // Pの文字を描画
                    GUI.Label(playerRect, "P", new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white }
                    });

                    // プレイヤーの向きを描画
                    DrawDirectionArrow(cellRect, stage.PlayerStartDirection, Color.white);
                }

                // 敵の描画
                if (stage.EnemyStartData != null)
                {
                    for (int i = 0; i < stage.EnemyStartData.Count; i++)
                    {
                        var enemy = stage.EnemyStartData[i];
                    if (enemy.Position.x == x && enemy.Position.y == y)
                    {
                        Rect enemyRect = new Rect(
                            cellRect.x + CELL_SIZE * 0.1f,
                            cellRect.y + CELL_SIZE * 0.1f,
                            CELL_SIZE * 0.8f,
                            CELL_SIZE * 0.8f
                        );

                        EditorGUI.DrawRect(enemyRect, enemyColor);

                        // 敵の種類を示す文字
                        string typeName = enemy.EnemyType.ToString();
                        string label = typeName.Length >= 2 ? typeName.Substring(0, 2) : typeName;
                        GUI.Label(enemyRect, label, new GUIStyle(GUI.skin.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = Color.white }
                        });

                        // 行動順を右上に描画
                        Rect orderRect = new Rect(
                            cellRect.x + CELL_SIZE * 0.65f,
                            cellRect.y + CELL_SIZE * 0.05f,
                            CELL_SIZE * 0.3f,
                            CELL_SIZE * 0.3f
                        );
                        GUI.Label(orderRect, (i + 1).ToString(), new GUIStyle(GUI.skin.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontStyle = FontStyle.Bold,
                            fontSize = 10,
                            normal = { textColor = Color.yellow }
                        });

                            // 敵の向きを描画
                            DrawDirectionArrow(cellRect, enemy.Direction, Color.white);
                        }
                    }
                }

                if (stage.CrateStartPositions != null)
                {
                    foreach (var pos in stage.CrateStartPositions)
                    {
                        if (pos.x == x && pos.y == y)
                        {
                            Rect crateRect = new Rect(
                                cellRect.x + CELL_SIZE * 0.1f,
                                cellRect.y + CELL_SIZE * 0.1f,
                                CELL_SIZE * 0.8f,
                                CELL_SIZE * 0.8f
                            );
                            EditorGUI.DrawRect(crateRect, crateColor);
                            GUI.Label(crateRect, "CR", new GUIStyle(GUI.skin.label)
                            {
                                alignment = TextAnchor.MiddleCenter,
                                fontStyle = FontStyle.Bold,
                                normal = { textColor = Color.white }
                            });
                        }
                    }
                }

                // Tutorial Index Drawing
                if (cellType == CellType.Tutorial)
                {
                    int tIndex = stage.GetTutorialIndex(new Vector2Int(x, y));
                    if (tIndex > 0)
                    {
                         GUI.Label(cellRect, tIndex.ToString(), new GUIStyle(GUI.skin.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontStyle = FontStyle.Bold,
                            fontSize = 14,
                            normal = { textColor = Color.white }
                        });
                    }
                }

                // グリッドライン
                Handles.color = gridLineColor;
                Rect lineRect = new Rect(cellRect.x, cellRect.y, CELL_SIZE, CELL_SIZE);
                Handles.DrawSolidRectangleWithOutline(lineRect, Color.clear, gridLineColor);
            }
        }
    }


    private void DrawDirectionArrow(Rect cellRect, Direction direction, Color arrowColor)
    {
        Vector2 center = cellRect.center;
        float arrowSize = CELL_SIZE * 0.3f;

        Vector3[] trianglePoints = new Vector3[3];

        switch (direction)
        {
            case Direction.Up:
                trianglePoints[0] = new Vector3(center.x, cellRect.yMin + CELL_SIZE * 0.15f); // 上の頂点
                trianglePoints[1] = new Vector3(center.x - arrowSize / 2, cellRect.yMin + CELL_SIZE * 0.35f); // 左下
                trianglePoints[2] = new Vector3(center.x + arrowSize / 2, cellRect.yMin + CELL_SIZE * 0.35f); // 右下
                break;

            case Direction.Down:
                trianglePoints[0] = new Vector3(center.x, cellRect.yMax - CELL_SIZE * 0.15f); // 下の頂点
                trianglePoints[1] = new Vector3(center.x - arrowSize / 2, cellRect.yMax - CELL_SIZE * 0.35f); // 左上
                trianglePoints[2] = new Vector3(center.x + arrowSize / 2, cellRect.yMax - CELL_SIZE * 0.35f); // 右上
                break;

            case Direction.Left:
                trianglePoints[0] = new Vector3(cellRect.xMin + CELL_SIZE * 0.15f, center.y); // 左の頂点
                trianglePoints[1] = new Vector3(cellRect.xMin + CELL_SIZE * 0.35f, center.y - arrowSize / 2); // 右上
                trianglePoints[2] = new Vector3(cellRect.xMin + CELL_SIZE * 0.35f, center.y + arrowSize / 2); // 右下
                break;

            case Direction.Right:
                trianglePoints[0] = new Vector3(cellRect.xMax - CELL_SIZE * 0.15f, center.y); // 右の頂点
                trianglePoints[1] = new Vector3(cellRect.xMax - CELL_SIZE * 0.35f, center.y - arrowSize / 2); // 左上
                trianglePoints[2] = new Vector3(cellRect.xMax - CELL_SIZE * 0.35f, center.y + arrowSize / 2); // 左下
                break;
        }

        // 三角形を描画
        Handles.color = arrowColor;
        Handles.DrawAAConvexPolygon(trianglePoints);
    }

    private void HandleGridInput(Rect gridRect)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown ||
            (e.type == EventType.MouseDrag && e.button == 0))
        {
            Vector2 mousePos = e.mousePosition;

            // グリッド内のクリック位置を計算
            float localX = mousePos.x - gridRect.x - GRID_PADDING;
            float localY = mousePos.y - gridRect.y - GRID_PADDING;

            if (localX >= 0 && localX < stage.Width * CELL_SIZE &&
                localY >= 0 && localY < stage.Height * CELL_SIZE)
            {
                int gridX = Mathf.FloorToInt(localX / CELL_SIZE);
                int gridY = stage.Height - 1 - Mathf.FloorToInt(localY / CELL_SIZE);

                HandleCellClick(gridX, gridY);

                e.Use();
                Repaint();
            }
        }
    }

    private void HandleCellClick(int x, int y)
    {
        Undo.RecordObject(stage, "Edit Stage Grid");

        switch (currentMode)
        {
            case EditMode.Wall:
                stage.SetWallAt(x, y, true);
                stage.RemoveTutorialData(new Vector2Int(x, y));
                break;

            case EditMode.Floor:
                stage.SetWallAt(x, y, false);
                stage.RemoveTutorialData(new Vector2Int(x, y));
                break;

            case EditMode.SyncMap:
                stage.SetCellTypeAt(x, y, CellType.SyncMap);
                stage.RemoveTutorialData(new Vector2Int(x, y));
                break;

            case EditMode.Player:
                stage.SetPlayerPosition(new Vector2Int(x, y));
                break;

            case EditMode.Enemy:
                // 同じ位置に既に敵がいる場合は削除
                stage.RemoveEnemyAt(new Vector2Int(x, y));

                int ammo = (currentEnemyType == EnemyType.Skeleton) ? currentAmmo : -1;
                stage.AddEnemy(new Vector2Int(x, y), currentEnemyType, currentEnemyDirection, currentMaxHP, 0, ammo, currentDropsCoin);

                stage.UpdateEnemyActionIndices();
                serializedObject.Update();
                if (enemyReorderableList != null)
                {
                    enemyReorderableList.index = stage.EnemyStartData.Count - 1; // 新しく追加した敵を選択
                }
                break;

            case EditMode.EraseUnit:
                Vector2Int clickPos = new Vector2Int(x, y);
                // プレイヤーがその位置にいるかチェック
                if (stage.PlayerStartPosition == clickPos)
                {
                    // プレイヤーを画面外に移動（-1, -1）
                    stage.SetPlayerPosition(new Vector2Int(-1, -1));
                }
                // 敵がその位置にいる場合は削除
                stage.RemoveEnemyAt(clickPos);
                
                // Crateがその位置にいる場合は削除
                stage.RemoveCrateAt(clickPos);

                stage.UpdateEnemyActionIndices();
                serializedObject.Update();

                break;

            case EditMode.Tutorial:
                stage.SetCellTypeAt(x, y, CellType.Tutorial);
                stage.SetTutorialData(new Vector2Int(x, y), currentTutorialIndex);
                break;

            case EditMode.StairUp:
                // 上階段セルを配置
                stage.SetCellTypeAt(x, y, CellType.StairUp);
                stage.RemoveTutorialData(new Vector2Int(x, y));
                break;

            case EditMode.StairDown:
                // 下階段セルを配置
                stage.SetCellTypeAt(x, y, CellType.StairDown);
                stage.RemoveTutorialData(new Vector2Int(x, y));
                break;

            case EditMode.BrokenStairUp:
                // 壊れた上階段セルを配置
                stage.SetCellTypeAt(x, y, CellType.BrokenStairUp);
                stage.RemoveTutorialData(new Vector2Int(x, y));
                break;

            case EditMode.Crate:
                // Crateを配置 (既にあれば無視される想定)
                stage.AddCrate(new Vector2Int(x, y));
                break;

            case EditMode.StairLink:
                CellType type = stage.GetWallAt(x, y);
                if (type == CellType.StairUp || type == CellType.StairDown)
                {
                    // 既にリストにあるかチェック
                    Vector2Int pos = new Vector2Int(x, y);
                    bool exists = false;
                    for (int i = 0; i < stage.StairConnections.Count; i++)
                    {
                        if (stage.StairConnections[i].Position == pos)
                        {
                            exists = true;
                            // 選択状態にする（reorderable listのインデックスを設定）
                            if (stairConnectionsReorderableList != null)
                            {
                                stairConnectionsReorderableList.index = i;
                            }
                            break;
                        }
                    }

                    if (!exists)
                    {
                        // リストに追加
                        var connectionsProp = serializedObject.FindProperty("stairConnections");
                        connectionsProp.InsertArrayElementAtIndex(connectionsProp.arraySize);
                        var element = connectionsProp.GetArrayElementAtIndex(connectionsProp.arraySize - 1);
                        element.FindPropertyRelative("Position").vector2IntValue = pos;
                        element.FindPropertyRelative("TargetStage").objectReferenceValue = null;

                        serializedObject.ApplyModifiedProperties();

                        // 追加した要素を選択
                        if (stairConnectionsReorderableList != null)
                        {
                            stairConnectionsReorderableList.index = connectionsProp.arraySize - 1;
                        }
                        Debug.Log($"Added stair connection for {pos}");
                    }
                }
                else
                {
                    Debug.LogWarning("階段ではない場所です。階段をクリックして設定を追加してください。");
                }
                break;
        }

        EditorUtility.SetDirty(stage);
            Repaint();
    }

    private void DrawLegend()
    {
        EditorGUILayout.BeginVertical("Box");
        EditorGUILayout.LabelField("凡例", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawColorBox(wallColor);
        EditorGUILayout.LabelField("壁", GUILayout.Width(60));
        DrawColorBox(floorColor);
        EditorGUILayout.LabelField("床", GUILayout.Width(60));
        DrawColorBox(syncMapColor);
        EditorGUILayout.LabelField("SyncMap", GUILayout.Width(70));
        DrawColorBox(tutorialColor);
        EditorGUILayout.LabelField("Tutorial", GUILayout.Width(70));
        DrawColorBox(stairUpColor);
        EditorGUILayout.LabelField("階段↑", GUILayout.Width(60));
        DrawColorBox(stairDownColor);
        EditorGUILayout.LabelField("階段↓", GUILayout.Width(60));
        DrawColorBox(brokenStairUpColor);
        EditorGUILayout.LabelField("壊階段↑", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawColorBox(playerColor);
        EditorGUILayout.LabelField("プレイヤー (P)", GUILayout.Width(100));
        DrawColorBox(enemyColor);
        EditorGUILayout.LabelField("敵 (E)", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawColorBox(Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
        EditorGUI.DrawRect(rect, color);
    }

    private void DrawEnemyList()
    {
        showEnemyList = EditorGUILayout.Foldout(showEnemyList, "敵リスト", true);

        if (!showEnemyList) return;

        EditorGUILayout.BeginVertical("Box");

        if (stage.EnemyStartData.Count == 0)
        {
            EditorGUILayout.LabelField("敵が配置されていません", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            // ReorderableListを描画
            enemyReorderableList.DoLayoutList();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawUtilityButtons()
    {
        EditorGUILayout.BeginVertical("Box");
        EditorGUILayout.LabelField("ユーティリティ", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("全て壁にする"))
        {
            Undo.RecordObject(stage, "Fill Walls");
            for (int y = 0; y < stage.Height; y++)
            {
                for (int x = 0; x < stage.Width; x++)
                {
                    stage.SetWallAt(x, y, true);
                }
            }
            EditorUtility.SetDirty(stage);
        }

        if (GUILayout.Button("全て床にする"))
        {
            Undo.RecordObject(stage, "Fill Floors");
            for (int y = 0; y < stage.Height; y++)
            {
                for (int x = 0; x < stage.Width; x++)
                {
                    stage.SetWallAt(x, y, false);
                }
            }
            EditorUtility.SetDirty(stage);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("外周を壁にする"))
        {
            Undo.RecordObject(stage, "Create Border");
            for (int x = 0; x < stage.Width; x++)
            {
                stage.SetWallAt(x, 0, true);
                stage.SetWallAt(x, stage.Height - 1, true);
            }
            for (int y = 0; y < stage.Height; y++)
            {
                stage.SetWallAt(0, y, true);
                stage.SetWallAt(stage.Width - 1, y, true);
            }
            EditorUtility.SetDirty(stage);
        }

        if (GUILayout.Button("全ての敵を削除"))
        {
            Undo.RecordObject(stage, "Clear Enemies");
            stage.ClearEnemies();
            EditorUtility.SetDirty(stage);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
