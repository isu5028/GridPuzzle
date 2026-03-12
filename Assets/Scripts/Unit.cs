using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using System.Threading;
using UniRx;
using System.Collections.Generic;

public abstract class Unit : MonoBehaviour
{
    [SerializeField] protected float moveDuration = 0.2f;
    [SerializeField] protected float attackDuration = 0.3f;
    [SerializeField] protected float attackDistance = 0.7f;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private Vector2Int[] attackTargetPos = null!;
    [SerializeField] protected Transform spriteTransform = null!;
    [SerializeField] protected TextMeshPro hpText = null!;
    [SerializeField] protected TextMeshPro orderText = null!;

    protected virtual System.Collections.Generic.IEnumerable<TextMeshPro> GetAdditionalTextsToFade()
    {
        yield break;
    }
    protected virtual System.Collections.Generic.IEnumerable<Transform> GetAdditionalTransformsToFade()
    {
        yield break;
    }
    [SerializeField] protected Transform hpTransform = null!;
    [SerializeField] private Transform orderTransform = null!;
    [SerializeField] private ColorFlash colorFlash = null!;
    [SerializeField] private HPSpriteAnimation hpSpriteAnimation = null!;
    [SerializeField] protected OrderSpriteAnimation orderSpriteAnimation = null!;
    [SerializeField] protected Transform defeatedIconTransform = null!; // 追加：倒された敵のアイコン
    [SerializeField] private Transform maskTransform = null!;
    [SerializeField] private Transform maskedTransform = null!;
    [SerializeField] protected UnitAnimator unitAnimator = null!;

    [Header("Death Animation")]
    [SerializeField] private Shader dissolveShader = null!;  // DitherDissolveシェーダーを設定

    // Public Accessor for Animator
    public UnitAnimator UnitAnimator => unitAnimator;
    public OrderSpriteAnimation OrderSpriteAnimation => orderSpriteAnimation;

    protected GridManager gridManager = null!;
    protected TurnManager turnManager = null!;
    protected CommandManager commandManager = null!;
    protected Vector2Int currentPos;
    protected Direction currentDirection;
    protected bool isMoving = false;
    public Vector2Int CurrentPos => currentPos;
    public Direction CurrentDirection => currentDirection;
    public bool IsMoving => isMoving;

    protected bool isAnimating = false;
    public bool IsAnimating => isAnimating; // Public accessor
    public bool IsBusy => isMoving || isAnimating; // Convenience accessor

    private Vector3 maskOriginalPos;
    private Vector3 maskedOriginalPos;
    private Vector3 hpOriginalPos;
    private Vector3 orderOriginalPos;
    private Vector3 defeatedIconOriginalPos;

    [SerializeField] protected int maxHP = 3;
    protected ReactiveProperty<int> hp = new ReactiveProperty<int>(3);
    public IReadOnlyReactiveProperty<int> HP => hp;
    public int MaxHP => maxHP;

    protected int order = 0;
    public int Order => order;

    [SerializeField] protected bool isDead = false;
    public bool IsDead => isDead;

    private CancellationTokenSource dieCancellationTokenSource;

