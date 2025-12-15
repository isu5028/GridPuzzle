using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement; // シーン遷移用
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UniRx;


public class OptionPresenter : MonoBehaviour
{
    [SerializeField] private OptionView view = null!;
    [SerializeField] private TurnManager turnManager = null!;
    [SerializeField] private PixelPerfectCamera pixelPerfectCamera = null!;
    [SerializeField] private HUDFeedback optionFeedback = null!;
    [SerializeField] private GridManager gridManager = null!;
    [SerializeField] private OptionLoadPresenter optionLoadPresenter;
    [SerializeField] private OptionWarningPresenter warningPresenter = null!;
    [SerializeField] private TransitionOut transitionOut = null!;
    private OptionModel model = null!;
    private bool isProcessingInput = false;
    private float inputCooldown = 0.2f;
    private float lastInputTime = 0f;
    private bool isAnimating = false;

    // 入力加速用の変数
    private float initialCooldown = 0.3f; // 初期クールダウン時間
    private float minCooldown = 0.02f; // 最小クールダウン時間
    private float accelerationRate = 0.5f; // クールダウンの減少率
    private Direction lastDirection;
    private int consecutiveInputCount = 0;
    private bool isMaxSpeed = false; // 最大速度に到達したか

    // Sub menu state
    private bool isSubMenuOpen = false;

    public void SetOptionState(bool isOpen)
    {
        turnManager.SetOptionState(isOpen);
    }

    public bool GetOptionState()
    {
        return turnManager.IsOptionOpen;
    }

    void Start()
    {
        // Modelの初期化
        model = new OptionModel();

        // 初期の見た目を反映
        RefreshView();
        ApplyScreenSettings();
    }

    void Update()
    {
        // オプション開閉（最優先で処理）
        if (view.InputOption.triggered && !isAnimating && !gridManager.IsEventPlaying)
        {
            if (isSubMenuOpen)
            {
                // Drop input if sub menu is open (handled by sub presenter)
                return;
            }
            else if (GetOptionState())
            {
                HandleCloseAsync().Forget();
                optionFeedback.PlayFeedback();
            }
            else
            {
                HandleOpenAsync().Forget();
                view.InitialShow();
                optionFeedback.PlayFeedback();
            }
            return;
        }

        if (!GetOptionState() || isSubMenuOpen) return;

        // 決定ボタン
        if (view.InputDecide.triggered && !isAnimating)
        {
            HandleSubmit();
            return;
        }

        // 上下左右の入力
        Vector2 moveInput = view.InputMove.ReadValue<Vector2>();

        // 入力がない場合は即座にクールダウンをリセット
        if (moveInput == Vector2.zero)
        {
            ResetInputAcceleration();
            isProcessingInput = false; // クールダウンもリセット
            return;
        }

        // 入力クールダウン処理
        if (isProcessingInput && Time.time - lastInputTime > inputCooldown)
        {
            isProcessingInput = false;
        }

        if (isProcessingInput) return;

        // 方向入力の処理
        Direction currentDirection = Utility.GetDirectionFromStickInput(moveInput);
        // 方向に応じた処理
        switch (currentDirection)
        {
            case Direction.Up:
                HandleVerticalMove(-1);
                SetInputCooldown(currentDirection, false); // 加速なし
                break;
            case Direction.Down:
                HandleVerticalMove(1);
                SetInputCooldown(currentDirection, false); // 加速なし
                break;
            case Direction.Left:
                HandleHorizontalMove(-1, isMaxSpeed ? 2 : 1);
                SetInputCooldown(currentDirection, true); // 加速あり
                break;
            case Direction.Right:
                HandleHorizontalMove(1, isMaxSpeed ? 2 : 1);
                SetInputCooldown(currentDirection, true); // 加速あり
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
            // 加速あり: クールダウンを徐々に短くする
            inputCooldown = Mathf.Max(
                minCooldown,
                initialCooldown * Mathf.Pow(accelerationRate, consecutiveInputCount - 1)
            );
            // 最大速度に達したかチェック
            isMaxSpeed = Mathf.Approximately(inputCooldown, minCooldown);
        }
        else
        {
            // 加速なし: 常に初期クールダウン
            inputCooldown = initialCooldown;
        }
    }

    private void ResetInputAcceleration()
    {
        consecutiveInputCount = 0;
        inputCooldown = initialCooldown;
        isMaxSpeed = false;
    }

    // 上下移動の処理
    private void HandleVerticalMove(int direction)
    {
        model.MoveSelection(direction);
        view.UpdateCursorPosition((int)model.CurrentItem);
        AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
    }

