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
    private List<TurnRecord> redoRecords = new List<TurnRecord>();
    private List<ICommand> currentTurnCommands = new List<ICommand>();
    private TurnRecord pendingRecord = null;
    public bool HasPendingTurnRecord => pendingRecord != null;
    private bool isUndoing = false;
    public bool IsUndoing => isUndoing;
    private bool isRedoing = false;
    public bool IsRedoing => isRedoing;


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

    // CompleteTurn()後に発生したコマンドを直前のターンレコードに追記する
    // （例：MoveComplete後に非同期で起動されるCrateBoundaryTransitionCommandなど）
    public void AppendToLastTurn(ICommand command)
    {
        command.Execute();
        if (turnRecords.Count > 0)
        {
            turnRecords[turnRecords.Count - 1].commands.Add(command);
        }
        else
        {
            // フォールバック：記録可能なターンがなければ通常の currentTurnCommands に追加
            currentTurnCommands.Add(command);
        }
    }

    // 非同期で遅延実行されたコマンド用。
    // ターンが開いている間は現在ターンへ、閉じている場合は直前ターンへ追記する。
    public void ExecuteCommandWithTurnFallback(ICommand command)
    {
        command.Execute();

        if (pendingRecord != null)
        {
            currentTurnCommands.Add(command);
            return;
        }

        if (turnRecords.Count > 0)
        {
            turnRecords[turnRecords.Count - 1].commands.Add(command);
            return;
        }

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

        // コマンドが一つもない空のターンは記録しない
        // (Move以外のアクションなどで意図せずターン終了が呼ばれた場合や、
        //  ステージロード直後の初期化由来の不要なターン記録を防ぐため)
        if (currentTurnCommands.Count == 0)
        {
            pendingRecord = null;
            return;
        }

        pendingRecord.commands = new List<ICommand>(currentTurnCommands);
        turnRecords.Add(pendingRecord);

        currentTurnCommands.Clear();
        pendingRecord = null;

        // 新しいアクションが実行されたらRedoスタックをクリア
        // （Redo中の CompleteTurn ではクリアしない）
        if (!isRedoing)
        {
            redoRecords.Clear();
        }

        // 古いターンを削除
        if (turnRecords.Count > maxStoredTurns)
        {
            turnRecords.RemoveRange(0, turnRecords.Count - maxStoredTurns);
        }
    }

    public async UniTask<bool> UndoTurn(Player player, List<Enemy> enemies)
    {
        if (turnRecords.Count == 0)
        {
            Debug.Log("Undoできるターンがありません");
            return false;
        }

        AudioManager.Instance.PlaySE(AudioManager.SEType.reset);
        isUndoing = true;

        TurnRecord record = turnRecords[turnRecords.Count - 1];
        turnRecords.RemoveAt(turnRecords.Count - 1);
        Debug.Log("Undoing turn with " + record.commands.Count + " commands.");

        for(int i = record.commands.Count - 1; i >= 0; i--)
        {
            var cmd = record.commands[i];
            try
            {
                if (cmd is IAsyncUndoCommand asyncCmd)
                {
                    await asyncCmd.UndoAsync();
                }
                else
                {
                    cmd.Undo();
                }
            }
            catch (UnityEngine.MissingReferenceException)
            {
                // 対象が破棄されている場合はスキップ
                Debug.LogWarning("Skipped undo command for missing object.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Undo Error: {e}");
            }
        }

        // Undoしたレコードをredoスタックに退避
        redoRecords.Add(record);

        isUndoing = false;
        return true;
    }

    public async UniTask<bool> RedoTurn(Player player, List<Enemy> enemies)
    {
        if (redoRecords.Count == 0)
        {
            Debug.Log("Redoできるターンがありません");
            return false;
        }

        AudioManager.Instance.PlaySE(AudioManager.SEType.reset);
        isRedoing = true;

        TurnRecord record = redoRecords[redoRecords.Count - 1];
        redoRecords.RemoveAt(redoRecords.Count - 1);
        Debug.Log("Redoing turn with " + record.commands.Count + " commands.");

        // CaptureTurnStart 相当の処理
        pendingRecord = new TurnRecord();

        for (int i = 0; i < record.commands.Count; i++)
        {
            var cmd = record.commands[i];
            try
            {
                ExecuteCommand(cmd);
            }
            catch (UnityEngine.MissingReferenceException)
            {
                Debug.LogWarning("Skipped redo command for missing object.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Redo Error: {e}");
            }
        }

        // CompleteTurn 相当の処理（isRedoing中なのでredoRecordsはクリアされない）
        CompleteTurn();

        isRedoing = false;
        return true;
    }

    public void ResetTurn()
    {
        AudioManager.Instance.PlaySE(AudioManager.SEType.reset);

        // ターン履歴をクリア
        turnRecords.Clear();
        redoRecords.Clear();
        currentTurnCommands.Clear();
        pendingRecord = null;
    }

    public void Warped()
    {
        // 現在のターン履歴をクリア
        turnRecords.Clear();
        redoRecords.Clear();
        currentTurnCommands.Clear();
        pendingRecord = null;
    }

    public bool CanUndo() => turnRecords.Count > 0;
    public bool CanRedo() => redoRecords.Count > 0;
}