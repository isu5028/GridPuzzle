using UnityEngine;

// 純粋なC#クラスとして定義（MonoBehaviourである必要はない）
public class WorldMapModel
{
    private WorldMapSO worldMapSO;
    private int mapLength = 8; // 定数管理またはGameProgressManagerから取得
    public int MapSize => mapLength;

    public WorldMapModel(WorldMapSO worldMapSO)
    {
        this.worldMapSO = worldMapSO;
    }

    public StageSO GetStageSO(Vector2Int pos)
    {
        return worldMapSO.GetStageAt(pos.x, pos.y);
    }

    public StageSO GetStageAt(Vector2Int pos)
    {
        return worldMapSO.GetStageAt(pos.x, pos.y);
    }

    public Vector2Int GetCurrentPlayerPosition()
    {
        return GameProgressManager.Instance.GetCurrentMapPosition();
    }

    public WorldMapCellType GetCellType(Vector2Int pos)
    {
        return GameProgressManager.Instance.GetCellType(pos.x, pos.y);
    }

    public WorldMapCellType[][] GetAllMapData()
    {
        return GameProgressManager.Instance.GetMapData();
    }

    public bool IsTraversable(Vector2Int targetPos)
    {
        // 範囲外チェック
        if (targetPos.x < 0 || targetPos.x >= mapLength ||
            targetPos.y < 0 || targetPos.y >= mapLength)
        {
            return false;
        }
        return true;
        // Unknownチェック
        //var type = GetCellType(targetPos);
        //return type != WorldMapCellType.Unknown;
    }

    // 現在地をクリア済みにする等の操作
    public void MarkCurrentAsCleared()
    {
        var pos = GetCurrentPlayerPosition();
        GameProgressManager.Instance.MarkPositionAsCleared(pos);
    }

    public void ResetClearedStatus()
    {
        GameProgressManager.Instance.ResetStageCleared();
    }
}