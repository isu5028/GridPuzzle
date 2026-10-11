using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// ゲームクリア時のエンディング演出を管理するクラス。
/// GridUIManager と同じ GameObject にアタッチし、Inspector で各参照を設定してください。
///
/// 演出フロー:
/// 1. プレイヤー DitherDissolve フェードアウト
/// 2. ステージ (GridParent) が下へゆっくりスクロール
/// 3. 左 HUD (Option/Map/Coin) が左へ、右 HUD (Reset/Undo/Redo) が右へスライドアウト
/// 4. タイトルロゴが DitherDissolve フェードイン (BottomLeft→TopRight)
/// 5. タイトルロゴ表示維持
/// 6. タイトルロゴが DitherDissolve フェードアウト (TopRight→BottomLeft)
/// 7. クレジットカード x4 フェードイン/アウト
/// 8. 暗転してタイトルシーンへ
/// </summary>
public class GameClearSequence : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector 設定
    // ─────────────────────────────────────────────────────────────

    [Header("Grid")]
    [SerializeField] private GridVisualizer gridVisualizer = null!;

    [Header("HUD Transforms – Left side (will slide left)")]
    [SerializeField] private Transform hudOption = null!;
    [SerializeField] private Transform hudMap = null!;
    [SerializeField] private Transform hudCoin = null!;

    [Header("HUD Transforms – Right side (will slide right)")]
    [SerializeField] private Transform hudReset = null!;
    [SerializeField] private Transform hudUndo = null!;
    [SerializeField] private Transform hudRedo = null!;

    [Header("Title Logo")]
    [Tooltip("Title ルート GameObject（非アクティブになっている親）")]
    [SerializeField] private GameObject titleRoot = null!;
    [Tooltip("Logo の SpriteRenderer を持つ GameObject")]
    [SerializeField] private SpriteRenderer titleLogoRenderer = null!;

    [Header("Credits")]
    [Tooltip("クレジットカードに使用するフォント（null の場合デフォルト）")]
    [SerializeField] private TMP_FontAsset creditFont = null!;
    [SerializeField] private float creditFontSize = 6f;
    [SerializeField] private Color creditColor = Color.white;
    [Tooltip("GridVisualizer と同じ TMP_SDF_DitherDissolve シェーダーを設定してください")]
    [SerializeField] private Shader dissolveTextShader = null;
    [SerializeField] private float creditDissolvePatternScale = 1f;
    [SerializeField] private float creditDissolveSweepSpread = 1f;

    [Header("Timings (seconds)")]
    [SerializeField] private float playerFadeOutDuration = 0.8f;
    [SerializeField] private float stageSlideDownDuration = 3.0f;
    [SerializeField] private float hudSlideOutDuration = 0.6f;
    [SerializeField] private float hudSlideOutOffset = 320f;
    [SerializeField] private float logoFadeInDuration = 1.5f;
    [SerializeField] private float logoHoldDuration = 2.0f;
    [SerializeField] private float logoFadeOutDuration = 1.0f;
    [SerializeField] private float creditFadeInDuration = 0.8f;
    [SerializeField] private float creditHoldDuration = 2.5f;
    [SerializeField] private float creditFadeOutDuration = 0.5f;
    [SerializeField] private float finalBlackDuration = 1.0f;
    [SerializeField] private float finalHoldDuration = 0.8f;
    [SerializeField] private float stageSlideDownAmount = 400f;

    // ─────────────────────────────────────────────────────────────
    //  クレジット文字列
    // ─────────────────────────────────────────────────────────────

    private static readonly string[] CreditLinesJapanese =
    {
        "制作\n\nisu",
        "フォント\n\n5x5 ノスタルジア\n魔導太丸ゴシック\nマキ丸ハンド\nx16y32pxGridGazer",
        "音楽\n\nトーマス大森音楽工房 様",
        "Thank You for Playing!",
    };

    private static readonly string[] CreditLinesEnglish =
    {
        "Created by\n\nisu",
        "Fonts\n\n5x5 Nostalgia\nMadou Futo Maru Gothic\nMakimaru Hand\nx16y32pxGridGazer",
        "Music\n\nThomas Oomori Music Studio",
        "Thank You for Playing!",
    };

    private static string[] CreditLines =>
        GameSettings.Language == 0 ? CreditLinesEnglish : CreditLinesJapanese;

    // ─────────────────────────────────────────────────────────────
    //  黒幕スプライト（Fade to Black 用）
    // ─────────────────────────────────────────────────────────────

    private SpriteRenderer blackOverlay;
    private Texture2D blackOverlayTexture;
    private Sprite blackOverlaySprite;

    // ─────────────────────────────────────────────────────────────
    //  公開 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// エンディング演出全体を再生し、Title シーンへ遷移する。
    /// GridUIManager.PlayEndingSequence() から await してください。
    /// </summary>
    public async UniTask PlayAsync()
    {
        // ── Phase 0: 準備 ────────────────────────────────────────
        CreateBlackOverlay();
        SetBlackOverlayAlpha(0f);

        // ── Phase 1: プレイヤー フェードアウト ──────────────────
        // Player はPrefabから実行時生成されるのでFindObjectOfTypeで取得
        var player = FindObjectOfType<Player>();
        if (player != null)
        {
            await player.FadeOutAsync(playerFadeOutDuration, goingDown: true);
        }

        // ── Phase 2 & 3: ステージ下スクロール + HUD スライドアウト（並列）
        {
            var tasks = new List<UniTask>();

            // ステージを下へ
            if (gridVisualizer != null)
            {
                var stageTween = gridVisualizer.MoveGridParent(
                    new Vector3(0f, -stageSlideDownAmount, 0f),
                    stageSlideDownDuration);
                tasks.Add(stageTween.AsyncWaitForCompletion().AsUniTask());
            }

            // 左 HUD をスライドアウト
            tasks.Add(SlideHudOffScreen(hudOption, -hudSlideOutOffset, hudSlideOutDuration));
            tasks.Add(SlideHudOffScreen(hudMap,    -hudSlideOutOffset, hudSlideOutDuration));
            tasks.Add(SlideHudOffScreen(hudCoin,   -hudSlideOutOffset, hudSlideOutDuration));

            // 右 HUD をスライドアウト
            tasks.Add(SlideHudOffScreen(hudReset, +hudSlideOutOffset, hudSlideOutDuration));
            tasks.Add(SlideHudOffScreen(hudUndo,  +hudSlideOutOffset, hudSlideOutDuration));
            tasks.Add(SlideHudOffScreen(hudRedo,  +hudSlideOutOffset, hudSlideOutDuration));

            await UniTask.WhenAll(tasks);
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f));

        // ── Phase 4: タイトルロゴ フェードイン ──────────────────
        DitherDissolveController logoDissolve = null!;
        if (titleRoot != null && titleLogoRenderer != null)
        {
            titleRoot.SetActive(true);

            // DitherDissolveController を Logo の SpriteRenderer と同じ GO に追加
            logoDissolve = titleLogoRenderer.GetComponent<DitherDissolveController>();
            if (logoDissolve == null)
                logoDissolve = titleLogoRenderer.gameObject.AddComponent<DitherDissolveController>();

            // 初期状態を「完全に非表示」にしてからフェードイン
            logoDissolve.SetSweepDirection(SweepDirection.TopRightToBottomLeft);
            logoDissolve.SetDissolveAmount(1f);

            // DitherDissolveController.PlayDissolve(from, to, duration)
            logoDissolve.PlayDissolve(1f, 0f, logoFadeInDuration);
            await UniTask.Delay(System.TimeSpan.FromSeconds(logoFadeInDuration));
        }

        // ── Phase 5: タイトルロゴ 表示キープ ────────────────────
        await UniTask.Delay(System.TimeSpan.FromSeconds(logoHoldDuration));

        // ── Phase 6: タイトルロゴ フェードアウト（逆方向）───────
        if (logoDissolve != null)
        {
            logoDissolve.SetSweepDirection(SweepDirection.BottomLeftToTopRight);
            logoDissolve.PlayDissolve(0f, 1f, logoFadeOutDuration);
            await UniTask.Delay(System.TimeSpan.FromSeconds(logoFadeOutDuration));
        }

        if (titleRoot != null)
            titleRoot.SetActive(false);

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.3f));

        // ── Phase 7: クレジットカード ─────────────────────────
        foreach (var line in CreditLines)
        {
            await ShowCreditCardAsync(line);
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f));
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.3f));

        // ── Phase 8: 暗転 → タイトルへ ──────────────────────────
        await FadeToBlackAsync(finalBlackDuration);
        await UniTask.Delay(System.TimeSpan.FromSeconds(finalHoldDuration));

        CleanupBlackOverlayResources();
        SceneManager.LoadScene("Title");
    }

    private void OnDestroy()
    {
        CleanupBlackOverlayResources();
    }

    // ─────────────────────────────────────────────────────────────
    //  HUD スライドアウト
    // ─────────────────────────────────────────────────────────────

    private async UniTask SlideHudOffScreen(Transform t, float xOffset, float duration)
    {
        if (t == null) return;
        float targetX = t.localPosition.x + xOffset;
        await t.DOLocalMoveX(targetX, duration)
               .SetEase(Ease.InCubic)
               .AsyncWaitForCompletion();
    }

    // ─────────────────────────────────────────────────────────────
    //  クレジットカード表示
    // ─────────────────────────────────────────────────────────────

    private async UniTask ShowCreditCardAsync(string text)
    {
        // TextMeshPro オブジェクトを動的生成（world-space）
        var go = new GameObject("CreditCard");
        go.transform.SetParent(null);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = creditFontSize;
        tmp.color = creditColor; // アルファは dissolve マテリアル側で制御するので 1 のまま
        tmp.sortingOrder = 3000;
        if (creditFont != null) tmp.font = creditFont;
        // 自動折り返しを無効化 → \n のみで改行され、横幅不足による途中折り返しを防ぐ
        tmp.enableWordWrapping = false;
        // RectTransform を十分広く設定（world-space TMP はデフォルトが極小）
        tmp.rectTransform.sizeDelta = new Vector2(300f, 200f);

        // DitherDissolve マテリアルを生成して fontMaterial に適用
        Material dissolveMat = CreateCreditDissolveMaterial(tmp);

        if (dissolveMat != null)
        {
            // フェードイン（_DissolveAmount: 1→0）
            dissolveMat.SetFloat("_SweepDirection", (float)SweepDirection.TopRightToBottomLeft);
            dissolveMat.SetFloat("_DissolveAmount", 1f);
            await DOTween.To(
                    () => dissolveMat.GetFloat("_DissolveAmount"),
                    v => dissolveMat.SetFloat("_DissolveAmount", v),
                    0f, creditFadeInDuration)
                .SetEase(Ease.OutQuad)
                .AsyncWaitForCompletion();

            // ホールド
            await UniTask.Delay(System.TimeSpan.FromSeconds(creditHoldDuration));

            // フェードアウト（_DissolveAmount: 0→1）
            dissolveMat.SetFloat("_SweepDirection", (float)SweepDirection.BottomLeftToTopRight);
            await DOTween.To(
                    () => dissolveMat.GetFloat("_DissolveAmount"),
                    v => dissolveMat.SetFloat("_DissolveAmount", v),
                    1f, creditFadeOutDuration)
                .SetEase(Ease.InQuad)
                .AsyncWaitForCompletion();
        }
        else
        {
            // dissolveShader が未設定の場合は alpha フォールバック
            tmp.color = new Color(creditColor.r, creditColor.g, creditColor.b, 0f);
            await DOTween.To(
                    () => tmp.color.a,
                    a => tmp.color = new Color(creditColor.r, creditColor.g, creditColor.b, a),
                    1f, creditFadeInDuration)
                .SetEase(Ease.OutQuad).AsyncWaitForCompletion();
            await UniTask.Delay(System.TimeSpan.FromSeconds(creditHoldDuration));
            await DOTween.To(
                    () => tmp.color.a,
                    a => tmp.color = new Color(creditColor.r, creditColor.g, creditColor.b, a),
                    0f, creditFadeOutDuration)
                .SetEase(Ease.InQuad).AsyncWaitForCompletion();
        }

        if (dissolveMat != null) Destroy(dissolveMat);
        Destroy(go);
    }

    /// <summary>
    /// GridVisualizer.CreateTextDissolveMaterial と同じ手順で TMP 用 DitherDissolve マテリアルを生成。
    /// dissolveTextShader が未設定の場合は null を返す。
    /// </summary>
    private Material CreateCreditDissolveMaterial(TextMeshPro tmp)
    {
        if (tmp == null || tmp.font == null || dissolveTextShader == null) return null;

        Material src = tmp.fontSharedMaterial;
        Material mat = new Material(dissolveTextShader);

        // TMP SDF 描画に必須のプロパティをコピー
        CopyCreditMatTex(src, mat, "_MainTex");
        CopyCreditMatFloat(src, mat, "_TextureWidth");
        CopyCreditMatFloat(src, mat, "_TextureHeight");
        CopyCreditMatFloat(src, mat, "_GradientScale");
        CopyCreditMatFloat(src, mat, "_ScaleX");
        CopyCreditMatFloat(src, mat, "_ScaleY");
        CopyCreditMatFloat(src, mat, "_Sharpness");
        CopyCreditMatFloat(src, mat, "_FaceDilate");
        CopyCreditMatFloat(src, mat, "_WeightNormal");
        CopyCreditMatFloat(src, mat, "_WeightBold");
        if (src != null && src.HasProperty("_FaceColor") && mat.HasProperty("_FaceColor"))
            mat.SetColor("_FaceColor", src.GetColor("_FaceColor"));

        if (mat.HasProperty("_PatternScale"))
            mat.SetFloat("_PatternScale", Mathf.Max(0.01f, creditDissolvePatternScale));
        if (mat.HasProperty("_UseScreenSpaceDither"))
            mat.SetFloat("_UseScreenSpaceDither", 1f);
        if (mat.HasProperty("_SweepSpread"))
            mat.SetFloat("_SweepSpread", Mathf.Max(0f, creditDissolveSweepSpread));
        if (mat.HasProperty("_DissolveAmount"))
            mat.SetFloat("_DissolveAmount", 1f); // 最初は完全に非表示

        tmp.fontMaterial = mat;
        // fontMaterial セット後、TMP が管理するインスタンスを返す
        return tmp.fontMaterial;
    }

    private static void CopyCreditMatTex(Material src, Material dst, string name)
    {
        if (src != null && src.HasProperty(name) && dst.HasProperty(name))
            dst.SetTexture(name, src.GetTexture(name));
    }

    private static void CopyCreditMatFloat(Material src, Material dst, string name)
    {
        if (src != null && src.HasProperty(name) && dst.HasProperty(name))
            dst.SetFloat(name, src.GetFloat(name));
    }

    // ─────────────────────────────────────────────────────────────
    //  Fade to Black
    // ─────────────────────────────────────────────────────────────

    private void CreateBlackOverlay()
    {
        if (blackOverlay != null) return;

        var go = new GameObject("BlackOverlay");
        go.transform.SetParent(null);
        go.transform.localPosition = new Vector3(0f, 0f, -0.1f); // カメラ前面

        blackOverlay = go.AddComponent<SpriteRenderer>();
        // ピクセルパーフェクト用の大きな白スプライトを Texture2D から生成
        blackOverlayTexture = new Texture2D(1, 1);
        blackOverlayTexture.SetPixel(0, 0, Color.white);
        blackOverlayTexture.Apply();
        blackOverlaySprite = Sprite.Create(blackOverlayTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        blackOverlay.sprite = blackOverlaySprite;
        blackOverlay.color = new Color(0f, 0f, 0f, 0f);
        // 全画面を覆う大きさ（PixelsPerUnit=1 で大きく）
        go.transform.localScale = new Vector3(2000f, 2000f, 1f);
        blackOverlay.sortingOrder = 9999;
    }

    private void CleanupBlackOverlayResources()
    {
        if (blackOverlay != null)
        {
            Destroy(blackOverlay.gameObject);
            blackOverlay = null;
        }

        if (blackOverlaySprite != null)
        {
            Destroy(blackOverlaySprite);
            blackOverlaySprite = null;
        }

        if (blackOverlayTexture != null)
        {
            Destroy(blackOverlayTexture);
            blackOverlayTexture = null;
        }
    }

    private void SetBlackOverlayAlpha(float a)
    {
        if (blackOverlay == null) return;
        blackOverlay.color = new Color(0f, 0f, 0f, a);
    }

    private async UniTask FadeToBlackAsync(float duration)
    {
        if (blackOverlay == null) return;

        await DOTween.To(
                () => blackOverlay.color.a,
                a => blackOverlay.color = new Color(0f, 0f, 0f, a),
                1f,
                duration)
            .SetEase(Ease.InQuad)
            .AsyncWaitForCompletion();
    }
}
