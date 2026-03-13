using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using TMPro;

public class HUDFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private SpriteRenderer character;
    [SerializeField] private TextMeshPro text;
    Color unselectedColor;
    Color selectedColor;
    [SerializeField] float delay = 0.2f;
    [SerializeField] private bool shakeIconOnInteractable = true; // 追加：揺れるアニメーションの有無（デフォルトtrue）

    private Vector3 originalScale;
    private CancellationTokenSource cts;
    private Color currentColor; // 追加：現在の基準色

    private bool? lastInteractableState = null;
    private Vector3 iconOriginalPos;

    void Awake()
    {
        unselectedColor = Utility.mainColors[0];
        selectedColor = Utility.mainColors[1];
        currentColor = unselectedColor; // 初期状態では非選択色
        originalScale = transform.localScale;
        if (icon != null) iconOriginalPos = icon.transform.localPosition;
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

    // 追加: 押下可能かどうかで基準色を切り替える
    public void SetInteractable(bool interactable)
    {
        bool changed = !lastInteractableState.HasValue || lastInteractableState.Value != interactable;
        lastInteractableState = interactable;

        currentColor = interactable ? unselectedColor : Utility.mainColors[3]; // mainColors[3] は暗い色
        if (icon != null) icon.color = currentColor;
        if (character != null) character.color = currentColor;
        if (text != null) text.color = currentColor;

        // ワープ可能マスに移動するたびにアイコンを左右に揺らす
        if (shakeIconOnInteractable && interactable && icon != null)
        {
            icon.transform.DOKill();
            icon.transform.localScale = originalScale;
            icon.transform.localPosition = iconOriginalPos;
            icon.transform.DOPunchPosition(new Vector3(0, 2, 0), 0.3f, 12, 0.5f).SetLink(icon.gameObject);
        }
    }

    public void SetVisible(bool isVisible)
    {
        if (icon != null) icon.gameObject.SetActive(isVisible);
        if (character != null) character.gameObject.SetActive(isVisible);
        if (text != null) text.gameObject.SetActive(isVisible);
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
            // DOKillで揺れが止まった際に位置がズレたままになるのを防ぐ
            icon.transform.localPosition = iconOriginalPos;

            if (icon != null) icon.color = selectedColor;
            if (character != null) character.color = selectedColor;
            if(text != null) text.color = selectedColor;

            float animDuration = delay / 2f;
            icon.transform.DOScale(originalScale * 0.9f, animDuration).SetEase(Ease.OutQuad);
            await UniTask.Delay(TimeSpan.FromSeconds(animDuration), cancellationToken: cts.Token);

            icon.transform.DOScale(originalScale, animDuration).SetEase(Ease.InQuad);
            await UniTask.Delay(TimeSpan.FromSeconds(animDuration), cancellationToken: cts.Token);

            if (icon != null) icon.color = currentColor;
            if (character != null) character.color = currentColor;
            if(text != null) text.color = currentColor;
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }
}
