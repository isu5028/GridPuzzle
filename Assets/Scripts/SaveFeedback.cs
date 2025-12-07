using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;

public class SaveFeedback : MonoBehaviour
{
    Vector3 originPos;
    Vector3 hiddenPos;
    private CancellationTokenSource cts;

    void Start()
    {
        originPos = transform.localPosition;
        hiddenPos = new Vector3(originPos.x, originPos.y - Screen.height / 16, originPos.z);
        transform.localPosition = hiddenPos;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.B))
        {
            PlayFeedback();
        }
#endif
    }

    public void PlayFeedback()
    {
        PlayFeedbackAsync().Forget();
    }

    async UniTask PlayFeedbackAsync()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();

        try
        {
            transform.DOLocalMove(originPos, 0.3f).SetEase(Ease.OutCubic);
            await UniTask.Delay(TimeSpan.FromSeconds(1f),  cancellationToken: cts.Token);
            transform.DOLocalMove(hiddenPos, 0.3f).SetEase(Ease.InCubic);
        }
        catch(OperationCanceledException)
        {

        }
    }
}
