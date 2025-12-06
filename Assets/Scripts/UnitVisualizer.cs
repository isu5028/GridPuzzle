using UnityEngine;
using DG.Tweening;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class UnitVisualizer : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private float damageShakeStrength = 5f;
    [SerializeField] private float attackShakeStrength = 4f;

    [Header("Visual Components")]
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
    [SerializeField] private UnitAnimator unitAnimator = null!;

    public float MoveDuration => moveDuration;
    public UnitAnimator Animator => unitAnimator; // Expose for specific control if needed

    private Vector3 maskOriginalPos;
    private Vector3 maskedOriginalPos;
    private Vector3 hpOriginalPos;
    private Vector3 orderOriginalPos;

    private void Awake()
    {
        if (unitAnimator == null) TryGetComponent(out unitAnimator);
    }

    private void Start()
    {
        if (maskTransform != null) maskOriginalPos = maskTransform.localPosition;
        if (maskedTransform != null) maskedOriginalPos = maskedTransform.localPosition;
        if (hpTransform != null) hpOriginalPos = hpTransform.localPosition;
        if (orderTransform != null) orderOriginalPos = orderTransform.localPosition;
    }

    public void Initialize(Direction startDirection, int currentHP, int maxHP, int order)
    {
        unitAnimator.SetDirection(startDirection);
        UpdateHPText(currentHP);
        UpdateOrderText(order);
    }

    public void SetDirection(Direction direction)
    {
        unitAnimator.SetDirection(direction);
    }

    public void UpdateHPText(int hp)
    {
        if (hpText != null) hpText.text = hp.ToString();
    }

    public void UpdateOrderText(int order)
    {
        if (orderText != null) orderText.text = (order + 1).ToString();
    }

    public Tween MoveTo(Vector3 worldPos, TweenCallback onComplete = null)
    {
        orderSpriteAnimation.PlayAnimation();
        var tween = transform.DOMove(worldPos, moveDuration).SetEase(Ease.OutCubic);
        if (onComplete != null) tween.OnComplete(onComplete);
        return tween;
    }

    public async UniTask PlayAttackAnimation(Vector3 targetPosOffset, Action onImpact, bool isPlayer)
    {
        orderSpriteAnimation.PlayAnimation();
        Vector3 originalPos = spriteTransform.position;
        Vector3 attackPos = originalPos + targetPosOffset;

        Sequence attackSequence = DOTween.Sequence();

        attackSequence
            .Append(spriteTransform.DOMove(attackPos, attackDuration * 0.3f).SetEase(Ease.InQuad))
            .AppendCallback(() =>
            {
                onImpact?.Invoke();
                if (isPlayer) CameraManager.Instance.ShakeCamera(0.5f, attackShakeStrength, targetPosOffset.normalized);
            })
            .Append(spriteTransform.DOMove(originalPos, attackDuration * 0.7f).SetEase(Ease.OutQuad));

        await attackSequence.AsyncWaitForCompletion();
    }

    public void PlayDamageAnimation(bool isDead, Vector2 knockbackDirection, float knockbackDistance, bool isPlayer)
    {
        // ノックバック方向と距離に基づいて移動
        // 実際の座標変更ではなく、見た目の演出としてのノックバック（Spriteだけ動かすなど）
        // 元のUnitの実装ではKnockbackAsyncで移動していた

        hpSpriteAnimation.PlayAnimation();

        if (isDead)
        {
            unitAnimator.SetDeath();
             CameraManager.Instance.ShakeCamera(1f, 5f, knockbackDirection);
        }
        else
        {
            colorFlash.AnimateToColor(Utility.mainColors[2], flashDuration);
            if (isPlayer) CameraManager.Instance.ShakeCamera(0.5f, attackShakeStrength, knockbackDirection);
        }
    }

    public async UniTask PlayKnockback(Vector3 knockbackOffset)
    {
        Vector3 originalPos = spriteTransform.position;

        // maskも動かす必要がある（元の実装より）
        var seq = DOTween.Sequence();
        seq.Join(spriteTransform.DOMove(originalPos + knockbackOffset, moveDuration * 0.5f).SetEase(Ease.InCubic));
        seq.Join(maskTransform.DOLocalMove(maskOriginalPos + knockbackOffset, moveDuration * 0.5f).SetEase(Ease.InCubic));

        await seq.AsyncWaitForCompletion();
    }

    public async UniTask PlayDeathSequence(CancellationToken token)
    {
        try
        {
            colorFlash.AnimateToColor(Utility.mainColors[0], flashDuration);
            await UniTask.Delay((int)(flashDuration * 1000), cancellationToken: token);
            await UniTask.Delay(200, cancellationToken: token);

            // 落下演出
            spriteTransform.DOLocalMoveY(-50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
            await UniTask.Delay(100, cancellationToken: token);
            hpTransform.DOLocalMoveY(hpOriginalPos.y - 50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
            await UniTask.Delay(50, cancellationToken: token);
            orderTransform.DOLocalMoveY(orderOriginalPos.y - 50.0f, 0.4f).SetEase(Ease.InCubic).SetLink(gameObject);
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }

    public void ResetVisuals()
    {
        // Kill existing tweens
        spriteTransform.DOKill();
        hpTransform.DOKill();
        orderTransform.DOKill();
        maskTransform.DOKill();

        // Reset positions
        spriteTransform.localPosition = Vector3.zero;
        spriteTransform.localScale = Vector3.one;
        maskTransform.localPosition = maskOriginalPos;
        maskedTransform.localPosition = maskedOriginalPos;
        hpTransform.localPosition = hpOriginalPos;
        orderTransform.localPosition = orderOriginalPos;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
