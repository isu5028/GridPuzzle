using UnityEngine;
using Cysharp.Threading.Tasks;

public class EnemyAICrystal : EnemyAI
{
    public override int ModifyDamage(int damage)
    {
        // 死亡しない (Take 0 damage)
        return 0;
    }

    public override void OnDamageTaken(Enemy enemy, Vector2Int attackerPos)
    {
        // 攻撃を受けたらパレットを変更
        if (PaletteManager.Instance != null)
        {
            PaletteManager.Instance.NextPalette();
            SteamAchievementHelper.TryUnlockChangeColorAchievement();
        }

        // Camera Shake (Similar to Player Attack)
        if (CameraManager.Instance != null && enemy.gameObject.activeInHierarchy)
        {
             Vector2 knockbackDirection = ((Vector2)enemy.CurrentPos - (Vector2)attackerPos).normalized;
             // Player Attack Shake: 0.4f, 8.0f, 20f
             CameraManager.Instance.ShakeCamera(0.4f, 8.0f, 20f, knockbackDirection);
        }
    }

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        // 向きは常に右向き
        if (enemy.CurrentDirection != Direction.Right)
        {
            enemy.SetDirection(Direction.Right);
        }

        // 移動もしない、攻撃もしない
        await UniTask.CompletedTask;
    }

    public override string GetHPText(int hp)
    {
        return "∞";
    }

    public override string GetOrderText(int order)
    {
        return "∞";
    }
}
