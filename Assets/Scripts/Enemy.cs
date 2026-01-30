using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using TMPro;
using UniRx;

public class Enemy : Unit
{
    [SerializeField] private EnemyType type;
    public EnemyType Type { get { return type; } }
    private EnemyAI enemyAI = null!;
    public EnemyAI EnemyAI => enemyAI;

    [SerializeField] private TextMeshPro ammoText = null!;
    [SerializeField] private Transform ammoTransform = null!;
    [SerializeField] private AmmoSpriteAnimation ammoSpriteAnimation = null!;
    private Vector3 ammoOriginalPos; // Store original position for revival

    private int maxAmmo = -1;
    protected ReactiveProperty<int> currentAmmo = new ReactiveProperty<int>(-1);
    public IReadOnlyReactiveProperty<int> CurrentAmmo => currentAmmo;

    public override void Initialize(GridManager gridMgr,
                           TurnManager turnMgr,
                           CommandManager commandMgr,
                           Vector2Int startPos,
                           Direction startDirection,
                           int maxHp,
                           int startOrder)
    {
        Initialize(gridMgr, turnMgr, commandMgr, startPos, startDirection, maxHp, startOrder, -1);
    }

    public void Initialize(GridManager gridMgr,
                           TurnManager turnMgr,
                           CommandManager commandMgr,
                           Vector2Int startPos,
                           Direction startDirection,
                           int maxHp,
                           int startOrder,
                           int startAmmo)
    {
        base.Initialize(gridMgr, turnMgr, commandMgr, startPos, startDirection, maxHp, startOrder);
        enemyAI = GetComponent<EnemyAI>();
        enemyAI.Initialize(gridMgr, commandMgr);

        // Update Text again to apply AI overrides (e.g. infinite symbol)
        UpdateHPText(maxHp);
        UpdateOrderText(startOrder);

        maxAmmo = startAmmo;
        currentAmmo.Value = startAmmo;

        if (ammoText != null)
        {
            if (startAmmo >= 0)
            {
                ammoText.gameObject.SetActive(true);
                ammoText.text = startAmmo.ToString();
            }
            else
            {
                ammoText.gameObject.SetActive(false);
            }
        }

        // Subscribe only if using ammo to check updates
        if (startAmmo >= 0)
        {
            currentAmmo.Subscribe(x =>
            {
                if (ammoText != null) ammoText.text = x.ToString();
                if (ammoSpriteAnimation != null) ammoSpriteAnimation.PlayAnimation();
            }).AddTo(this);
        }
    }

    public void SetAmmo(int value)
    {
        if (maxAmmo < 0) return; // Not using ammo
        if (value < 0) value = 0;
        if (value > maxAmmo) value = maxAmmo;
        currentAmmo.Value = value;
    }

    public void ConsumeAmmo(int amount = 1)
    {
        if (maxAmmo < 0) return;
        SetAmmo(currentAmmo.Value - amount);
    }

    public void RecoverAmmo(int amount = 1)
    {
        if (maxAmmo < 0) return;
        SetAmmo(currentAmmo.Value + amount);
    }

    public bool HasAmmo()
    {
        if (maxAmmo < 0) return true; // Infinite if not set (or handle as restricted? User said "only ChaserRange has it", others ignore)
        // If maxAmmo is -1, it means "No Ammo System".
        // The user requirement: "EnemyChaserRangeを持つUnitだけにはint型のAmmo...0になると攻撃も行動も行わなくなる"
        // So for ChaserRange, maxAmmo will be >= 0.
        // For others, maxAmmo is -1.
        // If maxAmmo == -1, HasAmmo should be true (unrestricted).
        return currentAmmo.Value > 0;
    }

    public async UniTask ExecuteTurn()
    {
        await enemyAI.ExecuteAI(this);
    }

    public override void TakeDamage(int damage, Vector2Int attackerPos, bool shakeCamera = true)
    {
        int effectiveDamage = damage;
        if (enemyAI != null)
        {
            effectiveDamage = enemyAI.ModifyDamage(damage);
            enemyAI.OnDamageTaken(this, attackerPos);
        }
        base.TakeDamage(effectiveDamage, attackerPos, shakeCamera);
    }

    protected override void InitializeVisuals()
    {
        base.InitializeVisuals();
        if (ammoTransform != null)
        {
            ammoOriginalPos = ammoTransform.localPosition;
        }
    }

    public override void ApplyDeadStateVisual()
    {
        base.ApplyDeadStateVisual();
        if (!isDead) return;

        if (ammoTransform != null)
        {
            ammoTransform.DOKill();
            ammoTransform.localPosition = new Vector3(ammoOriginalPos.x, ammoOriginalPos.y - 50.0f, ammoOriginalPos.z);
        }
    }

    protected override async UniTask DieAsync(Vector2Int attackerPos)
    {
        // Start Ammo animation in parallel with base animation
        // Timing: Flash(150)+Delay(200) = 350 -> Sprite
        // 350 + 100 = 450 -> HP
        // 450 + 50 = 500 -> Order
        // Let's do 500 + 50 = 550 for Ammo

        AnimateDieAmmo().Forget();

        await base.DieAsync(attackerPos);
    }

    private async UniTask AnimateDieAmmo()
    {
        if (ammoTransform == null) return;

        try
        {
             // Wait for roughly the time when Order finishes starting or slightly after
             // Flash (150) + 200 + 100 + 50 = 500ms is when Order starts.
             // Let's wait 550ms.
             await UniTask.Delay(550, cancellationToken: this.GetCancellationTokenOnDestroy());

             ammoTransform.DOLocalMoveY(ammoOriginalPos.y - 50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
        }
        catch (System.OperationCanceledException) { }
    }

    public override void Revive()
    {
        base.Revive();
        if (ammoTransform != null)
        {
            ammoTransform.DOKill();

            if (ammoOriginalPos == Vector3.zero && ammoTransform.localPosition != Vector3.zero)
                 ammoOriginalPos = ammoTransform.localPosition;

            ammoTransform.localPosition = ammoOriginalPos;
        }
        if (ammoSpriteAnimation != null)
        {
            ammoSpriteAnimation.transform.localScale = Vector3.one; // Reset scale if stuck
        }
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

    public int GetAIState()
    {
        return enemyAI != null ? enemyAI.GetState() : 0;
    }

    public void SetAIState(int state)
    {
        if (enemyAI != null) enemyAI.SetState(state);
    }

    public void ReviveSilent()
    {
        isDead = false;
        gameObject.SetActive(true);
        unitAnimator.SetDirection(currentDirection);
    }

    protected override void UpdateHPText(int value)
    {
        if (enemyAI != null)
        {
            string customText = enemyAI.GetHPText(value);
            if (customText != null)
            {
                if (hpText != null) hpText.text = customText;
                return;
            }
        }
        base.UpdateHPText(value);
    }

    protected override void UpdateOrderText(int value)
    {
        if (enemyAI != null)
        {
            string customText = enemyAI.GetOrderText(value);
            if (customText != null)
            {
                if (orderText != null) orderText.text = customText;
                return;
            }
        }
        base.UpdateOrderText(value);
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