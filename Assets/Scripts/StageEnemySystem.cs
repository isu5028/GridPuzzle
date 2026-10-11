using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// GridManagerから分離された敵管理システム
/// </summary>
public class StageEnemySystem : IStageEnemySystem
{
    private GridManager gm;

    public StageEnemySystem(GridManager gridManager)
    {
        this.gm = gridManager;
    }

    public void CleanupAllEnemies()
    {
        foreach (var list in gm.StageEnemiesMap.Values)
        {
            foreach (var enemy in list)
            {
                if (enemy != null)
                {
                    enemy.gameObject.SetActive(false);
                    Object.Destroy(enemy.gameObject);
                }
            }
        }
        gm.StageEnemiesMap.Clear();
    }

    /// <summary>
    /// 全ステージの敵をリセット（初期位置に戻す）
    /// </summary>
    public void ResetAllEnemies()
    {
        CleanupAllEnemies();
        gm.LoadedStageEnemyData.Clear();
        gm.PendingStageEnemies.Clear();
    }

    public void AddPendingEnemy(StageSO stage, Enemy enemy)
    {
        if (!gm.PendingStageEnemies.ContainsKey(stage))
        {
            gm.PendingStageEnemies[stage] = new List<Enemy>();
        }
        gm.PendingStageEnemies[stage].Add(enemy);
    }

