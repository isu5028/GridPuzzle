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

    /// <summary>
    /// 外部からoriginalMaterialを設定する（UnitがdissolveMaterialを設定した後に呼び出す）
    /// </summary>
    public void SetOriginalMaterial(Material material)
    {
        originalMaterial = material;
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
        // ローカル変数でこのトランジション専用のマテリアルを管理
        // （共有フィールドinstanceMaterialを使うと、キャンセル時に次のトランジションの
        //  マテリアルを誤ってnullにするrace conditionが発生するため）
        Material localMaterial = null;
        try
        {
            if (baseMaterial == null) return;

            // このトランジション専用のマテリアルインスタンスを作成して適用
            localMaterial = new Material(baseMaterial);
            instanceMaterial = localMaterial;
            spriteRenderer.material = localMaterial;

            float elapsedTime = 0f;

            // 色を変化させる（0 → 1）
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                t = 1;

                localMaterial.SetColor("_TargetColor", targetColor);
                localMaterial.SetFloat("_BlendAmount", t);

                await UniTask.Yield(cancellationToken);
            }

            localMaterial.SetFloat("_BlendAmount", 0f);
        }
        catch (System.OperationCanceledException)
        {
            // キャンセルされた場合
        }
        finally
        {
            // このトランジション専用のマテリアルのみを破棄する
            // instanceMaterial == localMaterial で比較することで、
            // 次のトランジションがinstanceMaterialを上書きした後でも安全に動作する
            if (localMaterial != null)
            {
                if (instanceMaterial == localMaterial)
                {
                    // 自分が最新のトランジション → 元のマテリアルに戻す
                    if (spriteRenderer != null)
                        spriteRenderer.material = originalMaterial;
                    instanceMaterial = null;
                }
                // else: 新しいトランジションが開始済みのため、そちらに任せる
                Destroy(localMaterial);
            }
        }
    }

    private void CleanupMaterial()
    {
        if (instanceMaterial != null)
        {
            if (spriteRenderer != null)
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