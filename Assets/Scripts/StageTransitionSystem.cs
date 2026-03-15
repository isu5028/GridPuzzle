using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;

public partial class StageTransitionSystem : IStageTransitionSystem
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

        // Calculate transition offset based on STAGE SIZE
        Vector3 transitionOffset = GridCalculation.CalculateTransitionOffset(gm.CurrentStageSOInternal, nextstage, entryDirection, gm.Visualizer.CellSize);

        gm.Visualizer.SetupTransitionTilemap(nextstage, transitionOffset, destroyed, GameProgressManager.Instance.IsPositionCleared);

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

             if (enemy.IsDead)
             {
                 enemy.gameObject.SetActive(false);
                 continue;
             }

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
    public async UniTask WarpWithAnimationAsync(StageSO targetStage, Vector2Int targetPos, Direction targetDir, bool restoreHP = false, GridCalculation.WarpDirection? overrideWarpDir = null, List<Crate> pushedCrates = null, List<Vector2Int> crateSourcePositions = null, List<Vector2Int> crateTargetPositions = null)
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
        GridCalculation.WarpDirection warpDir = overrideWarpDir ?? GridCalculation.DetermineWarpDirection(
            gm.CurrentStageSOInternal.MapPosition, targetStage.MapPosition);

        // 2. プレイヤーをフェードアウト（スライドと並行実行）※境界遷移時はフェードしない
        bool goingDown = targetStage.MapPosition.y > gm.CurrentStageSOInternal.MapPosition.y;
        if (gm.UnitManager.Player != null && overrideWarpDir == null)
        {
            gm.UnitManager.Player.FadeOutAsync(0.15f, goingDown).Forget();
        }

        // 3. 遷移先タイルマップをセットアップ
        HashSet<Vector2Int> destroyed = gm.DestroyedWalls.ContainsKey(targetStage) ? gm.DestroyedWalls[targetStage] : null;

        bool isSceneChange = sourceStage != targetStage;

        if (isSceneChange)
        {
            // Calculate Transition Offset (Dynamic based on Stage Size for cardinal directions)
            Vector3 transitionOffset = GridCalculation.CalculateWarpTransitionOffset(warpDir, gm.Visualizer.TilemapOffset, sourceStage, targetStage, gm.Visualizer.CellSize);
            Vector3 initialOffset = -transitionOffset;

            gm.Visualizer.SetupWarpTransitionTilemap(targetStage, initialOffset, targetPos, destroyed, GameProgressManager.Instance.IsPositionCleared);

            // 4. 遷移先の敵を準備
            PrepareEnemiesForTransition(targetStage, -transitionOffset);

            gm.UnitManager.SetAllEnemiesActive(false);

            if (gm.UnitManager.Player != null)
                gm.UnitManager.Player.transform.SetParent(gm.Visualizer.GridParent);

            // 5. スライドアニメーション
            AudioManager.Instance.PlaySE(AudioManager.SEType.warp);
            var gridTween = gm.Visualizer.MoveGridParent(transitionOffset, 0.2f);

            // プレイヤーのシームレス移動アニメーション（境界遷移などの場合）
            if (gm.UnitManager.Player != null && overrideWarpDir != null)
            {
                // 次のステージの初期オフセットを取得（TransitionTilemapの位置）
                // Vector3 stageOffset = GridCalculation.CalculateWarpInitialOffset(warpDir, gm.Visualizer.TilemapOffset);
                Vector3 stageOffset = -transitionOffset; // Effectively initialOffset

                // 遷移先のローカル座標を計算（GridParent基準、今のGridParentは移動前なので0ベース）
                // GridToWorldPositionはTilemapOffset込みの座標を返す
                // これにStageOffsetを加算することで、遷移先ステージ上での該当タイルの位置になる
                Vector3 playerTargetLocalPos = stageOffset + (gm.Visualizer.GridToWorldPosition(targetPos) - gm.Visualizer.GridParent.position);

                // GridToWorldPositionはWorld座標を返すが、GridParentが動いていない前提ならLocalとみなせる（親が0,0,0なら）
                // ただし、もしGridToWorldPositionの実装がTransform.positionを使っているなら、親の位置を引く必要がある。
                // 確認したコードでは `tilemap.CellToWorld(...) + ...` なので、通常はLocalに近いが、Tilemapが親の子ならLocal。

                // 並行してプレイヤーを移動（イージングはスライドに合わせる）
                var playerTween = gm.UnitManager.Player.transform.DOLocalMove(playerTargetLocalPos, 0.2f).SetEase(Ease.OutQuad);

                await UniTask.WhenAll(
                    gridTween.AsyncWaitForCompletion().AsUniTask(),
                    playerTween.AsyncWaitForCompletion().AsUniTask()
                );
            }
            else
            {
                await UniTask.WhenAll(
                    gridTween.AsyncWaitForCompletion().AsUniTask()
                );
            }

            // Kill tweens to ensure clean state
            if(gm.UnitManager.Player != null) gm.UnitManager.Player.transform.DOKill();
        }

        // 6. コマンド経由でステージをロード（Undo/Redo対応）
        // ResetGridParentPositionの前に親子関係を解除しないと、PlayerがGridParentと一緒に移動してしまい、
        // その後のWarpToNewStageで視覚的な位置飛び（スナップ）が発生するため、ここで一時的に解除する。
        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.transform.SetParent(null);
        }
        gm.Visualizer.ResetGridParentPosition();

        // コマンド実行前にスナップショットを取得（Undo用）
        gm.TurnManager.EnsureTurnStartSnapshot();

        var cmd = new WarpTransitionCommand(gm, sourceStage, targetStage,
            sourcePos, sourceDir, targetPos, targetDir, restoreHP,
            clearWalls: restoreHP && overrideWarpDir == null,
            pushedCrates: pushedCrates,
            crateSourcePositions: crateSourcePositions,
            crateTargetPositions: crateTargetPositions); // マップワープ時のみ壁をリセット
        gm.CommandManager.ExecuteCommand(cmd);

        // ターンを完了させて履歴に記録（敵のターンはスキップ）
        await gm.TurnManager.OnPlayerActionComplete(true);

        // 7. クリーンアップ
        gm.Visualizer.ClearTransitionTiles();

        // 8. プレイヤーをフェードイン
        if (gm.UnitManager.Player != null && overrideWarpDir == null)
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

        Vector3 transitionOffset = GridCalculation.CalculateTransitionOffset(gm.CurrentStageSOInternal, nextStage, entryDirection, gm.Visualizer.CellSize);
        gm.Visualizer.SetupTransitionTilemap(nextStage, transitionOffset, destroyed, GameProgressManager.Instance.IsPositionCleared);

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
        gm.IsTransitioning = false;
    }

    public bool CheckAndPerformBoundaryTransition(Unit unit) => CheckAndPerformBoundaryTransition(unit, null);

    public bool CheckAndPerformBoundaryTransition(Unit unit, Vector2Int? overrideTargetPos)
    {
        if (unit == null || gm.IsTransitioning) return false;

        Vector2Int pos = overrideTargetPos ?? unit.CurrentPos;
        StageSO currentStage = gm.CurrentStageSO;

        // Check if strictly OOB
        if (gm.IsPositionInBounds(pos)) return false;

        // Determine Direction and Neighbor
        StageSO neighbor = null;
        Vector2Int targetPos = Vector2Int.zero;
        Direction targetDir = unit.CurrentDirection;
        GridCalculation.WarpDirection warpDir = GridCalculation.WarpDirection.Down; // Default

        if (pos.x < 0)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Left);
            if (neighbor != null)
            {
                int y = Mathf.Clamp(pos.y, 0, neighbor.Height - 1);
                targetPos = new Vector2Int(neighbor.Width - 1, y);
                warpDir = GridCalculation.WarpDirection.Left;
            }
        }
        else if (pos.x >= currentStage.Width)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Right);
            if (neighbor != null)
            {
                int y = Mathf.Clamp(pos.y, 0, neighbor.Height - 1);
                targetPos = new Vector2Int(0, y);
                warpDir = GridCalculation.WarpDirection.Right;
            }
        }
        else if (pos.y < 0)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Down);
            if (neighbor != null)
            {
                int x = Mathf.Clamp(pos.x, 0, neighbor.Width - 1);
                targetPos = new Vector2Int(x, neighbor.Height - 1);
                warpDir = GridCalculation.WarpDirection.Down;
            }
        }
        else if (pos.y >= currentStage.Height)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Up);
             if (neighbor != null)
            {
                int x = Mathf.Clamp(pos.x, 0, neighbor.Width - 1);
                targetPos = new Vector2Int(x, 0);
                warpDir = GridCalculation.WarpDirection.Up;
            }
        }

        if (neighbor != null)
        {
            gm.EnsureStageCratesLoaded(neighbor);

            Vector2Int dirVec = Utility.GetVectorFromDirection(targetDir);

            List<Crate> sourceStageCrates = new List<Crate>();
            Vector2Int sourceScanPos = unit.CurrentPos + dirVec;
            while (TryGetCrateAt(currentStage, sourceScanPos, out Crate sourceCrate))
            {
                sourceStageCrates.Add(sourceCrate);
                sourceScanPos += dirVec;
            }

            List<Crate> targetStageCrates = new List<Crate>();
            Vector2Int targetScanPos = targetPos;
            while (TryGetCrateAt(neighbor, targetScanPos, out Crate targetCrate))
            {
                targetStageCrates.Add(targetCrate);
                targetScanPos += dirVec;
            }

            List<Crate> pushedCrates = null;
            List<Vector2Int> crateSourceLayout = null;
            List<Vector2Int> crateTargetLayout = null;

            int totalCratesToPush = sourceStageCrates.Count + targetStageCrates.Count;
            if (totalCratesToPush > 0)
            {
                pushedCrates = new List<Crate>(totalCratesToPush);
                crateSourceLayout = new List<Vector2Int>(totalCratesToPush);
                crateTargetLayout = new List<Vector2Int>(totalCratesToPush);
            }

            HashSet<Vector2Int> vacatedByTargetCrates = new HashSet<Vector2Int>();
            foreach (var crate in targetStageCrates)
            {
                vacatedByTargetCrates.Add(crate.CurrentPos);
            }

            HashSet<Vector2Int> reservedTargets = new HashSet<Vector2Int>();

            for (int i = 0; i < sourceStageCrates.Count; i++)
            {
                Crate crate = sourceStageCrates[i];
                Vector2Int crateNextPos = targetPos + dirVec * (i + 1);

                if (!CanPushCrateInto(neighbor, crateNextPos, vacatedByTargetCrates, reservedTargets))
                {
                    return false;
                }

                pushedCrates.Add(crate);
                crateSourceLayout.Add(crate.CurrentPos);
                crateTargetLayout.Add(crateNextPos);
            }

            for (int i = 0; i < targetStageCrates.Count; i++)
            {
                Crate crate = targetStageCrates[i];
                Vector2Int crateNextPos = targetPos + dirVec * (sourceStageCrates.Count + i + 1);

                if (!CanPushCrateInto(neighbor, crateNextPos, vacatedByTargetCrates, reservedTargets))
                {
                    return false;
                }

                pushedCrates.Add(crate);
                crateSourceLayout.Add(crate.CurrentPos);
                crateTargetLayout.Add(crateNextPos);
            }

            WarpWithAnimationAsync(neighbor, targetPos, targetDir, false, warpDir, pushedCrates, crateSourceLayout, crateTargetLayout).Forget();
            return true;
        }

        return false;
    }

    private bool TryGetCrateAt(StageSO stage, Vector2Int pos, out Crate crate)
    {
        crate = null;
        if (stage == null) return false;

        gm.EnsureStageCratesLoaded(stage);
        if (!gm.StageCratesMap.TryGetValue(stage, out var crates)) return false;

        foreach (var candidate in crates)
        {
            if (candidate == null || candidate.IsDead) continue;
            if (candidate.CurrentPos == pos)
            {
                crate = candidate;
                return true;
            }
        }

        return false;
    }

    private bool IsPositionInBounds(StageSO stage, Vector2Int pos)
    {
        return stage != null
            && pos.x >= 0 && pos.x < stage.Width
            && pos.y >= 0 && pos.y < stage.Height;
    }

    private bool CanPushCrateInto(StageSO stage, Vector2Int pos, HashSet<Vector2Int> vacatedByTargetCrates, HashSet<Vector2Int> reservedTargets)
    {
        if (!IsPositionInBounds(stage, pos)) return false;
        if (!gm.IsWalkableAt(stage, pos)) return false;
        if (!reservedTargets.Add(pos)) return false;
        if (gm.GetEnemyAt(stage, pos) != null) return false;

        if (!TryGetCrateAt(stage, pos, out _)) return true;
        return vacatedByTargetCrates.Contains(pos);
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
    private bool clearWalls; // マップワープ時のみtrueにして壁をリセット

    // ワープ前の壁破壊状態を保存（Undo用）
    private Dictionary<StageSO, HashSet<Vector2Int>> destroyedWallsSnapshot;

    private List<Crate> pushedCrates;
    private List<Vector2Int> crateSourcePositions;
    private List<Vector2Int> crateTargetPositions;
    private List<StageSO> crateOriginalStages;

    public WarpTransitionCommand(GridManager gm, StageSO source, StageSO target,
        Vector2Int sPos, Direction sDir, Vector2Int tPos, Direction tDir, bool restoreHP = false, bool clearWalls = false, List<Crate> pushedCrates = null, List<Vector2Int> crateSourcePositions = null, List<Vector2Int> crateTargetPositions = null)
    {
        this.gm = gm;
        this.sourceStage = source;
        this.targetStage = target;
        this.sourcePos = sPos;
        this.sourceDir = sDir;
        this.targetPos = tPos;
        this.targetDir = tDir;
        this.restoreHP = restoreHP;
        this.clearWalls = clearWalls;
        this.pushedCrates = pushedCrates;
        this.crateSourcePositions = crateSourcePositions;
        this.crateTargetPositions = crateTargetPositions;

        if (pushedCrates != null && pushedCrates.Count > 0)
        {
            crateOriginalStages = new List<StageSO>(pushedCrates.Count);
            foreach (var crate in pushedCrates)
            {
                StageSO stageOfCrate = crate != null ? gm.GetStageOfCrate(crate) : null;
                crateOriginalStages.Add(stageOfCrate ?? sourceStage);
            }
        }

        // HP回復する場合は現在のHPを記録
        if (restoreHP && gm.UnitManager.Player != null)
        {
            previousHP = gm.UnitManager.Player.HP.Value;
            wasDead = gm.UnitManager.Player.IsDead;
        }

        // ワープ前の壁破壊状態をスナップショット保存（Undo用）
        destroyedWallsSnapshot = new Dictionary<StageSO, HashSet<Vector2Int>>();
        foreach (var kvp in gm.DestroyedWalls)
        {
            destroyedWallsSnapshot[kvp.Key] = new HashSet<Vector2Int>(kvp.Value);
        }
    }

    public void Execute()
    {
        // マップワープ時のみ壁の破壊状態をリセット（境界遷移や階段移動では維持）
        if (clearWalls)
        {
            gm.ClearDestroyedWallsKeepPermanent();
        }

        if (pushedCrates != null && crateTargetPositions != null && pushedCrates.Count > 0)
        {
            for (int i = 0; i < pushedCrates.Count && i < crateTargetPositions.Count; i++)
            {
                Crate crate = pushedCrates[i];
                if (crate == null) continue;

                StageSO originalStage = (crateOriginalStages != null && i < crateOriginalStages.Count)
                    ? crateOriginalStages[i]
                    : sourceStage;

                if (originalStage != null && gm.StageCratesMap.ContainsKey(originalStage))
                {
                    gm.StageCratesMap[originalStage].Remove(crate);
                }

                if (!gm.StageCratesMap.ContainsKey(targetStage)) gm.StageCratesMap[targetStage] = new System.Collections.Generic.List<Crate>();
                if (!gm.StageCratesMap[targetStage].Contains(crate)) gm.StageCratesMap[targetStage].Add(crate);

                crate.WarpTo(crateTargetPositions[i], crate.CurrentDirection);
            }
        }

        PerformTransition(targetStage, targetPos, targetDir);

        if (restoreHP && gm.UnitManager.Player != null && !gm.UnitManager.Player.IsDead)
        {
            gm.RestorePlayerHP();
        }

        if (clearWalls)
        {
            gm.SaveGame(GameProgressManager.SaveType.Auto);
        }
    }

    public void Undo() { UndoAsync().Forget(); }

    public async UniTask UndoAsync()
    {
        // 壁の破壊状態をワープ前に復元
        gm.DestroyedWalls.Clear();
        foreach (var kvp in destroyedWallsSnapshot)
        {
            gm.DestroyedWalls[kvp.Key] = new HashSet<Vector2Int>(kvp.Value);
        }
        gm.ReapplyPermanentDestroyedWalls();

        if (pushedCrates != null && crateSourcePositions != null && pushedCrates.Count > 0)
        {
            for (int i = 0; i < pushedCrates.Count && i < crateSourcePositions.Count; i++)
            {
                Crate crate = pushedCrates[i];
                if (crate == null) continue;

                if (gm.StageCratesMap.ContainsKey(targetStage)) gm.StageCratesMap[targetStage].Remove(crate);

                StageSO originalStage = (crateOriginalStages != null && i < crateOriginalStages.Count)
                    ? crateOriginalStages[i]
                    : sourceStage;

                if (originalStage == null)
                {
                    originalStage = sourceStage;
                }

                if (originalStage != null)
                {
                    if (!gm.StageCratesMap.ContainsKey(originalStage)) gm.StageCratesMap[originalStage] = new System.Collections.Generic.List<Crate>();
                    if (!gm.StageCratesMap[originalStage].Contains(crate)) gm.StageCratesMap[originalStage].Add(crate);
                }

                crate.WarpTo(crateSourcePositions[i], crate.CurrentDirection);
            }
        }

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
        // 1. Deparent player first to avoid moving it when GridParent resets
        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.gameObject.transform.SetParent(null);
        }

        gm.CurrentStageSOInternal = stage;

        // 2. Load Stage (This resets GridParent position to 0,0,0)
        // Note: LoadStage calls WarpToNewStage internally, but we might want to ensure it happens after reparenting if needed.
        // However, LoadStage usually instantiates or moves player.
        gm.LoadStage(stage, false, p, d, false);

        // 3. Reparent Player to GridParent (now at 0,0,0)
        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.gameObject.transform.SetParent(gm.Visualizer.GridParent);

            // 4. Force Warp to ensure local position is correct relative to GridParent
            // (LoadStage called it, but if parenting changed afterwards, we might need to refresh local pos)
            // But since LoadStage usually handles it, this is just a safety measure.
            // If LoadStage already parented it (it might not have if player existed), let's ensure.
             gm.UnitManager.Player.WarpToNewStage(p, d);
        }
    }
}

