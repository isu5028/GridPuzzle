using UnityEngine;

public class ConsumeAmmoCommand : ICommand
{
    private Enemy enemy;
    private int amount;

    public ConsumeAmmoCommand(Enemy enemy, int amount = 1)
    {
        this.enemy = enemy;
        this.amount = amount;
    }

    public void Execute()
    {
        enemy.ConsumeAmmo(amount);
    }

    public void Undo()
    {
        enemy.RecoverAmmo(amount);
    }
}
