using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Cysharp.Threading;

[RequireComponent(typeof(UnitManager))]
public class TurnManager : MonoBehaviour
{
    [SerializeField] private HUDFeedback undoFeedback = null!;
    [SerializeField] private HUDFeedback resetFeedback = null!;
    private GridManager gridManager = null!;
    private UnitManager unitManager = null!;
    private bool isPlayerTurn = true;
    public bool IsPlayerTurn => isPlayerTurn;
    private bool isProcessingTurn = false;
    public bool IsProcessingTurn => isProcessingTurn;
    private CommandManager commandManager = null!;
    public CommandManager CommandManager => commandManager;
    private bool snapshotCapturedThisTurn = false;
    private bool isOptionOpen = false;
    public bool IsOptionOpen => isOptionOpen;
    private bool isMapOpen = false;
    public bool IsMapOpen => isMapOpen;

    public void SetOptionState(bool isOpen)
    {
        isOptionOpen = isOpen;
    }

    public void Initialize()
    {
        gridManager = GetComponent<GridManager>();
        unitManager = GetComponent<UnitManager>();
        commandManager = GetComponent<CommandManager>();

        isPlayerTurn = true;
        isProcessingTurn = false;

        // 最初のターン開始時スナップショット
        commandManager.CaptureTurnStart(unitManager.Player, unitManager.Enemies);
        snapshotCapturedThisTurn = true;
    }

    public void SetMapState(bool isOpen)
    {
        isMapOpen = isOpen;
    }

    // プレイヤーが今ターン初めて行動する前に呼ばれる
    public void EnsureTurnStartSnapshot()
    {
        if (!snapshotCapturedThisTurn)
        {
            commandManager.CaptureTurnStart(unitManager.Player, unitManager.Enemies);
            snapshotCapturedThisTurn = true;
        }
    }

    public async UniTask OnPlayerActionComplete(bool skipEnemyTurn = false)
    {
        if (isProcessingTurn) return;

        isProcessingTurn = true;
        isPlayerTurn = false;

        try
        {
            if (!skipEnemyTurn)
            {
                await ExecuteEnemyTurns();
            }

            commandManager.CompleteTurn();

            // UnitManagerへの状態変更通知
            unitManager.NotifyUnitStateChanged();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TurnManager] Error during enemy turn execution: {e}");
        }
        finally
        {
            isPlayerTurn = true;
            isProcessingTurn = false;
            snapshotCapturedThisTurn = false; // 次ターン最初の行動前にスナップショット取得させる
        }
    }

    private async UniTask ExecuteEnemyTurns()
    {
        // 全ての敵が行動し終えた後の最低待機時間（ターン区切りを明確にするため）
        const float minTotalSeconds = 0.15f;

        float startAll = Time.realtimeSinceStartup;

        // Create a snapshot of the enemy list to allow modification (e.g. map transition removals) during iteration
        var enemiesSnapshot = new List<Enemy>(unitManager.Enemies);

        foreach (var enemy in enemiesSnapshot)
        {
            if (enemy != null && !enemy.IsDead && enemy.gameObject.activeInHierarchy)
            {
                await enemy.ExecuteTurn();
            }
        }

        // 敵がいてもいなくても、ターン終了後の最低待機時間を確保
        // これにより連続入力時に一瞬で移動することを防ぐ
        float elapsed = Time.realtimeSinceStartup - startAll;
        if (elapsed < minTotalSeconds)
        {
            int waitMs = Mathf.CeilToInt((minTotalSeconds - elapsed) * 1000f);
            await UniTask.Delay(waitMs);
        }
    }

    public async UniTask UndoTurn()
    {
        if (!isPlayerTurn || isProcessingTurn) return;
        bool executedUndo = await commandManager.UndoTurn(unitManager.Player, unitManager.Enemies);

        if (executedUndo)
        {
            // Undoで敵が復活する可能性があるので通知
            unitManager.NotifyUnitStateChanged();
            undoFeedback.PlayFeedback();
        }
    }

    public async UniTask RedoTurn()
    {
        if (!isPlayerTurn || isProcessingTurn) return;
        bool executedRedo = await commandManager.RedoTurn(unitManager.Player, unitManager.Enemies);

        if (executedRedo)
        {
            unitManager.NotifyUnitStateChanged();
            undoFeedback.PlayFeedback();
        }
    }

    public void ResetTurn()
    {
        if (!isPlayerTurn || isProcessingTurn) return;
        commandManager.ResetTurn();
        gridManager.Reset();

        // Resetで敵が復活するので通知
        unitManager.NotifyUnitStateChanged();
        resetFeedback.PlayFeedback();
    }
}