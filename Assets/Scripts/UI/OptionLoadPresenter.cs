using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class OptionLoadPresenter : MonoBehaviour
{
    [SerializeField] private TransitionOut transitionOut = null!;
    [SerializeField] private OptionLoadView view = null!;
    [SerializeField] private OptionLoadConfirmationView confirmationView = null!;
    private GridManager gridManager;

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

    // Confirmation State
    private bool isConfirmationOpen = false;
    private int confirmationIndex = 0; // 0: Execute, 1: Cancel

    private void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();

        inputMove = InputSystem.actions.FindAction("Move");
        inputDecide = InputSystem.actions.FindAction("Decide");
        inputCancel = InputSystem.actions.FindAction("Option");
    }

    public async UniTask<bool> OpenAsync(UniTask? concurrentAnimation = null, Func<UniTask> onCancelAnimation = null)
    {
        isActive = true;
        closeCompletionSource = new UniTaskCompletionSource();
        isLoaded = false;
        this.onCancelAnimation = onCancelAnimation;
        isConfirmationOpen = false;

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
        if (view.IsAnimating || (isConfirmationOpen && confirmationView.IsAnimating)) return;

        if (inputCancel.triggered)
        {
            if (isConfirmationOpen)
            {
                CloseConfirmationAsync().Forget();
            }
            else
            {
                CloseAsync(false).Forget();
            }
            return;
        }

        if (inputDecide.triggered)
        {
            if (isConfirmationOpen)
            {
                ExecuteConfirmation();
                confirmationView.BlinkCursorAsync().Forget();
            }
            else
            {
                ExecuteLoadSelection();
                view.BlinkCursorAsync().Forget();
            }
            return;
        }

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

        if (isConfirmationOpen)
        {
            HandleConfirmationInput(dir);
        }
        else
        {
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

    private void HandleConfirmationInput(Direction dir)
    {
        // 0: Execute, 1: Cancel
        if (dir == Direction.Up || dir == Direction.Down)
        {
            confirmationIndex = (confirmationIndex == 0) ? 1 : 0;
            confirmationView.UpdateCursorPosition(confirmationIndex);
            AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
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

        // Show Confirmation
        OpenConfirmationAsync().Forget();
    }

    private async UniTaskVoid OpenConfirmationAsync()
    {
        GameProgressManager.SaveType targetType = currentLoadIndex == 0
            ? GameProgressManager.SaveType.Auto
            : GameProgressManager.SaveType.Manual;

        var data = GameProgressManager.Instance.GetSaveData(targetType);
        if (data == null)
        {
             // No data found logic
             return;
        }

        AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);

        isConfirmationOpen = true;
        confirmationIndex = 0;
        confirmationView.UpdateCursorPosition(confirmationIndex);

        // Animate Confirmation Show AND LoadView HideUp
        await UniTask.WhenAll(
            confirmationView.ShowAsync(),
            view.HideUpAsync()
        );
    }

    private async UniTaskVoid CloseConfirmationAsync()
    {
         AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);

         // Animate Confirmation Hide AND LoadView Show (Return)
         await UniTask.WhenAll(
            confirmationView.HideAsync(),
            view.ShowAsync()
         );

         isConfirmationOpen = false;
    }

    private void ExecuteConfirmation()
    {
        HandleConfirmationDecideAsync().Forget();
    }

    bool isLoading = false;

    private async UniTaskVoid HandleConfirmationDecideAsync()
    {

        if (confirmationView != null) await confirmationView.BlinkCursorAsync();

        if (isLoading) return;

        if (confirmationIndex == 0) // Execute Load
        {
            GameProgressManager.SaveType targetType = currentLoadIndex == 0
                ? GameProgressManager.SaveType.Auto
                : GameProgressManager.SaveType.Manual;

            await transitionOut.AnimateAsync();

            GameProgressManager.Instance.NextLoadType = targetType;
            AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            isLoading = true;
        }
        else
        {
             CloseAsync(false).Forget();
        }
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

        bool wasConfirmationOpen = isConfirmationOpen;

        // Ensure confirmation is hidden if it was open
        if (isConfirmationOpen)
        {
             isConfirmationOpen = false;
             confirmationView.HideAsync().Forget();
        }

        UniTask viewHideTask;
        if (wasConfirmationOpen)
        {
            viewHideTask = view.HideUpAsync();
        }
        else
        {
            viewHideTask = view.HideAsync();
        }

        if (!loaded && onCancelAnimation != null)
        {
            await UniTask.WhenAll(viewHideTask, onCancelAnimation.Invoke());
        }
        else
        {
            await viewHideTask;
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
