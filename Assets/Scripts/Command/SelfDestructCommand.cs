using UnityEngine;

public class SelfDestructCommand : ICommand
{
    private Enemy enemy;
    private int prevHP;
    private bool wasDead;
    private Vector2Int attackerPos;

    public SelfDestructCommand(Enemy enemy, Vector2Int attackerPos)
    {
        this.enemy = enemy;
        this.attackerPos = attackerPos;
    }

    private bool prevDropsCoin;
    private bool prevRegisterDefeated;

    public void Execute()
    {
        prevHP = enemy.HP.Value;
        wasDead = enemy.IsDead;
        prevDropsCoin = enemy.DropsCoin;
        prevRegisterDefeated = enemy.RegisterDefeated;

        enemy.SetDropsCoin(false);
        enemy.RegisterDefeated = false;
        enemy.TakeDamage(999, attackerPos, true);
    }

    public void Undo()
    {
        enemy.SetHP(prevHP);
        enemy.SetDropsCoin(prevDropsCoin);
        enemy.RegisterDefeated = prevRegisterDefeated;
        if (!wasDead && enemy.IsDead)
        {
            enemy.Revive();
        }
    }
}
