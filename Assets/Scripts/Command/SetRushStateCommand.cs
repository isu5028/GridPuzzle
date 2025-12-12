using UnityEngine;

public class SetRushStateCommand : ICommand
{
    private EnemyAIRush ai;
    private int prevState;
    private int newState;

    public SetRushStateCommand(EnemyAIRush ai, int newState)
    {
        this.ai = ai;
        this.prevState = ai.GetState();
        this.newState = newState;
    }

    public void Execute()
    {
        ai.SetState(newState);
    }

    public void Undo()
    {
        ai.SetState(prevState);
    }
}
