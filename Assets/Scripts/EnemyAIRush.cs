using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class EnemyAIRush : EnemyAI
{
    public enum RushState
    {
        Idle,
        Charging, // Found player, preparing to rush (1 turn wait)
    }

    private RushState currentState = RushState.Idle;
    private Direction chargeDirection;
    private int rushDamage = 1;

    // AI State Persistence
    // Pack State (2 bits) and Direction (2 bits, shifted by 2)
    public override int GetState()
    {
        return (int)currentState | ((int)chargeDirection << 2);
    }

    public override void SetState(int state)
    {
        currentState = (RushState)(state & 0x3); // First 2 bits
        chargeDirection = (Direction)((state >> 2) & 0x3); // Next 2 bits

        // Visual Sync: Ensure enemy faces the charge direction if Charging
        if (currentState == RushState.Charging)
        {
            var enemy = GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.SetDirection(chargeDirection);
            }
        }
    }

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        if (currentState == RushState.Charging)
        {
            // Execute Rush
            await ExecuteRush(enemy, chargeDirection);

            // Use Command for State Change to support Undo
            commandManager.ExecuteCommand(new SetRushStateCommand(this, (int)RushState.Idle));
            return;
        }

        // Idle: Look for player to charge
        Unit player = gridManager.PlayerScript;
        if (player == null) return;

        Vector2Int diff = player.CurrentPos - enemy.CurrentPos;
        bool isAligned = (diff.x == 0 || diff.y == 0);

        if (isAligned)
        {
            Direction dir = GetDirectionTo(enemy.CurrentPos, player.CurrentPos);
            // Face target
            if (enemy.CurrentDirection != dir)
            {
                enemy.SetDirection(dir);
            }
            // Enter Charge State using Command
            chargeDirection = dir;

            // Pack State and Direction for Command
            int targetState = (int)RushState.Charging | ((int)dir << 2);
            commandManager.ExecuteCommand(new SetRushStateCommand(this, targetState));

            // Visual feedback
            enemy.OrderSpriteAnimation.PlayAnimation();

            // Wait time for Charge initiation
            await UniTask.Delay(200);
        }
        else
        {
            // Just idle or random turn?
            // "Rotate if needed? or just stay?" - usually random patrol or static.
            // Let's keep it static for now as requested.
            if(currentState != RushState.Idle)
            {
                 commandManager.ExecuteCommand(new SetRushStateCommand(this, (int)RushState.Idle));
            }
        }
    }

    // Recursive rush logic (simulates continuous movement)
    private async UniTask ExecuteRush(Enemy enemy, Direction dir)
    {
        bool isMoving = true;

        if (enemy.gameObject.activeInHierarchy)
        {
             // Small Shake at start of rush
             CameraManager.Instance.ShakeCamera(0.5f, 2.0f, 60f, Utility.GetVectorFromDirection(dir));
        }

        while (isMoving)
        {
            Vector2Int currentPos = enemy.CurrentPos;
            Vector2Int nextPos = currentPos + Utility.GetVectorFromDirection(dir);
            StageSO currentStage = gridManager.CurrentStageSO; // Note: if enemy moves off-screen, it's removed from unitManager, but "enemy" obj still exists in memory?

            // We need a helper to check collision on ANY stage.
            StageSO enemyStage = GetEnemyStage(enemy);
            if (enemyStage == null) break; // Should not happen

            // Check what is at nextPos
            // 1. Out of Bounds
            if (nextPos.x < 0 || nextPos.x >= enemyStage.Width || nextPos.y < 0 || nextPos.y >= enemyStage.Height)
            {
                // マップ端に到達 -> 停止
                commandManager.ExecuteCommand(new RushAttackCommand(enemy, gridManager));

                if (enemy.gameObject.activeInHierarchy)
                {
                    if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);

                    enemy.OrderSpriteAnimation.PlayAnimation();
                    // Large Shake
                    CameraManager.Instance.ShakeCamera(0.3f, 15.0f, 60f, Utility.GetVectorFromDirection(dir));

                    await UniTask.WaitUntil(() => !enemy.IsMoving);
                }

                isMoving = false;
                break;
            }

            // 2. Wall
            CellType cell = gridManager.GetCellTypeAt(enemyStage, nextPos); // Use the new global checker
            if (cell == CellType.Wall)
            {
                // Break Wall
                BreakWallCommand breakCmd = new BreakWallCommand(gridManager, enemyStage, nextPos, enemy, rushDamage);
                commandManager.ExecuteCommand(breakCmd);

                // Visual feedback only if active
                if (enemy.gameObject.activeInHierarchy)
                {
                    if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);
                    enemy.OrderSpriteAnimation.PlayAnimation();
                    // Large Shake on Wall Hit
                    CameraManager.Instance.ShakeCamera(0.3f, 8.0f, 80f, Utility.GetVectorFromDirection(dir));
                }

                isMoving = false; // Stop after breaking
                break;
            }

            // 3. Unit (Player or Enemy)
            Unit targetUnit = GetUnitAt(enemyStage, nextPos); // Helper needed
            if (targetUnit != null)
            {
                // Hit Unit
                // Apply Damage. Since targetUnit might be generic Unit, we can call TakeDamage.
                // But `GetUnitAt` needs to return a Unit.
                // If it's on inactive stage, we might need to load it or simulate damage?
                // The constraint says "Stop in front of it".

                // If the target is Player, Player is always technically "on the active stage" logic-wise if we are simulating frame-by-frame,
                // but if Player is on Stage A, and Enemy is rushing in Stage B, they won't collide.
                // So checking `GetUnitAt(enemyStage)` is correct.

                // Visual Attack if active
                if (enemy.gameObject.activeInHierarchy)
                {
                   // enemy.TryAttack(); replaced by Command
                   // Use RushAttackCommand for same-stage attack + recoil

                   // Wait for target to be ready (e.g. Player moving towards us)
                   if (targetUnit.IsBusy) await UniTask.WaitUntil(() => !targetUnit.IsBusy);

                   commandManager.ExecuteCommand(new RushAttackCommand(enemy, targetUnit.CurrentPos, gridManager));
                   // Large Shake on Unit Hit
                   CameraManager.Instance.ShakeCamera(0.3f, 15.0f, 120f, Utility.GetVectorFromDirection(dir));

                   await UniTask.WaitUntil(() => !enemy.IsMoving && (targetUnit == null || !targetUnit.IsBusy));
                   await UniTask.Delay(100); // プレイヤーがすぐに行動できないように一瞬待つ
                }
                else
                {
                    // Off-screen attack
                    commandManager.ExecuteCommand(new CrossStageAttackCommand(enemy, targetUnit, rushDamage));
                }

                isMoving = false; // Stop
                break;
            }

            // 4. Empty -> Move
            // Suppress the default step sound
            MoveCommand moveCmd = new MoveCommand(enemy, currentPos, nextPos, dir, dir, false);
            // Note: MoveCommand normally animates. If enemy is inactive, it should skip animation.
            // We need to ensure MoveCommand handles inactive enemies gracefully (DG.Tween might complain on inactive object).
            // Let's verify MoveCommand. Or create simple "Teleport" command for off-screen?
            // MoveCommand calls `unit.MoveTo`. Unit.MoveTo uses DOTween.
            // If gameObject is inactive, DOTween on Transform works but is invisible.
            commandManager.ExecuteCommand(moveCmd);

            if (enemy.gameObject.activeInHierarchy)
            {
                // Play Rush Sound explicitly (Manual Interval control if needed, or rely on Loop)
                // Since this runs every frame/step, we play it every step.
                // Rush sound is likely a quick "Whoosh" or "Step".
                // We use debounce in AudioManager so it won't spam.
                if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.rush);

                await UniTask.Delay(50);
            }
            else
            {
                // Need small delay to avoid infinite loop freeze if logic is buggy,
                // but functionally instant.
                // To be safe against stack overflow in recursive scenarios or simple infinite loops:
                await UniTask.Yield();
            }

            // 階段遷移チェック（アクティブな場合のみ）
            if (enemy.gameObject.activeInHierarchy)
            {
                var transition = gridManager.GetEnemyStairDestination(enemy, nextPos, dir);
                if (transition != null)
                {
                    if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                    await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, dir);
                    isMoving = false;
                    break;
                }
            }
        }
    }

    private StageSO GetEnemyStage(Enemy enemy)
    {
       // Inefficient search, but reliable
       // GridManager doesn't expose "GetStageOfEnemy" efficiently yet.
       // But we know if it is active, it's CurrentStage.
       if (enemy.gameObject.activeInHierarchy) return gridManager.CurrentStageSO;

       // Otherwise search logic (we can't easily access private stageEnemiesMap from here without getter).
       // GridManager should probably expose a method `GetStageOfEnemy(Enemy)`
       return gridManager.GetStageOfEnemy(enemy);
    }

    private Unit GetUnitAt(StageSO stage, Vector2Int pos)
    {
        if (stage == gridManager.CurrentStageSO)
        {
            return gridManager.GetUnitAtPosition(pos);
        }
        else
        {
            // If stage is NOT current, Player is NOT there (Player is always on current)
            // So only check enemies.

            // Check Enemies in list
            // Need GridManager to expose `GetEnemiesOnStage(StageSO)`.
            List<Enemy> enemies = gridManager.GetEnemiesOnStage(stage); // Helper needed
            if (enemies != null)
            {
                foreach (var e in enemies)
                {
                    if (e.CurrentPos == pos && !e.IsDead) return e;
                }
            }
        }
        return null;
    }

    private Direction GetDirectionTo(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y)) return diff.x > 0 ? Direction.Right : Direction.Left;
        return diff.y > 0 ? Direction.Up : Direction.Down;
    }
}