    // 左右移動（音量調整）の処理
    private void HandleHorizontalMove(int direction, int amount = 1)
    {
        switch (model.CurrentItem)
        {
            case OptionModel.ItemType.BgmVolume:
                for (int i = 0; i < amount; i++)
                {
                    model.ChangeBgm(direction);
                }
                view.UpdateBgmDisplay(model.BgmLevel, direction);
                AudioManager.Instance.SetBGMVolume(model.BgmLevel / 100f);
                GameSettings.BgmVolume = model.BgmLevel; // Sync
                GameProgressManager.Instance.SaveOptions(); // Save
                break;

            case OptionModel.ItemType.SeVolume:
                if(model.SeLevel == 0 && direction < 0) break;
                if(model.SeLevel == model.MaxLevel && direction > 0) break;
                for (int i = 0; i < amount; i++)
                {
                    model.ChangeSe(direction);
                }
                view.UpdateSeDisplay(model.SeLevel, direction);
                AudioManager.Instance.SetSEVolume(model.SeLevel / 100f);
                GameSettings.SeVolume = model.SeLevel; // Sync
                GameProgressManager.Instance.SaveOptions(); // Save
                AudioManager.Instance.PlaySE(AudioManager.SEType.tap);
                break;

            case OptionModel.ItemType.ScreenMode:
                model.ChangeScreenMode(direction);
                view.UpdateScreenModeDisplay(model.ScreenMode, direction);
                ApplyScreenSettings(); // Validates and Applies
                GameProgressManager.Instance.SaveOptions(); // Save
                break;

            case OptionModel.ItemType.Resolution:
                model.ChangeResolution(direction);
                view.UpdateResolutionDisplay(model.CurrentResolution, direction);
                ApplyScreenSettings(); // Validates and Applies
                GameProgressManager.Instance.SaveOptions(); // Save
                break;

            case OptionModel.ItemType.GameSpeed:
                model.ChangeGameSpeed(direction);
                view.UpdateGameSpeedDisplay(model.GameSpeed, direction);
                ApplyGameSpeed(); // Validates and Applies
                GameProgressManager.Instance.SaveOptions(); // Save
                break;

            case OptionModel.ItemType.Language:
                model.ChangeLanguage(direction);
                view.UpdateLanguageDisplay(model.Language, direction);
                ApplyLanguage(); // Validates and Applies
                GameProgressManager.Instance.SaveOptions(); // Save
                break;
        }
    }

    private void HandleSubmit()
    {
        //AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);

        switch (model.CurrentItem)
        {
            case OptionModel.ItemType.Save:
                view.BlinkCursorAsync().Forget();
                HandleSaveAsync().Forget();
                break;

            case OptionModel.ItemType.Load:
                view.BlinkCursorAsync().Forget();
                HandleLoadAsync().Forget();
                break;

            case OptionModel.ItemType.Close:
                view.BlinkCursorAsync().Forget();
                HandleCloseAsync().Forget();
                break;

            case OptionModel.ItemType.ReturnToTitle:
                view.BlinkCursorAsync().Forget();
                HandleReturnToTitleAsync().Forget();
                break;
        }
    }

    private async UniTaskVoid HandleCloseAsync()
    {
        AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
        isAnimating = true;
        await view.HideAsync(); // アニメーション完了を待つ
        SetOptionState(false); // アニメーション後に状態変更
        isAnimating = false;
    }

    private async UniTaskVoid HandleOpenAsync()
    {
        AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
        isAnimating = true;
        SetOptionState(true);
        await view.ShowAsync();
        isAnimating = false;
    }

    private void ApplyScreenSettings()
    {
        FullScreenMode mode = model.ScreenMode == OptionModel.ScreenModeType.Fullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

#if !UNITY_WEBGL
        Screen.SetResolution(model.CurrentResolution.x, model.CurrentResolution.y, mode);
#endif

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.ApplyResolutionSettings(model.CurrentResolution);
        }

        GameSettings.screenMode = (mode == FullScreenMode.Windowed) ? 0 : 1;
        GameSettings.Resolution = model.CurrentResolution;
    }

    private void ApplyGameSpeed()
    {
        GameSettings.GameSpeed = model.GameSpeed == OptionModel.GameSpeedType.Normal ? 0 : 1;
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

    private void RefreshView()
    {
        view.UpdateCursorPosition((int)model.CurrentItem);
        view.UpdateBgmDisplay(model.BgmLevel);
        view.UpdateSeDisplay(model.SeLevel);

        view.UpdateScreenModeDisplay(model.ScreenMode);
        view.UpdateResolutionDisplay(model.CurrentResolution);
        view.UpdateGameSpeedDisplay(model.GameSpeed);
        view.UpdateLanguageDisplay(model.Language);
    }

    private async UniTaskVoid HandleSaveAsync()
    {
        // 敵ターン中なら終了待ち
        if (!turnManager.IsPlayerTurn || turnManager.IsProcessingTurn)
        {
            Debug.Log("Waiting for turn to finish...");
            // フィードバックを出したい場合はここでUI表示などを行う

            await UniTask.WaitUntil(() => turnManager.IsPlayerTurn && !turnManager.IsProcessingTurn);
        }

        gridManager.SaveGame(GameProgressManager.SaveType.Manual);
        AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
        Debug.Log("Game Saved via Option");
        // "Saved" ポップアップなどを出すならここ
    }

    private async UniTaskVoid HandleLoadAsync()
    {
        isSubMenuOpen = true;

        // Viewを上へ隠す
        isAnimating = true;

        // Load Menuを開いて待機 (HideUpAsyncを渡して並列再生)
        // さらに、Cancel時はShowAsync()を並列再生するようコールバックを渡す
        bool loaded = await optionLoadPresenter.OpenAsync(
            view.HideUpAsync(),
            () => {
                 // OptionViewを元に戻す（Loadが閉じるのと同時）
                 return view.ShowAsync();
            }
        );

        isAnimating = false; // Ensure cleared

        if (loaded)
        {
            // ロード完了時はメニュー全体を閉じる
            SetOptionState(false);
            isSubMenuOpen = false;
            return;
        }

        // キャンセルで戻ってきた場合
        // OpenAsync内でShowAsyncが完了しているため、ここでは待たない
        // ただし状態リセットは必要

        isSubMenuOpen = false;
    }
    private async UniTaskVoid HandleReturnToTitleAsync()
    {
        isSubMenuOpen = true;

        bool confirmed = await warningPresenter.OpenAsync("OPTION_RETURN_WARNING", view.HideAsync(), () => view.ShowAsync());

        if (confirmed)
        {
             // Transition Out
             if (transitionOut != null) await transitionOut.AnimateAsync();
             // Load Title
             SceneManager.LoadScene("Title");
        }
        else
        {
             isSubMenuOpen = false;
        }
    }
}