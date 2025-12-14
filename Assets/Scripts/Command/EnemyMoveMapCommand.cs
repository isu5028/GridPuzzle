using UnityEngine;

public class EnemyMoveMapCommand : ICommand
{
    private GridManager gridManager;
    private Enemy enemy;
    private StageSO currentStage;
    private StageSO nextStage;
    private Vector2Int currentPos;
    private Vector2Int newMapPos;
    private Direction currentDirection;
    private Direction moveDirection;

    private int originalOrder;

    public EnemyMoveMapCommand(GridManager gridManager, Enemy enemy, StageSO currentStage, StageSO nextStage, Vector2Int currentPos, Vector2Int newMapPos, Direction currentDirection, Direction moveDirection)
    {
        this.gridManager = gridManager;
        this.enemy = enemy;
        this.currentStage = currentStage;
        this.nextStage = nextStage;
        this.currentPos = currentPos;
        this.newMapPos = newMapPos;
        this.currentDirection = currentDirection;
        this.moveDirection = moveDirection;
        this.originalOrder = enemy.Order;
    }

    public void Execute()
    {
        gridManager.MoveEnemyToStage(enemy, nextStage, newMapPos, moveDirection);

        // Update map cell status based on enemy presence
        gridManager.SyncStageStatus(currentStage);
        gridManager.SyncStageStatus(nextStage);
    }

    public void Undo()
    {
        gridManager.MoveEnemyToStage(enemy, currentStage, currentPos, currentDirection);
        enemy.SetOrder(originalOrder);
        gridManager.SortEnemies();

        // Restore map cell status
        gridManager.SyncStageStatus(currentStage);
        gridManager.SyncStageStatus(nextStage);
    }


}