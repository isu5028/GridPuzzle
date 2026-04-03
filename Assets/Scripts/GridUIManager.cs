using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Localization.Components;

/// <summary>
/// GridManager内のUI演出を一括管理するクラス
/// </summary>
public class GridUIManager : MonoBehaviour
{
    [SerializeField] private TextPopUpEach popup = null!;
    [SerializeField] private ShowWipeOut showWipeOut = null!;
    [SerializeField] private TransitionOut transitionOut = null!;
    [SerializeField] private End end = null!;
    [SerializeField] private WorldMapPresenter worldMapPresenter = null!;
    [SerializeField] private StairConfirmationPresenter stairConfirmationPresenter = null!;
    [SerializeField] private SaveFeedback saveFeedback = null!;

    [Header("Ending Sequence")]
    [SerializeField] private GameClearSequence gameClearSequence = null!;

    [Header("Shop Item Hint")]
    [SerializeField] private Transform shopItemHintPanel = null!;
    [SerializeField] private LocalizeStringEvent shopItemHintLocalize = null!;
    [SerializeField] private string shopItemHintStringTable = "StringTable";
    [SerializeField] private string shopItemHintKeyPrefix = "HINT_";
    [SerializeField] private float shopItemHintSlideDuration = 0.3f;

    private Vector3 shopItemHintVisiblePosition;
    private Vector3 shopItemHintHiddenPosition;
    private Tween shopItemHintTween;
    private bool isShopItemHintVisible;

    public bool IsStairDialogOpen => stairConfirmationPresenter != null && stairConfirmationPresenter.IsOpen;
    public SaveFeedback SaveFeedback => saveFeedback;
    public StairConfirmationPresenter StairConfirmationPresenter => stairConfirmationPresenter;
    public WorldMapPresenter WorldMapPresenter => worldMapPresenter;
    public bool IsShopItemHintVisible => isShopItemHintVisible;

    public void Initialize()
    {
        if (showWipeOut == null) showWipeOut = FindObjectOfType<ShowWipeOut>();
        if (transitionOut == null) transitionOut = FindObjectOfType<TransitionOut>();
        if (end == null) end = FindObjectOfType<End>();
        if (worldMapPresenter == null) worldMapPresenter = FindObjectOfType<WorldMapPresenter>();

        InitializeShopItemHintPanel();
    }

    public void ShowPopup()
    {
        popup?.Show();
    }


    public void HideWipeOut()
    {
        showWipeOut?.Hide();
    }

    public void ShowWipeOut()
    {
        showWipeOut?.Show();
    }

    public void SubscribeWipeOut(System.Action onHide)
    {
        if (showWipeOut != null)
        {
            showWipeOut.OnHide += onHide;
        }
    }

    public void UnsubscribeWipeOut(System.Action onHide)
    {
        if (showWipeOut != null)
        {
            showWipeOut.OnHide -= onHide;
        }
    }

    public async UniTask PlayEndingSequence()
    {
        if (gameClearSequence != null)
        {
            await gameClearSequence.PlayAsync();
        }
        else
        {
            // フォールバック: 旧来の演出
            if (showWipeOut != null)
            {
                showWipeOut.Show();
                AudioManager.Instance.PlaySE(AudioManager.SEType.wipeout);
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(2.0f));

            if (transitionOut != null)
            {
                await transitionOut.AnimateAsync();
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));

            if (end != null)
            {
                end.Show();
            }
        }
    }

    public void ShowShopItemHint(int itemId)
    {
        if (shopItemHintPanel == null) return;

        SetShopItemHintReference(itemId);

        shopItemHintTween?.Kill();
        shopItemHintPanel.gameObject.SetActive(true);
        shopItemHintPanel.localPosition = shopItemHintHiddenPosition;
        shopItemHintTween = shopItemHintPanel
            .DOLocalMove(shopItemHintVisiblePosition, Mathf.Max(0.01f, shopItemHintSlideDuration))
            .SetUpdate(true)
            .SetEase(Ease.OutCubic);

        isShopItemHintVisible = true;
    }

    public void HideShopItemHint()
    {
        if (shopItemHintPanel == null) return;
        if (!isShopItemHintVisible && !shopItemHintPanel.gameObject.activeSelf) return;

        shopItemHintTween?.Kill();
        shopItemHintTween = shopItemHintPanel
            .DOLocalMove(shopItemHintHiddenPosition, Mathf.Max(0.01f, shopItemHintSlideDuration))
            .SetUpdate(true)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                if (shopItemHintPanel != null)
                {
                    shopItemHintPanel.gameObject.SetActive(false);
                }
            });

        isShopItemHintVisible = false;
    }

    public void HideShopItemHintImmediate()
    {
        if (shopItemHintPanel == null) return;

        shopItemHintTween?.Kill();
        shopItemHintPanel.localPosition = shopItemHintHiddenPosition;
        shopItemHintPanel.gameObject.SetActive(false);
        isShopItemHintVisible = false;
    }

    private void InitializeShopItemHintPanel()
    {
        if (shopItemHintPanel == null) return;

        shopItemHintVisiblePosition = shopItemHintPanel.localPosition;
        shopItemHintHiddenPosition = new Vector3(
            shopItemHintVisiblePosition.x,
            shopItemHintVisiblePosition.y - Screen.height,
            shopItemHintVisiblePosition.z);

        shopItemHintPanel.localPosition = shopItemHintHiddenPosition;
        shopItemHintPanel.gameObject.SetActive(false);
        isShopItemHintVisible = false;
    }

    private void SetShopItemHintReference(int itemId)
    {
        if (shopItemHintLocalize == null) return;

        int hintNumber = Mathf.Max(1, itemId + 1);
        string key = $"{shopItemHintKeyPrefix}{hintNumber}";

        shopItemHintLocalize.StringReference.SetReference(shopItemHintStringTable, key);
        shopItemHintLocalize.RefreshString();
    }
}
