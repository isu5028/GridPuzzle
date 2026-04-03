using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class ShowWipeOut : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip inAnimation;
    [SerializeField] private AnimationClip outAnimation;
    [SerializeField] private SpriteMask spriteMask;
    [SerializeField] private float inDuration;
    [SerializeField] private float outDuration;
    [SerializeField] private float showingDuration;
    [SerializeField] private Transform wipeoutTransform;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeStrength = 0.5f;
    [SerializeField] private int shakeVibrato = 10;
    private float updateEndTime;
    private CancellationTokenSource autoHideCts;
    private Vector3 originalPos;
    private Vector3 hiddenOffset;

    private void CancelAutoHide()
    {
        autoHideCts?.Cancel();
        autoHideCts?.Dispose();
        autoHideCts = null;
    }

    private void OnDestroy()
    {
        CancelAutoHide();
    }

    private void Start()
    {
        originalPos = transform.localPosition;
        hiddenOffset = new Vector3(0, -500, 0);
        transform.localPosition = originalPos + hiddenOffset;
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
        transform.localPosition = originalPos;
        animator.enabled = true;
        animator.Play(inAnimation.name);
        updateEndTime = Time.time + inAnimation.length;

        CancelAutoHide();
        autoHideCts = new CancellationTokenSource();

        if (wipeoutTransform != null)
        {
            wipeoutTransform.DOComplete();
        }

        //AutoHideAsync(autoHideCts.Token).Forget();
    }

    private async UniTaskVoid AutoHideAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay((int)(showingDuration * 1000), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        Hide();
    }

    public event System.Action OnHide;

    public void Hide()
    {
        CancelAutoHide();
        animator.Play(outAnimation.name);
        updateEndTime = Time.time + outAnimation.length;
        OnHide?.Invoke();
    }
}
