using UnityEngine;
using System;

[Serializable]
public enum EnemyType
{
    None,
    Slime,
    Goblin,
    Skeleton,
    Zombie,
    Boar,
    Crystal,
}

public enum CellType
{
    Wall,
    Floor,
    Warp,
    DamageFloor,
    Ice,
    SyncMap,
    Tutorial,
}

public enum Direction
{
    Right, Down, Left, Up
}

public enum WorldMapCellType
{
    Empty,
    Unknown,
    Uncleared,
    Cleared,
    CurrentUnknown,
    CurrentUnCleared,
    CurrentCleared
}

public static class Utility
{
    public static readonly Color[] mainColors = {
        ColorFromRGB(255, 255, 255),
        ColorFromRGB(183, 70, 70),
        ColorFromRGB(108, 84, 111),
        ColorFromRGB(52, 57, 81),
    };

    public static Color ColorFromRGB(int r, int g, int b, int a = 255)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    public static Direction GetDirectionFromStickInput(Vector2 input)
    {
        if (input == Vector2.zero)
        {
            Debug.LogWarning("GetDirectionFromStickInput: Zero input provided");
            return Direction.Right;
        }
        else
        {
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                if (input.x > 0) return Direction.Right;
                else return Direction.Left;
            }
            else
            {
                if (input.y > 0) return Direction.Up;
                else return Direction.Down;
            }
        }
    }

    public static Direction GetDirectionFromVector(Vector2Int vector)
    {
        if (vector == Vector2Int.zero)
        {
            Debug.LogWarning("GetDirectionFromVector: Zero vector provided");
            return Direction.Right;
        }
        else
        {
            if (Mathf.Abs(vector.x) > Mathf.Abs(vector.y))
            {
                if (vector.x > 0) return Direction.Right;
                else return Direction.Left;
            }
            else
            {
                if (vector.y > 0) return Direction.Up;
                else return Direction.Down;
            }
        }
    }

    public static Vector2Int GetVectorFromDirection(Direction direction)
    {
        switch (direction)
        {
            case Direction.Right:
                return Vector2Int.right;
            case Direction.Down:
                return Vector2Int.down;
            case Direction.Left:
                return Vector2Int.left;
            case Direction.Up:
                return Vector2Int.up;
            default:
                Debug.LogError("GetVectorFromDirection: Invalid direction " + direction);
                return Vector2Int.zero;
        }
    }

    public static Vector2Int RotateVector(Vector2Int vec, Direction direction)
    {
        switch (direction)
        {
            case Direction.Right:
                return vec; // (1, 0) → (1, 0)
            case Direction.Up:
                return new Vector2Int(-vec.y, vec.x); // (1, 0) → (0, 1)
            case Direction.Left:
                return new Vector2Int(-vec.x, -vec.y); // (1, 0) → (-1, 0)
            case Direction.Down:
                return new Vector2Int(vec.y, -vec.x); // (1, 0) → (0, -1)
            default:
                return vec;
        }
    }

    public static T GetNextEnum<T>(T src, int direction) where T : Enum
    {
        T[] arr = (T[])Enum.GetValues(src.GetType());
        int j = Array.IndexOf<T>(arr, src) + direction;
        int length = arr.Length;
        // ループ処理
        j = (j % length + length) % length;
        return arr[j];
    }
}