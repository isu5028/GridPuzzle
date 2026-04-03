using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class TileSyncMapController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip inAnimation;
    [SerializeField] private AnimationClip outAnimation;
    [SerializeField] private SpriteMask spriteMask;
    [SerializeField] private float inDuration;
    [SerializeField] private float outDuration;
    [SerializeField] private float showingDuration;
    [SerializeField] private Transform parentTransform;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Animator activatedAnimator;
    [SerializeField] private AnimationClip activatedAnimation;
    private float updateEndTime;
    private CancellationTokenSource autoHideCts;

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
        //Hide();
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


    public void Show(bool immediate = false)
    {
        Debug.Log("Show TileSyncMap");
        if (immediate)
        {
            animator.Play(inAnimation.name, 0, 1.0f);
        }
        else
        {
            animator.Play(inAnimation.name);
        }
        updateEndTime = Time.time + inAnimation.length;

        CancelAutoHide();
        autoHideCts = new CancellationTokenSource();

        if (parentTransform != null)
        {
            parentTransform.DOComplete();
        }
    }

    public void Hide()
    {
        CancelAutoHide();
        animator.Play(outAnimation.name);
        updateEndTime = Time.time + outAnimation.length;
    }

    private bool isActivated = false;

    public void PlayActivated()
    {
        if (isActivated) return;

        if (activatedAnimator != null && activatedAnimation != null)
        {
            activatedAnimator.enabled = true;
            activatedAnimator.Play(activatedAnimation.name);
            isActivated = true;
        }
    }

    public void ResetActivated()
    {
        if (!isActivated) return;

        if (activatedAnimator != null)
        {
            activatedAnimator.Rebind();
            activatedAnimator.Update(0f);
        }
        isActivated = false;
    }
}
