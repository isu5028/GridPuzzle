using Cysharp.Threading.Tasks;

public class EnemyAICrate : EnemyAI
{
    public override UniTask ExecuteAI(Enemy enemy)
    {
        // 箱は何もしない
        return UniTask.CompletedTask;
    }

    public override string GetHPText(int hp)
    {
        return string.Empty; // HPを表示しない
    }

    public override string GetOrderText(int order)
    {
        return string.Empty; // Orderを表示しない
    }
}
