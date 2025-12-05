using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public int resetMapX;
    public int resetMapY;
    // --- ワールドマップ情報 ---
    public int currentMapX; // ワールドマップ上のX
    public int currentMapY; // ワールドマップ上のY
    public List<Vector2IntData> clearedList = new List<Vector2IntData>();
    public List<Vector2IntData> visitedList = new List<Vector2IntData>();

    // --- ステージ内のプレイヤー情報 ---
    public int playerGridX;
    public int playerGridY;
    public int playerDirection;
    public int playerHP;
    public List<EnemySaveData> enemyList = new List<EnemySaveData>();
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
    public int index;
}