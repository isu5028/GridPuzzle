using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public interface IStageTransitionSystem
{
    UniTask SwitchStageAsync(StageSO nextstage, Direction entryDirection);
    UniTask WarpToStageAsync(StageSO stage, Vector2Int gridPos, Direction dir, System.Action onComplete = null);
    void WarpToStage(StageSO stage, Vector2Int gridPos, Direction dir);
    void WarpToStageAtStairUp(StageSO stage);
    UniTask WarpToStageAtStairUpAsync(StageSO stage);
    UniTask WarpWithAnimationAsync(StageSO targetStage, Vector2Int targetPos, Direction targetDir, bool restoreHP = false, GridCalculation.WarpDirection? overrideWarpDir = null, System.Collections.Generic.List<Crate> pushedCrates = null, System.Collections.Generic.List<Vector2Int> crateSourcePositions = null, System.Collections.Generic.List<Vector2Int> crateTargetPositions = null);
    void SwitchStage(StageSO nextstage, Direction entryDirection);
    void SwitchStageDirect(StageSO nextstage, Direction entryDirection);
    UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection);
    UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection, Vector2Int entryPos);
    void PrepareEnemiesForTransition(StageSO nextstage, Vector3 transitionOffset);
    bool CheckAndPerformBoundaryTransition(Unit unit);
    bool CheckAndPerformBoundaryTransition(Unit unit, Vector2Int? overrideTargetPos);
    bool CheckAndPerformEnemyBoundaryTransfer(Enemy enemy);
}

public interface IStageEnemySystem
{
    Enemy GetEnemyAt(StageSO stage, Vector2Int pos);
    void EnsureStageEnemiesLoaded(StageSO stage);
    bool CheckIfStageHasEnemy(StageSO stage);
    StageSO GetStageOfEnemy(Enemy enemy);
    List<Enemy> GetEnemiesOnStage(StageSO stage);
    void ResetAllEnemies(); // GridSaveSystemから呼ばれる
    void ApplySavedEnemyData(StageSO stage, List<EnemySaveData> savedEnemies); // GridSaveSystemから呼ばれる
    void AddPendingEnemy(StageSO stage, Enemy enemy);
    void CleanupAllEnemies();
    void PrepareEnemiesForStage(StageSO stage);
    int GetMaxEnemyOrderInStage(StageSO stage);
}

public interface IStairTraversalSystem
{
    bool CheckAndShowStairDialog();
    (StageSO targetStage, Vector2Int targetPos)? GetEnemyStairDestination(Enemy enemy, Vector2Int stairPos, Direction approachDirection);
    UniTask MoveEnemyToStage(Enemy enemy, StageSO targetStage, Vector2Int targetPos, Direction direction);
    (StageSO targetStage, Vector2Int targetPos)? GetCrateStairDestination(Crate crate, Vector2Int stairPos, Direction approachDirection);
    UniTask MoveCrateToStage(Crate crate, StageSO targetStage, Vector2Int targetPos, Direction direction);
    UniTask MoveCrateThroughBoundary(Crate crate, StageSO sourceStage, StageSO targetStage, Vector2Int targetPos, Direction direction);
    bool IsPositionOccupiedInStage(StageSO stage, Vector2Int pos);
}

public interface IShopSystem
{
    bool IsShopStage(StageSO stage);
    bool TryGetShopItemAt(StageSO stage, Vector2Int pos, out int itemId, out int price);
    bool IsShopItemPurchased(StageSO stage, Vector2Int pos);
    bool CanPurchaseShopItem(StageSO stage, Vector2Int pos);
    bool TryPurchaseShopItem(StageSO stage, Vector2Int pos, out int itemId, out int price);
    void MarkShopItemPurchased(StageSO stage, Vector2Int pos);
    void ResetShopPurchases(StageSO stage);
    void ResetAllShopPurchases();
    void Tick(float deltaTime);
    void OnStageLoaded(StageSO stage);
    void OnUnitStateChanged(Unit unit);
    void OnShopItemPurchased(StageSO stage, Vector2Int pos, int itemId, int price);
    void SetSuppressNextKingEnterDialogue(bool suppress);
}
