using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// GridManager内のUI演出を一括管理するクラス
/// </summary>
public class GridUIManager : MonoBehaviour
{
    [SerializeField] private TextPopUpEach popup = null!;
    [SerializeField] private ShowWin showWin = null!;
    [SerializeField] private ShowWipeOut showWipeOut = null!;
    [SerializeField] private TransitionOut transitionOut = null!;
    [SerializeField] private End end = null!;
    [SerializeField] private WorldMapPresenter worldMapPresenter = null!;
    [SerializeField] private StairConfirmationPresenter stairConfirmationPresenter = null!;
    [SerializeField] private SaveFeedback saveFeedback = null!;

    public bool IsStairDialogOpen => stairConfirmationPresenter != null && stairConfirmationPresenter.IsOpen;
    public SaveFeedback SaveFeedback => saveFeedback;
    public StairConfirmationPresenter StairConfirmationPresenter => stairConfirmationPresenter;
    public WorldMapPresenter WorldMapPresenter => worldMapPresenter;

    public void Initialize()
    {
        if (showWipeOut == null) showWipeOut = FindObjectOfType<ShowWipeOut>();
        if (transitionOut == null) transitionOut = FindObjectOfType<TransitionOut>();
        if (end == null) end = FindObjectOfType<End>();
        if (worldMapPresenter == null) worldMapPresenter = FindObjectOfType<WorldMapPresenter>();
    }

    public void ShowPopup()
    {
        popup?.Show();
    }

    public void ShowWin()
    {
        showWin?.Show();
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
