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

    public async UniTask OnPlayerActionComplete()
    {
        if (isProcessingTurn) return;

        isProcessingTurn = true;
        isPlayerTurn = false;

        try
        {
            await ExecuteEnemyTurns();

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
        const float minTotalSeconds = 0.2f;

        bool anyExecuted = false;
        float startAll = Time.realtimeSinceStartup;

        // Create a snapshot of the enemy list to allow modification (e.g. map transition removals) during iteration
        var enemiesSnapshot = new List<Enemy>(unitManager.Enemies);

        foreach (var enemy in enemiesSnapshot)
        {
            // Check if enemy is still valid and active in the current context
            // If enemy was removed from UnitManager (e.g. moved to another map), we might still want to execute?
            // "Active" check (gameObject.activeSelf) handles this if MoveEnemyToStage deactivates it.
            // But if it deactivate itself, it shouldn't run?
            // The enemy.ExecuteTurn() is awaited. If it moved, it finished its turn.

            if (enemy != null && !enemy.IsDead && enemy.gameObject.activeInHierarchy)
            {
                anyExecuted = true;
                await enemy.ExecuteTurn();
            }
        }

        if (anyExecuted)
        {
            float elapsed = Time.realtimeSinceStartup - startAll;
            if (elapsed < minTotalSeconds)
            {
                int waitMs = Mathf.CeilToInt((minTotalSeconds - elapsed) * 1000f);
                await UniTask.Delay(waitMs);
            }
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