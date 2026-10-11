using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class BlockedPrefab : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float scaleUpDuration = 0.15f;
    [SerializeField] private float holdDuration = 0.1f;
    [SerializeField] private float scaleDownDuration = 0.3f;
    [SerializeField] private Ease scaleUpEase = Ease.OutBack;
    [SerializeField] private Ease scaleDownEase = Ease.InBack;

    [Header("Float Settings")]
    [SerializeField] private float floatHeight = 1.0f;
    [SerializeField] private Ease floatEase = Ease.OutQuad;

    async void Start()
    {
        transform.localScale = Vector3.zero;

        var ct = this.GetCancellationTokenOnDestroy();

        // 全体を通して上方向に浮き上がる（並行実行）
        float totalDuration = scaleUpDuration + holdDuration + scaleDownDuration;
        transform.DOLocalMoveY(transform.localPosition.y + floatHeight, totalDuration)
            .SetEase(floatEase)
            .SetLink(gameObject);

        // Scale(0,0,0) → Scale(1,1,1)
        var tweenUp = transform.DOScale(Vector3.one, scaleUpDuration)
            .SetEase(scaleUpEase)
            .SetLink(gameObject);
        await UniTask.WaitWhile(() => tweenUp.IsActive() && !tweenUp.IsComplete(), cancellationToken: ct);

        // Hold
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(holdDuration),
            cancellationToken: ct);

        // Scale(1,1,1) → Scale(0,0,0)
        var tweenDown = transform.DOScale(Vector3.zero, scaleDownDuration)
            .SetEase(scaleDownEase)
            .SetLink(gameObject);
        await UniTask.WaitWhile(() => tweenDown.IsActive() && !tweenDown.IsComplete(), cancellationToken: ct);

        // Destroy
        Destroy(gameObject);
    }
}
