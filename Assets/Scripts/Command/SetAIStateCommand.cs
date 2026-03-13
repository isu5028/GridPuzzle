using UnityEngine;

public class SetAIStateCommand : ICommand
{
    private EnemyAI ai;
    private int prevState;
    private int newState;

    public SetAIStateCommand(EnemyAI ai, int newState)
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