    private GameObject getEnemyPrefab(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Slime: return gm.SlimeEnemyPrefab;
            case EnemyType.Goblin: return gm.GoblinEnemyPrefab;
            case EnemyType.Skeleton: return gm.SkeletonEnemyPrefab;
            case EnemyType.Zombie: return gm.ZombieEnemyPrefab;
            case EnemyType.Boar: return gm.BoarEnemyPrefab;
            case EnemyType.Crystal: return gm.CrystalEnemyPrefab;
            case EnemyType.Ghost: return gm.GhostEnemyPrefab;
            case EnemyType.Bomb: return gm.BombEnemyPrefab;
            case EnemyType.King: return gm.KingEnemyPrefab;
            default: return null!;
        }
    }

    private bool IsCrystalStage(StageSO stage)
    {
        if (stage == null || stage.EnemyStartData == null) return false;

        foreach (var enemyData in stage.EnemyStartData)
        {
            if (enemyData.EnemyType == EnemyType.Crystal) return true;
        }

        return false;
    }

    private bool ShouldSpawnInStage(StageSO stage, EnemyType enemyType)
    {
        if (!IsCrystalStage(stage)) return true;
        return enemyType == EnemyType.Crystal;
    }

    private EnemyType ResolveEnemyTypeForSaveData(StageSO stage, EnemySaveData data)
    {
        if (data.enemyType != 0)
        {
            return (EnemyType)data.enemyType;
        }

        if (data.index >= 0 && data.index < stage.EnemyStartData.Count)
        {
            return stage.EnemyStartData[data.index].EnemyType;
        }

        return EnemyType.None;
    }

    private void EnforceCrystalOnlyStageEnemies(StageSO stage, List<Enemy> enemies)
    {
        if (!IsCrystalStage(stage) || enemies == null) return;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (enemy == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            if (enemy.Type != EnemyType.Crystal)
            {
                enemy.gameObject.SetActive(false);
                Object.Destroy(enemy.gameObject);
                enemies.RemoveAt(i);
            }
        }
    }

    public Enemy GetEnemyAt(StageSO stage, Vector2Int pos)
    {
        EnsureStageEnemiesLoaded(stage);
        if (!gm.StageEnemiesMap.ContainsKey(stage)) return null;

        foreach (var e in gm.StageEnemiesMap[stage])
        {
            if (e.CurrentPos == pos && !e.IsDead) return e;
        }
        return null;
    }

    public void EnsureStageEnemiesLoaded(StageSO stage)
    {
        List<Enemy> currentEnemies;
        if (gm.StageEnemiesMap.ContainsKey(stage))
        {
            currentEnemies = gm.StageEnemiesMap[stage];
        }
        else
        {
            currentEnemies = new List<Enemy>();
            gm.StageEnemiesMap.Add(stage, currentEnemies);

            if (!gm.LoadedStageEnemyData.ContainsKey(stage))
            {
                var newEnemies = GenerateEnemies(stage);
                currentEnemies.AddRange(newEnemies);
                foreach(var e in newEnemies) e.gameObject.SetActive(false);
            }
        }

        if (gm.LoadedStageEnemyData.ContainsKey(stage))
        {
            List<EnemySaveData> saveDataList = gm.LoadedStageEnemyData[stage];
            List<Enemy> pending = gm.PendingStageEnemies.ContainsKey(stage) ? gm.PendingStageEnemies[stage] : null;

            // Combine current and pending as candidates for recycling
            List<Enemy> candidates = new List<Enemy>(currentEnemies);
            if (pending != null) candidates.AddRange(pending);

            // Generate (recycling from candidates)
            List<Enemy> newEnemies = GenerateEnemiesFromSaveData(stage, saveDataList, candidates);

            // Rebuild current enemies list to match Save Data exactly
            currentEnemies.Clear();
            currentEnemies.AddRange(newEnemies);
            foreach(var e in newEnemies) e.gameObject.SetActive(false);

            // Cleanup candidates that were NOT recycled (meaning they are not in Save Data)
            foreach (var cand in candidates)
            {
                if (cand != null && !newEnemies.Contains(cand))
                {
                    cand.gameObject.SetActive(false);
                    Object.Destroy(cand.gameObject);
                }
            }

            if (pending != null) pending.Clear();
            gm.LoadedStageEnemyData.Remove(stage);
        }
    }

    // GridManager内部やSaveSystemから呼ばれる
    public void ApplySavedEnemyData(StageSO stage, List<EnemySaveData> savedEnemies)
    {
        if (savedEnemies == null) return;

        if (gm.StageEnemiesMap.ContainsKey(stage))
        {
            foreach (var e in gm.StageEnemiesMap[stage])
            {
                if (e != null)
                {
                    e.gameObject.SetActive(false);
                    Object.Destroy(e.gameObject);
                }
            }
            gm.StageEnemiesMap[stage].Clear();
        }

        var newEnemies = GenerateEnemiesFromSaveData(stage, savedEnemies);
        gm.StageEnemiesMap[stage] = newEnemies;

        if (stage == gm.CurrentStageSOInternal)
        {
            gm.UnitManager.SetEnemies(newEnemies);
        }
    }

    private List<Enemy> GenerateEnemiesFromSaveData(StageSO stage, List<EnemySaveData> saveDataList, List<Enemy> pendingEnemies = null)
    {
        List<Enemy> generatedEnemies = new List<Enemy>();
        List<EnemyStartData> defaultDataList = stage.EnemyStartData;

        foreach (var data in saveDataList)
        {
            EnemyType enemyTypeToUse = ResolveEnemyTypeForSaveData(stage, data);
            if (!ShouldSpawnInStage(stage, enemyTypeToUse))
            {
                continue;
            }

            bool dropsCoinConfigured = false;
            if (data.index >= 0 && data.index < stage.EnemyStartData.Count)
            {
                dropsCoinConfigured = stage.EnemyStartData[data.index].DropsCoin;
            }

            Enemy recycled = null;
            if (pendingEnemies != null)
            {
                foreach(var p in pendingEnemies)
                {
                    if (p.CurrentPos.x == data.gridX && p.CurrentPos.y == data.gridY && p.Type == enemyTypeToUse)
                    {
                        recycled = p;
                        break;
                    }
                }
            }

            if (recycled != null)
            {
                generatedEnemies.Add(recycled);
                pendingEnemies.Remove(recycled);

                recycled.SetHP(data.currentHP);
                recycled.SetAIState(data.aiState);
                if (enemyTypeToUse == EnemyType.Crystal)
                {
                    recycled.ReviveSilent();
                }
                else if(data.isDead)
                {
                    recycled.ForceKillSilent();
                }
                else recycled.ReviveSilent();

                ApplySharedKingCoinIdentity(stage, recycled, enemyTypeToUse, dropsCoinConfigured);
                recycled.UpdateDefeatedIcon();

                recycled.transform.SetParent(gm.Visualizer.GridParent);
                continue;
            }

            Vector3 worldPos = gm.GridToWorldPosition(new Vector2Int(data.gridX, data.gridY));

            GameObject prefab = enemyTypeToUse != EnemyType.None ? getEnemyPrefab(enemyTypeToUse) : null;

            if (prefab != null)
            {
                 int orderToUse = data.order;
                 if (orderToUse == 0 && data.index >= 0 && data.index < stage.EnemyStartData.Count)
                     orderToUse = stage.EnemyStartData[data.index].SetActionOrder;

                 int maxHPToUse = data.maxHP;
                 if (maxHPToUse == 0)
                 {
                     if (data.index >= 0 && data.index < stage.EnemyStartData.Count)
                        maxHPToUse = stage.EnemyStartData[data.index].MaxHP;
                     else
                        maxHPToUse = 3;
                 }

                bool dropsCoinToUse = dropsCoinConfigured;

                var newEnemy = InstantiateAndInitializeEnemy(prefab, worldPos, new Vector2Int(data.gridX, data.gridY), (Direction)data.direction, maxHPToUse, orderToUse, data.ammo, dropsCoinToUse);

                newEnemy.EnemyID = string.IsNullOrEmpty(data.enemyID) ? $"{stage.name}_{data.index}" : data.enemyID;
                ApplySharedKingCoinIdentity(stage, newEnemy, enemyTypeToUse, dropsCoinConfigured);
                newEnemy.SetHP(data.currentHP);
                newEnemy.SetAIState(data.aiState);
                if (enemyTypeToUse == EnemyType.Crystal)
                {
                    newEnemy.ReviveSilent();
                }
                else if(data.isDead)
                {
                    newEnemy.ForceKillSilent();
                }

                newEnemy.name = $"Enemy_{enemyTypeToUse}_{data.index}_Loaded";
                newEnemy.SetDirection((Direction)data.direction);
                newEnemy.UpdateDefeatedIcon();

                generatedEnemies.Add(newEnemy);
            }
        }
        return generatedEnemies;
    }

    // GridManager.LoadStage から呼ばれる必要があるので PrepareEnemiesForStage はここにはない (LoadStage内で直接呼ぶか、Helperへ)
    // 実は GridManager.LoadStage がこのメソッドを呼ぶ必要があるが、partial class ではないのでアクセスできない。
    // そのため、public か internal にして公開する必要がある。
    // しかし上記のインターフェースには定義していない。GridManagerがインスタンスを持っているので直接呼べば良い。
    // 今回はインターフェースに追加せず、publicメソッドとして定義する。
    public void PrepareEnemiesForStage(StageSO stage)
    {
        // Robustness: Ensure enemies from ALL other stages are deactivated.
        DeactivateAllEnemiesExcept(stage);

        // 出口ステージは訪問ごとにショップ購入状態を反映するため常に再生成する
        if (stage != null && stage.IsExitStage && gm.StageEnemiesMap.ContainsKey(stage))
        {
            var existingList = gm.StageEnemiesMap[stage];
            foreach (var e in existingList)
            {
                if (e != null)
                {
                    e.gameObject.SetActive(false);
                    Object.Destroy(e.gameObject);
                }
            }
            gm.StageEnemiesMap.Remove(stage);
            gm.LoadedStageEnemyData.Remove(stage);
        }

        EnsureStageEnemiesLoaded(stage);

        List<Enemy> enemies = gm.StageEnemiesMap[stage];
        EnforceCrystalOnlyStageEnemies(stage, enemies);
        gm.UnitManager.SetEnemies(enemies);

        foreach (var enemy in enemies)
        {
            enemy.gameObject.SetActive(true);
            enemy.WarpTo(enemy.CurrentPos, enemy.CurrentDirection);
            enemy.SetDissolveAmount(0f);
            enemy.UpdateDefeatedIcon();

            if (enemy.Type == EnemyType.Crystal && enemy.IsDead)
            {
                enemy.ReviveSilent();
            }

            if (enemy.IsDead)
            {
                enemy.ApplyDeadStateVisual();
            }
        }

        gm.UnitManager.SortEnemiesByOrder();
    }

    /// <summary>
    /// 指定したステージ以外の全ての敵を非アクティブにする
    /// </summary>
    private void DeactivateAllEnemiesExcept(StageSO targetStage)
    {
        foreach (var kvp in gm.StageEnemiesMap)
        {
            if (kvp.Key == targetStage) continue;

            foreach (var enemy in kvp.Value)
            {
                if (enemy != null && enemy.gameObject.activeSelf)
                {
                    enemy.gameObject.SetActive(false);
                }
            }
        }
    }

    private List<Enemy> GenerateEnemies(StageSO stage)
    {
        List<EnemyStartData> enemyDataList = stage.EnemyStartData;
        List<Enemy> generatedEnemies = new List<Enemy>();
        int count = 0;

        foreach (var enemyData in enemyDataList)
        {
            if (!ShouldSpawnInStage(stage, enemyData.EnemyType))
            {
                continue;
            }

            // 出口ステージのKingは全ショップアイテム購入済のときのみ出現
            if (stage.IsExitStage && enemyData.EnemyType == EnemyType.King)
            {
                if (gm.ShopSystem == null || !gm.ShopSystem.AreAllShopItemsPurchasedAcrossAllShopStages())
                {
                    count++;
                    continue;
                }
            }

            Vector3 worldPos = gm.GridToWorldPosition(enemyData.Position);
            GameObject prefab = getEnemyPrefab(enemyData.EnemyType);

            var enemyScript = InstantiateAndInitializeEnemy(prefab, worldPos, enemyData.Position, enemyData.Direction, enemyData.MaxHP, enemyData.SetActionOrder, enemyData.Ammo, enemyData.DropsCoin);
            enemyScript.name = $"Enemy_{enemyData.EnemyType}_{count}";
            enemyScript.EnemyID = $"{stage.name}_{count}";
            ApplySharedKingCoinIdentity(stage, enemyScript, enemyData.EnemyType, enemyData.DropsCoin);
            enemyScript.UpdateDefeatedIcon();

            count++;

            enemyScript.SetDirection(enemyData.Direction);
            generatedEnemies.Add(enemyScript);
        }
        return generatedEnemies;
    }

    private static void ApplySharedKingCoinIdentity(StageSO stage, Enemy enemy, EnemyType enemyType, bool dropsCoinConfigured)
    {
        if (stage == null || enemy == null) return;
        if (enemyType != EnemyType.King) return;

        // 出口Kingは常に「既に一度倒されている」扱い
        // 1) 専用IDを付与
        // 2) 敗北済みに即登録
        // 3) コインドロップを無効化
        if (stage.IsExitStage)
        {
            enemy.EnemyID = Enemy.ExitKingAlwaysDefeatedID;
            enemy.SetDropsCoin(false);
            GameProgressManager.Instance.MarkEnemyDefeated(Enemy.ExitKingAlwaysDefeatedID);
            return;
        }

        // ShopのコインドロップKingは共有IDで管理
        // これによりShop内で「既にコインを落としたか」の状態を一貫させる
        bool shouldShareCoinState = dropsCoinConfigured;
        if (!shouldShareCoinState) return;

        enemy.EnemyID = Enemy.KingCoinSharedID;
        enemy.SetDropsCoin(true);
    }

    private Enemy InstantiateAndInitializeEnemy(GameObject prefab, Vector3 worldPos, Vector2Int gridPos, Direction dir, int maxHp, int order, int ammo = -1, bool dropsCoin = false)
    {
        GameObject enemyObj = Object.Instantiate(prefab, worldPos, Quaternion.identity, gm.Visualizer.GridParent);
        var enemyScript = enemyObj.GetComponent<Enemy>();

        if (maxHp <= 0)
        {
            maxHp = enemyScript.MaxHP;
            if (maxHp <= 0) maxHp = 3;
        }

        var aiRanged = enemyObj.GetComponent<EnemyAIChaserRanged>();
        var aiBomb = enemyObj.GetComponent<EnemyAIBomb>();
        if (aiRanged == null && aiBomb == null)
        {
            ammo = -1;
        }

        enemyScript.Initialize(gm, gm.TurnManager, gm.CommandManager, gridPos, dir, maxHp, order, ammo, dropsCoin);
        return enemyScript;
    }

    public bool CheckIfStageHasEnemy(StageSO stage)
    {
        if (gm.StageEnemiesMap.TryGetValue(stage, out var enemies))
        {
             foreach (var enemy in enemies)
             {
                 if(enemy != null && !enemy.IsDead) return true;
             }
             return false;
        }

        if (gm.LoadedStageEnemyData.TryGetValue(stage, out var saveDataList))
        {
            foreach (var data in saveDataList)
            {
                if (!data.isDead) return true;
            }
            return false;
        }

        return stage.EnemyStartData.Count > 0;
    }

    public StageSO GetStageOfEnemy(Enemy enemy)
    {
        foreach (var pair in gm.StageEnemiesMap)
        {
            if (pair.Value.Contains(enemy)) return pair.Key;
        }
        return null;
    }

    public List<Enemy> GetEnemiesOnStage(StageSO stage)
    {
        if (gm.StageEnemiesMap.TryGetValue(stage, out var list))
        {
            return list;
        }
        return null; // Should be empty list? or null ok?
    }

    public int GetMaxEnemyOrderInStage(StageSO stage)
    {
        int maxOrder = 0;

        if (gm.StageEnemiesMap.TryGetValue(stage, out var enemies))
        {
            foreach (var e in enemies)
            {
                if (e != null && !e.IsDead && e.Order > maxOrder) maxOrder = e.Order;
            }
        }

        if (gm.PendingStageEnemies.TryGetValue(stage, out var pending))
        {
            foreach (var pe in pending)
            {
                if (pe != null && !pe.IsDead && pe.Order > maxOrder) maxOrder = pe.Order;
            }
        }

        if (!gm.StageEnemiesMap.ContainsKey(stage))
        {
            if (gm.LoadedStageEnemyData.TryGetValue(stage, out var dataList))
            {
                foreach (var d in dataList)
                {
                    if (!d.isDead && d.order > maxOrder) maxOrder = d.order;
                }
            }
            else if (stage.EnemyStartData != null)
            {
                foreach (var esd in stage.EnemyStartData)
                {
                    if (esd.SetActionOrder > maxOrder) maxOrder = esd.SetActionOrder;
                }
            }
        }

        return maxOrder;
    }
}
