using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;

public class StageTransitionSystem : IStageTransitionSystem
{
    private GridManager gm;

    public StageTransitionSystem(GridManager gridManager)
    {
        this.gm = gridManager;
    }

    public async UniTask SwitchStageAsync(StageSO nextstage, Direction entryDirection)
    {
        if (gm.IsTransitioning) return;
        gm.IsTransitioning = true;

        if(gm.UnitManager.Player != null) gm.UnitManager.Player.ForceActionComplete();

        // 1. Setup Transition Tilemap
        HashSet<Vector2Int> destroyed = gm.DestroyedWalls.ContainsKey(nextstage) ? gm.DestroyedWalls[nextstage] : null;
        gm.Visualizer.SetupTransitionTilemap(nextstage, entryDirection, destroyed, GameProgressManager.Instance.IsPositionCleared);

        var dirVector = Utility.GetVectorFromDirection(entryDirection);
        // Visualizer puts tilemap at -offset. We need to move Parent by +offset.
        Vector3 transitionOffset = new Vector3(
            dirVector.x * gm.Visualizer.TilemapOffset.x * 2,
            dirVector.y * gm.Visualizer.TilemapOffset.y * 2,
            0f);

        // 2. Prepare Enemies
        // Enemies should be placed at TransitionMap position (which is -transitionOffset relative to current map)
        PrepareEnemiesForTransition(nextstage, -transitionOffset);

        gm.UnitManager.SetAllEnemiesActive(false);

        if (gm.UnitManager.Player != null)
             gm.UnitManager.Player.transform.SetParent(gm.Visualizer.GridParent);

        // 3. Play Animation
        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);
        await gm.Visualizer.MoveGridParent(transitionOffset, 0.2f).AsyncWaitForCompletion();

        // 4. Update Logical State
        Vector2Int entryPos = GridCalculation.CalculateEntryPosition(nextstage, entryDirection, gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentPos : Vector2Int.zero);

        gm.LoadStage(nextstage, false, entryPos, entryDirection, true);

        // 5. Cleanup
        gm.Visualizer.ClearTransitionTiles();