public class EnemyCrossStageMoveCommand : ICommand, IAsyncUndoCommand
{
    private GridManager gm;
    private Enemy enemy;
    private StageSO sourceStage;
    private StageSO targetStage;
    private Vector2Int sourcePos;
    private Vector2Int targetPos;
    private Direction sourceDir;
    private Direction targetDir;
    private bool wasActive;
    private int originalOrder;

    public EnemyCrossStageMoveCommand(GridManager gm, Enemy enemy, StageSO source, StageSO target, Vector2Int tPos, Direction tDir)
    {
        this.gm = gm;
        this.enemy = enemy;
        this.sourceStage = source;
        this.targetStage = target;
        this.targetPos = tPos;
        this.targetDir = tDir;
        this.sourcePos = enemy.CurrentPos;
        this.sourceDir = enemy.CurrentDirection;
        this.wasActive = enemy.gameObject.activeSelf;
        this.originalOrder = enemy.Order;
    }

    public void Execute()
    {
        int newOrder = gm.GetMaxEnemyOrderInStage(targetStage) + 1;
        enemy.SetOrder(newOrder);
        Transfer(sourceStage, targetStage, targetPos, targetDir);
    }

    public void Undo()
    {
        enemy.SetOrder(this.originalOrder);
        Transfer(targetStage, sourceStage, sourcePos, sourceDir, true);
    }

