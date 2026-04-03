using System.Collections.Generic;
using UnityEngine;

public class ShopSystem : IShopSystem
{
    private readonly GridManager gm;
    private readonly Dictionary<StageSO, HashSet<Vector2Int>> purchasedShopItemsByStage = new Dictionary<StageSO, HashSet<Vector2Int>>();

    private float KingDialogDisplaySeconds => gm.Visualizer.KingPromptDisplayDuration;
    private const float KingIdleDialogDelaySeconds = 3f;

    private StageSO currentKingStage;
    private Enemy currentKingEnemy;
    private bool isKingDialogVisible;
    private bool isKingSmileAnimationPlaying;
    private float kingDialogVisibleTimer;
    private float kingDialogSilentTimer;
    private string lastShownKingDialogueText = string.Empty;
    private bool suppressNextKingEnterDialogue;
    private readonly Dictionary<int, int> kingLastHpByInstanceId = new Dictionary<int, int>();

    private static readonly string[] IdleLinesNotSoldOut =
    {
        "右のから買うのをおすすめするで",
        "たのしんでる？",
        "ぎょうさんコインあつめたなあ！",
        "立ち読みは勘弁してな",
    };

    private static readonly string[] IdleLinesNotSoldOutEn =
    {
        "I'd recommend buying the one on the right.",
        "Having fun?",
        "Look at all those coins!",
        "Hey, this ain't a library!",
    };

    private static readonly string[] IdleLinesSoldOut =
    {
        "たいしたもんやで",
        "出口で待ってるで",
    };

    private static readonly string[] IdleLinesSoldOutEn =
    {
        "Gotta hand it to ya.",
        "Catch you at the exit!",
    };

    private static readonly string[] DamageLines =
    {
        "あいた",
        "いたい",
        "なにすんのや",
        "やめてや",
        "堪忍して",
    };

    private static readonly string[] DamageLinesEn =
    {
        "Ow",
        "Ouch",
        "What was that for.",
        "Cut it out.",
        "Give me a break.",
    };

    private static readonly string[] PurchaseLines =
    {
        "まいど！",
        "おおきに！",
    };

    private static readonly string[] PurchaseLinesEn =
    {
        "Pleasure doing business!",
        "Thanks a bunch!",
    };

    private const string EnterLine   = "いらっしゃい！";
    private const string EnterLineEn = "Welcome!";

    private const string SoldOutLine   = "なんと！売り切れてもうたわ";
    private const string SoldOutLineEn = "Wow! You cleaned me right out!";

    // 出口ステージでのKing台詞
    private static readonly string[] ExitIdleLines =
    {
        "ほんまおおきに",
        "また遊んでな",
        "たのしんでくれた？",
        "もっとおもろいもんつくって待ってるで",
    };

    private static readonly string[] ExitIdleLinesEn =
    {
        "Thanks a million!",
        "Catch you next time!",
        "Did ya have fun?",
        "I'll be cooking up something even cooler for next time!",
    };

    private const string ExitEnterLine   = "見送ったるわ";
    private const string ExitEnterLineEn = "Here to see you off!";

    private static bool IsEnglish => GameSettings.Language == 0;

    public ShopSystem(GridManager gridManager)
    {
        gm = gridManager;
    }

