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
                if (enemy != null) Object.Destroy(enemy.gameObject);
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
            default: return null!;
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

            List<Enemy> newEnemies = GenerateEnemiesFromSaveData(stage, saveDataList, pending);

            currentEnemies.AddRange(newEnemies);
            foreach(var e in newEnemies) e.gameObject.SetActive(false);

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
                if (e != null) Object.Destroy(e.gameObject);
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
            Enemy recycled = null;
            if (pendingEnemies != null)
            {
                foreach(var p in pendingEnemies)
                {
                    if (p.CurrentPos.x == data.gridX && p.CurrentPos.y == data.gridY && (int)p.Type == data.enemyType)
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
                if(data.isDead) recycled.ForceKillSilent();
                else recycled.ReviveSilent();

                recycled.transform.SetParent(gm.Visualizer.transform);
                continue;
            }

            Vector3 worldPos = gm.GridToWorldPosition(new Vector2Int(data.gridX, data.gridY));

            GameObject prefab = null;
            if (data.enemyType != 0)
            {
                 prefab = getEnemyPrefab((EnemyType)data.enemyType);
            }
            else
            {
                 if(data.index >= 0 && data.index < stage.EnemyStartData.Count)
                    prefab = getEnemyPrefab(stage.EnemyStartData[data.index].EnemyType);
            }

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

                var newEnemy = InstantiateAndInitializeEnemy(prefab, worldPos, new Vector2Int(data.gridX, data.gridY), (Direction)data.direction, maxHPToUse, orderToUse, data.ammo);

                newEnemy.SetHP(data.currentHP);
                newEnemy.SetAIState(data.aiState);
                if(data.isDead) newEnemy.ForceKillSilent();

                newEnemy.name = $"Enemy_{(EnemyType)data.enemyType}_{data.index}_Loaded";
                newEnemy.SetDirection((Direction)data.direction);

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
        EnsureStageEnemiesLoaded(stage);

        List<Enemy> enemies = gm.StageEnemiesMap[stage];
        gm.UnitManager.SetEnemies(enemies);

        foreach (var enemy in enemies)
        {
            enemy.gameObject.SetActive(true);
            enemy.WarpTo(enemy.CurrentPos, enemy.CurrentDirection);
            enemy.SetDissolveAmount(0f);

            if (enemy.IsDead)
            {
                enemy.ApplyDeadStateVisual();
            }
        }

        gm.UnitManager.SortEnemiesByOrder();
    }

    private List<Enemy> GenerateEnemies(StageSO stage)
    {
        List<EnemyStartData> enemyDataList = stage.EnemyStartData;
        List<Enemy> generatedEnemies = new List<Enemy>();
        int count = 0;

        foreach (var enemyData in enemyDataList)
        {
            Vector3 worldPos = gm.GridToWorldPosition(enemyData.Position);
            GameObject prefab = getEnemyPrefab(enemyData.EnemyType);

            var enemyScript = InstantiateAndInitializeEnemy(prefab, worldPos, enemyData.Position, enemyData.Direction, enemyData.MaxHP, enemyData.SetActionOrder, enemyData.Ammo);
            enemyScript.name = $"Enemy_{enemyData.EnemyType}_{count}";
            count++;

            enemyScript.SetDirection(enemyData.Direction);
            generatedEnemies.Add(enemyScript);
        }
        return generatedEnemies;
    }

    private Enemy InstantiateAndInitializeEnemy(GameObject prefab, Vector3 worldPos, Vector2Int gridPos, Direction dir, int maxHp, int order, int ammo = -1)
    {
        GameObject enemyObj = Object.Instantiate(prefab, worldPos, Quaternion.identity, gm.Visualizer.GridParent);
        var enemyScript = enemyObj.GetComponent<Enemy>();

        var ai = enemyObj.GetComponent<EnemyAIChaserRanged>();
        if (ai == null)
        {
            ammo = -1;
        }

        enemyScript.Initialize(gm, gm.TurnManager, gm.CommandManager, gridPos, dir, maxHp, order, ammo);
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
}
