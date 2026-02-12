using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ステージごとの動的データ（敵の状態、破壊された壁など）を管理するクラス
/// GridManagerからデータを分離して責務を分けるために導入
/// </summary>
public class StageDataManager
{
    // Stage -> List of Active/Inactive Enemy Instances
    private Dictionary<StageSO, List<Enemy>> stageEnemiesMap = new Dictionary<StageSO, List<Enemy>>();
    public Dictionary<StageSO, List<Enemy>> StageEnemiesMap => stageEnemiesMap;

    // Stage -> Save Data for Enemies (not yet instantiated)
    private Dictionary<StageSO, List<EnemySaveData>> loadedStageEnemyData = new Dictionary<StageSO, List<EnemySaveData>>();
    public Dictionary<StageSO, List<EnemySaveData>> LoadedStageEnemyData => loadedStageEnemyData;

    // Stage -> Pending Enemies (for Undo/Redo duplication prevention)
    private Dictionary<StageSO, List<Enemy>> pendingStageEnemies = new Dictionary<StageSO, List<Enemy>>();
    public Dictionary<StageSO, List<Enemy>> PendingStageEnemies => pendingStageEnemies;

    // Stage -> Set of Destroyed Wall Positions
    private Dictionary<StageSO, HashSet<Vector2Int>> destroyedWalls = new Dictionary<StageSO, HashSet<Vector2Int>>();
    public Dictionary<StageSO, HashSet<Vector2Int>> DestroyedWalls => destroyedWalls;

    public void ClearAll()
    {
        stageEnemiesMap.Clear();
        loadedStageEnemyData.Clear();
        pendingStageEnemies.Clear();
        destroyedWalls.Clear();
    }

    public void ClearEnemies()
    {
        stageEnemiesMap.Clear();
        loadedStageEnemyData.Clear();
        pendingStageEnemies.Clear();
    }

    public void ClearWalls()
    {
        destroyedWalls.Clear();
    }
}
