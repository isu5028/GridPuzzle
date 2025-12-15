using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class ShowWin : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip inAnimation;
    [SerializeField] private AnimationClip outAnimation;
    [SerializeField] private SpriteMask spriteMask;
    [SerializeField] private float inDuration;
    [SerializeField] private float outDuration;
    [SerializeField] private float showingDuration;
    [SerializeField] private Transform winTransform;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeStrength = 0.5f;
    [SerializeField] private int shakeVibrato = 10;
    private float updateEndTime;
    private CancellationTokenSource autoHideCts;

    private void OnDestroy()
    {
        autoHideCts?.Cancel();
        autoHideCts?.Dispose();
    }

    private void Start()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (Time.time <= updateEndTime || Time.time - Time.deltaTime <= updateEndTime)
        {
            spriteMask.sprite = targetRenderer.sprite;
        }

#if UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.U))
        {
            Hide();
        }
        if(Input.GetKeyDown(KeyCode.I))
        {
            Show();
        }
#endif
    }


    public void Show()
    {
        animator.Play(inAnimation.name);
        updateEndTime = Time.time + inAnimation.length;

        autoHideCts?.Cancel();
        autoHideCts = new CancellationTokenSource();

        if (winTransform != null)
        {
            winTransform.DOComplete();
        }

        AutoHideAsync(autoHideCts.Token).Forget();
    }

    private async UniTaskVoid AutoHideAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay((int)(showingDuration * 1000), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        Hide();
    }

    public void Hide()
    {
        autoHideCts?.Cancel();
        animator.Play(outAnimation.name);
        updateEndTime = Time.time + outAnimation.length;
    }
}
