using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Cysharp.Threading;

public class TurnManager : MonoBehaviour
{
    private GridManager gridManager = null!;
    private bool isPlayerTurn = true;
    public bool IsPlayerTurn => isPlayerTurn;
    private bool isProcessingTurn = false;
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
        commandManager = GetComponent<CommandManager>();

        isPlayerTurn = true;
        isProcessingTurn = false;

        // 最初のターン開始時スナップショット
        commandManager.CaptureTurnStart(gridManager.PlayerScript, gridManager.EnemyScripts);
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
            commandManager.CaptureTurnStart(gridManager.PlayerScript, gridManager.EnemyScripts);
            snapshotCapturedThisTurn = true;
        }
    }

    public async UniTask OnPlayerActionComplete()
    {
        if (isProcessingTurn) return;

        isProcessingTurn = true;
        isPlayerTurn = false;

        await ExecuteEnemyTurns();

        commandManager.CompleteTurn();

        // Warpタイルを更新（敵が全滅した場合にタイルを変更）
        gridManager.UpdateWarpTilesPublic();

        isPlayerTurn = true;
        isProcessingTurn = false;

        snapshotCapturedThisTurn = false; // 次ターン最初の行動前にスナップショット取得させる
    }

    private async UniTask ExecuteEnemyTurns()
    {
        const float minTotalSeconds = 0.2f;

        bool anyExecuted = false;
        float startAll = Time.realtimeSinceStartup;

        foreach (var enemy in gridManager.EnemyScripts)
        {
            if (enemy != null && !enemy.IsDead)
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
        await commandManager.UndoTurn(gridManager.PlayerScript, gridManager.EnemyScripts);

        // Undoで敵が復活する可能性があるのでWarpタイルを更新
        gridManager.UpdateWarpTilesPublic();
    }

    public void ResetTurn()
    {
        if (!isPlayerTurn || isProcessingTurn) return;
        commandManager.ResetTurn();
        gridManager.Reset();

        // Resetで敵が復活するのでWarpタイルを更新
        gridManager.UpdateWarpTilesPublic();
    }
}