        gm.IsTransitioning = false;
    }

    public void PrepareEnemiesForTransition(StageSO nextstage, Vector3 transitionOffset)
    {
        gm.StageEnemySystem.EnsureStageEnemiesLoaded(nextstage);

        if (!gm.StageEnemiesMap.TryGetValue(nextstage, out var enemies)) return;

        foreach (var enemy in enemies)
        {
             if (enemy == null) continue;

             enemy.gameObject.SetActive(true);

             // Visual only: GridToWorld returns pos relative to (0,0). Add offset to place relative to TransitionMap.
             enemy.transform.position = gm.GridToWorldPosition(enemy.CurrentPos) + transitionOffset;
             enemy.SetDissolveAmount(0f);
        }
    }

    public async UniTask WarpToStageAsync(StageSO stage, Vector2Int gridPos, Direction dir, System.Action onComplete = null)
    {
        if (gm.IsTransitioning) return;

        // No WarpEffect in Visualizer, just warp
        WarpToStage(stage, gridPos, dir);
        if (onComplete != null) onComplete.Invoke();

        await UniTask.Yield(); // Just to keep async signature valid
    }

    public void WarpToStage(StageSO stage, Vector2Int gridPos, Direction dir)
    {
        gm.LoadStageWithCustomStart(stage, gridPos, dir);
    }

    public void WarpToStageAtStairUp(StageSO stage)
    {
        // Simple fallback to default start pos
        if (stage == null) return;
        gm.LoadStageWithCustomStart(stage, stage.PlayerStartPosition, stage.PlayerStartDirection);
    }

    public async UniTask WarpToStageAtStairUpAsync(StageSO stage)
    {
         if (gm.IsTransitioning) return;
         WarpToStageAtStairUp(stage);
         await UniTask.Yield();
    }

    /// <summary>
    /// マップワープ時にスライドアニメーション付きで遷移する
    /// </summary>
    public async UniTask WarpWithAnimationAsync(StageSO targetStage, Vector2Int targetPos, Direction targetDir, bool restoreHP = false)
    {
        if (gm.IsTransitioning) return;
        if (targetStage == null) return;



        gm.IsTransitioning = true;

        if (gm.UnitManager.Player != null)
        {
             // Don't force action complete here as it triggers enemy turns
        }

        // プレイヤーの現在位置・方向を記憶（Undo用）
        Vector2Int sourcePos = gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentPos : Vector2Int.zero;
        Direction sourceDir = gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentDirection : Direction.Right;
        StageSO sourceStage = gm.CurrentStageSOInternal;

        // 1. ワープ方向を計算
        GridCalculation.WarpDirection warpDir = GridCalculation.DetermineWarpDirection(
            gm.CurrentStageSOInternal.MapPosition, targetStage.MapPosition);

        // 2. プレイヤーをフェードアウト（スライドと並行実行）
        bool goingDown = targetStage.MapPosition.y > gm.CurrentStageSOInternal.MapPosition.y;
        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.FadeOutAsync(0.15f, goingDown).Forget();
        }

        // 3. 遷移先タイルマップをセットアップ
        HashSet<Vector2Int> destroyed = gm.DestroyedWalls.ContainsKey(targetStage) ? gm.DestroyedWalls[targetStage] : null;

        bool isSceneChange = sourceStage != targetStage;

        if (isSceneChange)
        {
            gm.Visualizer.SetupWarpTransitionTilemap(targetStage, warpDir, targetPos, destroyed, GameProgressManager.Instance.IsPositionCleared);

            // 4. 遷移先の敵を準備
            Vector3 transitionOffset = gm.Visualizer.GetWarpTransitionOffset(warpDir);
            PrepareEnemiesForTransition(targetStage, transitionOffset);

            gm.UnitManager.SetAllEnemiesActive(false);

            if (gm.UnitManager.Player != null)
                gm.UnitManager.Player.transform.SetParent(gm.Visualizer.GridParent);

            // 5. スライドアニメーション
            AudioManager.Instance.PlaySE(AudioManager.SEType.warp);
            gm.Visualizer.MoveGridParent(transitionOffset, 0.2f);
            await UniTask.Delay(200);
        }

        // 6. コマンド経由でステージをロード（Undo/Redo対応）
        gm.Visualizer.ResetGridParentPosition();

        // コマンド実行前にスナップショットを取得（Undo用）
        gm.TurnManager.EnsureTurnStartSnapshot();

        var cmd = new WarpTransitionCommand(gm, sourceStage, targetStage,
            sourcePos, sourceDir, targetPos, targetDir, restoreHP);
        gm.CommandManager.ExecuteCommand(cmd);

        // ターンを完了させて履歴に記録（敵のターンはスキップ）
        await gm.TurnManager.OnPlayerActionComplete(true);

        // 7. クリーンアップ
        gm.Visualizer.ClearTransitionTiles();

        // 8. プレイヤーをフェードイン
        if (gm.UnitManager.Player != null)
        {
            await gm.UnitManager.Player.FadeInAsync(0.2f, goingDown);
        }

        gm.SyncWorldMapStatus();
        gm.IsTransitioning = false;
    }

    public void SwitchStage(StageSO nextstage, Direction entryDirection)
    {
        SwitchStageAsync(nextstage, entryDirection).Forget();
    }

    public void SwitchStageDirect(StageSO nextstage, Direction entryDirection)
    {
        // No animation
        Vector2Int entryPos = GridCalculation.CalculateEntryPosition(nextstage, entryDirection, gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentPos : Vector2Int.zero);
        gm.LoadStage(nextstage, false, entryPos, entryDirection); // LoadStage handles warping player to entryPos
        gm.SyncWorldMapStatus();
    }

    public async UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection)
    {
        await SwitchStageAsync(nextStage, entryDirection);
    }

    public async UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection, Vector2Int entryPos)
    {
         if (gm.IsTransitioning) return;
        gm.IsTransitioning = true;

        if(gm.UnitManager.Player != null) gm.UnitManager.Player.ForceActionComplete();

        HashSet<Vector2Int> destroyed = gm.DestroyedWalls.ContainsKey(nextStage) ? gm.DestroyedWalls[nextStage] : null;
        gm.Visualizer.SetupTransitionTilemap(nextStage, entryDirection, destroyed, GameProgressManager.Instance.IsPositionCleared);

        var dirVector = Utility.GetVectorFromDirection(entryDirection);
        Vector3 transitionOffset = new Vector3(
            dirVector.x * gm.Visualizer.TilemapOffset.x * 2,
            dirVector.y * gm.Visualizer.TilemapOffset.y * 2,
            0f);

        PrepareEnemiesForTransition(nextStage, -transitionOffset);

        gm.UnitManager.SetAllEnemiesActive(false);

        if (gm.UnitManager.Player != null)
             gm.UnitManager.Player.transform.SetParent(gm.Visualizer.GridParent);

        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);
        await gm.Visualizer.MoveGridParent(transitionOffset, 0.2f).AsyncWaitForCompletion();

        // Forced entry pos
        gm.LoadStage(nextStage, false, entryPos, entryDirection, true);

        // Cleanup
        gm.Visualizer.ClearTransitionTiles();

        gm.IsTransitioning = false;
    }
}

