using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StageEnemyData
{
    public int stageX;
    public int stageY;
    public List<EnemySaveData> enemies;
}

[System.Serializable]
public class StageWallData
{
    public int stageX;
    public int stageY;
    public List<Vector2IntData> destroyedWalls;
}

[System.Serializable]
public class SaveData
{
    public int resetMapX;
    public int resetMapY;
    public string saveTime; // 保存日時
    public int coins; // 所持コイン数
    public int totalCoins; // 累計コイン数
    public List<string> defeatedEnemies = new List<string>(); // 倒した敵のIDリスト

    // --- ワールドマップ情報 ---
    public int currentMapX; // ワールドマップ上のX
    public int currentMapY; // ワールドマップ上のY
    public List<Vector2IntData> clearedList = new List<Vector2IntData>();
    public List<Vector2IntData> visitedList = new List<Vector2IntData>();

    // --- 階層マップ情報 ---
    public List<Vector2IntData> hierarchicalUnlockedCells = new List<Vector2IntData>();
    public List<Vector2IntData> hierarchicalClearedCells = new List<Vector2IntData>();

    // --- ステージ内のプレイヤー情報 ---
    public int playerGridX;
    public int playerGridY;
    public int playerDirection;
    public int playerHP;
    public List<EnemySaveData> enemyList = new List<EnemySaveData>();
    public List<StageEnemyData> allStagesEnemies = new List<StageEnemyData>();
    public List<StageWallData> allStagesWalls = new List<StageWallData>();

    // --- オプション設定 ---
    // オプションはOptionSaveDataで管理するため削除
    // public int screenMode;
    // public int resolutionWidth;
    // public int resolutionHeight;
    // public int gameSpeed;
    // public int language;
}

[System.Serializable]
public class Vector2IntData
{
    public int x;
    public int y;

    public Vector2IntData(Vector2Int v)
    {
        x = v.x;
        y = v.y;
    }

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }
}

[System.Serializable]
public class EnemySaveData
{
    public int gridX;
    public int gridY;
    public int direction;
    public bool isDead;
    public int currentHP;
    public int aiState;
    public int enemyType;
    public int order;
    public int maxHP;
    public int index;
    public int ammo; // Added for Ranged Enemy Ammo persistence
    public string enemyID; // 一意のID
}