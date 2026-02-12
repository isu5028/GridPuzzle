using UnityEngine;

public static class GridCalculation
{
    public enum WarpDirection
    {
        Right,
        Down,
        Left,
        Up,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Same
    }

    public static WarpDirection DetermineWarpDirection(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;  // 正: 右, 負: 左
        int dy = to.y - from.y;  // 正: 下(深い階), 負: 上(浅い階)

        // 斜め方向
        if (dy > 0 && dx < 0) return WarpDirection.DownLeft;   // 下の階・左
        if (dy > 0 && dx > 0) return WarpDirection.DownRight;  // 下の階・右
        if (dy < 0 && dx < 0) return WarpDirection.UpLeft;     // 上の階・左
        if (dy < 0 && dx > 0) return WarpDirection.UpRight;    // 上の階・右

        // 垂直方向のみ
        if (dy > 0) return WarpDirection.Down;
        if (dy < 0) return WarpDirection.Up;

        // 水平方向のみ
        if (dx > 0) return WarpDirection.Right;
        if (dx < 0) return WarpDirection.Left;

        return WarpDirection.Same;
    }

    /// <summary>
    /// 階段移動用のワープ方向を決定
    /// </summary>
    public static WarpDirection DetermineStairWarpDirection(StageSO fromStage, StageSO toStage, bool goingDown)
    {
        if (fromStage == null || toStage == null)
        {
            return goingDown ? WarpDirection.Down : WarpDirection.Up;
        }

        // 階段の列（StairColumn）を使用して水平方向を決定
        int fromColumn = fromStage.StairColumn;
        int toColumn = toStage.StairColumn;

        // ハブ（中央列=1）からの移動の場合
        if (fromStage.MapPosition.y == 0)
        {
            // ハブ→ダンジョン
            if (toColumn == 0) return WarpDirection.DownLeft;   // 左の階段
            if (toColumn == 2) return WarpDirection.DownRight;  // 右の階段
            return WarpDirection.Down;  // 中央の階段
        }

        // ダンジョン→ハブの場合
        if (toStage.MapPosition.y == 0)
        {
            if (fromColumn == 0) return WarpDirection.UpRight;  // 左列からハブへ
            if (fromColumn == 2) return WarpDirection.UpLeft;   // 右列からハブへ
            return WarpDirection.Up;  // 中央列からハブへ
        }

        // 同じ列内での移動（通常の階段移動）
        if (fromColumn == toColumn)
        {
            return goingDown ? WarpDirection.Down : WarpDirection.Up;
        }

        // 異なる列への移動（通常はないが念のため）
        int dx = toColumn - fromColumn;
        if (goingDown)
        {
            if (dx < 0) return WarpDirection.DownLeft;
            if (dx > 0) return WarpDirection.DownRight;
            return WarpDirection.Down;
        }
        else
        {
            if (dx < 0) return WarpDirection.UpLeft;
            if (dx > 0) return WarpDirection.UpRight;
            return WarpDirection.Up;
        }
    }

    public static Vector2Int FindStairUpPosition(StageSO stage)
    {
        if (stage == null) return stage.PlayerStartPosition;

        // ステージ内でStairUpセルを探す
        for (int y = 0; y < stage.Height; y++)
        {
            for (int x = 0; x < stage.Width; x++)
            {
                // Note: This relies on StageSO's static data directly.
                // It does NOT consider dynamically changes (destroyed walls) unless GetWallAt logic is replicated.
                // Assuming StairUp are static map features.
                if (stage.GetWallAt(x, y) == CellType.StairUp)
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        // 見つからなければ初期位置
        return stage.PlayerStartPosition;
    }

    public static Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection, Vector2Int currentPos)
    {
        int targetX = 0;
        int targetY = 0;

        switch (fromDirection)
        {
            case Direction.Right: // 左端に出る
                targetX = 0;
                targetY = Mathf.Clamp(currentPos.y, 0, nextStage.Height - 1);
                break;
            case Direction.Left: // 右端に出る
                targetX = nextStage.Width - 1;
                targetY = Mathf.Clamp(currentPos.y, 0, nextStage.Height - 1);
                break;
            case Direction.Up: // 下端に出る
                targetX = Mathf.Clamp(currentPos.x, 0, nextStage.Width - 1);
                targetY = 0;
                break;
            case Direction.Down: // 上端に出る
                targetX = Mathf.Clamp(currentPos.x, 0, nextStage.Width - 1);
                targetY = nextStage.Height - 1;
                break;
        }
        return new Vector2Int(targetX, targetY);
    }
}
