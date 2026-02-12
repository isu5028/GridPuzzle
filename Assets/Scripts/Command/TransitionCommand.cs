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
    private Direction opposite;
    private Vector2Int undoPos; // Undoで戻るべき座標
    private Vector2Int entryPos; // 新しいステージでの入場位置（Y座標を保持）

    public TransitionCommand(GridManager manager, StageSO from, StageSO to, Direction dir, Vector2Int returnPos)
    {
        this.gridManager = manager;
        this.fromStage = from;
        this.toStage = to;
        this.entryDirection = dir;
        this.opposite = Utility.GetOppositeDirection(dir);
        this.undoPos = returnPos;
        this.entryPos = GridCalculation.CalculateEntryPosition(to, dir, returnPos);
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