using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using System.Threading;

public abstract class Unit : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float attackDistance = 0.7f;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private Vector2Int[] attackTargetPos = null!;
    [SerializeField] private Transform spriteTransform = null!;
    [SerializeField] private TextMeshPro hpText = null!;
    [SerializeField] private TextMeshPro orderText = null!;
    [SerializeField] private Transform hpTransform = null!;
    [SerializeField] private Transform orderTransform = null!;
    [SerializeField] private ColorFlash colorFlash = null!;
    [SerializeField] private HPSpriteAnimation hpSpriteAnimation = null!;
    [SerializeField] private OrderSpriteAnimation orderSpriteAnimation = null!;
    [SerializeField] private Transform maskTransform = null!;
    [SerializeField] private Transform maskedTransform = null!;
    protected UnitAnimator unitAnimator = null!;
    public UnitAnimator UnitAnimator => unitAnimator;
    protected GridManager gridManager = null!;
    protected TurnManager turnManager = null!;
    protected CommandManager commandManager = null!;
    protected Vector2Int currentPos;
    protected Direction currentDirection;
    protected bool isMoving = false;
    public Vector2Int CurrentPos => currentPos;
    public Direction CurrentDirection => currentDirection;
    public bool IsMoving => isMoving;


    private Vector3 maskOriginalPos;
    private Vector3 maskedOriginalPos;
    private Vector3 hpOriginalPos;
    private Vector3 orderOriginalPos;
    private int maxHP = 10;
    private int hp = 10;
    private int order = 0;
    protected bool isDead = false;
    public int MaxHP => maxHP;
    public int HP => hp;
    public int Order => order;
    public bool IsDead => isDead;
    public void SetHP(int value)
    {
        hp = value;
        if (hp < 0) hp = 0;
        if (hp > maxHP) hp = maxHP;
        hpText.text = hp.ToString();
    }

    protected virtual void Awake()
    {
        TryGetComponent(out unitAnimator);
    }

    private void Start()
    {
        maskOriginalPos = maskTransform.localPosition;
        maskedOriginalPos = maskedTransform.localPosition;
        hpOriginalPos = hpTransform.localPosition;
        orderOriginalPos = orderTransform.localPosition;
    }

    public virtual void Initialize(GridManager gridMgr,
                           TurnManager turnMgr,
                           CommandManager commandMgr,
                           Vector2Int startPos,
                           Direction startDirection,
                           int maxHp,
                           int startOrder)
    {
        gridManager = gridMgr;
        turnManager = turnMgr;
        commandManager = commandMgr;
        currentPos = startPos;
        currentDirection = startDirection;
        unitAnimator.SetDirection(currentDirection);
        maxHP = maxHp;
        hp = maxHp;
        hpText.text = hp.ToString();
        order = startOrder;
        orderText.text = (order + 1).ToString();
    }

    protected virtual void OnEnable()
    {
        unitAnimator.SetDirection(currentDirection);
    }

    public void SetDirection(Direction direction)
    {
        currentDirection = direction;
        unitAnimator.SetDirection(direction);
    }

    public void SetPosition(Vector2Int position)
    {
        currentPos = position;
        transform.position = gridManager.GridToWorldPosition(position);
    }

    protected async UniTask<bool> TryMove(Direction direction)
    {
        if (isMoving) return false;

        Vector2Int directionVector = Utility.GetVectorFromDirection(direction);
        Vector2Int targetPos = currentPos + directionVector;

        Vector2Int[] attackPos = new Vector2Int[attackTargetPos.Length];
        for (int i = 0; i < attackTargetPos.Length; i++)
        {
            attackPos[i] = currentPos + Utility.RotateVector(attackTargetPos[i], currentDirection);
            if (gridManager.GetUnitAtPosition(attackPos[i]) != null)
            {
                unitAnimator.SetDirection(direction);
                TryAttack();
                return false;
            }
        }

        if (!gridManager.IsCellWalkable(targetPos))
        {
            unitAnimator.SetDirection(direction);
            OnMoveFailed();
            return false;
        }

        // コマンドを作成して実行
        MoveCommand moveCommand = new MoveCommand(this, currentPos, targetPos, currentDirection, direction);
        commandManager.ExecuteCommand(moveCommand);
        return true; // 移動コマンドを実行した
    }

    public void MoveTo(Vector2Int position, Direction direction)
    {
        isMoving = true;
        orderSpriteAnimation.PlayAnimation();
        transform.DOLocalMove(gridManager.GridToWorldPosition(position), moveDuration).SetEase(Ease.OutCubic).OnComplete(() => isMoving = false) ;
        unitAnimator.SetDirection(direction);
        currentPos = position;
        currentDirection = direction;
        if (!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.move);
    }

    // プレイヤーではスペースキーを押した際と攻撃対象に対して移動しようとした際に実行される
    public void TryAttack()
    {
        //if (isMoving) return;

        Vector2Int[] attackPos = new Vector2Int[attackTargetPos.Length];
        for (int i = 0; i < attackTargetPos.Length; i++)
        {
            attackPos[i] = currentPos + Utility.RotateVector(attackTargetPos[i], currentDirection);
        }
        AttackCommand attackCommand = new AttackCommand(this, attackPos, gridManager);
        commandManager.ExecuteCommand(attackCommand);
    }

    protected virtual void OnActionComplete()
    {
        // Playerクラスでオーバーライドして使用
    }

    protected virtual void OnMoveFailed()
    {
        // Playerクラスでオーバーライドして使用
    }

    public void Attack(Vector2Int[] targetPosition)
    {
        AttackAsync(targetPosition).Forget();
    }

    private async UniTask AttackAsync(Vector2Int[] targetPosition)
    {
        isMoving = true;
        orderSpriteAnimation.PlayAnimation();
        Vector3 originalPos = spriteTransform.position;

        Vector2Int directionVector = Utility.GetVectorFromDirection(currentDirection);
        Vector3 attackOffset = new Vector3(directionVector.x, directionVector.y, 0) * attackDistance;

        Vector3 attackPos = originalPos + attackOffset;

        Sequence attackSequence = DOTween.Sequence();

        attackSequence
            .Append(spriteTransform.DOMove(attackPos, attackDuration * 0.3f).SetEase(Ease.InQuad))
            .AppendCallback(() =>
            {
                foreach (var pos in targetPosition)
                {
                    Unit targetUnit = gridManager.GetUnitAtPosition(pos);
                    if (targetUnit != null)
                    {
                        targetUnit.TakeDamage(1, currentPos);
                        if (this is Player) CameraManager.Instance.ShakeCamera(0.5f, 4f, Utility.GetVectorFromDirection(currentDirection));
                    }
                }
                if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);
            })
            .Append(spriteTransform.DOMove(originalPos, attackDuration * 0.7f).SetEase(Ease.OutQuad));

        await attackSequence.AsyncWaitForCompletion();

        isMoving = false;
        OnActionComplete();
    }

    public void TakeDamage(int damage, Vector2Int attackerPos)
    {
        Vector2 knockbackDirection = ((Vector2)currentPos - (Vector2)attackerPos).normalized;

        hp -= damage;
        hpSpriteAnimation.PlayAnimation();
        if(hp <= 0)
        {
            unitAnimator.SetDeath();
            hp = 0;
            Die(attackerPos);
            CameraManager.Instance.ShakeCamera(1f, 5f, knockbackDirection);
        }
        else
        {
            colorFlash.AnimateToColor(Utility.mainColors[2], flashDuration);

            if(this is Player) CameraManager.Instance.ShakeCamera(0.5f, 4f, knockbackDirection);
        }
        hpText.text = hp.ToString();
    }

    private CancellationTokenSource dieCancellationTokenSource;

    private void Die(Vector2Int attackerPos)
    {
        isDead = true;
        DieAsync(attackerPos).Forget();
    }

    private async UniTask DieAsync(Vector2Int attackerPos)
    {
        dieCancellationTokenSource?.Cancel();
        dieCancellationTokenSource?.Dispose();

        dieCancellationTokenSource = new CancellationTokenSource();
        var token = dieCancellationTokenSource.Token;

        try
        {
            KnockbackAsync(attackerPos).Forget();
            colorFlash.AnimateToColor(Utility.mainColors[0], flashDuration);
            await UniTask.Delay((int)(flashDuration * 1000), cancellationToken: token);
            await UniTask.Delay(200, cancellationToken: token);

            //maskedTransform.DOLocalMoveY(-50.0f, 0.4f).SetEase(Ease.InCubic);
            spriteTransform.DOLocalMoveY(-50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
            await UniTask.Delay(100, cancellationToken: token);
            hpTransform.DOLocalMoveY(hpOriginalPos.y - 50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
            await UniTask.Delay(50, cancellationToken: token);
            orderTransform.DOLocalMoveY(orderOriginalPos.y - 50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
        }
        catch (System.OperationCanceledException)
        {
            // キャンセルされた場合は何もしない
        }
    }
    // Undo時の復活処理
    public void Revive()
    {
        isDead = false;

        // 死亡アニメーションをキャンセル
        dieCancellationTokenSource?.Cancel();
        dieCancellationTokenSource?.Dispose();
        dieCancellationTokenSource = null;

        // 実行中のDOTweenアニメーションをキル
        spriteTransform.DOKill();
        hpTransform.DOKill();
        orderTransform.DOKill();
        maskTransform.DOKill();

        spriteTransform.localPosition = Vector3.zero;
        unitAnimator.SetDirection(currentDirection);
        spriteTransform.localScale = Vector3.one;
        maskTransform.localPosition = maskOriginalPos;
        maskedTransform.localPosition = maskedOriginalPos;
        hpTransform.localPosition = hpOriginalPos;
        orderTransform.localPosition = orderOriginalPos;
    }

    private async UniTask KnockbackAsync(Vector2Int attackerPos)
    {
        Vector3 originalPos = spriteTransform.position;
        //Vector3 maskOriginalPos = maskTransform.localPosition;

        Vector2 knockbackDirection = ((Vector2)currentPos - (Vector2)attackerPos).normalized;

        Vector3 knockbackOffset = new Vector3(knockbackDirection.x, knockbackDirection.y, 0) * attackDistance;

        spriteTransform.DOMove(originalPos + knockbackOffset, moveDuration * 0.5f).SetEase(Ease.InCubic);
        maskTransform.DOLocalMove(maskOriginalPos + knockbackOffset, moveDuration * 0.5f).SetEase(Ease.InCubic);
    }

    private void OnDestroy()
    {
        dieCancellationTokenSource?.Cancel();
        dieCancellationTokenSource?.Dispose();
    }

}