using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New WorldMap", menuName = "Grid Puzzle/World Map")]
public class WorldMapSO : ScriptableObject
{
    [Header("Hierarchical Map Settings")]
    [SerializeField] private StageSO rootStage = null!;
    public StageSO RootStage => rootStage;

    [SerializeField] private List<StageSO> allStages = new List<StageSO>();

    // --- 便利機能 ---

    /// <summary>
    /// フロアインデックスでステージを取得する（階段移動用）
    /// 同じフロアに複数ステージがある場合は最初に見つかったものを返す
    /// 推奨: StageSO.StairConnections を使用して明示的に遷移先を指定する
    /// </summary>
    public StageSO GetStageByFloorIndex(int floorIndex)
    {
        if (allStages == null) return null;

        foreach (var stage in allStages)
        {
            if (stage != null && stage.FloorIndex == floorIndex)
            {
                return stage;
            }
        }
        return null; // 見つからなかった場合
    }

    /// <summary>
    /// 全ステージのリストを取得
    /// </summary>
    public List<StageSO> GetAllStages()
    {
        return allStages ?? new List<StageSO>();
    }

    // 以前のグリッドベースの機能は廃止（互換性のためにnullを返す）
    public StageSO GetStageAt(int x, int y) => null;
    public Vector2Int GetStageCoordinates(StageSO stage) => new Vector2Int(-1, -1);
    public StageSO GetNeighborStage(StageSO currentStage, Direction direction) => null;
}