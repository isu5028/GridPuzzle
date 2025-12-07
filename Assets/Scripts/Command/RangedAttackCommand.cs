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

    public RangedAttackCommand(Enemy attacker, Vector2Int targetPos, GridManager grid)
    {
        this.attacker = attacker;
        this.targetPos = targetPos;
        this.gridManager = grid;
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

        attacker.PerformRangedAttack(targetPos, targetUnit);
    }

    public void Undo()
    {
        if (hitUnit && targetUnit != null)
        {
            targetUnit.SetHP(previousHP);
            if (!wasDead && targetUnit.IsDead)
            {
                targetUnit.Revive();
            }
        }
    }
}
