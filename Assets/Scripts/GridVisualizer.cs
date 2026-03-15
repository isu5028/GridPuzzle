using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using TMPro;

public class GridVisualizer : MonoBehaviour
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap tilemap = null!;
    [SerializeField] private Tilemap transitionTilemap = null!;
    [SerializeField] private TileBase wallTile = null!;
    [SerializeField] private TileBase floorTile = null!;
    [SerializeField] private TileBase floorGoblinTile = null!;
    [SerializeField] private TileBase stairUpTile = null!;    // 上階段タイル
    [SerializeField] private TileBase stairDownTile = null!;  // 下階段タイル
    [SerializeField] private TileBase brokenStairUpTile = null!; // 壊れた上階段タイルのプレハブ/タイル
    [SerializeField] private GameObject tileSyncMapPrefab = null!;
    [SerializeField] private List<GameObject> tutorialPrefabs = new List<GameObject>();
    [Header("Shop Item")]
    [SerializeField] private Sprite shopItemSprite = null!;
    [SerializeField] private TMP_FontAsset shopItemPriceFont = null!;
    [SerializeField] private Color shopItemPriceColor = Color.white;
    [SerializeField] private float shopItemSpriteCellRatio = 0.7f;
    [SerializeField] private float shopItemSpriteYOffsetCellRatio = 0.08f;
    [SerializeField] private float shopItemPriceYOffsetCellRatio = 0.40f;
    [SerializeField] private float shopItemPriceFontSize = 12f;
    [SerializeField] private float shopItemPriceScaleCellRatio = 0.08f;
    [SerializeField] private int shopItemSortingOrderOffset = 2;
    [SerializeField] private float shopItemPriceHoverShakeDistanceCellRatio = 0.08f;
    [Tooltip("1往復あたりの揺れ時間（秒）")]
    [SerializeField] private float shopItemPriceHoverShakeDuration = 1f;
    [Tooltip("振幅の減衰カーブ（周期には影響しません）")]
    [SerializeField] private Ease shopItemPriceHoverShakeEase = Ease.Linear;
    [SerializeField] private int shopItemPriceShakeRoundTrips = 1;
    [SerializeField] private float shopItemPriceShakeDecayPower = 1.1f;

    [Header("Shop Prompt")]
    [SerializeField] private TMP_FontAsset shopPromptFont = null!;
    [SerializeField] private Color shopPromptTextColor = Color.black;
    [SerializeField] private Color shopPromptBackgroundColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Vector2 shopPromptBackgroundSizeCellRatio = new Vector2(1.25f, 0.45f);
    [SerializeField] private float shopPromptYOffsetCellRatio = 0.95f;
    [SerializeField] private float shopPromptFontSize = 10f;
    [SerializeField] private float shopPromptScaleCellRatio = 0.08f;
    [SerializeField] private int shopPromptSortingOrderOffset = 230;
    [SerializeField] private int shopPromptSortingOrderMarginAboveUnit = 8;
    [SerializeField] private float shopPromptHorizontalPaddingCellRatio = 0.18f;
    [SerializeField] private float kingPromptHorizontalPaddingCellRatio = 0.18f;
    [SerializeField] private float kingPromptMinBackgroundWidthCellRatio = 0f;
    [SerializeField] private float kingPromptTextLeftPaddingCellRatio = 0.18f;
    [SerializeField] private float kingPromptLeftEdgeOffsetCellRatio = 0f;
    [Tooltip("吹き出しを表示してから自動非表示するまでの秒数（0以下で無効）")]
    [SerializeField] private float kingPromptDisplayDuration = 3f;
    [SerializeField] private float kingPromptTypewriterCharInterval = 0.04f;
    [SerializeField] private int kingPromptScalePulseMaxCount = 3;
    [SerializeField] private float kingPromptScaleSquashYMultiplier = 0.84f;
    [SerializeField] private float kingPromptScaleSquashDuration = 0.08f;
    [SerializeField] private float kingPromptScaleRestoreDuration = 0.13f;
    [SerializeField] private Ease kingPromptScaleSquashEase = Ease.OutQuad;
    [SerializeField] private Ease kingPromptScaleRestoreEase = Ease.OutQuad;
    [SerializeField] private float kingPromptBubblePulseDistanceCellRatio = 0.06f;
    [SerializeField] private float kingPromptBubblePulseUpDuration = 0.03f;
    [SerializeField] private float kingPromptBubblePulseDownDuration = 0.07f;
    [SerializeField] private Ease kingPromptBubblePulseUpEase = Ease.OutQuad;
    [SerializeField] private Ease kingPromptBubblePulseDownEase = Ease.OutQuad;
    [SerializeField] private float shopPromptEntranceUpDistanceCellRatio = 0.08f;
    [SerializeField] private float shopPromptEntranceDownDistanceCellRatio = 0.03f;
    [SerializeField] private float shopPromptEntranceUpDuration = 0.06f;
    [SerializeField] private float shopPromptEntranceDownDuration = 0.07f;
    [SerializeField] private float shopPromptEntranceReturnDuration = 0.08f;
    [SerializeField] private float shopPromptEntranceNarrowScaleX = 0.94f;
    [SerializeField] private Ease shopPromptEntranceUpEase = Ease.OutQuad;
    [SerializeField] private Ease shopPromptEntranceDownEase = Ease.OutQuad;
    [SerializeField] private Ease shopPromptEntranceReturnEase = Ease.OutQuad;

    [Header("Shop Prompt Dissolve")]
    [SerializeField] private float shopPromptDissolveDuration = 0.2f;
    [SerializeField] private Ease shopPromptDissolveInEase = Ease.OutQuad;
    [SerializeField] private Ease shopPromptDissolveOutEase = Ease.InQuad;
    [SerializeField] private float shopPromptDissolvePatternScale = 1f;
    [SerializeField] private float shopPromptDissolveSweepDirection = 0f;
    [SerializeField] private float shopPromptDissolveSweepSpread = 1f;

    [SerializeField] private Transform gridParent = null!;

    [Header("Settings")]
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private Vector2 tilemapOffset = new Vector2(-96f, -64f);

    [Header("Wall Break Effect")]
    [SerializeField] private Shader dissolveShader = null;
	[SerializeField] private Shader dissolveTextShader = null;
    public float CellSize => cellSize;
    public float KingPromptDisplayDuration => kingPromptDisplayDuration;
    public Vector2 TilemapOffset => tilemapOffset;
    public Transform GridParent => gridParent;
    public Tilemap MainTilemap => tilemap;
    public Tilemap TransitionTilemap => transitionTilemap;

    private List<GameObject> spawnedSyncMapObjects = new List<GameObject>();
    private List<GameObject> spawnedTutorialObjects = new List<GameObject>();
    private List<GameObject> spawnedShopItemObjects = new List<GameObject>();
    private List<GameObject> spawnedTransitionObjects = new List<GameObject>();

    private struct ShopItemVisualEntry
    {
        public StageSO stage;
        public Vector2Int position;
        public GameObject priceObject;
        public Vector3 priceBaseLocalPosition;
    }

    private readonly List<ShopItemVisualEntry> shopItemVisualEntries = new List<ShopItemVisualEntry>();
    private Func<StageSO, Vector2Int, bool> isShopItemPurchasedResolver;

    private GameObject shopPromptRoot;
    private SpriteRenderer shopPromptBackgroundRenderer;
    private TextMeshPro shopPromptText;
    private Material shopPromptBackgroundMaterial;
    private Material shopPromptTextDissolveMaterial;
    private Tween shopPromptTween;
    private Tween shopPromptShakeTween;
    private Tween shopPromptEntranceTween;
    private bool shopPromptTargetVisible;
    private float shopPromptShakeOffsetX;
    private float shopPromptEntranceOffsetY;
    private bool hasShopPromptAnchorWorldPos;
    private Vector3 shopPromptAnchorWorldPos;
    private GameObject kingPromptRoot;
    private SpriteRenderer kingPromptBackgroundRenderer;
    private TextMeshPro kingPromptText;
    private Material kingPromptBackgroundMaterial;
    private Material kingPromptTextDissolveMaterial;
    private Tween kingPromptTween;
    private Tween kingPromptTypewriterTween;
    private Tween kingPromptAutoHideTween;
    private Tween kingPromptScalePulseTween;
    private Tween kingPromptBubblePulseTween;
    private bool kingPromptTargetVisible;
    private int kingPromptQueuedScalePulseCount;
    private int kingPromptScalePulsePlayedCount;
    private int kingPromptQueuedBubblePulseCount;
    private float kingPromptBubbleOffsetY;
    private string kingPromptCurrentMessage;
    private string kingPromptPendingMessage;
    private Vector3 kingPromptPendingWorldPos;
    private bool kingPromptHasPending;
    private Transform kingPromptPulseTarget;
    private Vector3 kingPromptPulseBaseScale = Vector3.one;
    private static Sprite shopPromptWhiteSprite;

    private struct SyncMapEntry
    {
        public Vector2Int coords;
        public TileSyncMapController controller;
    }
    private List<SyncMapEntry> syncMapEntries = new List<SyncMapEntry>();

    // グリッド座標をワールド座標に変換
    public Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        // Tilemapの中心に合わせる
        return tilemap.CellToWorld(new Vector3Int(gridPos.x, gridPos.y, 0))
               + new Vector3(cellSize / 2f, cellSize / 2f, 0f) + new Vector3(tilemapOffset.x, tilemapOffset.y, 0);
    }

    // ワールド座標をグリッド座標に変換
    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        Vector3Int cellPos = tilemap.WorldToCell(worldPos);
        return new Vector2Int(cellPos.x, cellPos.y);
    }

    public void ClearAllTiles(bool keepTutorial = false)
    {
        tilemap.ClearAllTiles();
        CleanupObjects(spawnedSyncMapObjects);
        StopAllShopItemPriceShakes();
        CleanupObjects(spawnedShopItemObjects);
        shopItemVisualEntries.Clear();
        HideShopPromptImmediate();
        HideKingPromptImmediate();
        ClearSyncMapEntries();
        if (!keepTutorial)
        {
            CleanupObjects(spawnedTutorialObjects);
        }
    }

    public void ClearTransitionTiles()
    {
        transitionTilemap.ClearAllTiles();
        CleanupObjects(spawnedTransitionObjects);
    }

    public void ClearSyncMapEntries()
    {
        syncMapEntries.Clear();
    }

    public void SetShopItemPurchasedResolver(Func<StageSO, Vector2Int, bool> resolver)
    {
        isShopItemPurchasedResolver = resolver;
    }

    public void SetShopItemPurchasedVisual(StageSO stage, Vector2Int pos, bool isPurchased)
    {
        for (int i = 0; i < shopItemVisualEntries.Count; i++)
        {
            var entry = shopItemVisualEntries[i];
            if (entry.stage == stage && entry.position == pos && entry.priceObject != null)
            {
                StopShopItemPriceShake(entry.priceObject, entry.priceBaseLocalPosition);
                entry.priceObject.SetActive(!isPurchased);
            }
        }
    }

    public void PlayShopItemPriceShake(StageSO stage, Vector2Int pos)
    {
        if (stage == null) return;
        if (!TryGetShopItemVisualEntry(stage, pos, out var entry)) return;
        if (entry.priceObject == null || !entry.priceObject.activeInHierarchy) return;

        PlayShopItemPriceShakeTween(entry.priceObject, entry.priceBaseLocalPosition);
    }

    public void ShowShopPromptAt(Vector3 itemWorldPos, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            HideShopPrompt();
            return;
        }

        EnsureShopPromptObjects();
        if (shopPromptRoot == null || shopPromptText == null) return;

        bool wasVisible = shopPromptTargetVisible && shopPromptRoot.activeSelf;

        if (hasShopPromptAnchorWorldPos && (shopPromptAnchorWorldPos - itemWorldPos).sqrMagnitude > 0.0001f)
        {
            StopShopPromptShake();
        }

        hasShopPromptAnchorWorldPos = true;
        shopPromptAnchorWorldPos = itemWorldPos;

        shopPromptText.text = message;
        UpdatePromptLayout(
            shopPromptRoot,
            shopPromptBackgroundRenderer,
            shopPromptText,
            shopPromptBackgroundMaterial,
            itemWorldPos,
            alignLeftEdgeToAnchor: false,
            leftAlignText: false,
            horizontalPaddingCellRatio: shopPromptHorizontalPaddingCellRatio,
            minBackgroundWidthCellRatio: shopPromptBackgroundSizeCellRatio.x,
            leftPaddingCellRatio: shopPromptHorizontalPaddingCellRatio,
            anchorOffsetXCellRatio: 0f,
            extraWorldOffsetX: shopPromptShakeOffsetX,
            extraWorldOffsetY: shopPromptEntranceOffsetY
        );

        ApplyPromptSortingFromUnits(shopPromptBackgroundRenderer, shopPromptText);

        ShowPromptAnimated(
            shopPromptRoot,
            shopPromptText,
            shopPromptBackgroundMaterial,
            shopPromptTextDissolveMaterial,
            ref shopPromptTween,
            ref shopPromptTargetVisible
        );

        if (!wasVisible)
        {
            PlayShopPromptEntranceAnimation();
        }
    }

    public void HideShopPrompt()
    {
        StopShopPromptShake();
        StopShopPromptEntranceAnimation();
        hasShopPromptAnchorWorldPos = false;
        HidePromptAnimated(
            shopPromptRoot,
            shopPromptText,
            shopPromptBackgroundMaterial,
            shopPromptTextDissolveMaterial,
            ref shopPromptTween,
            ref shopPromptTargetVisible
        );
    }

    public void PlayShopPromptShake()
    {
        EnsureShopPromptObjects();
        if (shopPromptRoot == null || !shopPromptRoot.activeInHierarchy) return;

        StopShopPromptShake();

        float shakeDistance = Mathf.Max(0.01f, cellSize * shopItemPriceHoverShakeDistanceCellRatio);
        float roundTripDuration = Mathf.Max(0.05f, shopItemPriceHoverShakeDuration);
        int roundTrips = Mathf.Max(1, shopItemPriceShakeRoundTrips);
        float decayPower = Mathf.Max(0.1f, shopItemPriceShakeDecayPower);
        float totalDuration = roundTripDuration * roundTrips;

        shopPromptShakeTween = DOVirtual
            .Float(0f, 1f, totalDuration, t =>
            {
                float decayT = Mathf.Clamp01(DOVirtual.EasedValue(0f, 1f, t, shopItemPriceHoverShakeEase));
                float envelope = Mathf.Pow(1f - decayT, decayPower);
                float wave = Mathf.Sin(t * roundTrips * Mathf.PI * 2f);
                shopPromptShakeOffsetX = shakeDistance * envelope * wave;
            })
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                shopPromptShakeOffsetX = 0f;
                shopPromptShakeTween = null;
            });
    }

    public void ShowKingPromptAt(Vector3 kingWorldPos, string message, Transform kingTransform = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            HideKingPrompt();
            return;
        }

        SetKingPromptPulseTarget(kingTransform);

        EnsureKingPromptObjects();
        if (kingPromptRoot == null || kingPromptText == null) return;

        bool isCurrentlyShowing = kingPromptTargetVisible && kingPromptRoot.activeSelf;
        if (isCurrentlyShowing)
        {
            // 同じメッセージが表示中なら何もしない（毎フレーム呼ばれても安全、タイマーも維持）
            if (message == kingPromptCurrentMessage && !kingPromptHasPending)
            {
                UpdateKingPromptLayout(kingWorldPos);
                ApplyPromptSortingFromUnits(kingPromptBackgroundRenderer, kingPromptText);
                return;
            }

            // 異なるメッセージ：自動非表示タイマーをリセットしてフェードアウト→フェードイン
            kingPromptAutoHideTween?.Kill();
            kingPromptAutoHideTween = null;
            kingPromptPendingMessage = message;
            kingPromptPendingWorldPos = kingWorldPos;
            kingPromptHasPending = true;
            HideKingPromptForTransition();
        }
        else
        {
            kingPromptAutoHideTween?.Kill();
            kingPromptAutoHideTween = null;
            kingPromptHasPending = false;
            DoShowKingPromptImmediate(kingWorldPos, message);
        }
    }

    private void HideKingPromptForTransition()
    {
        if (kingPromptRoot == null || !kingPromptRoot.activeSelf)
        {
            if (kingPromptHasPending) ApplyPendingKingPrompt();
            return;
        }

        StopKingPromptTypewriter(revealAllCharacters: false);
        StopKingScalePulses();
        StopKingBubblePulses();

        KillPromptTween(ref kingPromptTween);
        SetPromptSweepDirection(kingPromptBackgroundMaterial, fadeIn: false);
        SetPromptSweepDirection(kingPromptTextDissolveMaterial, fadeIn: false);
        kingPromptTargetVisible = false;

        float startBg = GetPromptDissolveAmount(kingPromptBackgroundMaterial);
        float startText = GetPromptDissolveAmount(kingPromptTextDissolveMaterial);
        const float endAmount = 1f;

        if (!HasPromptDissolveMaterial(kingPromptBackgroundMaterial) || shopPromptDissolveDuration <= 0f)
        {
            SetPromptDissolveAmount(kingPromptBackgroundMaterial, endAmount);
            SetPromptDissolveAmount(kingPromptTextDissolveMaterial, endAmount);
            kingPromptRoot.SetActive(false);
            if (kingPromptHasPending) ApplyPendingKingPrompt();
            return;
        }

        kingPromptTween = DOVirtual.Float(0f, 1f, shopPromptDissolveDuration, t =>
        {
            SetPromptDissolveAmount(kingPromptBackgroundMaterial, Mathf.Lerp(startBg, endAmount, t));
            SetPromptDissolveAmount(kingPromptTextDissolveMaterial, Mathf.Lerp(startText, endAmount, t));
        })
        .SetEase(shopPromptDissolveOutEase)
        .OnComplete(() =>
        {
            if (kingPromptRoot != null)
            {
                SetPromptDissolveAmount(kingPromptBackgroundMaterial, endAmount);
                SetPromptDissolveAmount(kingPromptTextDissolveMaterial, endAmount);
                kingPromptRoot.SetActive(false);
            }
            if (kingPromptHasPending) ApplyPendingKingPrompt();
        });
    }

    private void ApplyPendingKingPrompt()
    {
        kingPromptHasPending = false;
        DoShowKingPromptImmediate(kingPromptPendingWorldPos, kingPromptPendingMessage);
    }

    private void UpdateKingPromptLayout(Vector3 kingWorldPos)
    {
        UpdatePromptLayout(
            kingPromptRoot,
            kingPromptBackgroundRenderer,
            kingPromptText,
            kingPromptBackgroundMaterial,
            kingWorldPos,
            alignLeftEdgeToAnchor: true,
            leftAlignText: true,
            horizontalPaddingCellRatio: kingPromptHorizontalPaddingCellRatio,
            minBackgroundWidthCellRatio: kingPromptMinBackgroundWidthCellRatio,
            leftPaddingCellRatio: kingPromptTextLeftPaddingCellRatio,
            anchorOffsetXCellRatio: kingPromptLeftEdgeOffsetCellRatio,
            extraWorldOffsetY: kingPromptBubbleOffsetY
        );
    }

    private void DoShowKingPromptImmediate(Vector3 kingWorldPos, string message)
    {
        kingPromptCurrentMessage = message;
        kingPromptText.text = message;
        kingPromptText.maxVisibleCharacters = int.MaxValue;
        UpdateKingPromptLayout(kingWorldPos);
        ApplyPromptSortingFromUnits(kingPromptBackgroundRenderer, kingPromptText);
        ShowPromptAnimated(
            kingPromptRoot,
            kingPromptText,
            kingPromptBackgroundMaterial,
            kingPromptTextDissolveMaterial,
            ref kingPromptTween,
            ref kingPromptTargetVisible
        );
        StartKingPromptTypewriter();
    }

    public void HideKingPrompt()
    {
        kingPromptCurrentMessage = null;
        kingPromptHasPending = false;
        StopKingPromptTypewriter(revealAllCharacters: false);
        StopKingScalePulses();
        StopKingBubblePulses();
        kingPromptAutoHideTween?.Kill();
        kingPromptAutoHideTween = null;
        HidePromptAnimated(
            kingPromptRoot,
            kingPromptText,
            kingPromptBackgroundMaterial,
            kingPromptTextDissolveMaterial,
            ref kingPromptTween,
            ref kingPromptTargetVisible
        );
    }

    private void EnsureShopPromptObjects()
    {
        if (shopPromptRoot != null) return;

        Transform parent = gridParent != null ? gridParent : transform;

        shopPromptRoot = new GameObject("ShopPrompt");
        shopPromptRoot.transform.SetParent(parent, false);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(shopPromptRoot.transform, false);

        shopPromptBackgroundRenderer = bgObj.AddComponent<SpriteRenderer>();
        shopPromptBackgroundRenderer.sprite = GetShopPromptWhiteSprite();
        shopPromptBackgroundRenderer.color = shopPromptBackgroundColor;
        shopPromptBackgroundMaterial = CreatePromptDissolveMaterial(shopPromptBackgroundRenderer);
        bgObj.transform.localScale = new Vector3(
            Mathf.Max(0.1f, cellSize * shopPromptBackgroundSizeCellRatio.x),
            Mathf.Max(0.1f, cellSize * shopPromptBackgroundSizeCellRatio.y),
            1f
        );

        var tilemapRenderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
        if (tilemapRenderer != null)
        {
            shopPromptBackgroundRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
        }
        shopPromptBackgroundRenderer.sortingOrder = shopPromptSortingOrderOffset;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(shopPromptRoot.transform, false);
        textObj.transform.localScale = Vector3.one * Mathf.Max(0.01f, cellSize * shopPromptScaleCellRatio);

        shopPromptText = textObj.AddComponent<TextMeshPro>();
        if (shopPromptFont != null)
        {
            shopPromptText.font = shopPromptFont;
        }
        shopPromptText.alignment = TextAlignmentOptions.Center;
        shopPromptText.fontSize = shopPromptFontSize;
        shopPromptText.color = shopPromptTextColor;
        shopPromptText.enableAutoSizing = false;
        shopPromptText.raycastTarget = false;
        shopPromptText.text = string.Empty;

		if (shopPromptBackgroundRenderer != null)
		{
			shopPromptText.sortingLayerID = shopPromptBackgroundRenderer.sortingLayerID;
			shopPromptText.sortingOrder = shopPromptBackgroundRenderer.sortingOrder + 1;
		}

		shopPromptTextDissolveMaterial = CreateTextDissolveMaterial(shopPromptText);
        ApplyPromptSortingFromUnits(shopPromptBackgroundRenderer, shopPromptText);

        shopPromptTargetVisible = false;
        shopPromptRoot.SetActive(false);
    }

    private void EnsureKingPromptObjects()
    {
        if (kingPromptRoot != null) return;

        Transform parent = gridParent != null ? gridParent : transform;

        kingPromptRoot = new GameObject("KingPrompt");
        kingPromptRoot.transform.SetParent(parent, false);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(kingPromptRoot.transform, false);

        kingPromptBackgroundRenderer = bgObj.AddComponent<SpriteRenderer>();
        kingPromptBackgroundRenderer.sprite = GetShopPromptWhiteSprite();
        kingPromptBackgroundRenderer.color = shopPromptBackgroundColor;
        kingPromptBackgroundMaterial = CreatePromptDissolveMaterial(kingPromptBackgroundRenderer);
        bgObj.transform.localScale = new Vector3(
            Mathf.Max(0.1f, cellSize * shopPromptBackgroundSizeCellRatio.x),
            Mathf.Max(0.1f, cellSize * shopPromptBackgroundSizeCellRatio.y),
            1f
        );

        var tilemapRenderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
        if (tilemapRenderer != null)
        {
            kingPromptBackgroundRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
        }
        kingPromptBackgroundRenderer.sortingOrder = shopPromptSortingOrderOffset;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(kingPromptRoot.transform, false);
        textObj.transform.localScale = Vector3.one * Mathf.Max(0.01f, cellSize * shopPromptScaleCellRatio);

        kingPromptText = textObj.AddComponent<TextMeshPro>();
        if (shopPromptFont != null)
        {
            kingPromptText.font = shopPromptFont;
        }
        kingPromptText.alignment = TextAlignmentOptions.Center;
        kingPromptText.fontSize = shopPromptFontSize;
        kingPromptText.color = shopPromptTextColor;
        kingPromptText.enableAutoSizing = false;
        kingPromptText.raycastTarget = false;
        kingPromptText.text = string.Empty;
		kingPromptText.color = shopPromptTextColor;

		if (kingPromptBackgroundRenderer != null)
		{
			kingPromptText.sortingLayerID = kingPromptBackgroundRenderer.sortingLayerID;
			kingPromptText.sortingOrder = kingPromptBackgroundRenderer.sortingOrder + 1;
		}

		kingPromptTextDissolveMaterial = CreateTextDissolveMaterial(kingPromptText);
        ApplyPromptSortingFromUnits(kingPromptBackgroundRenderer, kingPromptText);

        kingPromptTargetVisible = false;
        kingPromptRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        activationTween?.Kill();
        StopAllShopItemPriceShakes();
        HideShopPromptImmediate();
        HideKingPromptImmediate();
        StopKingPromptTypewriter(revealAllCharacters: false);
        StopKingScalePulses();
        StopKingBubblePulses();
        StopShopPromptEntranceAnimation();

        if (shopPromptBackgroundMaterial != null)
        {
            Destroy(shopPromptBackgroundMaterial);
            shopPromptBackgroundMaterial = null;
        }

        if (shopPromptTextDissolveMaterial != null)
        {
            Destroy(shopPromptTextDissolveMaterial);
            shopPromptTextDissolveMaterial = null;
        }

        if (kingPromptBackgroundMaterial != null)
        {
            Destroy(kingPromptBackgroundMaterial);
            kingPromptBackgroundMaterial = null;
        }

        if (kingPromptTextDissolveMaterial != null)
        {
            Destroy(kingPromptTextDissolveMaterial);
            kingPromptTextDissolveMaterial = null;
        }

        kingPromptAutoHideTween?.Kill();
        kingPromptAutoHideTween = null;
    }

    private Material CreatePromptDissolveMaterial(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null || dissolveShader == null)
        {
            return null;
        }

        Material material = new Material(dissolveShader);

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", renderer.sprite.texture);
        }

        if (material.HasProperty("_PatternScale"))
        {
            material.SetFloat("_PatternScale", Mathf.Max(0.01f, shopPromptDissolvePatternScale));
        }

        if (material.HasProperty("_DitherAspect"))
        {
            material.SetFloat("_DitherAspect", 1f);
        }

        if (material.HasProperty("_UseScreenSpaceDither"))
        {
            material.SetFloat("_UseScreenSpaceDither", 1f);
        }

        if (material.HasProperty("_SweepDirection"))
        {
            material.SetFloat("_SweepDirection", shopPromptDissolveSweepDirection);
        }

        if (material.HasProperty("_SweepSpread"))
        {
            material.SetFloat("_SweepSpread", Mathf.Max(0f, shopPromptDissolveSweepSpread));
        }

        if (material.HasProperty("_DissolveAmount"))
        {
            material.SetFloat("_DissolveAmount", 1f);
        }

        renderer.material = material;
        return material;
    }

    private Material CreateTextDissolveMaterial(TextMeshPro text)
    {
        if (text == null || text.font == null || dissolveTextShader == null)
        {
            return null;
        }

        // Material Variant に shader をセットするとエラーになるため、
        // dissolveTextShader を直接使って新規マテリアルを作成し、
        // TMP に必要なプロパティだけ元マテリアルからコピーする。
        Material src = text.fontSharedMaterial;
        Material mat = new Material(dissolveTextShader);

        // TMP SDF 描画に必須のプロパティをコピー
        CopyMaterialPropertyIfExists(src, mat, "_MainTex");
        CopyMaterialFloatIfExists(src, mat, "_TextureWidth");
        CopyMaterialFloatIfExists(src, mat, "_TextureHeight");
        CopyMaterialFloatIfExists(src, mat, "_GradientScale");
        CopyMaterialFloatIfExists(src, mat, "_ScaleX");
        CopyMaterialFloatIfExists(src, mat, "_ScaleY");
        CopyMaterialFloatIfExists(src, mat, "_Sharpness");
        CopyMaterialFloatIfExists(src, mat, "_FaceDilate");
        CopyMaterialFloatIfExists(src, mat, "_WeightNormal");
        CopyMaterialFloatIfExists(src, mat, "_WeightBold");
        if (src.HasProperty("_FaceColor"))
            mat.SetColor("_FaceColor", src.GetColor("_FaceColor"));

        // Dissolve パラメータを設定
        if (mat.HasProperty("_PatternScale"))
            mat.SetFloat("_PatternScale", Mathf.Max(0.01f, shopPromptDissolvePatternScale));
        if (mat.HasProperty("_UseScreenSpaceDither"))
            mat.SetFloat("_UseScreenSpaceDither", 1f);
        if (mat.HasProperty("_SweepDirection"))
            mat.SetFloat("_SweepDirection", shopPromptDissolveSweepDirection);
        if (mat.HasProperty("_SweepSpread"))
            mat.SetFloat("_SweepSpread", Mathf.Max(0f, shopPromptDissolveSweepSpread));
        if (mat.HasProperty("_DissolveAmount"))
            mat.SetFloat("_DissolveAmount", 1f); // 最初は完全に非表示

        text.fontMaterial = mat;
        return text.fontMaterial;
    }

    private static void CopyMaterialPropertyIfExists(Material src, Material dst, string name)
    {
        if (src != null && src.HasProperty(name) && dst.HasProperty(name))
            dst.SetTexture(name, src.GetTexture(name));
    }

    private static void CopyMaterialFloatIfExists(Material src, Material dst, string name)
    {
        if (src != null && src.HasProperty(name) && dst.HasProperty(name))
            dst.SetFloat(name, src.GetFloat(name));
    }

    private static bool HasPromptDissolveMaterial(Material material)
    {
        return material != null && material.HasProperty("_DissolveAmount");
    }

    private static float GetPromptDissolveAmount(Material material)
    {
        if (!HasPromptDissolveMaterial(material)) return 0f;
        return material.GetFloat("_DissolveAmount");
    }

    private static void SetPromptDissolveAmount(Material material, float amount)
    {
        if (!HasPromptDissolveMaterial(material)) return;
        material.SetFloat("_DissolveAmount", Mathf.Clamp01(amount));
    }

    private static void SetPromptTextAlpha(TextMeshPro text, float alpha)
    {
        if (text == null) return;

        Color c = text.color;
        c.a = Mathf.Clamp01(alpha);
        text.color = c;
    }

    private static int NormalizeSweepDirection(float direction)
    {
        int normalized = Mathf.RoundToInt(direction) % 4;
        if (normalized < 0)
        {
            normalized += 4;
        }
        return normalized;
    }

    private static int GetOppositeSweepDirection(int direction)
    {
        return (direction + 2) % 4;
    }

    private void SetPromptSweepDirection(Material dissolveMaterial, bool fadeIn)
    {
        if (dissolveMaterial == null || !dissolveMaterial.HasProperty("_SweepDirection"))
        {
            return;
        }

        int baseDirection = NormalizeSweepDirection(shopPromptDissolveSweepDirection);
        int direction = fadeIn ? baseDirection : GetOppositeSweepDirection(baseDirection);
        dissolveMaterial.SetFloat("_SweepDirection", direction);
    }

    private void UpdatePromptLayout(
        GameObject root,
        SpriteRenderer backgroundRenderer,
        TextMeshPro text,
        Material dissolveMaterial,
        Vector3 anchorWorldPos,
        bool alignLeftEdgeToAnchor,
        bool leftAlignText,
        float horizontalPaddingCellRatio,
        float minBackgroundWidthCellRatio,
        float leftPaddingCellRatio,
        float anchorOffsetXCellRatio,
        float extraWorldOffsetX = 0f,
        float extraWorldOffsetY = 0f)
    {
        if (root == null || text == null) return;

        float backgroundScaleX = CalculatePromptBackgroundScaleX(backgroundRenderer, text, horizontalPaddingCellRatio, minBackgroundWidthCellRatio);
        float backgroundScaleY = Mathf.Max(0.1f, cellSize * shopPromptBackgroundSizeCellRatio.y);
        float halfBackgroundWidthLocal = 0f;

        if (backgroundRenderer != null)
        {
            backgroundRenderer.transform.localScale = new Vector3(backgroundScaleX, backgroundScaleY, 1f);
            halfBackgroundWidthLocal = GetPromptBackgroundHalfWidthLocal(backgroundRenderer);
        }

        UpdatePromptDitherAspect(backgroundRenderer, dissolveMaterial);

        Vector3 basePos = anchorWorldPos + new Vector3(
            cellSize * anchorOffsetXCellRatio,
            cellSize * shopPromptYOffsetCellRatio,
            0f
        );
        basePos.x += extraWorldOffsetX;
        basePos.y += extraWorldOffsetY;

        root.transform.position = basePos;

        if (alignLeftEdgeToAnchor)
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.transform.localPosition = new Vector3(halfBackgroundWidthLocal, 0f, 0f);
            }
        }
        else
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.transform.localPosition = Vector3.zero;
            }
        }

        if (leftAlignText)
        {
            float padding = WorldLengthToLocalX(root.transform, Mathf.Max(0f, cellSize * leftPaddingCellRatio));
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.transform.localPosition = alignLeftEdgeToAnchor
                ? new Vector3(padding, 0f, 0f)
                : new Vector3(-halfBackgroundWidthLocal + padding, 0f, 0f);
        }
        else
        {
            text.alignment = TextAlignmentOptions.Center;
            text.transform.localPosition = Vector3.zero;
        }
    }

    private float CalculatePromptBackgroundScaleX(SpriteRenderer backgroundRenderer, TextMeshPro text, float horizontalPaddingCellRatio, float minBackgroundWidthCellRatio)
    {
        float minScaleX = Mathf.Max(0.1f, cellSize * Mathf.Max(0f, minBackgroundWidthCellRatio));
        if (backgroundRenderer == null || backgroundRenderer.sprite == null || text == null)
        {
            return minScaleX;
        }

        float textPreferredWidthWorld = GetPromptTextPreferredWidthWorld(text);
        float horizontalPaddingWorld = Mathf.Max(0f, cellSize * horizontalPaddingCellRatio);
        float targetWidthWorld = textPreferredWidthWorld + horizontalPaddingWorld * 2f;
        float spriteWidthAtScaleOne = Mathf.Max(0.001f, backgroundRenderer.sprite.bounds.size.x);
        float targetScaleX = targetWidthWorld / spriteWidthAtScaleOne;

        return Mathf.Max(minScaleX, targetScaleX);
    }

    private static float GetPromptTextPreferredWidthWorld(TextMeshPro text)
    {
        if (text == null || string.IsNullOrEmpty(text.text)) return 0f;

        Vector2 preferred = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity);
        float worldScaleX = Mathf.Abs(text.transform.lossyScale.x);
        if (worldScaleX <= 0.0001f)
        {
            worldScaleX = Mathf.Abs(text.transform.localScale.x);
        }

        if (worldScaleX <= 0.0001f)
        {
            worldScaleX = 1f;
        }

        return preferred.x * worldScaleX;
    }

    private static float GetPromptBackgroundHalfWidthLocal(SpriteRenderer backgroundRenderer)
    {
        if (backgroundRenderer == null || backgroundRenderer.sprite == null) return 0f;

        float spriteWidth = Mathf.Max(0.001f, backgroundRenderer.sprite.bounds.size.x);
        float localScaleX = Mathf.Abs(backgroundRenderer.transform.localScale.x);
        return spriteWidth * localScaleX * 0.5f;
    }

    private static void UpdatePromptDitherAspect(SpriteRenderer backgroundRenderer, Material dissolveMaterial)
    {
        if (backgroundRenderer == null || dissolveMaterial == null) return;
        if (!dissolveMaterial.HasProperty("_DitherAspect")) return;

        float ditherAspect = CalculateDitherAspectCompensation(backgroundRenderer);
        dissolveMaterial.SetFloat("_DitherAspect", ditherAspect);
    }

    private static float CalculateDitherAspectCompensation(SpriteRenderer backgroundRenderer)
    {
        if (backgroundRenderer == null || backgroundRenderer.sprite == null) return 1f;

        Sprite sprite = backgroundRenderer.sprite;

        float worldWidth = sprite.bounds.size.x * Mathf.Abs(backgroundRenderer.transform.lossyScale.x);
        float worldHeight = sprite.bounds.size.y * Mathf.Abs(backgroundRenderer.transform.lossyScale.y);
        if (worldWidth <= 0.0001f || worldHeight <= 0.0001f) return 1f;

        float spritePixelWidth = Mathf.Max(1f, sprite.rect.width);
        float spritePixelHeight = Mathf.Max(1f, sprite.rect.height);

        float worldPerPixelX = worldWidth / spritePixelWidth;
        float worldPerPixelY = worldHeight / spritePixelHeight;
        if (worldPerPixelY <= 0.0001f) return 1f;

        return Mathf.Max(0.0001f, worldPerPixelX / worldPerPixelY);
    }

    private static float WorldLengthToLocalX(Transform reference, float worldLength)
    {
        if (reference == null) return worldLength;

        float worldScaleX = Mathf.Abs(reference.lossyScale.x);
        if (worldScaleX <= 0.0001f) return worldLength;

        return worldLength / worldScaleX;
    }

    private static void KillPromptTween(ref Tween tween)
    {
        if (tween != null && tween.IsActive())
        {
            tween.Kill();
        }
        tween = null;
    }

    private void ApplyPromptSortingFromUnits(SpriteRenderer backgroundRenderer, TextMeshPro text)
    {
        if (backgroundRenderer == null) return;

        int targetLayerId = backgroundRenderer.sortingLayerID;
        int targetOrder = shopPromptSortingOrderOffset;

        if (TryGetTopUnitSorting(out int topUnitLayerId, out int topUnitOrder))
        {
            int targetLayerValue = SortingLayer.GetLayerValueFromID(targetLayerId);
            int topUnitLayerValue = SortingLayer.GetLayerValueFromID(topUnitLayerId);

            if (topUnitLayerValue > targetLayerValue)
            {
                targetLayerId = topUnitLayerId;
                targetOrder = Mathf.Max(targetOrder, topUnitOrder + shopPromptSortingOrderMarginAboveUnit);
            }
            else if (topUnitLayerValue == targetLayerValue)
            {
                targetOrder = Mathf.Max(targetOrder, topUnitOrder + shopPromptSortingOrderMarginAboveUnit);
            }
        }

        backgroundRenderer.sortingLayerID = targetLayerId;
        backgroundRenderer.sortingOrder = targetOrder;

        if (text != null)
        {
            text.sortingLayerID = targetLayerId;
            text.sortingOrder = targetOrder + 1;
        }
    }

    private static bool TryGetTopUnitSorting(out int layerId, out int order)
    {
        layerId = 0;
        order = 0;

        Unit[] units = FindObjectsOfType<Unit>();
        if (units == null || units.Length == 0)
        {
            return false;
        }

        bool found = false;
        int bestLayerValue = int.MinValue;

        foreach (var unit in units)
        {
            if (unit == null) continue;

            SpriteRenderer[] renderers = unit.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;

                int layerValue = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                int rendererOrder = renderer.sortingOrder;

                if (!found || layerValue > bestLayerValue || (layerValue == bestLayerValue && rendererOrder > order))
                {
                    found = true;
                    bestLayerValue = layerValue;
                    layerId = renderer.sortingLayerID;
                    order = rendererOrder;
                }
            }
        }

        return found;
    }

    private bool TryGetShopItemVisualEntry(StageSO stage, Vector2Int pos, out ShopItemVisualEntry entry)
    {
        for (int i = 0; i < shopItemVisualEntries.Count; i++)
        {
            var candidate = shopItemVisualEntries[i];
            if (candidate.stage == stage && candidate.position == pos)
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    private void PlayShopItemPriceShakeTween(GameObject priceObject, Vector3 baseLocalPosition)
    {
        if (priceObject == null) return;
        StopShopItemPriceShake(priceObject, baseLocalPosition);

        float shakeDistance = Mathf.Max(0.01f, cellSize * shopItemPriceHoverShakeDistanceCellRatio);
        float roundTripDuration = Mathf.Max(0.05f, shopItemPriceHoverShakeDuration);
        int roundTrips = Mathf.Max(1, shopItemPriceShakeRoundTrips);
        float decayPower = Mathf.Max(0.1f, shopItemPriceShakeDecayPower);
        float totalDuration = roundTripDuration * roundTrips;
        Transform target = priceObject.transform;

        target.localPosition = baseLocalPosition;

        DOVirtual
            .Float(0f, 1f, totalDuration, t =>
            {
                if (priceObject == null) return;

                float phaseT = t;
                float decayT = Mathf.Clamp01(DOVirtual.EasedValue(0f, 1f, t, shopItemPriceHoverShakeEase));
                float envelope = Mathf.Pow(1f - decayT, decayPower);
                float wave = Mathf.Sin(phaseT * roundTrips * Mathf.PI * 2f);
                Vector3 pos = baseLocalPosition;
                pos.x += shakeDistance * envelope * wave;
                target.localPosition = pos;
            })
            .SetEase(Ease.Linear)
            .SetTarget(priceObject)
            .OnComplete(() =>
            {
                if (priceObject != null)
                {
                    priceObject.transform.localPosition = baseLocalPosition;
                }
            });
    }

    private void StopShopItemPriceShake(GameObject priceObject, Vector3 baseLocalPosition)
    {
        if (priceObject == null) return;

        DOTween.Kill(priceObject);

        if (priceObject != null)
        {
            priceObject.transform.localPosition = baseLocalPosition;
        }
    }

    private void StopAllShopItemPriceShakes()
    {
        for (int i = 0; i < shopItemVisualEntries.Count; i++)
        {
            var entry = shopItemVisualEntries[i];
            StopShopItemPriceShake(entry.priceObject, entry.priceBaseLocalPosition);
        }
    }

    private void ShowPromptAnimated(
        GameObject root,
        TextMeshPro text,
        Material dissolveMaterial,
        Material textDissolveMaterial,
        ref Tween tween,
        ref bool targetVisible)
    {
        if (root == null || text == null) return;

        if (targetVisible)
        {
            if (!root.activeSelf)
            {
                root.SetActive(true);
                SetPromptDissolveAmount(dissolveMaterial, 0f);
                SetPromptDissolveAmount(textDissolveMaterial, 0f);
            }
            return;
        }

        targetVisible = true;
        KillPromptTween(ref tween);

        if (!root.activeSelf)
        {
            root.SetActive(true);
        }

        SetPromptSweepDirection(dissolveMaterial, fadeIn: true);
        SetPromptSweepDirection(textDissolveMaterial, fadeIn: true);

        if (!HasPromptDissolveMaterial(dissolveMaterial) || shopPromptDissolveDuration <= 0f)
        {
            SetPromptDissolveAmount(dissolveMaterial, 0f);
            SetPromptDissolveAmount(textDissolveMaterial, 0f);
            return;
        }

        float startBgDissolve = GetPromptDissolveAmount(dissolveMaterial);
        float startTextDissolve = GetPromptDissolveAmount(textDissolveMaterial);
        const float endDissolveAmount = 0f;

        tween = DOVirtual.Float(0f, 1f, shopPromptDissolveDuration, t =>
        {
            SetPromptDissolveAmount(dissolveMaterial, Mathf.Lerp(startBgDissolve, endDissolveAmount, t));
            SetPromptDissolveAmount(textDissolveMaterial, Mathf.Lerp(startTextDissolve, endDissolveAmount, t));
        })
        .SetEase(shopPromptDissolveInEase)
        .OnComplete(() =>
        {
            SetPromptDissolveAmount(dissolveMaterial, endDissolveAmount);
            SetPromptDissolveAmount(textDissolveMaterial, endDissolveAmount);
        });
    }

    private void HidePromptAnimated(
        GameObject root,
        TextMeshPro text,
        Material dissolveMaterial,
        Material textDissolveMaterial,
        ref Tween tween,
        ref bool targetVisible)
    {
        if (root == null) return;

        if (!targetVisible)
        {
            if (!root.activeSelf) return;
            if (tween != null && tween.IsActive()) return;
        }

        targetVisible = false;

        if (!root.activeSelf) return;

        KillPromptTween(ref tween);

        SetPromptSweepDirection(dissolveMaterial, fadeIn: false);
        SetPromptSweepDirection(textDissolveMaterial, fadeIn: false);

        if (!HasPromptDissolveMaterial(dissolveMaterial) || shopPromptDissolveDuration <= 0f)
        {
            SetPromptDissolveAmount(dissolveMaterial, 1f);
            SetPromptDissolveAmount(textDissolveMaterial, 1f);
            root.SetActive(false);
            return;
        }

        float startBgDissolve = GetPromptDissolveAmount(dissolveMaterial);
        float startTextDissolve = GetPromptDissolveAmount(textDissolveMaterial);
        const float endDissolveAmount = 1f;

        tween = DOVirtual.Float(0f, 1f, shopPromptDissolveDuration, t =>
        {
            SetPromptDissolveAmount(dissolveMaterial, Mathf.Lerp(startBgDissolve, endDissolveAmount, t));
            SetPromptDissolveAmount(textDissolveMaterial, Mathf.Lerp(startTextDissolve, endDissolveAmount, t));
        })
        .SetEase(shopPromptDissolveOutEase)
        .OnComplete(() =>
        {
            if (root != null)
            {
                SetPromptDissolveAmount(dissolveMaterial, endDissolveAmount);
                SetPromptDissolveAmount(textDissolveMaterial, endDissolveAmount);
                root.SetActive(false);
            }
        });
    }

    private void HideShopPromptImmediate()
    {
        StopShopPromptShake();
        StopShopPromptEntranceAnimation();
        hasShopPromptAnchorWorldPos = false;
        shopPromptTargetVisible = false;
        KillPromptTween(ref shopPromptTween);

        if (shopPromptBackgroundMaterial != null)
            SetPromptDissolveAmount(shopPromptBackgroundMaterial, 1f);

        if (shopPromptTextDissolveMaterial != null)
            SetPromptDissolveAmount(shopPromptTextDissolveMaterial, 1f);

        if (shopPromptRoot != null && shopPromptRoot.activeSelf)
            shopPromptRoot.SetActive(false);
    }

    private void PlayShopPromptEntranceAnimation()
    {
        if (shopPromptRoot == null)
        {
            return;
        }

        StopShopPromptEntranceAnimation();

        Transform rootTransform = shopPromptRoot.transform;
        Vector3 baseScale = Vector3.one;

        float upDistance = Mathf.Max(0f, cellSize * shopPromptEntranceUpDistanceCellRatio);
        float downDistance = Mathf.Max(0f, cellSize * shopPromptEntranceDownDistanceCellRatio);
        float upDuration = Mathf.Max(0.01f, shopPromptEntranceUpDuration);
        float downDuration = Mathf.Max(0.01f, shopPromptEntranceDownDuration);
        float returnDuration = Mathf.Max(0.01f, shopPromptEntranceReturnDuration);
        float narrowScaleX = Mathf.Clamp(shopPromptEntranceNarrowScaleX, 0.5f, 1f);

        rootTransform.localScale = baseScale;
        shopPromptEntranceOffsetY = 0f;

        Sequence sequence = DOTween.Sequence().SetTarget(shopPromptRoot);
        sequence
            .Append(DOVirtual.Float(0f, upDistance, upDuration, value =>
            {
                shopPromptEntranceOffsetY = value;
            }).SetEase(shopPromptEntranceUpEase))
            .Append(DOVirtual.Float(upDistance, -downDistance, downDuration, value =>
            {
                shopPromptEntranceOffsetY = value;
            }).SetEase(shopPromptEntranceDownEase))
            .Join(rootTransform.DOScale(new Vector3(narrowScaleX, baseScale.y, baseScale.z), downDuration).SetEase(shopPromptEntranceDownEase))
            .Append(DOVirtual.Float(-downDistance, 0f, returnDuration, value =>
            {
                shopPromptEntranceOffsetY = value;
            }).SetEase(shopPromptEntranceReturnEase))
            .Join(rootTransform.DOScale(baseScale, returnDuration).SetEase(shopPromptEntranceReturnEase))
            .OnComplete(() =>
            {
                if (shopPromptRoot != null)
                {
                    shopPromptRoot.transform.localScale = baseScale;
                }

                shopPromptEntranceOffsetY = 0f;
                shopPromptEntranceTween = null;
            })
            .OnKill(() =>
            {
                if (shopPromptRoot != null)
                {
                    shopPromptRoot.transform.localScale = baseScale;
                }

                shopPromptEntranceOffsetY = 0f;

                if (shopPromptEntranceTween == sequence)
                {
                    shopPromptEntranceTween = null;
                }
            });

        shopPromptEntranceTween = sequence;
    }

    private void StopShopPromptEntranceAnimation()
    {
        if (shopPromptEntranceTween != null)
        {
            shopPromptEntranceTween.Kill();
            shopPromptEntranceTween = null;
        }

        shopPromptEntranceOffsetY = 0f;

        if (shopPromptRoot != null)
        {
            shopPromptRoot.transform.localScale = Vector3.one;
        }
    }

    private void StopShopPromptShake()
    {
        if (shopPromptShakeTween != null)
        {
            shopPromptShakeTween.Kill();
            shopPromptShakeTween = null;
        }

        shopPromptShakeOffsetX = 0f;
    }

    private void HideKingPromptImmediate()
    {
        kingPromptCurrentMessage = null;
        kingPromptHasPending = false;
        StopKingPromptTypewriter(revealAllCharacters: false);
        StopKingScalePulses();
        StopKingBubblePulses();
        kingPromptAutoHideTween?.Kill();
        kingPromptAutoHideTween = null;
        kingPromptTargetVisible = false;
        KillPromptTween(ref kingPromptTween);

        if (kingPromptBackgroundMaterial != null)
            SetPromptDissolveAmount(kingPromptBackgroundMaterial, 1f);

        if (kingPromptTextDissolveMaterial != null)
            SetPromptDissolveAmount(kingPromptTextDissolveMaterial, 1f);

        if (kingPromptRoot != null && kingPromptRoot.activeSelf)
            kingPromptRoot.SetActive(false);
    }

    private void SetKingPromptPulseTarget(Transform kingTransform)
    {
        if (kingPromptPulseTarget == kingTransform)
        {
            if (kingPromptPulseTarget != null && kingPromptScalePulseTween == null && kingPromptQueuedScalePulseCount == 0)
            {
                kingPromptPulseBaseScale = kingPromptPulseTarget.localScale;
            }
            return;
        }

        StopKingScalePulses();
        StopKingBubblePulses();
        kingPromptPulseTarget = kingTransform;
        kingPromptPulseBaseScale = kingPromptPulseTarget != null ? kingPromptPulseTarget.localScale : Vector3.one;
    }

    private void StartKingPromptTypewriter()
    {
        StopKingPromptTypewriter(revealAllCharacters: false);
        StopKingBubblePulses();
        kingPromptQueuedScalePulseCount = 0;
        kingPromptScalePulsePlayedCount = 0;

        if (kingPromptText == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(kingPromptText.text))
        {
            kingPromptText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        kingPromptText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        int totalCharacterCount = kingPromptText.textInfo.characterCount;
        if (totalCharacterCount <= 0)
        {
            kingPromptText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        float charInterval = Mathf.Max(0f, kingPromptTypewriterCharInterval);
        if (charInterval <= 0f)
        {
            kingPromptText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        if (totalCharacterCount == 1)
        {
            kingPromptText.maxVisibleCharacters = 1;
            QueueKingScalePulses(1);
            QueueKingBubblePulses(1);
            PlayKingTypewriterSe(1);
            return;
        }

        int lastVisibleCharacterCount = 0;
        kingPromptText.maxVisibleCharacters = 0;

        kingPromptTypewriterTween = DOVirtual
            .Float(0f, totalCharacterCount - 1, (totalCharacterCount - 1) * charInterval, t =>
            {
                if (kingPromptText == null)
                {
                    return;
                }

                int visibleCharacterCount = Mathf.Clamp(Mathf.FloorToInt(t) + 1, 1, totalCharacterCount);
                if (visibleCharacterCount <= lastVisibleCharacterCount)
                {
                    return;
                }

                int newlyVisibleCharacterCount = visibleCharacterCount - lastVisibleCharacterCount;
                QueueKingScalePulses(newlyVisibleCharacterCount);
                QueueKingBubblePulses(newlyVisibleCharacterCount);
                PlayKingTypewriterSe(newlyVisibleCharacterCount);
                kingPromptText.maxVisibleCharacters = visibleCharacterCount;
                lastVisibleCharacterCount = visibleCharacterCount;
            })
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                if (kingPromptText != null)
                {
                    kingPromptText.maxVisibleCharacters = int.MaxValue;
                }

                kingPromptTypewriterTween = null;
            });
    }

    private void StopKingPromptTypewriter(bool revealAllCharacters = true)
    {
        if (kingPromptTypewriterTween != null)
        {
            kingPromptTypewriterTween.Kill();
            kingPromptTypewriterTween = null;
        }

        if (revealAllCharacters && kingPromptText != null)
        {
            kingPromptText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private void PlayKingTypewriterSe(int playCount)
    {
        if (playCount <= 0) return;
        if (AudioManager.Instance == null) return;

        for (int i = 0; i < playCount; i++)
        {
            AudioManager.Instance.PlaySE(AudioManager.SEType.king);
        }
    }

    private void QueueKingBubblePulses(int pulseCount)
    {
        if (pulseCount <= 0 || kingPromptRoot == null)
        {
            return;
        }

        kingPromptQueuedBubblePulseCount += pulseCount;
        if (kingPromptBubblePulseTween == null)
        {
            PlayNextKingBubblePulse();
        }
    }

    private void PlayNextKingBubblePulse()
    {
        if (kingPromptQueuedBubblePulseCount <= 0 || kingPromptRoot == null)
        {
            kingPromptBubblePulseTween = null;
            kingPromptBubbleOffsetY = 0f;
            return;
        }

        kingPromptQueuedBubblePulseCount--;

        float pulseDistance = Mathf.Max(0f, cellSize * kingPromptBubblePulseDistanceCellRatio);
        float pulseUpDuration = Mathf.Max(0.01f, kingPromptBubblePulseUpDuration);
        float pulseDownDuration = Mathf.Max(0.01f, kingPromptBubblePulseDownDuration);

        Sequence sequence = DOTween.Sequence().SetTarget(kingPromptRoot);
        sequence
            .Append(DOVirtual.Float(0f, 1f, pulseUpDuration, t =>
            {
                kingPromptBubbleOffsetY = pulseDistance * t;
            }).SetEase(kingPromptBubblePulseUpEase))
            .Append(DOVirtual.Float(1f, 0f, pulseDownDuration, t =>
            {
                kingPromptBubbleOffsetY = pulseDistance * t;
            }).SetEase(kingPromptBubblePulseDownEase))
            .OnComplete(() =>
            {
                if (kingPromptBubblePulseTween != sequence)
                {
                    return;
                }

                kingPromptBubblePulseTween = null;
                PlayNextKingBubblePulse();
            })
            .OnKill(() =>
            {
                kingPromptBubbleOffsetY = 0f;

                if (kingPromptBubblePulseTween == sequence)
                {
                    kingPromptBubblePulseTween = null;
                }
            });

        kingPromptBubblePulseTween = sequence;
    }

    private void QueueKingScalePulses(int pulseCount)
    {
        if (pulseCount <= 0 || kingPromptPulseTarget == null)
        {
            return;
        }

        int maxPulseCount = Mathf.Max(0, kingPromptScalePulseMaxCount);
        if (maxPulseCount <= 0)
        {
            return;
        }

        int remainingPulseCount = maxPulseCount - kingPromptScalePulsePlayedCount - kingPromptQueuedScalePulseCount;
        if (remainingPulseCount <= 0)
        {
            return;
        }

        kingPromptQueuedScalePulseCount += Mathf.Min(pulseCount, remainingPulseCount);
        if (kingPromptScalePulseTween == null)
        {
            PlayNextKingScalePulse();
        }
    }

    private void PlayNextKingScalePulse()
    {
        if (kingPromptPulseTarget == null)
        {
            kingPromptQueuedScalePulseCount = 0;
            kingPromptScalePulseTween = null;
            return;
        }

        if (kingPromptQueuedScalePulseCount <= 0)
        {
            kingPromptScalePulseTween = null;
            kingPromptPulseTarget.localScale = kingPromptPulseBaseScale;
            return;
        }

        kingPromptQueuedScalePulseCount--;
        kingPromptScalePulsePlayedCount++;

        Transform target = kingPromptPulseTarget;
        Vector3 baseScale = kingPromptPulseBaseScale;
        float squashYMultiplier = Mathf.Clamp(kingPromptScaleSquashYMultiplier, 0.05f, 1f);
        Vector3 squashScale = new Vector3(baseScale.x, baseScale.y * squashYMultiplier, baseScale.z);
        float squashDuration = Mathf.Max(0.01f, kingPromptScaleSquashDuration);
        float restoreDuration = Mathf.Max(0.01f, kingPromptScaleRestoreDuration);

        Sequence sequence = DOTween.Sequence().SetTarget(target);
        sequence
            .Append(target.DOScale(squashScale, squashDuration).SetEase(kingPromptScaleSquashEase))
            .Append(target.DOScale(baseScale, restoreDuration).SetEase(kingPromptScaleRestoreEase))
            .OnComplete(() =>
            {
                if (kingPromptScalePulseTween != sequence)
                {
                    return;
                }

                kingPromptScalePulseTween = null;
                PlayNextKingScalePulse();
            })
            .OnKill(() =>
            {
                if (target != null)
                {
                    target.localScale = baseScale;
                }

                if (kingPromptScalePulseTween == sequence)
                {
                    kingPromptScalePulseTween = null;
                }
            });

        kingPromptScalePulseTween = sequence;
    }

    private void StopKingScalePulses()
    {
        kingPromptQueuedScalePulseCount = 0;
        kingPromptScalePulsePlayedCount = 0;

        if (kingPromptScalePulseTween != null)
        {
            kingPromptScalePulseTween.Kill();
            kingPromptScalePulseTween = null;
        }

        if (kingPromptPulseTarget != null)
        {
            kingPromptPulseTarget.localScale = kingPromptPulseBaseScale;
        }
    }

    private void StopKingBubblePulses()
    {
        kingPromptQueuedBubblePulseCount = 0;

        if (kingPromptBubblePulseTween != null)
        {
            kingPromptBubblePulseTween.Kill();
            kingPromptBubblePulseTween = null;
        }

        kingPromptBubbleOffsetY = 0f;
    }

    private static Sprite GetShopPromptWhiteSprite()
    {
        if (shopPromptWhiteSprite != null) return shopPromptWhiteSprite;

        Texture2D tex = Texture2D.whiteTexture;
        shopPromptWhiteSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            1f
        );
        return shopPromptWhiteSprite;
    }

    private void CleanupObjects(List<GameObject> list)
    {
        foreach (var obj in list)
        {
            if (obj != null) Destroy(obj);
        }
        list.Clear();
    }

    // 論理データ(cellData)に基づいてTilemapを描画する
    public void RefreshTilemap(int width, int height, CellType[,] cellData, StageSO stage, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck, bool keepTutorial = false)
    {
        ClearAllTiles(keepTutorial);
        FillTilemapFromData(tilemap, width, height, cellData, stage, destroyedWalls, isClearedCheck, keepTutorial);
    }

    // StageSOから直接描画する（初期化時など、まだcellDataが空の場合用だが、
    // 基本的にはGridManagerがデータを作ってから呼ぶ方が良い。
    // ここでは便宜上、GridManagerから移設したロジックを流用しつつ整理する。

    public void SetTile(Vector2Int pos, TileBase tile)
    {
        var tilePos = GetTilePosFromGridPos(pos.x, pos.y);
        tilemap.SetTile(tilePos, tile);
    }

    public void SetTileAt(Tilemap targetMap, int x, int y, CellType type, StageSO stage, Func<Vector2Int, bool> isClearedCheck)
    {
        var pos = GetTilePosFromGridPos(x, y);

        switch (type)
        {
            case CellType.Wall:
                targetMap.SetTile(pos, wallTile);
                break;
            case CellType.Floor:
                bool isGoblinFloor = false;
                if (stage != null)
                {
                    foreach (var et in stage.EnemyStartData)
                    {
                        if (et.EnemyType == EnemyType.Goblin)
                        {
                            if (Mathf.Abs(et.Position.x - x) <= 2 && Mathf.Abs(et.Position.y - y) <= 2)
                            {
                                isGoblinFloor = true;
                                break;
                            }
                        }
                    }
                }
                TileBase floorTileToUse = (isGoblinFloor && floorGoblinTile != null) ? floorGoblinTile : floorTile;
                targetMap.SetTile(pos, floorTileToUse);
                break;
            case CellType.Warp:
                targetMap.SetTile(pos, floorTile);
                break;
            case CellType.Tutorial:
                targetMap.SetTile(pos, floorTile);
                if (tutorialPrefabs != null && stage != null)
                {
                    InstantiateTutorialPrefab(targetMap, pos, new Vector2Int(x, y), stage);
                }
                break;
            case CellType.SyncMap:
                targetMap.SetTile(pos, floorTile);
                if (tileSyncMapPrefab != null)
                {
                    InstantiateSyncMapPrefab(targetMap, pos, new Vector2Int(x, y), stage.SyncMapOffset, isClearedCheck);
                }
                break;
            case CellType.StairUp:
                // 上階段タイル（設定されていなければ床タイルで代用）
                targetMap.SetTile(pos, stairUpTile != null ? stairUpTile : floorTile);
                break;
            case CellType.StairDown:
                // 下階段タイル（設定されていなければ床タイルで代用）
                targetMap.SetTile(pos, stairDownTile != null ? stairDownTile : floorTile);
                break;
            case CellType.BrokenStairUp:
                // 壊れた上階段タイル（設定されていなければ通常の上階段、それもなければ床タイルで代用）
                targetMap.SetTile(pos, brokenStairUpTile != null ? brokenStairUpTile : (stairUpTile != null ? stairUpTile : floorTile));
                break;
            default:
                targetMap.SetTile(pos, null);
                break;
        }

        if (stage != null && x >= 0 && y >= 0 && x < stage.Width && y < stage.Height)
        {
            TrySpawnShopItemVisual(targetMap, pos, stage, new Vector2Int(x, y));
        }
    }

    private void InstantiateTutorialPrefab(Tilemap targetMap, Vector3Int tilePos, Vector2Int gridPos, StageSO stage)
    {
        int index = stage.GetTutorialIndex(gridPos);
        // Index is 1-based (usually 1, 2, 3...)
        // List is 0-based.
        // Assuming tutorialPrefabs[0] matches index 1? Or user sets index 0 for 1st tutorial?
        // Editor default index was 1.
        // Let's adjust: Prefab lookup = index - 1?
        // Or just use index if user manages it.
        // "iはTutorialのIndex". "TutorialPrefab[i]"
        // If index=1, usually `tutorialPrefabs[1]` if array is large enough.
        // BUT safer is `index - 1` if Tutorial 1 means first prefab.
        // Let's assume 1-based index in data, 0-based in list. So use `index - 1`.

        int prefabIndex = index - 1;

        if (prefabIndex >= 0 && prefabIndex < tutorialPrefabs.Count)
        {
            GameObject prefab = tutorialPrefabs[prefabIndex];
            if (prefab != null)
            {
                // Align to tile center
                Vector3 worldPos = targetMap.GetCellCenterWorld(tilePos);
                GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, targetMap.transform);

                if (targetMap == tilemap)
                {
                    spawnedTutorialObjects.Add(obj);
                }
                else if (targetMap == transitionTilemap)
                {
                    spawnedTransitionObjects.Add(obj);
                }
            }
        }
    }

    private void InstantiateSyncMapPrefab(Tilemap targetMap, Vector3Int tilePos, Vector2Int gridPos, Vector2Int offset, Func<Vector2Int, bool> isClearedCheck)
    {
        // タイルの中心位置を取得
        Vector3 worldPos = targetMap.GetCellCenterWorld(tilePos);
        GameObject obj = Instantiate(tileSyncMapPrefab, worldPos, Quaternion.identity, targetMap.transform);

        if (obj != null)
        {
            var controller = obj.GetComponent<TileSyncMapController>();
            if (controller != null)
            {
                // Calculate World Map Coordinates
                Vector2Int worldMapCoords = gridPos + offset;
                bool isCleared = isClearedCheck != null ? isClearedCheck(worldMapCoords) : false;

                obj.name = $"TileSyncMap_({worldMapCoords.x},{worldMapCoords.y})";

                if (isCleared)
                {
                    controller.Show(true);
                }
                else
                {
                    controller.Hide();
                }

                if (targetMap == tilemap)
                {
                    syncMapEntries.Add(new SyncMapEntry { coords = worldMapCoords, controller = controller });
                }
            }
        }

        if (targetMap == tilemap)
        {
            spawnedSyncMapObjects.Add(obj);
        }
        else if (targetMap == transitionTilemap)
        {
            spawnedTransitionObjects.Add(obj);
        }
    }

    private void TrySpawnShopItemVisual(Tilemap targetMap, Vector3Int tilePos, StageSO stage, Vector2Int gridPos)
    {
        if (stage == null || shopItemSprite == null) return;
        if (!stage.HasShopItemSlotAt(gridPos)) return;

        int itemId = stage.GetShopItemIdAt(gridPos);
        if (itemId < 0) return;

        int price = StageSO.GetShopItemPriceById(itemId);

        GameObject root = new GameObject($"ShopItem_{itemId}_{gridPos.x}_{gridPos.y}");
        root.transform.SetParent(targetMap.transform, false);
        root.transform.position = targetMap.GetCellCenterWorld(tilePos);

        GameObject spriteObj = new GameObject("ItemSprite");
        spriteObj.transform.SetParent(root.transform, false);
        spriteObj.transform.localPosition = new Vector3(0f, cellSize * shopItemSpriteYOffsetCellRatio, 0f);

        SpriteRenderer spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = shopItemSprite;

        var tilemapRenderer = targetMap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer != null)
        {
            spriteRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = tilemapRenderer.sortingOrder + shopItemSortingOrderOffset;
        }

        float spriteWidth = shopItemSprite.bounds.size.x;
        if (spriteWidth > 0f)
        {
            float targetWidth = Mathf.Max(0.1f, cellSize * shopItemSpriteCellRatio);
            float spriteScale = targetWidth / spriteWidth;
            spriteObj.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);
        }

        GameObject textObj = new GameObject("PriceText");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0f, -cellSize * shopItemPriceYOffsetCellRatio, 0f);
        textObj.transform.localScale = Vector3.one * Mathf.Max(0.01f, cellSize * shopItemPriceScaleCellRatio);

        TextMeshPro priceText = textObj.AddComponent<TextMeshPro>();
        if (shopItemPriceFont != null)
        {
            priceText.font = shopItemPriceFont;
        }
        priceText.text = price.ToString();
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.fontSize = shopItemPriceFontSize;
        priceText.color = shopItemPriceColor;
        priceText.enableAutoSizing = false;
        priceText.raycastTarget = false;
        priceText.sortingLayerID = spriteRenderer.sortingLayerID;
        priceText.sortingOrder = spriteRenderer.sortingOrder + 1;

        bool isPurchased = isShopItemPurchasedResolver != null && isShopItemPurchasedResolver(stage, gridPos);
        textObj.SetActive(!isPurchased);

        if (targetMap == tilemap)
        {
            spawnedShopItemObjects.Add(root);
            shopItemVisualEntries.Add(new ShopItemVisualEntry
            {
                stage = stage,
                position = gridPos,
                priceObject = textObj,
                priceBaseLocalPosition = textObj.transform.localPosition,
            });
        }
        else if (targetMap == transitionTilemap)
        {
            spawnedTransitionObjects.Add(root);
        }
    }

    private Vector3Int GetTilePosFromGridPos(int x, int y)
    {
        return new Vector3Int(x, y, 0) + new Vector3Int((int)tilemapOffset.x, (int)tilemapOffset.y, 0) / (int)cellSize;
    }

    private Tween activationTween;
    public event Action OnSyncMapActivated;

    public void UpdateSyncMapVisuals(Func<Vector2Int, bool> isClearedCheck, float delay = 0f, bool triggerEvent = false, bool allowActivation = true)
    {
        if (isClearedCheck == null) return;

        // Cancel any pending activation
        activationTween?.Kill();

        bool allCleared = true;
        // First pass: Check if all are cleared and update show/hide state
        foreach (var entry in syncMapEntries)
        {
            if (entry.controller != null)
            {
                if (isClearedCheck(entry.coords))
                {
                    entry.controller.Show();
                }
                else
                {
                    entry.controller.Hide();
                    allCleared = false;
                }
            }
        }

        // Second pass: If all are cleared, play activated animation
        if (allCleared && syncMapEntries.Count > 0)
        {
            if (allowActivation)
            {
                if (delay > 0f)
                {
                    activationTween = DOVirtual.DelayedCall(delay, () =>
                    {
                        foreach (var entry in syncMapEntries)
                        {
                            if (entry.controller != null)
                            {
                                entry.controller.PlayActivated();
                            }
                        }
                        if (triggerEvent) OnSyncMapActivated?.Invoke();
                    });
                }
                else
                {
                    foreach (var entry in syncMapEntries)
                    {
                        if (entry.controller != null)
                        {
                            entry.controller.PlayActivated();
                        }
                    }
                    if (triggerEvent) OnSyncMapActivated?.Invoke();
                }
            }
        }
        else
        {
             foreach (var entry in syncMapEntries)
            {
                if (entry.controller != null)
                {
                    entry.controller.ResetActivated();
                }
            }
        }
    }

    public void SetupTransitionTilemap(StageSO stage, Vector3 transitionOffset, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // Offset is the amount the PARENT will move.
        // Transition Map should be at -Offset relative to parent, so that when parent moves by Offset, Map is at (0,0).
        transitionTilemap.transform.position = -transitionOffset;

        // Transition用のTilemapを埋める
        // cellDataは使わずStageSOから直接読む（遷移先なので）
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls (Fix: Allow boundaries to be broken)
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }

    private void FillTilemapFromData(Tilemap targetMap, int width, int height, CellType[,] cellData, StageSO stage, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck, bool skipTutorialSpawn = false)
    {
        for (int y = -1; y <= height; y++)
        {
            for (int x = -1; x <= width; x++)
            {
                // 初期描画時はWarpの生存判定をどうするか？
                // 呼び出し元で制御してもらうため、ここではFloor扱いにしておくか、あるいはnullにしておく
                // 既存ロジックでは FillTilemap で WarpはFloorにしていた
                CellType type;
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    type = cellData[x, y];
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }

                var pos = GetTilePosFromGridPos(x, y);

                switch (type)
                {
                    case CellType.Wall:
                        targetMap.SetTile(pos, wallTile);
                        break;
                    case CellType.Floor:
                        bool isGoblinFloor = false;
                        if (stage != null)
                        {
                            foreach (var et in stage.EnemyStartData)
                            {
                                if (et.EnemyType == EnemyType.Goblin)
                                {
                                    if (Mathf.Abs(et.Position.x - x) <= 2 && Mathf.Abs(et.Position.y - y) <= 2)
                                    {
                                        isGoblinFloor = true;
                                        break;
                                    }
                                }
                            }
                        }
                        TileBase floorTileToUse = (isGoblinFloor && floorGoblinTile != null) ? floorGoblinTile : floorTile;
                        targetMap.SetTile(pos, floorTileToUse);
                        break;
                    case CellType.Warp:
                        targetMap.SetTile(pos, floorTile);
                        break;
                    case CellType.Tutorial:
                        targetMap.SetTile(pos, floorTile);
                        if (!skipTutorialSpawn && tutorialPrefabs != null && stage != null)
                        {
                            InstantiateTutorialPrefab(targetMap, pos, new Vector2Int(x, y), stage);
                        }
                        break;
                    case CellType.SyncMap:
                        targetMap.SetTile(pos, floorTile);
                        if (tileSyncMapPrefab != null)
                        {
                            InstantiateSyncMapPrefab(targetMap, pos, new Vector2Int(x, y), stage.SyncMapOffset, isClearedCheck);
                        }
                        break;
                    case CellType.StairUp:
                        // 上階段タイル（設定されていなければ床タイルで代用）
                        targetMap.SetTile(pos, stairUpTile != null ? stairUpTile : floorTile);
                        break;
                    case CellType.StairDown:
                        // 下階段タイル（設定されていなければ床タイルで代用）
                        targetMap.SetTile(pos, stairDownTile != null ? stairDownTile : floorTile);
                        break;
                    case CellType.BrokenStairUp:
                        // 壊れた上階段タイル
                        targetMap.SetTile(pos, brokenStairUpTile != null ? brokenStairUpTile : (stairUpTile != null ? stairUpTile : floorTile));
                        break;
                    default:
                        targetMap.SetTile(pos, null);
                        break;
                }

                if (stage != null && x >= 0 && y >= 0 && x < width && y < height)
                {
                    TrySpawnShopItemVisual(targetMap, pos, stage, new Vector2Int(x, y));
                }
            }
        }
    }

    public Tween MoveGridParent(Vector3 endValue, float duration)
    {
        return gridParent.DOMove(endValue, duration).SetEase(Ease.OutQuad);
    }

    public void ResetGridParentPosition()
    {
        gridParent.DOKill();
        gridParent.position = Vector3.zero;
    }

    /// <summary>
    /// 壁破壊時にDissolveアニメーションを再生する
    /// </summary>
    public void PlayWallBreakEffect(Vector2Int gridPos)
    {
        if (dissolveShader == null) return;

        // wallTileからスプライトを取得（最優先）
        Sprite wallSprite = null;
        if (wallTile is UnityEngine.Tilemaps.Tile tile)
        {
            wallSprite = tile.sprite;
        }

        // fallback: Tilemapからタイル位置のスプライトを取得
        if (wallSprite == null)
        {
            var tilePos = GetTilePosFromGridPos(gridPos.x, gridPos.y);
            wallSprite = tilemap.GetSprite(tilePos);
        }

        if (wallSprite == null) return;

        PlayWallBreakEffectAsync(gridPos, wallSprite).Forget();
    }

    private async UniTaskVoid PlayWallBreakEffectAsync(Vector2Int gridPos, Sprite wallSprite)
    {
        // 一時GOを生成
        GameObject go = new GameObject("WallBreakEffect");
        go.transform.SetParent(gridParent, false);
        go.transform.position = GridToWorldPosition(gridPos);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = wallSprite;
        sr.sortingLayerName = tilemap.GetComponent<TilemapRenderer>()?.sortingLayerName ?? "Default";
        sr.sortingOrder = (tilemap.GetComponent<TilemapRenderer>()?.sortingOrder ?? 0) + 1;

        // DitherDissolveマテリアルを動的生成
        Material mat = new Material(dissolveShader);
        mat.SetTexture("_MainTex", wallSprite.texture);
        // 命中直後から「すでに崩れ始めている」感を出すため先出し
        mat.SetFloat("_DissolveAmount", 0.0f);
        mat.SetFloat("_SweepDirection", 2f); // 右上から左下
        sr.material = mat;

        // カメラシェイク（壁破壊の衝撃）
        CameraManager.Instance.ShakeCamera(0.25f, 4.0f, 30f, Vector2.up);

        // Dissolveアニメーション：OutExpoで最初にガッと崩れ素早く消える
        float duration = 0.5f;
        await DOTween.To(
            () => mat.GetFloat("_DissolveAmount"),
            x => mat.SetFloat("_DissolveAmount", x),
            1f,
            duration)
            .SetEase(Ease.OutCubic)
            .AsyncWaitForCompletion();

        // クリーンアップ
        if (mat != null) UnityEngine.Object.Destroy(mat);
        if (go != null) UnityEngine.Object.Destroy(go);
    }

    /// <summary>
    /// 階段用の遷移タイルマップを設定（垂直方向）
    /// </summary>
    /// <param name="stage">移動先ステージ</param>
    /// <param name="goingDown">trueなら下の階へ（下から上へスライド）、falseなら上の階へ（上から下へスライド）</param>
    /// <param name="currentPlayerPos">現在のプレイヤー位置（階段の位置）</param>
    /// <param name="targetPlayerPos">移動先のプレイヤー位置</param>
    public void SetupStairTransitionTilemap(StageSO stage, bool goingDown, Vector2Int currentPlayerPos, Vector2Int targetPlayerPos, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // 座標の差分を計算（現在位置と遷移先位置を合わせるためのオフセット）
        // 現在(5,3)にいて、遷移先で(2,1)に出現するなら、次のステージを (5-2, 3-1) = (3, 2) だけずらす
        Vector2Int posDiff = currentPlayerPos - targetPlayerPos;
        float xPosOffset = posDiff.x * cellSize;
        float yPosOffset = posDiff.y * cellSize;

        // 下の階へ行く場合：新しいステージは画面上から入ってくる（現在ステージは下へ移動）
        // 上の階へ行く場合：新しいステージは画面下から入ってくる（現在ステージは上へ移動）
        float yTransitionOffset = goingDown ? tilemapOffset.y * 2 : -tilemapOffset.y * 2;
        transitionTilemap.transform.position = new Vector3(xPosOffset, yTransitionOffset + yPosOffset, 0);

        // Transition用のTilemapを埋める
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }

    /// <summary>
    /// 階段遷移のアニメーション用移動量を取得
    /// </summary>
    public Vector3 GetStairTransitionOffset(bool goingDown)
    {
        // 下の階へ行く場合：gridParentを下に移動（現在のステージが下へ消える、新しいステージが上から入る）
        // 上の階へ行く場合：gridParentを上に移動（現在のステージが上へ消える、新しいステージが下から入る）
        float yOffset = goingDown ? -tilemapOffset.y * 2 : tilemapOffset.y * 2;
        return new Vector3(0, yOffset, 0);
    }

    /// <summary>
    /// 階段用の遷移タイルマップを設定（ワープ方向を使用して斜め対応）
    /// </summary>
    /// <summary>
    /// 階段用の遷移タイルマップを設定（ワープ方向を使用して斜め対応）
    /// </summary>
    public void SetupStairTransitionTilemapWithDirection(StageSO stage, Vector3 initialOffset, Vector2Int currentPlayerPos, Vector2Int targetPlayerPos, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // 座標の差分を計算（現在位置と遷移先位置を合わせるためのオフセット）
        Vector2Int posDiff = currentPlayerPos - targetPlayerPos;
        float xPosOffset = posDiff.x * cellSize;
        float yPosOffset = posDiff.y * cellSize;

        // プレイヤー位置のオフセットを加算
        transitionTilemap.transform.position = new Vector3(initialOffset.x + xPosOffset, initialOffset.y + yPosOffset, 0);

        // Transition用のTilemapを埋める
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }

    /// <summary>
    /// ワープ遷移用のタイルマップを設定
    /// </summary>
    /// <summary>
    /// ワープ遷移用のタイルマップを設定
    /// </summary>
    public void SetupWarpTransitionTilemap(StageSO stage, Vector3 initialOffset, Vector2Int targetPlayerPos, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // 遷移タイルマップのオフセットを設定
        transitionTilemap.transform.position = initialOffset;

        // Transition用のTilemapを埋める
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }
}
