using UnityEngine;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

public class End : MonoBehaviour
{
    [SerializeField] private TextMeshPro textThanks;
    [SerializeField] private TextMeshPro textReturn;
    [SerializeField] private float duration = 0.5f;
    private CancellationTokenSource cts;
    private Vector3 thanksOriginalPos;
    private Vector3 returnOriginalPos;
    private Vector3 hiddenOffset = new Vector3(0, 200, 0);

    void Start()
    {
        thanksOriginalPos = textThanks.transform.localPosition;
        returnOriginalPos = textReturn.transform.localPosition;
        textThanks.transform.localPosition = thanksOriginalPos;
        textReturn.transform.localPosition = returnOriginalPos;

        //Show();
    }

    void Update()
    {

    }

    public void Show()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        ShowAsync(cts.Token).Forget();
    }

    public async UniTask ShowAsync(CancellationToken token)
    {
        await UniTask.Delay((int)(duration * 2000), cancellationToken: token).SuppressCancellationThrow();
        textThanks.transform.localPosition = thanksOriginalPos;
        textReturn.transform.localPosition = returnOriginalPos;

        textThanks.transform.DOLocalMoveY(thanksOriginalPos.y + hiddenOffset.y, duration).SetEase(Ease.OutCubic);
        await UniTask.Delay((int)(duration * 500), cancellationToken: token).SuppressCancellationThrow();
        textReturn.transform.DOLocalMoveY(returnOriginalPos.y + hiddenOffset.y, duration).SetEase(Ease.OutCubic);
        await UniTask.Delay((int)(duration * 500), cancellationToken: token).SuppressCancellationThrow();
    }
}