    public bool IsShopStage(StageSO stage)
    {
        if (stage == null) return false;

        if (stage == gm.CurrentStageSOInternal && gm.UnitManager != null)
        {
            foreach (var enemy in gm.UnitManager.Enemies)
            {
                if (enemy != null && !enemy.IsDead && enemy.Type == EnemyType.King)
                {
                    return true;
                }
            }
        }

        if (stage.EnemyStartData == null) return false;

        foreach (var enemyData in stage.EnemyStartData)
        {
            if (enemyData.EnemyType == EnemyType.King)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetShopItemAt(StageSO stage, Vector2Int pos, out int itemId, out int price)
    {
        itemId = -1;
        price = -1;

        if (stage == null) return false;
        if (!IsShopStage(stage)) return false;

        itemId = stage.GetShopItemIdAt(pos);
        if (itemId < 0) return false;

        price = StageSO.GetShopItemPriceById(itemId);
        return true;
    }

    public bool IsShopItemPurchased(StageSO stage, Vector2Int pos)
    {
        if (stage == null) return false;
        if (!purchasedShopItemsByStage.TryGetValue(stage, out var purchasedSet)) return false;
        return purchasedSet.Contains(pos);
    }

    public bool CanPurchaseShopItem(StageSO stage, Vector2Int pos)
    {
        if (!TryGetShopItemAt(stage, pos, out _, out int price)) return false;
        if (IsShopItemPurchased(stage, pos)) return false;

        var progress = GameProgressManager.Instance;
        if (progress == null || progress.Coins == null) return false;

        return progress.Coins.Value >= price;
    }

    public bool TryPurchaseShopItem(StageSO stage, Vector2Int pos, out int itemId, out int price)
    {
        itemId = -1;
        price = -1;

        if (!TryGetShopItemAt(stage, pos, out itemId, out price)) return false;
        if (IsShopItemPurchased(stage, pos)) return false;

        var progress = GameProgressManager.Instance;
        if (progress == null || progress.Coins == null) return false;

        int currentCoins = progress.Coins.Value;
        if (currentCoins < price) return false;

        progress.Coins.Value = Mathf.Max(0, currentCoins - price);
        MarkShopItemPurchased(stage, pos);
        return true;
    }

    public void MarkShopItemPurchased(StageSO stage, Vector2Int pos)
    {
        if (stage == null) return;
        if (!purchasedShopItemsByStage.TryGetValue(stage, out var purchasedSet))
        {
            purchasedSet = new HashSet<Vector2Int>();
            purchasedShopItemsByStage[stage] = purchasedSet;
        }

        purchasedSet.Add(pos);
    }

    public void ResetShopPurchases(StageSO stage)
    {
        if (stage == null) return;
        purchasedShopItemsByStage.Remove(stage);
    }

    public void ResetAllShopPurchases()
    {
        purchasedShopItemsByStage.Clear();
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        Enemy king = FindCurrentStageKingEnemy();
        StageSO stage = gm.CurrentStageSOInternal;

        if (king == null || stage == null || !IsKingDialogueStage(stage))
        {
            ClearCurrentKingDialogueState();
            return;
        }

        currentKingEnemy = king;

        if (TryHandleKingDamageDialogue(king))
        {
            return;
        }

        if (currentKingStage != stage)
        {
            currentKingStage = stage;
            if (suppressNextKingEnterDialogue)
            {
                suppressNextKingEnterDialogue = false;
            }
            else
            {
                string enterLine = stage.IsExitStage
                    ? (IsEnglish ? ExitEnterLineEn : ExitEnterLine)
                    : (IsEnglish ? EnterLineEn : EnterLine);
                ShowKingDialogue(enterLine);
            }
            TrackKingHp(king);
        }

        if (isKingDialogVisible)
        {
            kingDialogVisibleTimer += deltaTime;
            gm.Visualizer.ShowKingPromptAt(king.transform.position, GetCurrentKingDialogueText(), king.transform);

            if (kingDialogVisibleTimer >= KingDialogDisplaySeconds)
            {
                HideKingDialogue();
            }

            return;
        }

        kingDialogSilentTimer += deltaTime;
        if (kingDialogSilentTimer < KingIdleDialogDelaySeconds) return;

        string[] idleLines;
        if (stage.IsExitStage)
        {
            idleLines = IsEnglish ? ExitIdleLinesEn : ExitIdleLines;
        }
        else
        {
            bool soldOut = AreAllShopItemsPurchased(stage);
            idleLines = soldOut
                ? (IsEnglish ? IdleLinesSoldOutEn : IdleLinesSoldOut)
                : (IsEnglish ? IdleLinesNotSoldOutEn : IdleLinesNotSoldOut);
        }
        string nextLine = PickRandomLine(idleLines, lastShownKingDialogueText);
        if (!string.IsNullOrEmpty(nextLine))
        {
            ShowKingDialogue(nextLine, playSmileAnimation: true);
        }
    }

    public void OnStageLoaded(StageSO stage)
    {
        bool shouldSuppressEnterDialogue = suppressNextKingEnterDialogue;
        suppressNextKingEnterDialogue = false;

        if (stage == null || !IsKingDialogueStage(stage))
        {
            ClearCurrentKingDialogueState();
            return;
        }

        currentKingStage = stage;
        currentKingEnemy = FindCurrentStageKingEnemy();

        if (shouldSuppressEnterDialogue)
        {
            HideKingDialogue();
        }
        else
        {
            string enterLine = stage.IsExitStage
                ? (IsEnglish ? ExitEnterLineEn : ExitEnterLine)
                : (IsEnglish ? EnterLineEn : EnterLine);
            ShowKingDialogue(enterLine);
        }

        if (currentKingEnemy != null)
        {
            TrackKingHp(currentKingEnemy);
        }
    }

    public void OnUnitStateChanged(Unit unit)
    {
        Enemy enemy = unit as Enemy;
        if (enemy == null || enemy.Type != EnemyType.King) return;

        int instanceId = enemy.GetInstanceID();
        kingLastHpByInstanceId[instanceId] = enemy.HP.Value;

        bool isDeadNow = enemy.IsDead || enemy.HP.Value <= 0;
        if (!isDeadNow) return;

        if (currentKingEnemy == enemy || isKingDialogVisible)
        {
            ClearCurrentKingDialogueState();
        }

        kingLastHpByInstanceId.Remove(instanceId);
    }

    public void OnShopItemPurchased(StageSO stage, Vector2Int pos, int itemId, int price)
    {
        if (stage == null || stage != currentKingStage) return;
        if (currentKingEnemy == null) return;

        if (AreAllShopItemsPurchased(stage))
        {
            ShowKingDialogue(IsEnglish ? SoldOutLineEn : SoldOutLine, playSmileAnimation: true);
        }
        else
        {
            ShowKingDialogue(PickRandomLine(IsEnglish ? PurchaseLinesEn : PurchaseLines, lastShownKingDialogueText), playSmileAnimation: true);
        }
    }

    public void SetSuppressNextKingEnterDialogue(bool suppress)
    {
        suppressNextKingEnterDialogue = suppress;
    }

    public List<StageShopData> BuildShopPurchaseSaveData()
    {
        var result = new List<StageShopData>();
        foreach (var kvp in purchasedShopItemsByStage)
        {
            StageSO stage = kvp.Key;
            HashSet<Vector2Int> positions = kvp.Value;
            if (stage == null || positions == null || positions.Count == 0) continue;

            var data = new StageShopData
            {
                stageX = stage.MapPosition.x,
                stageY = stage.MapPosition.y,
                purchasedPositions = new List<Vector2IntData>()
            };
            foreach (var pos in positions)
            {
                data.purchasedPositions.Add(new Vector2IntData(pos));
            }
            result.Add(data);
        }
        return result;
    }

    public void RestoreShopPurchasesFromSaveData(List<StageShopData> savedData)
    {
        purchasedShopItemsByStage.Clear();
        if (savedData == null) return;

        foreach (var stageData in savedData)
        {
            if (stageData == null || stageData.purchasedPositions == null) continue;

            StageSO stage = FindStageByMapPosInternal(stageData.stageX, stageData.stageY);
            if (stage == null) continue;

            if (!purchasedShopItemsByStage.ContainsKey(stage))
            {
                purchasedShopItemsByStage[stage] = new HashSet<Vector2Int>();
            }
            foreach (var posData in stageData.purchasedPositions)
            {
                purchasedShopItemsByStage[stage].Add(posData.ToVector2Int());
            }
        }
    }

    private StageSO FindStageByMapPosInternal(int x, int y)
    {
        if (gm.StageSOs != null)
        {
            foreach (var s in gm.StageSOs)
            {
                if (s != null && s.MapPosition.x == x && s.MapPosition.y == y) return s;
            }
        }
        if (gm.WorldMapSO != null)
        {
            return gm.WorldMapSO.GetStageByMapPosition(new Vector2Int(x, y));
        }
        return null;
    }

    private Enemy FindCurrentStageKingEnemy()
    {
        if (gm == null || gm.UnitManager == null) return null;

        foreach (var enemy in gm.UnitManager.Enemies)
        {
            if (enemy != null && !enemy.IsDead && enemy.Type == EnemyType.King)
            {
                return enemy;
            }
        }

        return null;
    }

    private bool AreAllShopItemsPurchased(StageSO stage)
    {
        if (stage == null || stage.ShopItemPositions == null || stage.ShopItemPositions.Count == 0)
        {
            return false;
        }

        foreach (var pos in stage.ShopItemPositions)
        {
            if (!IsShopItemPurchased(stage, pos))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 全ショップステージの全アイテムを購入済みかどうかを返す
    /// </summary>
    public bool AreAllShopItemsPurchasedAcrossAllShopStages()
    {
        var allStages = gm.WorldMapSO != null
            ? (System.Collections.Generic.IEnumerable<StageSO>)gm.WorldMapSO.GetAllStages()
            : gm.StageSOs ?? System.Linq.Enumerable.Empty<StageSO>();

        bool foundAtLeastOne = false;
        foreach (var s in allStages)
        {
            if (s == null) continue;
            if (!IsShopStage(s)) continue;
            if (s.ShopItemPositions == null || s.ShopItemPositions.Count == 0) continue;
            foundAtLeastOne = true;
            if (!AreAllShopItemsPurchased(s)) return false;
        }
        return foundAtLeastOne;
    }

    /// <summary>
    /// Kingが对話を表示するステージかどうか（ショップまたは出口ステージ）
    /// </summary>
    private bool IsKingDialogueStage(StageSO stage)
    {
        if (stage == null) return false;
        if (IsShopStage(stage)) return true;
        // 出口ステージにKingが存在する場合
        if (stage.IsExitStage && FindCurrentStageKingEnemy() != null) return true;
        return false;
    }

    private void ShowKingDialogue(string line, bool playSmileAnimation = false)
    {
        if (string.IsNullOrEmpty(line) || currentKingEnemy == null)
        {
            return;
        }

        SetKingSmileAnimation(playSmileAnimation);
        gm.Visualizer.ShowKingPromptAt(currentKingEnemy.transform.position, line, currentKingEnemy.transform);
        SetCurrentKingDialogueText(line);
        lastShownKingDialogueText = line;
        isKingDialogVisible = true;
        kingDialogVisibleTimer = 0f;
        kingDialogSilentTimer = 0f;
    }

    private void HideKingDialogue()
    {
        StopKingSmileAnimation();
        gm.Visualizer.HideKingPrompt();
        SetCurrentKingDialogueText(string.Empty);
        isKingDialogVisible = false;
        kingDialogVisibleTimer = 0f;
        kingDialogSilentTimer = 0f;
    }

    private void ClearCurrentKingDialogueState()
    {
        StopKingSmileAnimation();
        currentKingStage = null;
        currentKingEnemy = null;
        kingDialogVisibleTimer = 0f;
        kingDialogSilentTimer = 0f;
        isKingDialogVisible = false;
        SetCurrentKingDialogueText(string.Empty);
        lastShownKingDialogueText = string.Empty;
        gm.Visualizer.HideKingPrompt();
    }

    private string currentKingDialogueText = string.Empty;

    private void SetCurrentKingDialogueText(string text)
    {
        currentKingDialogueText = text ?? string.Empty;
    }

    private string GetCurrentKingDialogueText()
    {
        return currentKingDialogueText;
    }

    private static string PickRandomLine(string[] lines, string avoidLine = null)
    {
        if (lines == null || lines.Length == 0) return string.Empty;

        if (lines.Length == 1)
        {
            return lines[0];
        }

        int idx = UnityEngine.Random.Range(0, lines.Length);
        string picked = lines[idx];

        if (!string.IsNullOrEmpty(avoidLine) && picked == avoidLine)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                int nextIdx = (idx + i + 1) % lines.Length;
                if (lines[nextIdx] != avoidLine)
                {
                    picked = lines[nextIdx];
                    break;
                }
            }
        }

        return picked;
    }

    private bool TryHandleKingDamageDialogue(Enemy kingEnemy)
    {
        if (kingEnemy == null) return false;

        int instanceId = kingEnemy.GetInstanceID();
        int currentHp = kingEnemy.HP.Value;

        if (!kingLastHpByInstanceId.TryGetValue(instanceId, out int prevHp))
        {
            kingLastHpByInstanceId[instanceId] = currentHp;
            return false;
        }

        kingLastHpByInstanceId[instanceId] = currentHp;

        bool tookDamage = currentHp < prevHp;
        bool isNonLethal = currentHp > 0 && !kingEnemy.IsDead;

        if (tookDamage && isNonLethal)
        {
            ShowKingDialogue(PickRandomLine(IsEnglish ? DamageLinesEn : DamageLines, lastShownKingDialogueText));
            return true;
        }

        return false;
    }

    private void TrackKingHp(Enemy kingEnemy)
    {
        if (kingEnemy == null) return;
        kingLastHpByInstanceId[kingEnemy.GetInstanceID()] = kingEnemy.HP.Value;
    }

    private void SetKingSmileAnimation(bool shouldPlaySmile)
    {
        if (!shouldPlaySmile)
        {
            StopKingSmileAnimation();
            return;
        }

        if (currentKingEnemy == null || currentKingEnemy.UnitAnimator == null)
        {
            isKingSmileAnimationPlaying = false;
            return;
        }

        isKingSmileAnimationPlaying = currentKingEnemy.UnitAnimator.PlaySmileAnimation();
    }

    private void StopKingSmileAnimation()
    {
        if (!isKingSmileAnimationPlaying)
        {
            return;
        }

        if (currentKingEnemy != null && currentKingEnemy.UnitAnimator != null)
        {
            currentKingEnemy.UnitAnimator.StopEmoteAnimation(currentKingEnemy.CurrentDirection);
        }

        isKingSmileAnimationPlaying = false;
    }
}
