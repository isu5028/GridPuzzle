using UnityEngine;
using Cysharp.Threading.Tasks;

public class Crate : Unit
{
    public override void Initialize(GridManager gridMgr, TurnManager turnMgr, CommandManager commandMgr, Vector2Int startPos, Direction startDirection, int maxHp, int startOrder)
    {
        base.Initialize(gridMgr, turnMgr, commandMgr, startPos, startDirection, 1, 0);

        // 箱はHP表記やOrderを持たないため、UIを無効にする
        if (hpText != null) hpText.gameObject.SetActive(false);
        if (orderText != null) orderText.gameObject.SetActive(false);
        if (hpTransform != null) hpTransform.gameObject.SetActive(false);
        if (orderSpriteAnimation != null) orderSpriteAnimation.gameObject.SetActive(false);

        // isEnemyフラグ等を無効化する（もし実装されていれば）
    }

    public override void TakeDamage(int damage, Vector2Int attackerPos, bool shakeCamera = true)
    {
        // プレイヤーの通常攻撃等ではダメージを受けない
    }

    /// <summary>
    /// 壁破壊攻撃など、特別な攻撃によって破壊される
    /// </summary>
    public void BreakCrate(Vector2Int attackerPos)
    {
        base.TakeDamage(999, attackerPos, false);
    }

    protected override void OnActionComplete()
    {
    }

    protected override void OnMoveComplete()
    {
        if (commandManager.IsUndoing) return;

        StageSO crateStage = gridManager.GetStageOfCrate(this) ?? gridManager.CurrentStageSOInternal;
        CellType currentCell = gridManager.GetCellTypeAt(crateStage, currentPos);
        if (currentCell == CellType.StairUp || currentCell == CellType.StairDown)
        {
            gridManager.MoveCrateThroughStairAsync(this).Forget();
        }
    }

    public void WarpTo(Vector2Int pos, Direction dir)
    {
        currentPos = pos;
        currentDirection = dir;
        transform.position = gridManager.GridToWorldPosition(pos);
        if (unitAnimator != null)
        {
            unitAnimator.SetDirection(dir);
        }
    }
}
