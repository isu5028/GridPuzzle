using UnityEngine;

public class BreakWallCommand : ICommand
{
    private GridManager gridManager;
    private StageSO stage;
    private Vector2Int position;
    private Unit attacker;
    private int recoilDamage;
    private int attackerPrevHP;
    private bool attackerWasDead;

    public BreakWallCommand(GridManager grid, StageSO stage, Vector2Int pos, Unit attacker = null, int recoilDamage = 0)
    {
        this.gridManager = grid;
        this.stage = stage;
        this.position = pos;
        this.attacker = attacker;
        this.recoilDamage = recoilDamage;
    }

    public void Execute()
    {
        gridManager.BreakWall(stage, position);

        if (attacker != null && recoilDamage > 0)
        {
            attackerPrevHP = attacker.HP.Value;
            attackerWasDead = attacker.IsDead;
            Debug.Log($"[BreakWall] Recoil Damage to {attacker.name}");
            attacker.TakeDamage(recoilDamage, attacker.CurrentPos);
        }
    }

    public void Undo()
    {
        gridManager.RestoreWall(stage, position);

        if (attacker != null && recoilDamage > 0)
        {
            attacker.SetHP(attackerPrevHP);
            if (!attackerWasDead && attacker.IsDead)
            {
                attacker.Revive();
            }
        }
    }
}
