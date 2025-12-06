using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using System.Threading;
using UniRx;

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
    [SerializeField] protected UnitAnimator unitAnimator = null!;

    // Public Accessor for Animator
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
    // UniRx ReactiveProperty
    private IntReactiveProperty hp = new IntReactiveProperty(10);
    public IReadOnlyReactiveProperty<int> HP => hp;
    public int MaxHP => maxHP;

    private int order = 0;
    public int Order => order;

    [SerializeField] protected bool isDead = false;
    public bool IsDead => isDead;

    private CancellationTokenSource dieCancellationTokenSource;

    public void SetHP(int value)
    {
        if (value < 0) value = 0;
        if (value > maxHP) value = maxHP;
        hp.Value = value;
    }

    private bool visualsInitialized = false;

    protected virtual void Start()
    {
        InitializeVisuals();

        // UniRx Subscribe
        hp.Subscribe(x =>
        {
            if(hpText != null) hpText.text = x.ToString();
        }).AddTo(this);
    }

    protected void InitializeVisuals()
    {
        if (visualsInitialized) return;

        maskOriginalPos = maskTransform.localPosition;
        maskedOriginalPos = maskedTransform.localPosition;
        hpOriginalPos = hpTransform.localPosition;
        orderOriginalPos = orderTransform.localPosition;

        visualsInitialized = true;
    }

    public void ApplyDeadStateVisual()
    {
        // まだ初期化されていなければ初期化する（ロード時などStartより先に呼ばれる場合用）
        InitializeVisuals();

        if (!isDead) return;

        // アニメーション停止
        spriteTransform.DOKill();
        hpTransform.DOKill();
        orderTransform.DOKill();

        // 死亡時の位置へ移動（DieAsyncと同じ状態にする）
        spriteTransform.localPosition = new Vector3(spriteTransform.localPosition.x, -50.0f, spriteTransform.localPosition.z);
        hpTransform.localPosition = new Vector3(hpOriginalPos.x, hpOriginalPos.y - 50.0f, hpOriginalPos.z);
        orderTransform.localPosition = new Vector3(orderOriginalPos.x, orderOriginalPos.y - 50.0f, orderOriginalPos.z);

        unitAnimator.SetDeath();
    }

    protected virtual void OnEnable()
    {

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
        hp.Value = maxHp;

        order = startOrder;
        orderText.text = (order + 1).ToString();
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
        return true;
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

    public void TryAttack()
    {
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
    }

    protected virtual void OnMoveFailed()
    {
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

        hp.Value -= damage;

        hpSpriteAnimation.PlayAnimation();
        if(hp.Value <= 0)
        {
            unitAnimator.SetDeath();
            hp.Value = 0;
            Die(attackerPos);
            CameraManager.Instance.ShakeCamera(1f, 5f, knockbackDirection);
        }
        else
        {
            colorFlash.AnimateToColor(Utility.mainColors[2], flashDuration);

            if(this is Player) CameraManager.Instance.ShakeCamera(0.5f, 4f, knockbackDirection);
        }
    }

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

            spriteTransform.DOLocalMoveY(-50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
            await UniTask.Delay(100, cancellationToken: token);
            // ここでOriginalPosからの相対移動を行っているが、Revive時にOriginalPosへ戻す際に
            // spriteTransformのpositionがゼロリセットされた後にローカル座標系がずれていないか注意
            hpTransform.DOLocalMoveY(hpOriginalPos.y - 50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
            await UniTask.Delay(50, cancellationToken: token);
            orderTransform.DOLocalMoveY(orderOriginalPos.y - 50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
        }
        catch (System.OperationCanceledException)
        {
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

        if (hpOriginalPos == Vector3.zero && hpTransform.localPosition != Vector3.zero)
             hpOriginalPos = hpTransform.localPosition;

        if (orderOriginalPos == Vector3.zero && orderTransform.localPosition != Vector3.zero)
             orderOriginalPos = orderTransform.localPosition;

        hpTransform.localPosition = hpOriginalPos;
        orderTransform.localPosition = orderOriginalPos;
    }

    private async UniTask KnockbackAsync(Vector2Int attackerPos)
    {
        Vector3 originalPos = spriteTransform.position;

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