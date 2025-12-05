using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class HorizontalUI : MonoBehaviour
{
    [SerializeField] private Transform rightArrow = null!;
    [SerializeField] private Transform leftArrow = null!;
    [SerializeField] private float moveDistance = 0.2f;
    [SerializeField] private float moveDuration = 0.15f;

    private Vector3 rightArrowStartPos;
    private Vector3 leftArrowStartPos;
    private Sequence rightArrowSequence;
    private Sequence leftArrowSequence;

    void Start()
    {
        rightArrowStartPos = rightArrow.localPosition;
        leftArrowStartPos = leftArrow.localPosition;
    }

    public void OnRightInput()
    {
        // 既存のアニメーションをキャンセル
        rightArrowSequence?.Kill();

        // 元の位置に即座にリセット
        rightArrow.localPosition = rightArrowStartPos;

        // 新しいアニメーションシーケンスを作成
        rightArrowSequence = DOTween.Sequence();
        rightArrowSequence
            .Append(rightArrow.DOLocalMoveX(rightArrowStartPos.x + moveDistance, moveDuration * 0.4f)
                .SetEase(Ease.OutQuad))
            .Append(rightArrow.DOLocalMoveX(rightArrowStartPos.x, moveDuration * 0.6f)
                .SetEase(Ease.InOutQuad))
            .OnComplete(() => rightArrowSequence = null);
    }

    public void OnLeftInput()
    {
        // 既存のアニメーションをキャンセル
        leftArrowSequence?.Kill();

        // 元の位置に即座にリセット
        leftArrow.localPosition = leftArrowStartPos;

        // 新しいアニメーションシーケンスを作成
        leftArrowSequence = DOTween.Sequence();
        leftArrowSequence
            .Append(leftArrow.DOLocalMoveX(leftArrowStartPos.x - moveDistance, moveDuration * 0.4f)
                .SetEase(Ease.OutQuad))
            .Append(leftArrow.DOLocalMoveX(leftArrowStartPos.x, moveDuration * 0.6f)
                .SetEase(Ease.InOutQuad))
            .OnComplete(() => leftArrowSequence = null);
    }

    private void OnDestroy()
    {
        // クリーンアップ
        rightArrowSequence?.Kill();
        leftArrowSequence?.Kill();
    }
}
