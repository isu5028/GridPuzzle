using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// スイープ方向の列挙型
/// </summary>
public enum SweepDirection
{
    BottomLeftToTopRight = 0,
    BottomRightToTopLeft = 1,
    TopRightToBottomLeft = 2,
    TopLeftToBottomRight = 3
}

/// <summary>
/// ディザディゾルブエフェクトを制御するコンポーネント
/// SpriteRendererまたはUI Imageに適用可能
/// </summary>
public class DitherDissolveController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Range(0f, 1f)]
    private float dissolveAmount = 0f;

    [SerializeField]
    private float patternScale = 1f;

    [Header("Sweep Settings")]
    [SerializeField]
    private SweepDirection sweepDirection = SweepDirection.BottomLeftToTopRight;

    [SerializeField, Range(0f, 2f)]
    private float sweepSpread = 1f;

    [Header("Animation")]
    [SerializeField]
    private bool autoPlay = false;

    [SerializeField]
    private float duration = 1f;

    [SerializeField]
    private AnimationCurve dissolveCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private Material materialInstance;
    private SpriteRenderer spriteRenderer;
    private Image uiImage;

    private void Awake()
    {
        // コンポーネントの取得
        spriteRenderer = GetComponent<SpriteRenderer>();
        uiImage = GetComponent<Image>();

        // マテリアルのインスタンスを作成
        if (spriteRenderer != null)
        {
            materialInstance = new Material(spriteRenderer.material);
            spriteRenderer.material = materialInstance;
        }
        else if (uiImage != null)
        {
            materialInstance = new Material(uiImage.material);
            uiImage.material = materialInstance;
        }
        else
        {
            Debug.LogError("SpriteRenderer or UI Image component not found!");
            return;
        }

        UpdateShaderProperties();
    }

    private void Start()
    {
        if (autoPlay)
        {
            PlayDissolve();
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && materialInstance != null)
        {
            UpdateShaderProperties();
        }
    }

    private void UpdateShaderProperties()
    {
        if (materialInstance == null) return;

        materialInstance.SetFloat("_DissolveAmount", dissolveAmount);
        materialInstance.SetFloat("_PatternScale", patternScale);
        materialInstance.SetFloat("_SweepDirection", (float)sweepDirection);
        materialInstance.SetFloat("_SweepSpread", sweepSpread);
    }

    /// <summary>
    /// ディゾルブアニメーションを再生
    /// </summary>
    public void PlayDissolve()
    {
        StartCoroutine(DissolveCoroutine(0f, 1f, duration));
    }

    /// <summary>
    /// リバースディゾルブアニメーションを再生（フェードイン）
    /// </summary>
    public void PlayReverseDissolve()
    {
        StartCoroutine(DissolveCoroutine(1f, 0f, duration));
    }

    /// <summary>
    /// カスタムディゾルブアニメーション
    /// </summary>
    public void PlayDissolve(float from, float to, float customDuration)
    {
        StartCoroutine(DissolveCoroutine(from, to, customDuration));
    }

    private IEnumerator DissolveCoroutine(float from, float to, float time)
    {
        float elapsed = 0f;

        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / time);
            float curvedT = dissolveCurve.Evaluate(t);

            dissolveAmount = Mathf.Lerp(from, to, curvedT);
            UpdateShaderProperties();

            yield return null;
        }

        dissolveAmount = to;
        UpdateShaderProperties();
    }

    /// <summary>
    /// ディゾルブ量を直接設定
    /// </summary>
    public void SetDissolveAmount(float value)
    {
        dissolveAmount = Mathf.Clamp01(value);
        UpdateShaderProperties();
    }

    /// <summary>
    /// パターンスケールを設定
    /// </summary>
    public void SetPatternScale(float scale)
    {
        patternScale = scale;
        UpdateShaderProperties();
    }

    /// <summary>
    /// スイープ方向を設定
    /// </summary>
    public void SetSweepDirection(SweepDirection direction)
    {
        sweepDirection = direction;
        UpdateShaderProperties();
    }

    /// <summary>
    /// スイープの時間差を設定（0=一様なディゾルブ, 大きいほど時間差が大きい）
    /// </summary>
    public void SetSweepSpread(float spread)
    {
        sweepSpread = Mathf.Clamp(spread, 0f, 2f);
        UpdateShaderProperties();
    }

    private void OnDestroy()
    {
        // インスタンスマテリアルを破棄
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }
}
