using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class AmmoSpriteAnimation : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float scaleMultiplier = 1.5f; // First Peak

    private Vector3 originalScale;
    private bool initialized = false;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;
        originalScale = transform.localScale;
        initialized = true;
    }

    public void PlayAnimation()
    {
        Initialize();

        // Kill existing tweens on this transform to handle rapid updates
        transform.DOKill();

        // Reset to original for consistent bounce base.
        transform.localScale = originalScale;

        Sequence sequence = DOTween.Sequence();

        float stepDuration = duration / 5.0f;

        // 1. Scale Up (Big)
        sequence.Append(transform.DOScale(originalScale * scaleMultiplier, stepDuration).SetEase(Ease.OutQuad));

        // 2. Scale Down (Small) - e.g. 0.8x
        sequence.Append(transform.DOScale(originalScale * 0.8f, stepDuration).SetEase(Ease.InOutQuad));

        // 3. Scale Up (Medium Big) - e.g. 1.2x (Assuming scaleMultiplier is ~1.5)
        float secondPeak = 1.0f + (scaleMultiplier - 1.0f) * 0.5f;
        sequence.Append(transform.DOScale(originalScale * secondPeak, stepDuration).SetEase(Ease.InOutQuad));

        // 4. Scale Down (Medium Small) - e.g. 0.9x
        sequence.Append(transform.DOScale(originalScale * 0.9f, stepDuration).SetEase(Ease.InOutQuad));

        // 5. Return to Original
        sequence.Append(transform.DOScale(originalScale, stepDuration).SetEase(Ease.OutQuad));

        sequence.Play();
    }
}
