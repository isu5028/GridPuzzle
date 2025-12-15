using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using System;

public class OptionWarningPresenter : MonoBehaviour
{
    [SerializeField] private OptionWarningView view = null!;

    private InputAction inputMove;
    private InputAction inputDecide;
    private InputAction inputCancel;

    private bool isActive = false;
    private UniTaskCompletionSource<bool> completionSource; // true: YES, false: NO
    private int selectedIndex = 1; // 0: YES, 1: NO (Default NO)

    private void Start()
    {
        inputMove = InputSystem.actions.FindAction("Move");
        inputDecide = InputSystem.actions.FindAction("Decide");
        inputCancel = InputSystem.actions.FindAction("Option");
    }

    public async UniTask<bool> OpenAsync(string warningKey, UniTask hideParentTask, Func<UniTask> showParentFunc)
    {
        isActive = true;
        completionSource = new UniTaskCompletionSource<bool>();
        selectedIndex = 1; // Default NO

        view.SetText(warningKey);
        view.UpdateCursorPosition(selectedIndex);

        var showWarning = view.ShowAsync();
        await UniTask.WhenAll(hideParentTask, showWarning);

        bool result = await completionSource.Task;

        var hideWarning = view.HideAsync();
        var showParent = showParentFunc();
        await UniTask.WhenAll(hideWarning, showParent);

        isActive = false;

        return result;
    }

    private void Update()
    {
        if (!isActive) return;

        if (view.IsAnimating) return;

        if (inputDecide.triggered)
        {
            HandleDecide();
            return;
        }

        if (inputCancel.triggered)
        {
            HandleCancel();
            return;
        }

        Vector2 moveInput = inputMove.ReadValue<Vector2>();
        if (moveInput.y != 0)
        {
            // Up/Down selection
            if (moveInput.y > 0 && selectedIndex == 1)
            {
                selectedIndex = 0;
                view.UpdateCursorPosition(selectedIndex);
                AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            }
            else if (moveInput.y < 0 && selectedIndex == 0)
            {
                selectedIndex = 1;
                view.UpdateCursorPosition(selectedIndex);
                AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            }
        }
    }

    private void HandleDecide()
    {
        bool isYes = (selectedIndex == 0);
        if (isYes)
        {
            AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
            completionSource.TrySetResult(true);
        }
        else
        {
             HandleCancel();
        }
    }

    private void HandleCancel()
    {
        AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
        completionSource.TrySetResult(false);
    }
}
