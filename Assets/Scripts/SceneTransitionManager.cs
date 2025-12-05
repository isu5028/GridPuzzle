using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private Canvas canvas;
    [SerializeField] private Image fadeImage;
    [SerializeField] private float defaultDuration = 0.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeCanvas();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeCanvas()
    {
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("TransitionCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // 最前面
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(canvasObj.transform, false);
            fadeImage = imageObj.AddComponent<Image>();
            fadeImage.color = Color.black;

            // 全画面に広げる
            RectTransform rect = fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            // 最初は透明に
            fadeImage.color = new Color(0, 0, 0, 0);
            fadeImage.raycastTarget = false; // 操作をブロックしない

            DontDestroyOnLoad(canvasObj);
        }
    }

    public async UniTask FadeOut(float duration = -1)
    {
        if (duration < 0) duration = defaultDuration;

        fadeImage.raycastTarget = true; // 操作ブロック
        await fadeImage.DOFade(1f, duration).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
    }

    public async UniTask FadeIn(float duration = -1)
    {
        if (duration < 0) duration = defaultDuration;

        await fadeImage.DOFade(0f, duration).SetEase(Ease.InQuad).AsyncWaitForCompletion();
        fadeImage.raycastTarget = false; // 操作ブロック解除
    }
}
