using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class HUDFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private SpriteRenderer character;
    Color unselectedColor;
    Color selectedColor;
    [SerializeField] float delay = 0.2f;

    private Vector3 originalScale;
    private CancellationTokenSource cts;

    void Start()
    {
        unselectedColor = Utility.mainColors[0];
        selectedColor = Utility.mainColors[1];
        originalScale = transform.localScale;
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
    }

    void Update()
    {
#if UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.B))
        {
            PlayFeedback();
        }
#endif
    }

    public void PlayFeedback()
    {
        PlayFeedbackAsync().Forget();
    }

    public async UniTask PlayFeedbackAsync()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();

        try
        {
            icon.transform.DOKill();
            icon.transform.localScale = originalScale;

            icon.color = selectedColor;
            character.color = selectedColor;

            float animDuration = delay / 2f;
            icon.transform.DOScale(originalScale * 0.9f, animDuration).SetEase(Ease.OutQuad);
            await UniTask.Delay(TimeSpan.FromSeconds(animDuration), cancellationToken: cts.Token);

            icon.transform.DOScale(originalScale, animDuration).SetEase(Ease.InQuad);
            await UniTask.Delay(TimeSpan.FromSeconds(animDuration), cancellationToken: cts.Token);

            icon.color = unselectedColor;
            character.color = unselectedColor;
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }
}
