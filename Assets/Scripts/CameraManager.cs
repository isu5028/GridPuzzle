using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering.Universal;
using System.Threading;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CameraManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static CameraManager Instance { get; private set; } = null!;



    [Header("Camera Settings")]
    [SerializeField] private Camera mainCamera = null!;
    [SerializeField] private PixelPerfectCamera pixelPerfectCamera = null!;

    [Header("Shake Settings")]
    [SerializeField] private float defaultShakeStrength = 0.5f;
    [SerializeField] private float defaultShakeDuration = 0.3f;
    [SerializeField] private GlitchEffect glitchEffect;

    private Vector3 originalPosition; // カメラの元の位置
    private Vector3 shakeOffset = Vector3.zero; // シェイクのオフセット
    private CancellationTokenSource shakeCancellationTokenSource;
    private bool isShaking = false; // シェイク中かどうかのフラグ
    private float currentShakeMagnitude = 0f; // 現在のシェイク強度

    // Camera movement variables
    [Header("Movement Settings")]
    [SerializeField] private bool isCameraMoving = false;
    [SerializeField] private Vector3 offsetPosition = Vector3.zero;
    [SerializeField] private float cameraSpeed = 5f;

    private void Awake()
    {
        // シングルトンの設定
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // カメラの初期設定
            if (mainCamera == null)
                mainCamera = Camera.main;

            originalPosition = mainCamera.transform.position;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 念のため、開始時に元の位置を記録
        // 念のため、開始時に元の位置を記録
        originalPosition = mainCamera.transform.position;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // PixelPerfectCameraの初期化
        if (pixelPerfectCamera == null && mainCamera != null)
        {
             mainCamera.TryGetComponent(out pixelPerfectCamera);
        }
        if (glitchEffect == null && mainCamera != null)
        {
            mainCamera.TryGetComponent(out glitchEffect);
        }
        ApplyResolutionSettings(GameSettings.Resolution);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Scene Loaded: " + scene.name);
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
             mainCamera.TryGetComponent(out pixelPerfectCamera);
             if (glitchEffect == null) mainCamera.TryGetComponent(out glitchEffect);
        }
        ApplyResolutionSettings(GameSettings.Resolution);
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        // シェイク中は位置を更新
        if (isShaking)
        {
            // シェイクを適用（常に元の位置を基準にする）
            mainCamera.transform.position = originalPosition + shakeOffset;
        }
        else if (isCameraMoving)
        {
            // カメラ移動処理
            Vector3 targetPosition = originalPosition + offsetPosition;
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                targetPosition,
                cameraSpeed * Time.deltaTime
            );
        }
        else
        {
            // シェイクも移動もしていない場合、元の位置に戻る
            // わずかにスムーズに戻るようにする
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                originalPosition,
                Time.deltaTime * 10f
            );
        }
    }

    public async UniTask MoveCameraTo(Vector3 targetPos, float duration)
    {
        await mainCamera.transform.DOMove(new Vector3(targetPos.x, targetPos.y, -10f), duration)
            .SetEase(Ease.InOutQuad)
            .AsyncWaitForCompletion();

    }

    public void SetPosition(Vector3 pos)
    {
        mainCamera.transform.position = new Vector3(pos.x, pos.y, -10f);
    }

    // 指定したステージサイズの中央座標を取得
    public Vector3 GetStageCenter(int width, int height, float cellSize, Vector2 offset)
    {
        float x = (width * cellSize) / 2.0f;
        float y = (height * cellSize) / 2.0f;
        return new Vector3(x, y, 0) + new Vector3(offset.x, offset.y, 0);
    }

    public void ShakeCamera()
    {
        ShakeCamera(defaultShakeDuration, defaultShakeStrength, 50f, Vector2.up);
    }

    public void PlayGlitch(float duration, float intensity)
    {
        if (glitchEffect != null)
        {
            glitchEffect.PlayGlitch(duration, intensity);
        }
    }

    // 方向を指定してカメラを揺らす
    public void ShakeCamera(float duration, float magnitude, float frequency, Vector2 direction)
    {
        // Check if a stronger shake is already active
        if (isShaking && magnitude < currentShakeMagnitude)
        {
            // Ignore smaller shake request while big one is running
            return;
        }

        // 前回のシェイクをキャンセル
        if (shakeCancellationTokenSource != null)
        {
            shakeCancellationTokenSource.Cancel();
            shakeCancellationTokenSource.Dispose();
            shakeCancellationTokenSource = null;
        }

        // Initialize for new shake
        currentShakeMagnitude = magnitude;
        shakeOffset = Vector3.zero;
        isShaking = false;

        // 新しいキャンセルトークンを作成
        shakeCancellationTokenSource = new CancellationTokenSource();
        // duration/magnitude are used directly (previously were multiplied by defaults which caused bug/squaring)
        DoShakeCamera(duration, magnitude, frequency, direction, shakeCancellationTokenSource.Token).Forget();
    }

    private async UniTask DoShakeCamera(float duration, float magnitude, float frequency, Vector2 direction, CancellationToken cancellationToken)
    {

        //Debug.Log($"ShakeCamera: duration={duration}, magnitude={magnitude}, frequency={frequency}, direction={direction}");

        // 1フレーム待ってからシェイク開始（キャンセル処理を確実にするため）
        await UniTask.Yield(cancellationToken);

        // シェイク開始時にフラグを立てる
        isShaking = true;

        float elapsed = 0f;

        // シェイクが終了するまでループ
        while(elapsed < duration)
        {
            // キャンセルされた場合は終了
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            elapsed += Time.deltaTime;

            // 減衰振動の計算
            float progress = elapsed / duration;
            float damping = 1.0f - Mathf.Clamp01(progress); // 徐々に弱くなる

            // Sin波で振動を作成
            float x = elapsed * frequency;
            float amplitude = Mathf.Sin(x) * damping * magnitude;

            // 方向に応じたシェイクオフセットを計算
            shakeOffset = direction.normalized * amplitude;

            // 1フレーム待機
            await UniTask.Yield(cancellationToken);
        }

        shakeOffset = Vector3.zero;
        shakeOffset = Vector3.zero;
        isShaking = false;
        currentShakeMagnitude = 0f;

    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (shakeCancellationTokenSource != null)
        {
            shakeCancellationTokenSource.Cancel();
            shakeCancellationTokenSource.Dispose();
            shakeCancellationTokenSource = null;
        }
    }

    public void ApplyResolutionSettings(Vector2Int resolution)
    {
        if (pixelPerfectCamera == null) return;

        int baseWidth = 960;
        int basePPU = 2;

        // 幅に基づいて倍率を計算
        float scale = (float)resolution.x / baseWidth;
        int ppu = Mathf.RoundToInt(basePPU * scale);

        if (ppu < 1) ppu = 1;

        pixelPerfectCamera.assetsPPU = ppu;
        pixelPerfectCamera.refResolutionX = resolution.x;
        pixelPerfectCamera.refResolutionY = resolution.y;
    }
}
