using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class SquareAround : MonoBehaviour
{
    [SerializeField] private Transform[] squares = new Transform[2];
    private Vector2[] targetPoints = new Vector2[4];
    [SerializeField] private float scale = 10f;
    [SerializeField] private float duration = 0.5f;

    void Start()
    {
        targetPoints[0] = new Vector2(-1, 1);
        targetPoints[1] = new Vector2(-1, -1);
        targetPoints[2] = new Vector2(1, -1);
        targetPoints[3] = new Vector2(1, 1);
        squares[0].localPosition = targetPoints[0] * scale;
        squares[1].localPosition = targetPoints[2] * scale;
        Anim().Forget();
    }

    async UniTaskVoid Anim()
    {
        int i = 1;
        while (true)
        {
            Vector2 targetPos = targetPoints[i] * scale;
            Vector2 targetPos2 = targetPoints[(i + 2) % 4] * scale;
            squares[0].DOLocalMove(targetPos, duration).SetEase(Ease.OutExpo);
            squares[1].DOLocalMove(targetPos2, duration).SetEase(Ease.OutExpo);

            await UniTask.Delay((int)(duration * 1000));
            i++;
            i = i % 4;
        }
    }
}