    public async UniTask UndoAsync()
    {
        Undo();
        await UniTask.Yield();
    }

    private void Transfer(StageSO from, StageSO to, Vector2Int pos, Direction dir, bool isUndo = false)
    {
        // 1. Remove from FromStage List
        if (gm.StageEnemiesMap.ContainsKey(from))
        {
            gm.StageEnemiesMap[from].Remove(enemy);
        }

        // 2. Add to ToStage List
        // Make sure the target stage is fully loaded (defaults generated or save applied)
        // before adding the new enemy, otherwise only this enemy will exist effectively "wiping" the stage's original enemies.
        gm.EnsureStageEnemiesLoaded(to);

        // Check for and remove any duplicate of this enemy that might have been loaded/generated
        // This prevents "Ghost" enemies (one active, one inactive duplicate)
            // 2. Add to ToStage List
            // Make sure the target stage is fully loaded (defaults generated or save applied)
            // before adding the new enemy, otherwise only this enemy will exist effectively "wiping" the stage's original enemies.
            gm.EnsureStageEnemiesLoaded(to);

            // Removing aggressive duplicate check that deleted valid enemies with same name (e.g. Enemy_Slime_0 vs Enemy_Slime_0)
            // Distinct instances should coexist even if names clash across stages.


        gm.StageEnemiesMap[to].Add(enemy);

        // 3. Update Enemy State
        enemy.WarpTo(pos, dir); // Sets internal state

        // 4. Handle Visuals and UnitManager Registration
        if (to == gm.CurrentStageSOInternal)
        {
            enemy.gameObject.SetActive(true);
            // Add to current active enemies if not already there
            if (!gm.UnitManager.Enemies.Contains(enemy))
            {
                gm.UnitManager.Enemies.Add(enemy);
            }
        }
        else
        {
            enemy.gameObject.SetActive(false);
            // Remove from current active enemies
            if (gm.UnitManager.Enemies.Contains(enemy))
            {
                gm.UnitManager.Enemies.Remove(enemy);
            }
        }

        if (from == gm.CurrentStageSOInternal)
        {
             // If leaving current stage, ensure removed from active list
             if (gm.UnitManager.Enemies.Contains(enemy))
             {
                 gm.UnitManager.Enemies.Remove(enemy);
             }
        }

        gm.UnitManager.SortEnemiesByOrder();
    }
}

