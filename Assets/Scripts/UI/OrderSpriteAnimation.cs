using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class OrderSpriteAnimation : MonoBehaviour
{
    [SerializeField] private float shakeAngle = 15f; // 回転角度
    [SerializeField] private float shakeDuration = 0.5f; // 全体の時間
    [SerializeField] private int shakeVibrato = 10; // 震える回数
    private Vector3 originalRotation;

    void Start()
    {
        originalRotation = transform.localEulerAngles;
    }
    void Update()
    {
    }

    async UniTask PlayShakeRotationAnimation()
    {
        // シーケンスを作成
        Sequence sequence = DOTween.Sequence();
        sequence.SetLink(gameObject);

        // 1回の往復にかかる時間
        float singleShakeDuration = shakeDuration / shakeVibrato;

        for (int i = 0; i < shakeVibrato; i++)
        {
            // フェードアウト効果のための減衰率
            float fadeOutFactor = 1f - ((float)i / shakeVibrato);
            float currentAngle = shakeAngle * fadeOutFactor;

            // 右に回転
            sequence.Append(transform.DOLocalRotate(
                new Vector3(0, 0, originalRotation.z + currentAngle),
                singleShakeDuration / 2f,
                RotateMode.Fast
            ).SetEase(Ease.InOutSine));

            // 左に回転
            sequence.Append(transform.DOLocalRotate(
                new Vector3(0, 0, originalRotation.z - currentAngle * 0.75f),
                singleShakeDuration / 2f,
                RotateMode.Fast
            ).SetEase(Ease.InOutSine));
        }

        // 最後に元の位置に戻る
        sequence.Append(transform.DOLocalRotate(
            originalRotation,
            singleShakeDuration / 2f,
            RotateMode.Fast
        ).SetEase(Ease.InOutSine));

        await sequence.AsyncWaitForCompletion();
    }

    // テスト用（任意のタイミングで呼び出せる）
    [ContextMenu("Play Animation")]
    public void PlayAnimation()
    {
        if (this == null || gameObject == null) return;
        PlayShakeRotationAnimation().Forget();
    }
}
