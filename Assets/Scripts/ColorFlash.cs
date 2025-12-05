using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class ColorFlash : MonoBehaviour
{
    [SerializeField] private Material baseMaterial; // 元となるマテリアル
    [SerializeField] private float animationDuration = 0.5f;

    private SpriteRenderer spriteRenderer;
    private Material originalMaterial; // 元のマテリアル
    private Material instanceMaterial; // アニメーション用のインスタンス
    private CancellationTokenSource cancellationTokenSource;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalMaterial = spriteRenderer.material;
    }

    public void AnimateToColor(Color targetColor, float duration = -1f)
    {
        if (duration < 0) duration = animationDuration;

        // 既存のアニメーションをキャンセル
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        cancellationTokenSource = new CancellationTokenSource();
        ColorTransition(targetColor, duration, cancellationTokenSource.Token).Forget();
    }

    private async UniTask ColorTransition(Color targetColor, float duration, CancellationToken cancellationToken)
    {
        try
        {
            // マテリアルのインスタンスを作成して適用
            instanceMaterial = new Material(baseMaterial);
            spriteRenderer.material = instanceMaterial;

            float elapsedTime = 0f;

            // 色を変化させる（0 → 1）
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                t = 1;

                instanceMaterial.SetColor("_TargetColor", targetColor);
                instanceMaterial.SetFloat("_BlendAmount", t);

                await UniTask.Yield(cancellationToken);
            }

            instanceMaterial.SetFloat("_BlendAmount", 0f);
        }
        catch (System.OperationCanceledException)
        {
            // キャンセルされた場合
        }
        finally
        {
            // マテリアルを破棄して元に戻す
            CleanupMaterial();
        }
    }

    private void CleanupMaterial()
    {
        if (instanceMaterial != null)
        {
            spriteRenderer.material = originalMaterial;
            Destroy(instanceMaterial);
            instanceMaterial = null;
        }
    }

    private void OnDestroy()
    {
        // キャンセルトークンをクリーンアップ
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        // オブジェクト破棄時にマテリアルもクリーンアップ
        CleanupMaterial();
    }
}