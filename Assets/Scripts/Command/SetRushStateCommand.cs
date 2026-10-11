using UnityEngine;

public class SetRushStateCommand : ICommand
{
    private EnemyAIRush ai;
    private Enemy enemy;
    private int prevState;
    private int newState;
    private Direction prevDirection;

    public SetRushStateCommand(EnemyAIRush ai, int newState)
    {
        this.ai = ai;
        this.enemy = ai != null ? ai.GetComponent<Enemy>() : null;
        this.prevState = ai.GetState();
        this.newState = newState;
        this.prevDirection = enemy != null ? enemy.CurrentDirection : Direction.Right;
    }

    public void Execute()
    {
        ai.SetState(newState);
    }

    public void Undo()
    {
        ai.SetState(prevState);

        // prevState が Idle の場合でも、チャージ開始時に変えた向きを確実に戻す
        if (enemy != null)
        {
            enemy.SetDirection(prevDirection);
        }
    }
}