public partial class StageTransitionSystem
{
    public bool CheckAndPerformEnemyBoundaryTransfer(Enemy enemy)
    {
        if (enemy == null) return false;

        Vector2Int pos = enemy.CurrentPos;
        StageSO currentStage = gm.GetStageOfEnemy(enemy);
        if (currentStage == null) return false;

        // Check if strictly OOB relative to currentStage dimensions
        if (pos.x >= 0 && pos.x < currentStage.Width && pos.y >= 0 && pos.y < currentStage.Height) return false;

        // Determine Direction and Neighbor
        StageSO neighbor = null;
        Vector2Int targetPos = Vector2Int.zero;
        Direction targetDir = enemy.CurrentDirection;

        if (pos.x < 0)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Left);
            if (neighbor != null)
            {
                int y = Mathf.Clamp(pos.y, 0, neighbor.Height - 1);
                targetPos = new Vector2Int(neighbor.Width - 1, y);
            }
        }
        else if (pos.x >= currentStage.Width)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Right);
            if (neighbor != null)
            {
                int y = Mathf.Clamp(pos.y, 0, neighbor.Height - 1);
                targetPos = new Vector2Int(0, y);
            }
        }
        else if (pos.y < 0)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Down);
            if (neighbor != null)
            {
                int x = Mathf.Clamp(pos.x, 0, neighbor.Width - 1);
                targetPos = new Vector2Int(x, neighbor.Height - 1);
            }
        }
        else if (pos.y >= currentStage.Height)
        {
            neighbor = currentStage.GetAdjacentStage(Direction.Up);
            if (neighbor != null)
            {
                int x = Mathf.Clamp(pos.x, 0, neighbor.Width - 1);
                targetPos = new Vector2Int(x, 0);
            }
        }

        if (neighbor != null)
        {
            // Execute Transfer Command
            var cmd = new EnemyCrossStageMoveCommand(gm, enemy, currentStage, neighbor, targetPos, targetDir);
            gm.CommandManager.ExecuteCommand(cmd);
            return true;
        }

        return false;
    }
}
