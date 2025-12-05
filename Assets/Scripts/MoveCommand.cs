using UnityEngine;

public class MoveCommand : ICommand
{
    private Unit unit;
    private Vector2Int fromPos;
    private Vector2Int toPos;
    private Direction fromDirection;
    private Direction toDirection;

    public MoveCommand(Unit unit, Vector2Int from, Vector2Int to, Direction fromDir, Direction toDir)
    {
        this.unit = unit;
        this.fromPos = from;
        this.toPos = to;
        this.fromDirection = fromDir;
        this.toDirection = toDir;
    }

    public void Execute()
    {
        unit.MoveTo(toPos, toDirection);
    }

    public void Undo()
    {
        unit.MoveTo(fromPos, fromDirection);
    }
}