using Cysharp.Threading.Tasks;

public class EnemyAIKing : EnemyAI
{
    public override UniTask ExecuteAI(Enemy enemy)
    {
        return UniTask.CompletedTask;
    }
}
