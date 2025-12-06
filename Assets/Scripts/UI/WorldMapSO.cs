using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New WorldMap", menuName = "Grid Puzzle/World Map")]
public class WorldMapSO : ScriptableObject
{
    // Inspectorで二次元配列っぽく見せるためのラッパークラス
    [System.Serializable]
    public class StageRow
    {
        public StageSO[] columns = null!; // 横一列のステージデータ
    }

    [Header("World Grid")]
    // これが「行」のリスト。行の中に「列」がある＝2次元配列になる
    [SerializeField] private StageRow[] rows;

    // --- 便利機能 ---

    // 座標を指定してステージを取得
    public StageSO GetStageAt(int x, int y)
    {
        // Convert logical y (0 = bottom) to array index (0 = top)
        if (rows == null || rows.Length == 0) return null;
        int arrayY = (rows.Length - 1) - y;
        if (arrayY < 0 || arrayY >= rows.Length) return null;
        if (rows[arrayY].columns == null) return null;
        if (x < 0 || x >= rows[arrayY].columns.Length) return null;

        return rows[arrayY].columns[x];
    }

    // 特定のステージがマップのどこにあるか検索（現在地の特定に使用）
    public Vector2Int GetStageCoordinates(StageSO stage)
    {
        if (rows == null) return new Vector2Int(-1, -1);
        for (int arrayY = 0; arrayY < rows.Length; arrayY++)
        {
            if (rows[arrayY].columns == null) continue;
            for (int x = 0; x < rows[arrayY].columns.Length; x++)
            {
                if (rows[arrayY].columns[x] == stage)
                {
                    // convert array index back to logical y (0 = bottom)
                    int logicalY = (rows.Length - 1) - arrayY;
                    return new Vector2Int(x, logicalY);
                }
            }
        }
        return new Vector2Int(-1, -1); // 見つからなかった場合
    }

    // 現在のステージと移動方向から、次のステージを取得する
    public StageSO GetNeighborStage(StageSO currentStage, Direction direction)
    {
        Vector2Int currentCoords = GetStageCoordinates(currentStage);

        // マップに含まれていないステージならnull
        if (currentCoords.x == -1) return null;

        Vector2Int targetCoords = currentCoords;

        // Y軸の増減はUnityの座標系ではなく「配列のインデックス」基準になることが多い
        // ここでは [0][0] を左上、Yが増えると下に行くと仮定するか、
        // 直感的に Yが増えると上(Up)とするかで実装が変わります。
        // 一般的な2Dマップ配列として「Yが増えると上」で計算します（Inspectorの並びとは逆になる点に注意）

        // ※InspectorのElement 0 が一番上なので、Y+1 をUpにするなら、配列indexは Y-1 する必要があります。
        // ここでは混乱を防ぐため「配列の見た目通り（上がElement 0）」＝「Yが増えると下(Down)」で実装します。

        // Here logical coordinates use y=0 at bottom, so Up increases y
        switch (direction)
        {
            case Direction.Up:    targetCoords.y += 1; break; // 上に行く＝yが増える
            case Direction.Down:  targetCoords.y -= 1; break; // 下に行く＝yが減る
            case Direction.Left:  targetCoords.x -= 1; break;
            case Direction.Right: targetCoords.x += 1; break;
        }

        return GetStageAt(targetCoords.x, targetCoords.y);
    }
}