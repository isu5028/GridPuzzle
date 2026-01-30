using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class TransitionCommand : ICommand, IAsyncUndoCommand
{
    private GridManager gridManager;
    private StageSO fromStage;
    private StageSO toStage;
    private Direction entryDirection; // Undoで戻る時の向き（あるいは位置）
    private Vector2Int undoPos; // Undoで戻るべき座標
    private Vector2Int entryPos; // 新しいステージでの入場位置（Y座標を保持）

    public TransitionCommand(GridManager grid, StageSO from, StageSO to, Direction dir, Vector2Int returnPos)
    {
        this.gridManager = grid;
        this.fromStage = from;
        this.toStage = to;
        this.entryDirection = dir;
        this.undoPos = returnPos;

        // 遷移前の位置からY座標を保持して計算
        this.entryPos = CalculateEntryPosition(to, dir, returnPos);
    }

    private Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection, Vector2Int currentPos)
    {
        int targetX = 0;
        int targetY = 0;

        // fromStageの高さを使って「上からのインデックス」を算出
        int fromHeight = fromStage != null ? fromStage.Height : nextStage.Height;
        int rowFromTop = (fromHeight - 1) - currentPos.y;

        switch (fromDirection)
        {
            case Direction.Right: // 左端に出る
                targetX = 0;
                targetY = Mathf.Clamp((nextStage.Height - 1) - rowFromTop, 0, nextStage.Height - 1);
                break;
            case Direction.Left: // 右端に出る
                targetX = nextStage.Width - 1;
                targetY = Mathf.Clamp((nextStage.Height - 1) - rowFromTop, 0, nextStage.Height - 1);
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

    public void Execute()
    {
        //gridManager.TurnManager.SkipEnemyTurnNext();

        //gridManager.SwitchStage(toStage, entryDirection, entryPos);
    }

    public void Undo()
    {
        // 既存の同期互換性を保つため、以前の動作はそのまま非同期で起動する
        UndoAsync().Forget();
    }

    public async UniTask UndoAsync()
    {
        // 前のステージに戻る（演出付きで戻る）
        Direction opposite = entryDirection switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => entryDirection
        };

        // 演出を再生してから直接ロードする（コマンド履歴には追加しない）
        await gridManager.PlayTransitionWithoutCommand(fromStage, opposite, undoPos);
    }
}