public class WarpTransitionCommand : ICommand, IAsyncUndoCommand
{
    private GridManager gm;
    private StageSO sourceStage;
    private StageSO targetStage;
    private Vector2Int sourcePos;
    private Direction sourceDir;
    private Vector2Int targetPos;
    private Direction targetDir;
    private bool restoreHP;
    private int previousHP;
    private bool wasDead;

    public WarpTransitionCommand(GridManager gm, StageSO source, StageSO target,
        Vector2Int sPos, Direction sDir, Vector2Int tPos, Direction tDir, bool restoreHP = false)
    {
        this.gm = gm;
        this.sourceStage = source;
        this.targetStage = target;
        this.sourcePos = sPos;
        this.sourceDir = sDir;
        this.targetPos = tPos;
        this.targetDir = tDir;
        this.restoreHP = restoreHP;

        // HP回復する場合は現在のHPを記録
        if (restoreHP && gm.UnitManager.Player != null)
        {
            previousHP = gm.UnitManager.Player.HP.Value;
            wasDead = gm.UnitManager.Player.IsDead;
        }
    }

    public void Execute()
    {
        PerformTransition(targetStage, targetPos, targetDir);

        if (restoreHP && gm.UnitManager.Player != null && !gm.UnitManager.Player.IsDead)
        {
            gm.RestorePlayerHP();
        }

        gm.SaveGame(GameProgressManager.SaveType.Auto);
    }

    public void Undo() { UndoAsync().Forget(); }

    public async UniTask UndoAsync()
    {
        PerformTransition(sourceStage, sourcePos, sourceDir);

        // HP回復をUndoする（元のHPに戻す）
        if (restoreHP && gm.UnitManager.Player != null)
        {
            // 防御的処置: ワープ前の状態に戻すために必要なら蘇生する（InstantDeathCommandのUndoでの蘇生を補完）
            if (gm.UnitManager.Player.IsDead && !wasDead)
            {
                gm.UnitManager.Player.Revive();
            }

            gm.UnitManager.Player.SetHP(previousHP);
            // 元が死亡状態だった場合は考慮しない（通常ワープ時に死亡状態はあり得ない）
        }
    }

    private void PerformTransition(StageSO stage, Vector2Int p, Direction d)
    {
        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.gameObject.transform.SetParent(gm.Visualizer.GridParent);
        }

        gm.CurrentStageSOInternal = stage;

        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.WarpToNewStage(p, d);
        }

        gm.LoadStage(stage, false, p, d, false);
    }
}
