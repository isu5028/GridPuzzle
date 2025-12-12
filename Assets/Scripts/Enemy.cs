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
    public void PerformRangedAttack(Vector2Int targetPos, Unit targetUnit, Transform arrowPrefab)
    {
        PerformRangedAttackAsync(targetPos, targetUnit, arrowPrefab).Forget();
    }

    private async UniTask PerformRangedAttackAsync(Vector2Int targetPos, Unit targetUnit, Transform arrowPrefab)
    {
        isMoving = true;
        orderSpriteAnimation.PlayAnimation();

        Vector3 originalPos = spriteTransform.position;
        Vector2 directionVec = Utility.GetVectorFromDirection(currentDirection);
        Vector3 attackOffset = new Vector3(directionVec.x, directionVec.y, 0) * attackDistance * 0.5f; // Pull back slightly or move forward slightly? Usually move forward.

        // Attack Animation (Lunging forward)
        Sequence sequence = DOTween.Sequence();
        sequence
            .Append(spriteTransform.DOMove(originalPos + attackOffset, attackDuration * 0.3f).SetEase(Ease.InQuad));

        await sequence.AsyncWaitForCompletion();

        // Spawn and launch Arrow
        if (arrowPrefab != null)
        {
            if (!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);

            // Instantiate arrow at current position
            // Adjust position slightly to be in front of enemy
            Vector3 spawnPos = transform.position;
            Transform arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
            Arrow arrowScript = arrowObj.GetComponent<Arrow>();
            if (arrowScript != null)
            {
                var source = new UniTaskCompletionSource();
                Vector3 targetWorldPos = gridManager.GridToWorldPosition(targetPos);

                arrowScript.Launch(spawnPos, targetWorldPos, () => {
                    // Apply Damage IMMEDIATELY when arrow reaches target
                    if (targetUnit != null)
                    {
                        targetUnit.TakeDamage(1, currentPos);
                    }
                    source.TrySetResult();
                });

                await source.Task;
            }
            else
            {
                // Fallback if script missing
                await UniTask.Delay(200);
            }
        }
        else
        {
             if (!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);
             // Fallback damage if no arrow
             if (targetUnit != null)
             {
                 targetUnit.TakeDamage(1, currentPos);
             }
        }

        // Return to original pos
        await spriteTransform.DOMove(originalPos, attackDuration * 0.7f).SetEase(Ease.OutQuad).AsyncWaitForCompletion();

        isMoving = false;
    }
}