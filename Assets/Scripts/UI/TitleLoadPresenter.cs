using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TitleLoadPresenter : MonoBehaviour
{
    [SerializeField] private TransitionOut transitionOut = null!;
    [SerializeField] private OptionLoadView view = null!;

    // Input Action references
    private InputAction inputMove;
    private InputAction inputDecide;
    private InputAction inputCancel;

    private int currentLoadIndex = 0; // 0: Auto, 1: Manual, 2: Close
    private bool isProcessingInput = false;
    private float inputCooldown = 0.2f;
    private float lastInputTime = 0f;

    // Input acceleration
    private float initialCooldown = 0.3f;
    private float minCooldown = 0.02f;
    private float accelerationRate = 0.5f;
    private int consecutiveInputCount = 0;
    private Direction lastDirection;

    private bool isActive = false;
    private bool isLoaded = false;
    private UniTaskCompletionSource closeCompletionSource;
    private Func<UniTask> onCancelAnimation;

    private void Start()
    {
        inputMove = InputSystem.actions.FindAction("Move");
        inputDecide = InputSystem.actions.FindAction("Decide");
        inputCancel = InputSystem.actions.FindAction("Option"); // Using 'Option' as Cancel/Back usually? Or specific Cancel key? In OptionPresenter it matches Option key for close.
        // Usually Cancel is B button. Option key is Start/Menu.
        // Let's assume inputCancel logic is consistent with OptionLoadPresenter.
    }

    public async UniTask<bool> OpenAsync(UniTask? concurrentAnimation = null, Func<UniTask> onCancelAnimation = null)
    {
        isActive = true;
        closeCompletionSource = new UniTaskCompletionSource();
        isLoaded = false;
        this.onCancelAnimation = onCancelAnimation;

        RefreshData();

        currentLoadIndex = 0;
        view.UpdateCursorPosition(currentLoadIndex);

        if (concurrentAnimation != null)
        {
            await UniTask.WhenAll(view.ShowAsync(), concurrentAnimation.Value);
        }
        else
        {
            await view.ShowAsync();
        }

        await closeCompletionSource.Task;

        return isLoaded;
    }

    private void Update()
    {
        if (!isActive) return;

        HandleInput();
    }

    private void HandleInput()
    {
        if (view.IsAnimating) return;

        if (inputCancel != null && inputCancel.triggered)
        {
            CloseAsync(false).Forget();
            return;
        }

        if (inputDecide != null && inputDecide.triggered)
        {
            ExecuteLoadSelection();
            view.BlinkCursorAsync().Forget();
            return;
        }

        if (inputMove != null)
        {
            Vector2 moveInput = inputMove.ReadValue<Vector2>();

            if (moveInput == Vector2.zero)
            {
                ResetInputAcceleration();
                isProcessingInput = false;
                return;
            }

            if (isProcessingInput && Time.time - lastInputTime > inputCooldown)
            {
                isProcessingInput = false;
            }

            if (isProcessingInput) return;

            Direction dir = Utility.GetDirectionFromStickInput(moveInput);
            HandleLoadSelectionInput(dir);
        }
    }

    private void HandleLoadSelectionInput(Direction dir)
    {
        if (dir == Direction.Up || dir == Direction.Left)
        {
            int nextIndex = currentLoadIndex - 1;
            if (nextIndex < 0) nextIndex = 2; // Loop to Close
            ChangeSelection(nextIndex);
            SetInputCooldown(dir, false);
        }
        else if (dir == Direction.Down || dir == Direction.Right)
        {
            int nextIndex = currentLoadIndex + 1;
            if (nextIndex > 2) nextIndex = 0; // Loop to Auto
            ChangeSelection(nextIndex);
            SetInputCooldown(dir, false);
        }
    }

    private void ChangeSelection(int index)
    {
        if (currentLoadIndex != index)
        {
            currentLoadIndex = index;
            view.UpdateCursorPosition(currentLoadIndex);
            AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
        }
    }

    private void ExecuteLoadSelection()
    {
        // 2 is Close
        if (currentLoadIndex == 2)
        {
             CloseAsync(false).Forget();
             return;
        }

        // 0: Auto, 1: Manual
        HandleLoadDecideAsync().Forget();
    }

    bool isLoading = false;

    private async UniTaskVoid HandleLoadDecideAsync()
    {
        if (isLoading) return;

        GameProgressManager.SaveType targetType = currentLoadIndex == 0
            ? GameProgressManager.SaveType.Auto
            : GameProgressManager.SaveType.Manual;

        // Ensure data exists
        var data = GameProgressManager.Instance.GetSaveData(targetType);
        if (data == null)
        {
            // Play Error sound?
            AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel); // Or separate error sound
            return;
        }

        isLoading = true;
        AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);

        // Transition logic from requirement: "TransitionOutが流れてからInGameシーンに入る"
        // Wait for TransitionOut
        if (transitionOut != null)
        {
            await transitionOut.AnimateAsync();
        }

        GameProgressManager.Instance.NextLoadType = targetType;
        SceneManager.LoadScene("InGame");
    }


    private async UniTaskVoid CloseAsync(bool loaded)
    {
        if (!isActive) return;

        if (!loaded)
        {
            AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
        }

        isActive = false;
        isLoaded = loaded;

        if (!loaded && onCancelAnimation != null)
        {
            await UniTask.WhenAll(view.HideAsync(), onCancelAnimation.Invoke());
        }
        else
        {
            await view.HideAsync();
        }

        closeCompletionSource.TrySetResult();
    }

    private void RefreshData()
    {
        var manualSave = GameProgressManager.Instance.GetSaveData(GameProgressManager.SaveType.Manual);
        var autoSave = GameProgressManager.Instance.GetSaveData(GameProgressManager.SaveType.Auto);

        string manualDate = manualSave != null ? manualSave.saveTime : "No Data";
        string autoDate = autoSave != null ? autoSave.saveTime : "No Data";

        view.SetSaveData(autoDate, manualDate);
    }

    private void SetInputCooldown(Direction currentInput, bool enableAcceleration)
    {
        isProcessingInput = true;
        lastInputTime = Time.time;
        lastDirection = currentInput;

        if (enableAcceleration)
        {
            consecutiveInputCount++;
            inputCooldown = Mathf.Max(
                minCooldown,
                initialCooldown * Mathf.Pow(accelerationRate, consecutiveInputCount - 1)
            );
        }
        else
        {
            inputCooldown = initialCooldown;
        }
    }

    private void ResetInputAcceleration()
    {
        consecutiveInputCount = 0;
        inputCooldown = initialCooldown;
    }
}
