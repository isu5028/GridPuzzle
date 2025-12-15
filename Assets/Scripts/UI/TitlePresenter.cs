using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TitlePresenter : MonoBehaviour
{
    [SerializeField] private TitleView view = null!;
    [SerializeField] private TitleLoadPresenter loadPresenter = null!;
    [SerializeField] private TransitionOut transitionOut = null!;

    private TitleModel model = new TitleModel();

    private InputAction inputMove;
    private InputAction inputDecide;

    private bool isProcessingInput = false;
    private float inputCooldown = 0.2f;
    private float lastInputTime = 0f;

    private bool isMenuOpen = true; // Title menu active

    private void Start()
    {
        inputMove = InputSystem.actions.FindAction("Move");
        inputDecide = InputSystem.actions.FindAction("Decide");

        RefreshView();
    }

    private void RefreshView()
    {
        view.UpdateStartText(model.HasSaveData());
        view.UpdateCursor((int)model.CurrentItem);
    }

    private void Update()
    {
        if (!isMenuOpen) return;

        HandleInput();
    }

    private void HandleInput()
    {
        // Decide
        if (inputDecide.triggered)
        {
            HandleSubmit();
            return;
        }

        // Move
        Vector2 moveInput = inputMove.ReadValue<Vector2>();
        if (moveInput == Vector2.zero)
        {
            isProcessingInput = false;
            return;
        }

        if (isProcessingInput && Time.time - lastInputTime > inputCooldown)
        {
            isProcessingInput = false;
        }

        if (isProcessingInput) return;

        Direction dir = Utility.GetDirectionFromStickInput(moveInput);
        if (dir == Direction.Up)
        {
            model.MoveSelection(-1);
            view.UpdateCursor((int)model.CurrentItem);
            AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            isProcessingInput = true;
            lastInputTime = Time.time;
        }
        else if (dir == Direction.Down)
        {
            model.MoveSelection(1);
            view.UpdateCursor((int)model.CurrentItem);
            AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            isProcessingInput = true;
            lastInputTime = Time.time;
        }
    }

    private void HandleSubmit()
    {
        switch (model.CurrentItem)
        {
            case TitleModel.TitleItem.Start:
                HandleStart();
                break;
            case TitleModel.TitleItem.Option:
                HandleOption();
                break;
            case TitleModel.TitleItem.Quit:
                HandleQuit();
                break;
        }
    }

    private void HandleStart()
    {
        AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);

        if (model.HasSaveData())
        {
            // Open Load Selection
            isMenuOpen = false;
            // Hide Title View -> Open Load View
            OpenLoadSelectionAsync().Forget();
        }
        else
        {
            // Start New Game directly
            StartNewGameAsync().Forget();
        }
    }

    private async UniTaskVoid OpenLoadSelectionAsync()
    {
        // Hide Title Menu
        // Open Load Menu
        // If Load Menu Cancelled -> Show Title Menu

        bool loaded = await loadPresenter.OpenAsync(
            view.HideAsync(),
            () => view.ShowAsync()
        );

        if (!loaded)
        {
            isMenuOpen = true; // Return to title control
        }
        // If loaded, the scene will change from presenter, so do nothing.
    }

    private async UniTaskVoid StartNewGameAsync()
    {
        isMenuOpen = false;
        // Play Transition
        if (transitionOut != null)
        {
            await transitionOut.AnimateAsync();
        }
        // Load InGame
        GameProgressManager.Instance.ResetEntireProgress(); // Ensure fresh start
        SceneManager.LoadScene("InGame");
    }

    [SerializeField] private TitleOptionPresenter optionPresenter = null!;

    // ...

    private void HandleOption()
    {
        AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
        OpenOptionAsync().Forget();
    }

    private async UniTaskVoid OpenOptionAsync()
    {
        isMenuOpen = false;
        await view.HideAsync();

        await optionPresenter.OpenAsync();

        RefreshView(); // Update view in case save data was deleted
        await view.ShowAsync();
        isMenuOpen = true;
    }

    private void HandleQuit()
    {
        AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel); // Or decide?
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