    // 動的に生成したマテリアル（Dissolveアニメーション用）
    private Material dissolveMaterial;  // メインスプライト用
    private List<Material> dissolveMaterials = new List<Material>();  // HP/Order関連用

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
            UpdateHPText(x);
        }).AddTo(this);
    }

    protected virtual void InitializeVisuals()
    {
        if (visualsInitialized) return;

        maskOriginalPos = maskTransform.localPosition;
        maskedOriginalPos = maskedTransform.localPosition;
        hpOriginalPos = hpTransform != null ? hpTransform.localPosition : Vector3.zero;
        orderOriginalPos = orderTransform != null ? orderTransform.localPosition : Vector3.zero;
        defeatedIconOriginalPos = defeatedIconTransform != null ? defeatedIconTransform.localPosition : Vector3.zero;

        // DitherDissolveシェーダーからマテリアルを動的に生成
        if (dissolveShader != null)
        {
            // メインスプライトにdissolveMaterialを適用
            SpriteRenderer spriteRenderer = spriteTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                dissolveMaterial = CreateAndApplyDissolveMaterial(spriteRenderer);

                // ColorFlashにdissolveMaterialを通知（フラッシュ後に正しいマテリアルに戻るように）
                ColorFlash colorFlashComponent = spriteTransform.GetComponent<ColorFlash>();
                if (colorFlashComponent != null)
                {
                    colorFlashComponent.SetOriginalMaterial(dissolveMaterial);
                }
            }

            // HP関連のSpriteRendererにもdissolveMaterialを適用
            if (hpTransform != null)
            {
                foreach (var sr in hpTransform.GetComponentsInChildren<SpriteRenderer>())
                {
                    Material mat = CreateAndApplyDissolveMaterial(sr);
                    dissolveMaterials.Add(mat);
                }
            }

            // Order関連のSpriteRendererにもdissolveMaterialを適用
            if (orderTransform != null)
            {
                foreach (var sr in orderTransform.GetComponentsInChildren<SpriteRenderer>())
                {
                    Material mat = CreateAndApplyDissolveMaterial(sr);
                    dissolveMaterials.Add(mat);
                }
            }

            // Defeated Icon関連のSpriteRendererにもdissolveMaterialを適用
            if (defeatedIconTransform != null)
            {
                foreach (var sr in defeatedIconTransform.GetComponentsInChildren<SpriteRenderer>())
                {
                    Material mat = CreateAndApplyDissolveMaterial(sr);
                    dissolveMaterials.Add(mat);
                }
            }

            foreach (var t in GetAdditionalTransformsToFade())
            {
                if (t != null)
                {
                    foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>())
                    {
                        Material mat = CreateAndApplyDissolveMaterial(sr);
                        dissolveMaterials.Add(mat);
                    }
                }
            }

            // TextMeshProにもdissolveMaterialを適用
            if (hpText != null)
            {
                Material mat = CreateAndApplyDissolveMaterialForTMP(hpText);
                if (mat != null) dissolveMaterials.Add(mat);
            }
            if (orderText != null)
            {
                Material mat = CreateAndApplyDissolveMaterialForTMP(orderText);
                if (mat != null) dissolveMaterials.Add(mat);
            }

            foreach (var t in GetAdditionalTextsToFade())
            {
                if (t != null)
                {
                    Material mat = CreateAndApplyDissolveMaterialForTMP(t);
                    if (mat != null) dissolveMaterials.Add(mat);
                }
            }
        }
        else
        {
            // dissolveShaderがnullの場合でも、既存のマテリアルにDissolveAmountがあればリセット
            EnsureDissolveAmountReset();
        }

        visualsInitialized = true;
    }

    /// <summary>
    /// SpriteRenderer用のdissolveMaterialを作成して適用
    /// </summary>
    private Material CreateAndApplyDissolveMaterial(SpriteRenderer sr)
    {
        Material mat = new Material(dissolveShader);
        if (sr.sprite != null)
        {
            mat.SetTexture("_MainTex", sr.sprite.texture);
        }
        mat.SetFloat("_DissolveAmount", 0f);
        sr.material = mat;
        return mat;
    }

    /// <summary>
    /// 既存のマテリアルにDissolveAmountプロパティがあれば0にリセットする（dissolveShaderがnullの場合のフォールバック）
    /// </summary>
    private void EnsureDissolveAmountReset()
    {
        SpriteRenderer spriteRenderer = spriteTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.material != null)
        {
            if (spriteRenderer.material.HasProperty("_DissolveAmount"))
            {
                // インスタンスマテリアルを作成してDissolveAmountを0にリセット
                if (dissolveMaterial == null)
                {
                    dissolveMaterial = spriteRenderer.material;
                }
                dissolveMaterial.SetFloat("_DissolveAmount", 0f);
            }
        }
    }

    /// <summary>
    /// TextMeshPro用のdissolveMaterialを作成して適用
    /// </summary>
    private Material CreateAndApplyDissolveMaterialForTMP(TextMeshPro tmp)
    {
        if (tmp == null) return null;
        // TMPは複雑なので、fontMaterialにDissolve効果を追加するのは難しい
        // 代わりにTMPのアルファを操作するか、親のCanvasGroupを使う方法もある
        // ここではシンプルにnullを返し、DieAsyncでアルファ操作を行う
        return null;
    }

    /// <summary>
    /// DitherDissolveでフェードアウト（見えなくする）
    /// </summary>
    /// <param name="duration">アニメーション時間</param>
    /// <param name="goingDown">trueなら上から下へ、falseなら下から上へスイープ</param>
    public async UniTask FadeOutAsync(float duration = 0.3f, bool goingDown = true)
    {
        if (dissolveMaterial == null) return;

        // スイープ方向を設定（0=BottomLeft, 1=BottomRight, 2=TopRight, 3=TopLeft）
        // goingDown: 上から下へ → 2 (TopRight to BottomLeft)
        // goingUp: 下から上へ → 0 (BottomLeft to TopRight)
        float sweepDirection = goingDown ? 2f : 0f;
        dissolveMaterial.SetFloat("_SweepDirection", sweepDirection);

        await DOTween.To(() => dissolveMaterial.GetFloat("_DissolveAmount"),
                         x => dissolveMaterial.SetFloat("_DissolveAmount", x),
                         1f,
                         duration)
                     .SetEase(Ease.InQuad)
                     .SetTarget(dissolveMaterial)
                     .AsyncWaitForCompletion();
    }

    /// <summary>
    /// DitherDissolveでフェードイン（見えるようにする）
    /// </summary>
    /// <param name="duration">アニメーション時間</param>
    /// <param name="goingDown">trueなら上から下へ、falseなら下から上へスイープ</param>
    public async UniTask FadeInAsync(float duration = 0.3f, bool goingDown = true)
    {
        if (dissolveMaterial == null) return;

        // スイープ方向を設定（フェードインは逆方向からスイープ）
        // goingDown: 下から上へ復帰 → 0 (BottomLeft to TopRight)
        // goingUp: 上から下へ復帰 → 2 (TopRight to BottomLeft)
        float sweepDirection = goingDown ? 0f : 2f;
        dissolveMaterial.SetFloat("_SweepDirection", sweepDirection);

        await DOTween.To(() => dissolveMaterial.GetFloat("_DissolveAmount"),
                         x => dissolveMaterial.SetFloat("_DissolveAmount", x),
                         0f,
                         duration)
                     .SetEase(Ease.OutQuad)
                     .SetTarget(dissolveMaterial)
                     .AsyncWaitForCompletion();
    }

    /// <summary>
    /// DissolveAmountを即座に設定（アニメーションなし）
    /// </summary>
    public void SetDissolveAmount(float amount)
    {
        if (dissolveMaterial != null)
        {
            dissolveMaterial.SetFloat("_DissolveAmount", amount);
        }
    }

    public virtual void ApplyDeadStateVisual()
    {
        // まだ初期化されていなければ初期化する（ロード時などStartより先に呼ばれる場合用）
        InitializeVisuals();

        if (!isDead) return;

        // アニメーション停止
        spriteTransform.DOKill();
        if (hpTransform != null) hpTransform.DOKill();
        if (orderTransform != null) orderTransform.DOKill();
        if (defeatedIconTransform != null) defeatedIconTransform.DOKill();

        // DitherDissolveのDissolveAmountを1に設定（完全に消えた状態）
        if (dissolveMaterial != null)
        {
            dissolveMaterial.SetFloat("_DissolveAmount", 1f);
        }
        foreach (var mat in dissolveMaterials)
        {
            if (mat != null) mat.SetFloat("_DissolveAmount", 1f);
        }

        // TextMeshProのアルファを0に
        if (hpText != null) hpText.alpha = 0f;
        if (orderText != null) orderText.alpha = 0f;
        foreach (var t in GetAdditionalTextsToFade())
        {
            if (t != null) t.alpha = 0f;
        }

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
        UpdateHPText(maxHp);

        order = startOrder;
        UpdateOrderText(order);
    }


    public void SetDirection(Direction direction)
    {
        currentDirection = direction;
        unitAnimator.SetDirection(direction);
    }

    public void SetOrder(int order)
    {
        this.order = order;
        UpdateOrderText(order);
    }

    public void SetOrderText(string text)
    {
        if (orderText != null) orderText.text = text;
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

    public void MoveTo(Vector2Int position, Direction direction, bool playSound = true)
    {
        isMoving = true;
        orderSpriteAnimation.PlayAnimation();
        transform.DOMove(gridManager.GridToWorldPosition(position), moveDuration).SetEase(Ease.OutCubic).OnComplete(() => isMoving = false) ;
        unitAnimator.SetDirection(direction);
        currentPos = position;
        currentDirection = direction;
        if (!commandManager.IsUndoing && playSound) AudioManager.Instance.PlaySE(AudioManager.SEType.move);
    }

    public void TryAttack()
    {
        Debug.Log($"[Unit.TryAttack] Called on InstanceID: {this.GetInstanceID()}, gameObject: {gameObject.name}");

        // SetActive(false)された後にUniRx購読が同フレームで発火しても実行しない
        // （リセット時に旧プレイヤーが新しい敵にゴースト攻撃するのを防ぐ）
        if (!gameObject.activeInHierarchy) return;

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

    public void Attack(Vector2Int[] targetPosition, int recoilDamage = 0)
    {
        if (gameObject.activeInHierarchy)
        {
            AttackAsync(targetPosition, recoilDamage).Forget();
        }
        else
        {
            // Instant execution for off-screen calculation
            foreach (var pos in targetPosition)
            {
                Unit targetUnit = gridManager.GetUnitAtPosition(pos);
                if (targetUnit != null)
                {
                    targetUnit.TakeDamage(1, currentPos, false);
                }
            }
            if (recoilDamage > 0)
            {
                TakeDamage(recoilDamage, currentPos, false);
            }
        }
    }

    private async UniTask AttackAsync(Vector2Int[] targetPosition, int recoilDamage)
    {
        isMoving = true;
        orderSpriteAnimation.PlayAnimation();

        // Use Local Position for robustness against Parent movement
        Vector3 originalLocalPos = Vector3.zero; // Assuming sprite rests at zero locally

        Vector2Int directionVector = Utility.GetVectorFromDirection(currentDirection);
        Vector3 attackOffset = new Vector3(directionVector.x, directionVector.y, 0) * attackDistance;

        Vector3 attackLocalPos = originalLocalPos + attackOffset;

        Sequence attackSequence = DOTween.Sequence();

        attackSequence
            .Append(spriteTransform.DOLocalMove(attackLocalPos, attackDuration * 0.3f).SetEase(Ease.InQuad))
            .AppendCallback(() =>
            {
                Debug.Log($"[Unit.AttackAsync Callback] Called by {name} (Active: {gameObject?.activeInHierarchy}), Target Pos: {targetPosition[0]}");

                if (this == null || !gameObject.activeInHierarchy)
                {
                    Debug.Log($"[Unit.AttackAsync Callback] Cancelled because attacker {name} is null or inactive.");
                    return;
                }

                foreach (var pos in targetPosition)
                {
                    Unit targetUnit = gridManager.GetUnitAtPosition(pos);
                    if (targetUnit != null)
                    {
                        Debug.Log($"[Unit.AttackAsync Callback] Attacking {targetUnit.name} at {pos}");
                        targetUnit.TakeDamage(1, currentPos);
                        if (this is Player) CameraManager.Instance.ShakeCamera(0.4f, 8.0f, 20f, Utility.GetVectorFromDirection(currentDirection));
                    }
                    else
                    {
                        Debug.Log($"[Unit.AttackAsync Callback] No target found at {pos}");
                    }
                }
                if (recoilDamage > 0)
                {
                    TakeDamage(recoilDamage, currentPos);
                }

                if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);
            })
            .Append(spriteTransform.DOLocalMove(originalLocalPos, attackDuration * 0.7f).SetEase(Ease.OutQuad))
            .SetLink(gameObject, DG.Tweening.LinkBehaviour.KillOnDisable); // SetActive(false)で即座に停止

        await attackSequence.AsyncWaitForCompletion();

        isMoving = false;
        OnActionComplete();
    }

    public virtual void TakeDamage(int damage, Vector2Int attackerPos, bool shakeCamera = true)
    {
        Debug.Log($"[Unit.TakeDamage] {name} took {damage} damage from {attackerPos}! Current HP: {hp.Value}");
        Vector2 knockbackDirection = ((Vector2)currentPos - (Vector2)attackerPos).normalized;

        hp.Value -= damage;

        if (gameObject.activeInHierarchy)
        {
            hpSpriteAnimation.PlayAnimation();
        }

        // Force Visual Update even if inactive
        UpdateHPText(hp.Value);

        if(hp.Value <= 0)
        {
            unitAnimator.SetDeath();
            hp.Value = 0;
            Die(attackerPos);
            if(gameObject.activeInHierarchy && shakeCamera) CameraManager.Instance.ShakeCamera(0.5f, 5.0f, 30f, knockbackDirection);
        }
        else
        {
            if (gameObject.activeInHierarchy)
            {
                colorFlash.AnimateToColor(Utility.mainColors[2], flashDuration);
                if(this is Player && shakeCamera) CameraManager.Instance.ShakeCamera(0.3f, 2.0f, 50f, knockbackDirection);
            }
        }
    }

    private void Die(Vector2Int attackerPos)
    {
        isDead = true;
        if (this is Player)
        {
            AudioManager.Instance.SetMasterLowPassFilter(500);
        }

        // 死亡時に即座に状態変化を通知（全滅判定を即時行うため）
        if (gridManager != null)
        {
             gridManager.UnitManager.NotifyUnitStateChanged(this);
        }

        if (gameObject.activeInHierarchy)
        {
            DieAsync(attackerPos).Forget();
        }
        else
        {
            // ...
        }
    }

    protected virtual async UniTask DieAsync(Vector2Int attackerPos)
    {
        transform.DOKill(); // Stop movement (parent)
        isMoving = false; // Reset logical movement state as tween is killed
        dieCancellationTokenSource?.Cancel();
        dieCancellationTokenSource?.Dispose();

        dieCancellationTokenSource = new CancellationTokenSource();
        var token = dieCancellationTokenSource.Token;

        isAnimating = true; // Mark as animating death
        try
        {
            KnockbackAsync(attackerPos).Forget();
            colorFlash.AnimateToColor(Utility.mainColors[0], flashDuration);
            await UniTask.Delay((int)(flashDuration * 1000), cancellationToken: token);
            await UniTask.Delay(200, cancellationToken: token);

            // DitherDissolveアニメーション
            float duration = 0.5f;

            // メインスプライト
            if (dissolveMaterial != null)
            {
                DOTween.To(() => dissolveMaterial.GetFloat("_DissolveAmount"),
                           x => dissolveMaterial.SetFloat("_DissolveAmount", x),
                           1f,
                           duration)
                       .SetEase(Ease.InQuad)
                       .SetTarget(dissolveMaterial)  // DOTween.Kill(dissolveMaterial)で停止できるように
                       .SetLink(gameObject);
            }

            // HP/Order関連のスプライト
            foreach (var mat in dissolveMaterials)
            {
                if (mat != null)
                {
                    DOTween.To(() => mat.GetFloat("_DissolveAmount"),
                               x => mat.SetFloat("_DissolveAmount", x),
                               1f,
                               duration)
                           .SetEase(Ease.InQuad)
                           .SetTarget(mat)  // DOTween.Kill(mat)で停止できるように
                           .SetLink(gameObject);
                }
            }

            // TextMeshProのアルファをフェードアウト
            if (hpText != null)
            {
                DOTween.To(() => hpText.alpha, x => hpText.alpha = x, 0f, duration)
                       .SetEase(Ease.InQuad)
                       .SetTarget(hpText)  // DOTween.Kill(hpText)で停止できるように
                       .SetLink(gameObject);
            }
            if (orderText != null)
            {
                DOTween.To(() => orderText.alpha, x => orderText.alpha = x, 0f, duration)
                       .SetEase(Ease.InQuad)
                       .SetTarget(orderText)  // DOTween.Kill(orderText)で停止できるように
                       .SetLink(gameObject);
            }
            foreach (var t in GetAdditionalTextsToFade())
            {
                if (t != null)
                {
                    DOTween.To(() => t.alpha, x => t.alpha = x, 0f, duration)
                           .SetEase(Ease.InQuad)
                           .SetTarget(t)  // DOTween.Kill(t)で停止できるように
                           .SetLink(gameObject);
                }
            }
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
             isAnimating = false;
        }
    }

    // Undo時の復活処理
    public virtual void Revive()
    {
        isDead = false;
        if (this is Player)
        {
            AudioManager.Instance.SetMasterLowPassFilter(20000);
        }

        isAnimating = false; // Reset animation state on revive

        // 死亡アニメーションをキャンセル
        dieCancellationTokenSource?.Cancel();
        dieCancellationTokenSource?.Dispose();
        dieCancellationTokenSource = null;

        // 実行中のDOTweenアニメーションをキル
        spriteTransform.DOKill();
        if (hpTransform != null) hpTransform.DOKill();
        if (orderTransform != null) orderTransform.DOKill();
        if (defeatedIconTransform != null) defeatedIconTransform.DOKill();
        maskTransform.DOKill();

        // マテリアルのDOTween（DissolveAmountアニメーション）もキル
        if (dissolveMaterial != null)
        {
            DOTween.Kill(dissolveMaterial);
        }
        foreach (var mat in dissolveMaterials)
        {
            if (mat != null) DOTween.Kill(mat);
        }

        // TextMeshProのDOTweenもキル
        if (hpText != null) DOTween.Kill(hpText);
        if (orderText != null) DOTween.Kill(orderText);
        foreach (var t in GetAdditionalTextsToFade())
        {
            if (t != null) DOTween.Kill(t);
        }

        spriteTransform.localPosition = Vector3.zero;
        unitAnimator.SetDirection(currentDirection);
        spriteTransform.localScale = Vector3.one;
        maskTransform.localPosition = maskOriginalPos;
        maskedTransform.localPosition = maskedOriginalPos;

        // DitherDissolveのDissolveAmountを0にリセット（表示状態に戻す）
        if (dissolveMaterial != null)
        {
            dissolveMaterial.SetFloat("_DissolveAmount", 0f);
        }
        foreach (var mat in dissolveMaterials)
        {
            if (mat != null) mat.SetFloat("_DissolveAmount", 0f);
        }

        // TextMeshProのアルファを1に戻す
        if (hpText != null) hpText.alpha = 1f;
        if (orderText != null) orderText.alpha = 1f;
        foreach (var t in GetAdditionalTextsToFade())
        {
            if (t != null) t.alpha = 1f;
        }

        if (hpOriginalPos == Vector3.zero && hpTransform != null && hpTransform.localPosition != Vector3.zero)
             hpOriginalPos = hpTransform.localPosition;

        if (orderOriginalPos == Vector3.zero && orderTransform != null && orderTransform.localPosition != Vector3.zero)
             orderOriginalPos = orderTransform.localPosition;

        if (defeatedIconOriginalPos == Vector3.zero && defeatedIconTransform != null && defeatedIconTransform.localPosition != Vector3.zero)
             defeatedIconOriginalPos = defeatedIconTransform.localPosition;

        if (hpTransform != null) hpTransform.localPosition = hpOriginalPos;
        if (orderTransform != null) orderTransform.localPosition = orderOriginalPos;
        if (defeatedIconTransform != null) defeatedIconTransform.localPosition = defeatedIconOriginalPos;
    }

    private async UniTask KnockbackAsync(Vector2Int attackerPos)
    {
        Vector3 originalPos = spriteTransform.position;

        Vector2 knockbackDirection = ((Vector2)currentPos - (Vector2)attackerPos).normalized;

        Vector3 knockbackOffset = new Vector3(knockbackDirection.x, knockbackDirection.y, 0) * attackDistance;

        spriteTransform.DOMove(originalPos + knockbackOffset, moveDuration * 0.5f).SetEase(Ease.InCubic);
        maskTransform.DOLocalMove(maskOriginalPos + knockbackOffset, moveDuration * 0.5f).SetEase(Ease.InCubic);
    }

    protected virtual void UpdateHPText(int value)
    {
        if (hpText != null) hpText.text = value.ToString();
    }

    protected virtual void UpdateOrderText(int value)
    {
        if (orderText != null) orderText.text = (value + 1).ToString();
    }

    private void OnDestroy()
    {
        dieCancellationTokenSource?.Cancel();
        dieCancellationTokenSource?.Dispose();
    }
}