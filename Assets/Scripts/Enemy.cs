using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;

public class Enemy : Unit
{
    [SerializeField] private EnemyType type;
    public EnemyType Type { get { return type; } }
    private EnemyAI enemyAI = null!;

    public override void Initialize(GridManager gridMgr,
                           TurnManager turnMgr,
                           CommandManager commandMgr,
                           Vector2Int startPos,
                           Direction startDirection,
                           int maxHp,
                           int startOrder)
    {
        base.Initialize(gridMgr, turnMgr, commandMgr, startPos, startDirection, maxHp, startOrder);
        enemyAI = GetComponent<EnemyAI>();
        enemyAI.Initialize(gridMgr, commandMgr);
    }

    public async UniTask ExecuteTurn()
    {
        await enemyAI.ExecuteAI(this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    public void ForceKillSilent()
    {
        isDead = true;
        ApplyDeadStateVisual();
        gameObject.SetActive(false); // 見た目を消す
    }

    // 強制移動（アニメーションなし）
    public void WarpTo(Vector2Int pos, Direction dir)
    {
        currentPos = pos;
        currentDirection = dir;
        transform.position = gridManager.GridToWorldPosition(pos);
        // Animatorの向き更新など
        unitAnimator.SetDirection(dir);
    }

    public void ReviveSilent()
    {
        isDead = false;
        gameObject.SetActive(true);
        unitAnimator.SetDirection(currentDirection);
    }
    public void PerformRangedAttack(Vector2Int targetPos, Unit targetUnit)
    {
        PerformRangedAttackAsync(targetPos, targetUnit).Forget();
    }

    private async UniTask PerformRangedAttackAsync(Vector2Int targetPos, Unit targetUnit)
    {
        // 攻撃アニメーション (その場で少し揺れるなど)
        Vector3 originalPos = transform.localPosition;
        Vector2 directionVec = Utility.GetVectorFromDirection(currentDirection);
        Vector3 shakeOffset = new Vector3(directionVec.x, directionVec.y, 0) * 0.2f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOLocalMove(originalPos + shakeOffset, 0.1f).SetEase(Ease.OutQuad));
        sequence.Append(transform.DOLocalMove(originalPos, 0.1f).SetEase(Ease.InQuad));

        // 発射音
        if (!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);

        await sequence.AsyncWaitForCompletion();

        // ダメージ適用
        if (targetUnit != null)
        {
            targetUnit.TakeDamage(1, currentPos);
            // エフェクトなどあればここ
        }
    }
}