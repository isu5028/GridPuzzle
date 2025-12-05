using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class HPSpriteAnimation : MonoBehaviour
{
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float squashAmount = 0.7f; // 潰れる度合い（0.7 = 30%縮む）
    [SerializeField] private float stretchAmount = 1.2f; // 広がる度合い（1.2 = 20%広がる）
    [SerializeField] private float baseScale = 1.5f;
    private Vector3 originalScale;

    private void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {

    }

    async UniTask PlaySquashStretchAnimation()
    {
        // シーケンスを作成
        Sequence sequence = DOTween.Sequence();

        // 1. 縦に潰れて横に広がる
        sequence.Append(transform.DOScale(
            new Vector3(originalScale.x * stretchAmount, originalScale.y * squashAmount, originalScale.z) * baseScale,
            animationDuration * 0.5f
        ).SetEase(Ease.OutQuad));

        // 2. 横に潰れて縦に広がる
        sequence.Append(transform.DOScale(
            new Vector3(originalScale.x * squashAmount, originalScale.y * stretchAmount, originalScale.z) * baseScale,
            animationDuration * 0.5f
        ).SetEase(Ease.InOutQuad));

        // 3. 元に戻る
        sequence.Append(transform.DOScale(
            originalScale,
            animationDuration * 0.5f
        ).SetEase(Ease.InQuad));

        await sequence.AsyncWaitForCompletion();
    }

    // テスト用（任意のタイミングで呼び出せる）
    [ContextMenu("Play Animation")]
    public void PlayAnimation()
    {
        PlaySquashStretchAnimation().Forget();
    }
}
