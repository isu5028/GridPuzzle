using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class ExplosionVFX : MonoBehaviour
{
    private SpriteRenderer spriteRenderer = null!;
    private Material dissolveMaterial = null!;

    public async UniTask PlayEffect(Shader dissolveShader, Sprite squareSprite, Vector3 startPos, float targetScale)
    {
        Debug.Log($"[ExplosionVFX] PlayEffect started at {startPos} with scale {targetScale}");

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = squareSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 100; // Very high
        // Use a common sorting layer or set it explicitly if you have one
        // spriteRenderer.sortingLayerName = "LowerEffect";

        if (dissolveShader == null)
        {
            Debug.LogError("[ExplosionVFX] Dissolve Shader is null!");
            return;
        }

        // Create a unique material instance
        dissolveMaterial = new Material(dissolveShader);
        spriteRenderer.material = dissolveMaterial;

        dissolveMaterial.SetFloat("_DissolveAmount", 0f);
        dissolveMaterial.SetFloat("_PatternScale",1f);
        dissolveMaterial.SetFloat("_UseTexture", 0f);
        dissolveMaterial.SetFloat("_SweepDirection", 0f);
        dissolveMaterial.SetFloat("_SweepSpread", 1.0f);

        // Ensure visible Z (standard is 0 in 2D, but let's be sure it's not behind camera)
        transform.position = new Vector3(startPos.x, startPos.y, -1f);
        transform.localScale = Vector3.zero;

        // 1. Expand
        Debug.Log("[ExplosionVFX] Expanding...");
        await transform.DOScale(targetScale, 0.15f).SetEase(Ease.OutCubic).AsyncWaitForCompletion();

        // 2. Dissolve
        Debug.Log("[ExplosionVFX] Dissolving...");
        await DOTween.To(() => dissolveMaterial.GetFloat("_DissolveAmount"),
                         x => dissolveMaterial.SetFloat("_DissolveAmount", x),
                         1f, 0.4f)
                     .SetEase(Ease.InQuad)
                     .AsyncWaitForCompletion();

        Debug.Log("[ExplosionVFX] Effect finished. Destroying.");
        Destroy(gameObject);
    }
}
