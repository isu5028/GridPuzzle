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
    UniTask WarpWithAnimationAsync(StageSO targetStage, Vector2Int targetPos, Direction targetDir, bool restoreHP = false);
    void SwitchStage(StageSO nextstage, Direction entryDirection);
    void SwitchStageDirect(StageSO nextstage, Direction entryDirection);
    UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection);
    UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection, Vector2Int entryPos);
    void PrepareEnemiesForTransition(StageSO nextstage, Vector3 transitionOffset);
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
}

public interface IStairTraversalSystem
{
    bool CheckAndShowStairDialog();
    (StageSO targetStage, Vector2Int targetPos)? GetEnemyStairDestination(Enemy enemy, Vector2Int stairPos, Direction approachDirection);
    UniTask MoveEnemyToStage(Enemy enemy, StageSO targetStage, Vector2Int targetPos, Direction direction);
    bool IsPositionOccupiedInStage(StageSO stage, Vector2Int pos);
}
