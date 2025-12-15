using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using System;

public class TitleOptionPresenter : MonoBehaviour
{
    [SerializeField] private TitleOptionView view = null!;
    [SerializeField] private OptionWarningPresenter warningPresenter = null!;
    private bool isWarningOpen = false;
    // Needs a Delete Confirmation View? Can reuse OptionLoadConfirmationView or similar?
    // For now, simple logic or creating a simple text confirmation view.
    // Let's assume we reuse OptionLoadConfirmationView for simplicity if possible, or just add a simple text confirm.
    // Requirement says: "DELETE SAVEDATA". Usually implies a confirmation.

    private TitleOptionModel model;

    private InputAction inputMove;
    private InputAction inputDecide;
    private InputAction inputCancel; // To close

    private bool isActive = false;
    private UniTaskCompletionSource closeCompletionSource;

    private bool isProcessingInput = false;
    private float inputCooldown = 0.2f;
    private float lastInputTime = 0f;

    // Input acceleration
    private float initialCooldown = 0.3f;
    private float minCooldown = 0.02f;
    private float accelerationRate = 0.5f;
    private int consecutiveInputCount = 0;
    private bool isMaxSpeed = false;
    private Direction lastDirection;

    private void Start()
    {
        inputMove = InputSystem.actions.FindAction("Move");
        inputDecide = InputSystem.actions.FindAction("Decide");
        inputCancel = InputSystem.actions.FindAction("Option"); // 'Option' key acts as cancel/back in options too usually
    }

    public async UniTask OpenAsync()
    {
        isActive = true;
        closeCompletionSource = new UniTaskCompletionSource();

        model = new TitleOptionModel();
        RefreshView();

        await view.ShowAsync();

        await closeCompletionSource.Task;
    }

    private void Update()
    {
        if (!isActive) return;
        HandleInput();
    }

    private void HandleInput()
    {
        if (isWarningOpen) return;

        if (inputCancel.triggered)
        {
            CloseAsync().Forget();
            return;
        }

        if (inputDecide.triggered)
        {
            HandleDecide();
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

        Direction currentDirection = Utility.GetDirectionFromStickInput(moveInput);

        switch (currentDirection)
        {
            case Direction.Up:
                HandleVerticalMove(-1);
                SetInputCooldown(currentDirection, false);
                break;
            case Direction.Down:
                HandleVerticalMove(1);
                SetInputCooldown(currentDirection, false);
                break;
            case Direction.Left:
                HandleHorizontalMove(-1, isMaxSpeed ? 2 : 1);
                SetInputCooldown(currentDirection, true);
                break;
            case Direction.Right:
                HandleHorizontalMove(1, isMaxSpeed ? 2 : 1);
                SetInputCooldown(currentDirection, true);
                break;
        }
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
            isMaxSpeed = Mathf.Approximately(inputCooldown, minCooldown);
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
        isMaxSpeed = false;
    }

    private void HandleVerticalMove(int direction)
    {
        model.MoveSelection(direction);
        view.UpdateCursor((int)model.CurrentItem);
        AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
    }

    private void HandleHorizontalMove(int direction, int amount = 1)
    {
        switch (model.CurrentItem)
        {
            case TitleOptionModel.ItemType.GameSpeed:
                model.ChangeGameSpeed(direction);
                view.UpdateGameSpeedDisplay(model.GameSpeed, direction);
                AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
                break;

            case TitleOptionModel.ItemType.BgmVolume:
                for (int i = 0; i < amount; i++)
                {
                    model.ChangeBgm(direction);
                }
                GameSettings.BgmVolume = model.BgmLevel;
                GameSettings.ApplySettings();
                view.UpdateBgmDisplay(model.BgmLevel, direction);
                break;

            case TitleOptionModel.ItemType.SeVolume:
                 if(model.SeLevel == 0 && direction < 0) break;
                 if(model.SeLevel == model.MaxLevel && direction > 0) break;
                 for (int i = 0; i < amount; i++)
                 {
                     model.ChangeSe(direction);
                 }
                 GameSettings.SeVolume = model.SeLevel;
                 GameSettings.ApplySettings();
                 view.UpdateSeDisplay(model.SeLevel, direction);
                 AudioManager.Instance.PlaySE(AudioManager.SEType.tap);
                 break;

            case TitleOptionModel.ItemType.ScreenMode:
                model.ChangeScreenMode(direction);
                view.UpdateScreenModeDisplay(model.ScreenMode, direction);
                ApplyScreenSettings();
                AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
                break;

            case TitleOptionModel.ItemType.Resolution:
                model.ChangeResolution(direction);
                view.UpdateResolutionDisplay(model.CurrentResolution, direction);
                ApplyScreenSettings();
                AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
                break;

            case TitleOptionModel.ItemType.Language:
                model.ChangeLanguage(direction);
                view.UpdateLanguageDisplay(model.Language, direction);
                ApplyLanguage();
                AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
                break;
        }
    }

    private void RefreshView(int direction = 0)
    {
        view.UpdateView(model, direction);
    }

    private void HandleDecide()
    {
        switch (model.CurrentItem)
        {
            case TitleOptionModel.ItemType.DeleteSaveData:
                HandleDeleteSaveData();
                break;
            case TitleOptionModel.ItemType.Close:
                CloseAsync().Forget();
                break;
            default:
                // Other items might need explicit apply on decide?
                // Or just changing them left/right is enough.
                // Apply Screen settings on Decide for Resolution/ScreenMode?
                ApplySettings();
                break;
        }
    }


    private void ApplySettings()
    {
        // Apply Mode/Res/Etc
        GameSettings.screenMode = (int)model.ScreenMode;
        GameSettings.Resolution = model.CurrentResolution;
        GameSettings.GameSpeed = (int)model.GameSpeed;
        GameSettings.Language = (int)model.Language;
        GameSettings.ApplySettings();
        GameProgressManager.Instance.SaveOptions(); // Save to file
        AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
    }

    private void ApplyLanguage()
    {
        GameSettings.Language = (int)model.Language;

        var locales = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales;
        if (locales != null && locales.Count > (int)model.Language)
        {
             UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = locales[(int)model.Language];
        }
    }

    private void ApplyScreenSettings()
    {
        FullScreenMode mode = model.ScreenMode == TitleOptionModel.ScreenModeType.Fullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        Screen.SetResolution(model.CurrentResolution.x, model.CurrentResolution.y, mode);



        GameSettings.screenMode = (mode == FullScreenMode.Windowed) ? 0 : 1;
        GameSettings.Resolution = model.CurrentResolution;
        GameSettings.ApplySettings();
    }



    private async void HandleDeleteSaveData()
    {
        isWarningOpen = true;
        bool confirmed = await warningPresenter.OpenAsync("OPTION_DELETE_WARNING", view.HideAsync(), () => view.ShowAsync());
        isWarningOpen = false;

        if (confirmed)
        {
            GameProgressManager.Instance.DeleteSaveFile(GameProgressManager.SaveType.Auto);
            GameProgressManager.Instance.DeleteSaveFile(GameProgressManager.SaveType.Manual);
            Debug.Log("Save Data Deleted");
        }
    }

    private async UniTaskVoid CloseAsync()
    {
        isActive = false;
        // Save options on close
        ApplySettings();

        AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
        await view.HideAsync();
        closeCompletionSource.TrySetResult();
    }

}
