using System.Collections.Generic;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

public class CommandManager : MonoBehaviour
{
    [SerializeField] private int maxStoredTurns = 500;

    private class TurnRecord
    {
        public List<ICommand> commands = new List<ICommand>();
    }
    private List<TurnRecord> turnRecords = new List<TurnRecord>();
    private List<ICommand> currentTurnCommands = new List<ICommand>();
    private TurnRecord pendingRecord = null;
    private bool isUndoing = false;
    public bool IsUndoing => isUndoing;


    // 初期化処理用
    private class InitialState
    {
        public int playerHP;
        public Vector2Int playerPosition;
        public Direction playerDirection;
        public List<int> enemyHPs = new List<int>();
        public List<Vector2Int> enemyPositions = new List<Vector2Int>();
        public List<Direction> enemyDirections = new List<Direction>();
    }

    public void ExecuteCommand(ICommand command)
    {
        command.Execute();
        currentTurnCommands.Add(command);
    }

    public void CaptureTurnStart(Player player, List<Enemy> enemies)
    {
        if (pendingRecord != null) return;

        pendingRecord = new TurnRecord();
    }

    public void CompleteTurn()
    {
        if (pendingRecord == null) return;

        pendingRecord.commands = new List<ICommand>(currentTurnCommands);
        turnRecords.Add(pendingRecord);

        currentTurnCommands.Clear();
        pendingRecord = null;

        // 古いターンを削除
        if (turnRecords.Count > maxStoredTurns)
        {
            turnRecords.RemoveRange(0, turnRecords.Count - maxStoredTurns);
        }
    }

    public async UniTask UndoTurn(Player player, List<Enemy> enemies)
    {
        if (turnRecords.Count == 0)
        {
            Debug.Log("Undoできるターンがありません");
            return;
        }

        AudioManager.Instance.PlaySE(AudioManager.SEType.reset);
        isUndoing = true;

        TurnRecord record = turnRecords[turnRecords.Count - 1];
        turnRecords.RemoveAt(turnRecords.Count - 1);
        Debug.Log("Undoing turn with " + record.commands.Count + " commands.");

        for(int i = record.commands.Count - 1; i >= 0; i--)
        {
            var cmd = record.commands[i];
            if (cmd is TransitionCommand tc)
            {
                await tc.UndoAsync();
            }
            else
            {
                cmd.Undo();
            }
        }
        isUndoing = false;
    }

    public void ResetTurn()
    {
        AudioManager.Instance.PlaySE(AudioManager.SEType.reset);

        // ターン履歴をクリア
        turnRecords.Clear();
        currentTurnCommands.Clear();
        pendingRecord = null;
    }

    public void Warped()
    {
        // 現在のターン履歴をクリア
        turnRecords.Clear();
        currentTurnCommands.Clear();
        pendingRecord = null;
    }

    public bool CanUndo() => turnRecords.Count > 0;
}