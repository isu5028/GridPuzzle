using UnityEngine;

public class RangedAttackCommand : ICommand
{
    private Enemy attacker;
    private Vector2Int targetPos;
    private GridManager gridManager;
    private Unit targetUnit;

    private int previousHP;
    private bool wasDead;
    private bool hitUnit;

    // Wall hit info
    private bool hitWall;
    private StageSO wallStage;
    private Vector2Int wallPos;

    private Transform arrowPrefab;

    public RangedAttackCommand(Enemy attacker, Vector2Int targetPos, GridManager grid, Transform arrowPrefab,
        bool hitWall = false, StageSO wallStage = null, Vector2Int wallPos = default)
    {
        this.attacker = attacker;
        this.targetPos = targetPos;
        this.gridManager = grid;
        this.arrowPrefab = arrowPrefab;
        this.hitWall = hitWall;
        this.wallStage = wallStage;
        this.wallPos = wallPos;
    }

    public void Execute()
    {
        targetUnit = gridManager.GetUnitAtPosition(targetPos);
        hitUnit = targetUnit != null;

        if (hitUnit)
        {
            previousHP = targetUnit.HP.Value;
            wasDead = targetUnit.IsDead;
        }

        // 壁ヒット時は矢の命中タイミングで壁を破壊する
        System.Action onHit = hitWall && wallStage != null
            ? () => gridManager.BreakWall(wallStage, wallPos)
            : (System.Action)null;

        attacker.ConsumeAmmo();
        attacker.PerformRangedAttack(targetPos, targetUnit, arrowPrefab, onHit);
    }

    public void Undo()
    {
        attacker.RecoverAmmo();

        if (hitUnit && targetUnit != null)
        {
            targetUnit.SetHP(previousHP);
            if (!wasDead && targetUnit.IsDead)
            {
                targetUnit.Revive();
            }
        }

        // 壁を元に戻す
        if (hitWall && wallStage != null)
        {
            gridManager.RestoreWall(wallStage, wallPos);
        }
    }
}
